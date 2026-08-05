using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Urg;

namespace PPYY.Lidar
{
    // 以前のペーパーヨーヨーコンテンツ(guideStar / Pin TerritoryのFileIO.cs)と同じ形式の
    // 外部ファイルからLIDAR関連の設定を読み込む。
    //
    // ファイル形式（3行。行の意味は固定、括弧・カンマは無視して数値だけを抽出するので多少崩れても読める）:
    //   1行目: キャリブレーション四隅の座標   例: (1.5,1),(1.5,-1),(0.2,-1),(0.2,1)
    //   2行目: テクスチャファイルの参照パス（このスクリプトでは扱わない。他機能が読む前提でそのまま残す）
    //   3行目: LIDARの接続先IPアドレス        例: 192.168.10.2
    //
    // 壁面に投影した矩形はセンサー座標系で軸に沿った長方形になる前提のため、
    // 実際に使うのは2つのX値・2つのY値だけ（guideStar版FileIO.csのsetCornerと同じ組み方）
    [DefaultExecutionOrder(-1000)] // UrgSensor.Awake()がIPアドレスを使って接続を開始する前に、確実にIPアドレスを反映させておく
    public class LidarCalibrationLoader : MonoBehaviour
    {
        [Tooltip("StreamingAssets フォルダからの相対パス")]
        public string fileName = "LidarCalibration.csv";

        [Tooltip("実行中にこのキーを押すとファイルを再読込し、キャリブレーションを反映し直す（IPアドレスは接続済みのため反映されない。反映するには再生し直す必要がある）")]
        public KeyCode reloadKey = KeyCode.I;

        [Header("接続先IPアドレス（ファイル3行目。未指定/ファイルが無い場合はこの値を使う）")]
        [Tooltip("未設定なら同じGameObjectのEthernetTransportを自動取得する")]
        public EthernetTransport ethernetTransport;
        public string defaultIpAddress = "192.168.10.2";

        string FullPath => Path.Combine(Application.streamingAssetsPath, fileName);

        void Awake()
        {
            if (ethernetTransport == null) ethernetTransport = GetComponent<EthernetTransport>();

            // IPアドレスはUrgSensor.Awake()内で接続が開始される前に確定させる必要があるため、
            // Start()ではなくAwake()で読み込む（他スクリプトより先に実行されるよう[DefaultExecutionOrder]で保証している）
            ApplyIpAddressFromFile();
        }

        void Start()
        {
            LoadCornersFromFile();
        }

        void Update()
        {
            if (Input.GetKeyDown(reloadKey))
            {
                LoadCornersFromFile();
            }
        }

        string[] ReadLines()
        {
            string path = FullPath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"LIDAR設定ファイルが見つかりません: {path}");
                return null;
            }

            try
            {
                return File.ReadAllLines(path, System.Text.Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogError($"LIDAR設定ファイルの読み込みに失敗しました: {e}");
                return null;
            }
        }

        void ApplyIpAddressFromFile()
        {
            if (ethernetTransport == null) return;

            var lines = ReadLines();
            string ip = defaultIpAddress;

            if (lines != null && lines.Length >= 3 && !string.IsNullOrWhiteSpace(lines[2]))
            {
                ip = lines[2].Trim();
            }

            ethernetTransport.ipAddress = ip;
            Debug.Log($"LIDAR接続先IPアドレス: {ip}");
        }

        void LoadCornersFromFile()
        {
            var lines = ReadLines();
            if (lines == null || lines.Length < 1) return;

            var matches = Regex.Matches(lines[0], @"[-+]?[0-9]*\.?[0-9]+");
            var points = new List<float>();
            foreach (Match m in matches)
            {
                if (float.TryParse(m.Value, out float value)) points.Add(value);
            }

            if (points.Count < 5)
            {
                Debug.LogWarning($"LIDARキャリブレーションファイルの数値が不足しています: {FullPath}");
                return;
            }

            // guideStar/Pin TerritoryのFileIO.csと同じ並び。
            // corner0=(x0,y0), corner1=(x0,y1), corner2=(x2,y1), corner3=(x2,y0)
            var corners = new Vector2[4];
            corners[0] = new Vector2(points[0], points[1]);
            corners[1] = new Vector2(points[0], points[3]);
            corners[2] = new Vector2(points[4], points[3]);
            corners[3] = new Vector2(points[4], points[1]);

            if (LidarSensorBridge.Instance != null)
            {
                LidarSensorBridge.Instance.SetSensorCorners(corners);
                Debug.Log($"LIDARキャリブレーションを読み込みました: {FullPath}");
            }
        }
    }
}
