using UnityEngine;
using TMPro;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY.Stage2
{
    // 1P/2P それぞれの中央お宝の得点・見た目（10段階、350点刻み、3500点以上で最大段階）を管理する
    public class Stage2TreasureManager : MonoBehaviour
    {
        public static Stage2TreasureManager Instance { get; private set; }

        public const int PointsPerTier = 250;

        [Header("画面分割の境界（ワールドX座標）。これより左=1P, 右=2P")]
        public float screenSplitX = 0f;

        [Header("お宝の見た目（配列インデックス0〜9が段階0〜9、350点刻み・3500点以上で最大段階）")]
        public SpriteRenderer p1TreasureRenderer;
        public SpriteRenderer p2TreasureRenderer;
        public Sprite[] treasureSprites = new Sprite[10];

        [Header("お宝ポイントのUIテキスト（任意）")]
        public TextMeshProUGUI p1PointsText;
        public TextMeshProUGUI p2PointsText;

        [Header("デバッグ用（ONにするとステージ1の引き継ぎ値を無視し、この値から開始する）")]
        public bool useDebugStartPoints = false;
        public int debugStartPoints = 1000;

        int pointsP1, pointsP2;
        int enemiesDefeatedP1, enemiesDefeatedP2;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            if (useDebugStartPoints)
            {
                // デバッグ用：Stage1を経由せずこのシーン単体で動作確認するための固定初期値
                pointsP1 = Mathf.Max(0, debugStartPoints);
                pointsP2 = Mathf.Max(0, debugStartPoints);
            }
            else
            {
                // ステージ1で取得した得点を初期値として引き継ぐ
                pointsP1 = Mathf.Max(0, GameSession.Stage1ScoreP1);
                pointsP2 = Mathf.Max(0, GameSession.Stage1ScoreP2);
            }

            // 倒した敵の数はステージ1からの累計を引き継ぐ
            enemiesDefeatedP1 = GameSession.EnemiesDefeatedP1;
            enemiesDefeatedP2 = GameSession.EnemiesDefeatedP2;

            RefreshVisual(PlayerSide.P1);
            RefreshVisual(PlayerSide.P2);
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

        public int GetPoints(PlayerSide side) => side == PlayerSide.P1 ? pointsP1 : pointsP2;

        public Vector3 GetTreasureWorldPosition(PlayerSide side)
        {
            var r = side == PlayerSide.P1 ? p1TreasureRenderer : p2TreasureRenderer;
            return r != null ? r.transform.position : Vector3.zero;
        }

        // 指定した額を奪う。実際に減った額（0点未満にはならないようクランプ後の差分）を返す
        public int StealPoints(PlayerSide side, int amount)
        {
            int before = GetPoints(side);
            int after = Mathf.Max(0, before - amount);
            SetPoints(side, after);
            return before - after;
        }

        // 取り返し／相手側への加算、どちらもこれで加算する
        public void AddPoints(PlayerSide side, int amount)
        {
            SetPoints(side, GetPoints(side) + amount);
        }

        void SetPoints(PlayerSide side, int value)
        {
            value = Mathf.Max(0, value);
            if (side == PlayerSide.P1) pointsP1 = value;
            else pointsP2 = value;
            RefreshVisual(side);
        }

        void RefreshVisual(PlayerSide side)
        {
            var renderer = side == PlayerSide.P1 ? p1TreasureRenderer : p2TreasureRenderer;
            if (renderer == null || treasureSprites == null || treasureSprites.Length == 0) return;

            int tier = Mathf.Clamp(GetPoints(side) / PointsPerTier, 0, treasureSprites.Length - 1);
            if (treasureSprites[tier] != null) renderer.sprite = treasureSprites[tier];

            var text = side == PlayerSide.P1 ? p1PointsText : p2PointsText;
            if (text != null) text.text = GetPoints(side).ToString();
        }
    }
}
