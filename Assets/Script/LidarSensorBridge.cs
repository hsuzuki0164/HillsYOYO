using System;
using System.Collections.Generic;
using UnityEngine;
using Urg;

namespace PPYY.Lidar
{
    // urg-unity(LIDARセンサー)からの検出結果を、壁面(プロジェクタ投影面)上のワールド座標に変換し、
    // 「壁にペーパーヨーヨーが当たった」タイミングをヒットイベントとして通知する。
    // シリアル/イーサネット接続をステージ遷移のたびに繋ぎ直さずに済むよう、
    // シーンをまたいで常駐させる(DontDestroyOnLoad)。タイトルシーンなど最初に読み込まれるシーンに置く
    public class LidarSensorBridge : MonoBehaviour
    {
        public static LidarSensorBridge Instance { get; private set; }

        [Header("接続するLIDARセンサー（同じGameObjectにSerialTransport/EthernetTransportも必要）")]
        public UrgSensor urgSensor;

        [Header("フィルタ・クラスタリング設定")]
        public int temporalMedianFrames = 3;
        public int spatialMedianIndices = 3;
        [Tooltip("これより遠い点は無視する（壁の奥・背景を除外する）")]
        public float distanceFilterThreshold = 2.25f;
        [Tooltip("同一クラスタとみなす距離のしきい値")]
        public float clusterDistanceThreshold = 0.1f;
        [Tooltip("これ未満の点数しかないクラスタはノイズとみなして無視する")]
        public int minClusterPointCount = 2;

        [Header("キャリブレーション（壁面の四隅に対応するセンサー座標。LidarCalibrationLoaderが外部ファイルから上書きする）")]
        public Vector2[] sensorCorners = new Vector2[4]
        {
            new Vector2(1.5f, 1f),
            new Vector2(1.5f, -1f),
            new Vector2(0.2f, -1f),
            new Vector2(0.2f, 1f),
        };

        [Header("同じ場所への連続ヒットを防ぐクールダウン（当てたまま静止している間の連打を防ぐ）")]
        public float hitCooldown = 0.5f;
        public float hitSuppressRadius = 0.3f;

        [Header("デバッグ表示（ビルド後の実機でもLIDARがどこを検出しているか確認できる。Gizmosではなく実オブジェクトなのでビルドでも見える）")]
        public bool showDebugMarkers = false;
        [Tooltip("実行中にShiftを押しながらこのキーを押すとON/OFFを切り替えられる")]
        public KeyCode toggleDebugMarkersKey = KeyCode.D;
        [Tooltip("現在検出中の（クールダウンで抑制されている場合も含む）クラスタ位置に常時表示するマーカーの色")]
        public Color trackingMarkerColor = new Color(0f, 1f, 1f, 0.6f);
        [Tooltip("実際にOnHitが発火した瞬間だけ一瞬表示するマーカーの色")]
        public Color hitMarkerColor = new Color(1f, 0f, 0f, 0.9f);
        public float markerWorldSize = 0.3f;
        public float hitMarkerLifetime = 0.4f;

        readonly List<SpriteRenderer> trackingMarkerPool = new List<SpriteRenderer>();
        Sprite debugSprite;

        // Stage1InputManager等が購読し、LIDARでのヒットをProcessHitへ直接つなぐ
        public event Action<Vector2> OnHit;

        EuclidianClusterExtraction clusterExtraction;
        readonly object queueLock = new object();
        DistanceRecord pendingRecord;

        List<DetectedLocation> latestLocations = new List<DetectedLocation>();
        List<List<int>> latestClusters = new List<List<int>>();

        struct RecentHit { public Vector2 position; public float time; }
        readonly List<RecentHit> recentHits = new List<RecentHit>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            clusterExtraction = new EuclidianClusterExtraction(clusterDistanceThreshold);

            if (urgSensor != null)
            {
                urgSensor.AddFilter(new TemporalMedianFilter(temporalMedianFrames));
                urgSensor.AddFilter(new SpatialMedianFilter(spatialMedianIndices));
                urgSensor.AddFilter(new DistanceFilter(distanceFilterThreshold));
                urgSensor.SetClusterExtraction(clusterExtraction);
                urgSensor.OnDistanceReceived += HandleDistanceReceived;
            }
        }

        void OnDestroy()
        {
            if (urgSensor != null) urgSensor.OnDistanceReceived -= HandleDistanceReceived;
        }

        // UrgSensorの受信スレッド（メインスレッドではない）から呼ばれる。
        // Unityオブジェクトには触れず、最新のフレームを保持しておくだけにする
        void HandleDistanceReceived(DistanceRecord data)
        {
            lock (queueLock)
            {
                pendingRecord = data;
            }
        }

        // 外部ファイルの再読込時にLidarCalibrationLoaderから呼ばれる
        public void SetSensorCorners(Vector2[] corners)
        {
            if (corners == null || corners.Length != 4) return;
            sensorCorners = corners;
        }

