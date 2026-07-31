using UnityEngine;
using TMPro;

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
        public TextMeshProUGUI p1ScoreText;
        public TextMeshProUGUI p2ScoreText;
        public TextMeshProUGUI p1KeyText;
        public TextMeshProUGUI p2KeyText;

        int scoreP1, scoreP2;
        int keyP1, keyP2;

        // 結果発表用の累計値。keyP1/P2は宝箱開封で消費されるため、消費されない累計はこちらで別途持つ
        int totalKeyP1, totalKeyP2;
        int chestsOpenedP1, chestsOpenedP2;
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
            if (side == PlayerSide.P1) { keyP1 += amount; totalKeyP1 += amount; }
            else { keyP2 += amount; totalKeyP2 += amount; }
            RefreshUI();
        }

        public void AddChestOpened(PlayerSide side)
        {
            if (side == PlayerSide.P1) chestsOpenedP1++;
            else chestsOpenedP2++;
        }

        public void AddEnemyDefeated(PlayerSide side)
        {
            if (side == PlayerSide.P1) enemiesDefeatedP1++;
            else enemiesDefeatedP2++;
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
        public int GetTotalKeysCollected(PlayerSide side) => side == PlayerSide.P1 ? totalKeyP1 : totalKeyP2;
        public int GetChestsOpened(PlayerSide side) => side == PlayerSide.P1 ? chestsOpenedP1 : chestsOpenedP2;
        public int GetEnemiesDefeated(PlayerSide side) => side == PlayerSide.P1 ? enemiesDefeatedP1 : enemiesDefeatedP2;

        void RefreshUI()
        {
            if (p1ScoreText) p1ScoreText.text = scoreP1.ToString();
            if (p2ScoreText) p2ScoreText.text = scoreP2.ToString();
            if (p1KeyText) p1KeyText.text = keyP1.ToString();
            if (p2KeyText) p2KeyText.text = keyP2.ToString();
        }
    }
}
