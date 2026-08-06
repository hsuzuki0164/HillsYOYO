using UnityEngine;
using TMPro;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY.Stage2
{
    // 1P/2P それぞれのお宝の得点・見た目を管理する。
    // 1Pにつき複数箇所（配列の要素数ぶん）のお宝スポットを持ち、敵はスポーン時にランダムでどれか1つを狙う。
    // 1箇所だけを見張れば済んでしまわないよう、スポットを複数に分けて難易度を上げるための構成
    public class Stage2TreasureManager : MonoBehaviour
    {
        public static Stage2TreasureManager Instance { get; private set; }

        public const int PointsPerTier = 250;

        [Header("画面分割の境界（ワールドX座標）。これより左=1P, 右=2P")]
        public float screenSplitX = 0f;

        [Header("1Pのお宝スポット（配列の要素数ぶんスポットが作られる。想定2箇所）")]
        public SpriteRenderer[] p1TreasureRenderers = new SpriteRenderer[2];
        [Header("2Pのお宝スポット（想定2箇所）")]
        public SpriteRenderer[] p2TreasureRenderers = new SpriteRenderer[2];
        [Tooltip("お宝の見た目（配列インデックス0〜9が段階0〜9、250点刻み・2250点以上で最大段階）。各スポット共通で使う")]
        public Sprite[] treasureSprites = new Sprite[10];

        [Header("お宝ポイントのUIテキスト（任意。1P/2Pそれぞれ、全スポット合計値を表示する）")]
        public TextMeshProUGUI p1PointsText;
        public TextMeshProUGUI p2PointsText;

        [Header("デバッグ用（ONにするとステージ1の引き継ぎ値を無視し、この値から開始する）")]
        public bool useDebugStartPoints = false;
        public int debugStartPoints = 1000;

        int[] pointsP1;
        int[] pointsP2;
        int enemiesDefeatedP1, enemiesDefeatedP2;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            pointsP1 = new int[Mathf.Max(1, p1TreasureRenderers.Length)];
            pointsP2 = new int[Mathf.Max(1, p2TreasureRenderers.Length)];
        }

        void Start()
        {
            int startP1, startP2;
            if (useDebugStartPoints)
            {
                // デバッグ用：Stage1を経由せずこのシーン単体で動作確認するための固定初期値
                startP1 = Mathf.Max(0, debugStartPoints);
                startP2 = Mathf.Max(0, debugStartPoints);
            }
            else
            {
                // ステージ1で取得した得点を初期値として引き継ぐ
                startP1 = Mathf.Max(0, GameSession.Stage1ScoreP1);
                startP2 = Mathf.Max(0, GameSession.Stage1ScoreP2);
            }

            // 引き継いだ得点を、各スポットへ均等に振り分ける
            DistributeStartingPoints(pointsP1, startP1);
            DistributeStartingPoints(pointsP2, startP2);

            // 倒した敵の数はステージ1からの累計を引き継ぐ
            enemiesDefeatedP1 = GameSession.EnemiesDefeatedP1;
            enemiesDefeatedP2 = GameSession.EnemiesDefeatedP2;

            RefreshVisual(PlayerSide.P1);
            RefreshVisual(PlayerSide.P2);
        }

        void DistributeStartingPoints(int[] points, int total)
        {
            if (points == null || points.Length == 0) return;
            int basePerSpot = total / points.Length;
            int remainder = total - basePerSpot * points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = basePerSpot + (i < remainder ? 1 : 0);
            }
        }

        public void AddEnemyDefeated(PlayerSide side)
        {
            if (side == PlayerSide.P1) enemiesDefeatedP1++;
            else enemiesDefeatedP2++;
        }

        public int GetEnemiesDefeated(PlayerSide side) => side == PlayerSide.P1 ? enemiesDefeatedP1 : enemiesDefeatedP2;

        public PlayerSide GetSideFromWorldX(float worldX)
        {
            return worldX < screenSplitX ? PlayerSide.P1 : PlayerSide.P2;
        }

        int[] GetPointsArray(PlayerSide side) => side == PlayerSide.P1 ? pointsP1 : pointsP2;
        SpriteRenderer[] GetRenderers(PlayerSide side) => side == PlayerSide.P1 ? p1TreasureRenderers : p2TreasureRenderers;

        // 指定した側の合計得点（全スポットの得点の合計）
        public int GetPoints(PlayerSide side)
        {
            var points = GetPointsArray(side);
            int total = 0;
            for (int i = 0; i < points.Length; i++) total += points[i];
            return total;
        }

        public int GetSpotCount(PlayerSide side) => GetPointsArray(side).Length;

        // 敵がスポーン時に狙うスポットをランダムに1つ選ぶ
        public int PickRandomSpotIndex(PlayerSide side)
        {
            var points = GetPointsArray(side);
            return points.Length > 0 ? Random.Range(0, points.Length) : 0;
        }

        public Vector3 GetTreasureWorldPosition(PlayerSide side, int spotIndex)
        {
            var renderers = GetRenderers(side);
            if (spotIndex < 0 || spotIndex >= renderers.Length || renderers[spotIndex] == null) return Vector3.zero;
            return renderers[spotIndex].transform.position;
        }

        // 指定スポットから額を奪う。実際に減った額（0点未満にはならないようクランプ後の差分）を返す
        public int StealPoints(PlayerSide side, int spotIndex, int amount)
        {
            var points = GetPointsArray(side);
            if (spotIndex < 0 || spotIndex >= points.Length) return 0;

            int before = points[spotIndex];
            int after = Mathf.Max(0, before - amount);
            points[spotIndex] = after;
            RefreshVisual(side);
            return before - after;
        }

        // 取り返し／相手側への加算、どちらもこれで加算する。
        // spotIndexが範囲外（相手側から横取りした場合など）ならスポット0へ加算する
        public void AddPoints(PlayerSide side, int spotIndex, int amount)
        {
            var points = GetPointsArray(side);
            if (points.Length == 0) return;
            if (spotIndex < 0 || spotIndex >= points.Length) spotIndex = 0;

            points[spotIndex] = Mathf.Max(0, points[spotIndex] + amount);
            RefreshVisual(side);
        }

        void RefreshVisual(PlayerSide side)
        {
            var renderers = GetRenderers(side);
            var points = GetPointsArray(side);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || treasureSprites == null || treasureSprites.Length == 0) continue;
                int tier = Mathf.Clamp(points[i] / PointsPerTier, 0, treasureSprites.Length - 1);
                if (treasureSprites[tier] != null) renderers[i].sprite = treasureSprites[tier];
            }

            var text = side == PlayerSide.P1 ? p1PointsText : p2PointsText;
            if (text != null) text.text = GetPoints(side).ToString();
        }
    }
}