        void Update()
        {
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shiftHeld && Input.GetKeyDown(toggleDebugMarkersKey))
            {
                showDebugMarkers = !showDebugMarkers;
                Debug.Log($"LIDARデバッグ表示: {(showDebugMarkers ? "ON" : "OFF")}");
            }

            DistanceRecord record;
            lock (queueLock)
            {
                record = pendingRecord;
                pendingRecord = null;
            }

            if (record != null)
            {
                latestLocations = record.FilteredResults;
                latestClusters = record.ClusteredIndices;
            }

            if (latestClusters == null || latestLocations == null) return;

            var affineConverter = BuildAffineConverter();
            if (affineConverter == null) return;

            int trackingMarkerCount = 0;

            foreach (var cluster in latestClusters)
            {
                if (cluster == null || cluster.Count < minClusterPointCount) continue;

                Vector2 center = Vector2.zero;
                foreach (var index in cluster)
                {
                    center += latestLocations[index].ToPosition2D();
                }
                center /= cluster.Count;

                if (!affineConverter.Sensor2WorldPosition(center, out Vector3 worldPos)) continue;

                Vector2 hitPos = worldPos;

                // クールダウンで抑制されている検出も含め、今フレーム有効な検出位置は常に表示する
                // （キャリブレーションのズレやノイズを見つけやすくするため）
                if (showDebugMarkers)
                {
                    ShowTrackingMarker(trackingMarkerCount, hitPos);
                    trackingMarkerCount++;
                }

                if (IsRecentlyHit(hitPos)) continue;

                recentHits.Add(new RecentHit { position = hitPos, time = Time.time });
                if (showDebugMarkers) SpawnHitFlashMarker(hitPos);

                var mainCam = Camera.main;
                Debug.Log($"[Lidar] OnHit worldPos={hitPos} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                    $"camera={(mainCam != null ? mainCam.name : "null")} camPos={(mainCam != null ? (Vector2)mainCam.transform.position : Vector2.zero)} " +
                    $"orthoSize={(mainCam != null && mainCam.orthographic ? mainCam.orthographicSize.ToString() : "n/a")}");

                OnHit?.Invoke(hitPos);
            }

            HideUnusedTrackingMarkers(showDebugMarkers ? trackingMarkerCount : 0);
        }

        void ShowTrackingMarker(int poolIndex, Vector2 position)
        {
            while (trackingMarkerPool.Count <= poolIndex)
            {
                trackingMarkerPool.Add(CreateMarker(trackingMarkerColor));
            }
            var marker = trackingMarkerPool[poolIndex];
            marker.transform.position = position;
            marker.gameObject.SetActive(true);
        }

        void HideUnusedTrackingMarkers(int usedCount)
        {
            for (int i = usedCount; i < trackingMarkerPool.Count; i++)
            {
                if (trackingMarkerPool[i].gameObject.activeSelf) trackingMarkerPool[i].gameObject.SetActive(false);
            }
        }

        void SpawnHitFlashMarker(Vector2 position)
        {
            var marker = CreateMarker(hitMarkerColor);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * markerWorldSize * 1.5f;
            Destroy(marker.gameObject, hitMarkerLifetime);
        }

        SpriteRenderer CreateMarker(Color color)
        {
            var go = new GameObject("LidarDebugMarker");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateDebugSprite();
            sr.color = color;
            sr.sortingOrder = 1000;
            go.transform.localScale = Vector3.one * markerWorldSize;
            return sr;
        }

        Sprite GetOrCreateDebugSprite()
        {
            if (debugSprite != null) return debugSprite;
            var tex = Texture2D.whiteTexture;
            debugSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            return debugSprite;
        }

        bool IsRecentlyHit(Vector2 pos)
        {
            for (int i = recentHits.Count - 1; i >= 0; i--)
            {
                if (Time.time - recentHits[i].time > hitCooldown)
                {
                    recentHits.RemoveAt(i);
                    continue;
                }
                if (Vector2.Distance(recentHits[i].position, pos) <= hitSuppressRadius) return true;
            }
            return false;
        }

        // 壁面(プロジェクタの投影範囲=画面全体)の四隅をワールド座標に変換し、
        // センサー座標系の四隅(sensorCorners)との対応からAffineConverterを組み立てる。
        // シーン毎にカメラが変わるため、常に最新のCamera.mainを使って毎フレーム組み立て直す
        AffineConverter BuildAffineConverter()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            var plane = new Plane(Vector3.forward, Vector3.zero);

            var worldCorners = new Vector3[4];
            worldCorners[0] = Screen2WorldPosition(new Vector2(0, Screen.height), cam, plane);
            worldCorners[1] = Screen2WorldPosition(new Vector2(Screen.width, Screen.height), cam, plane);
            worldCorners[2] = Screen2WorldPosition(new Vector2(Screen.width, 0), cam, plane);
            worldCorners[3] = Screen2WorldPosition(new Vector2(0, 0), cam, plane);

            return new AffineConverter(sensorCorners, worldCorners);
        }

        Vector3 Screen2WorldPosition(Vector2 screenPos, Camera cam, Plane plane)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (plane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            return Vector3.zero;
        }
    }
}
