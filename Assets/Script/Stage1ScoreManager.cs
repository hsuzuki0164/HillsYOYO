using UnityEngine;
using UnityEngine.UI;

namespace PPYY.Stage1
{
    public enum PlayerSide
    {
        P1, // 画面左半分
        P2, // 画面右半分
    }

    public class Stage1ScoreManager : MonoBehaviour
    {
        public static Stage1ScoreManager Instance { get; private set; }

        [Header("画面分割の境界（ワールドX座標）。これより左=1P, 右=2P")]
        public float screenSplitX = 0f;

        [Header("UI（未設定でも動作する）")]
        public Text p1ScoreText;
        public Text p2ScoreText;
        public Text p1KeyText;
        public Text p2KeyText;

        int scoreP1, scoreP2;
        int keyP1, keyP2;

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
            RefreshUI();
        }

        public PlayerSide GetSideFromWorldX(float worldX)
        {
            return worldX < screenSplitX ? PlayerSide.P1 : PlayerSide.P2;
        }

        public void AddScore(PlayerSide side, int amount)
        {
            if (side == PlayerSide.P1) scoreP1 += amount;
            else scoreP2 += amount;
            RefreshUI();
        }

        public void AddKey(PlayerSide side, int amount = 1)
        {
            if (side == PlayerSide.P1) keyP1 += amount;
            else keyP2 += amount;
            RefreshUI();
        }

        // 鍵が cost 未満なら false を返し、消費しない
        public bool TryUseKey(PlayerSide side, int cost = 1)
        {
            if (side == PlayerSide.P1)
            {
                if (keyP1 < cost) return false;
                keyP1 -= cost;
            }
            else
            {
                if (keyP2 < cost) return false;
                keyP2 -= cost;
            }
            RefreshUI();
            return true;
        }

        public int GetScore(PlayerSide side) => side == PlayerSide.P1 ? scoreP1 : scoreP2;
        public int GetKeys(PlayerSide side) => side == PlayerSide.P1 ? keyP1 : keyP2;

        void RefreshUI()
        {
            if (p1ScoreText) p1ScoreText.text = scoreP1.ToString();
            if (p2ScoreText) p2ScoreText.text = scoreP2.ToString();
            if (p1KeyText) p1KeyText.text = keyP1.ToString();
            if (p2KeyText) p2KeyText.text = keyP2.ToString();
        }
    }
}
