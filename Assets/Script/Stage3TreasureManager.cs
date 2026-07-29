using UnityEngine;
using TMPro;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY.Stage3
{
    // Stage2TreasureManager と同じ構造。1P/2P それぞれのお宝得点・見た目を管理し、
    // ボスの手による強奪や爆弾、撃破ボーナスの加減算先になる
    public class Stage3TreasureManager : MonoBehaviour
    {
        public static Stage3TreasureManager Instance { get; private set; }

        public const int PointsPerTier = 350;

        [Header("画面分割の境界（ワールドX座標）。これより左=1P, 右=2P")]
        public float screenSplitX = 0f;

        [Header("お宝の見た目（配列インデックス0〜9が段階0〜9）")]
        public SpriteRenderer p1TreasureRenderer;
        public SpriteRenderer p2TreasureRenderer;
        public Sprite[] treasureSprites = new Sprite[10];

        [Header("お宝ポイントのUIテキスト（任意）")]
        public TextMeshProUGUI p1PointsText;
        public TextMeshProUGUI p2PointsText;

        [Header("デバッグ用（ONにするとステージ2の引き継ぎ値を無視し、この値から開始する）")]
        public bool useDebugStartPoints = false;
        public int debugStartPoints = 1000;

        int pointsP1, pointsP2;

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
                pointsP1 = Mathf.Max(0, debugStartPoints);
                pointsP2 = Mathf.Max(0, debugStartPoints);
            }
            else
            {
                pointsP1 = Mathf.Max(0, GameSession.Stage2ScoreP1);
                pointsP2 = Mathf.Max(0, GameSession.Stage2ScoreP2);
            }

            RefreshVisual(PlayerSide.P1);
            RefreshVisual(PlayerSide.P2);
        }

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

        // 実際に減った額（0点未満にはならないようクランプ後の差分）を返す
        public int StealPoints(PlayerSide side, int amount)
        {
            int before = GetPoints(side);
            int after = Mathf.Max(0, before - amount);
            SetPoints(side, after);
            return before - after;
        }

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
            int points = GetPoints(side);

            if (renderer != null && treasureSprites != null && treasureSprites.Length > 0)
            {
                int tier = Mathf.Clamp(points / PointsPerTier, 0, treasureSprites.Length - 1);
                if (treasureSprites[tier] != null) renderer.sprite = treasureSprites[tier];
            }

            var text = side == PlayerSide.P1 ? p1PointsText : p2PointsText;
            if (text != null) text.text = points.ToString();
        }
    }
}
