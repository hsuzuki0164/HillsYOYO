using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY.Stage2
{
    // Stage1StageManager と同じ役割（制限時間→次シーン遷移）だが、
    // 得点の引き継ぎ元が Stage2TreasureManager になっている
    public class Stage2StageManager : MonoBehaviour
    {
        [Header("制限時間（秒）")]
        public float timeLimit = 120f;

        [Header("制限時間終了後に遷移するシーン名")]
        public string nextSceneName = "Stage3";

        [Header("UI（任意）")]
        public Text timerText;

        float remaining;
        bool finished;

        void Start()
        {
            remaining = timeLimit;
            UpdateTimerText();
        }

        void Update()
        {
            if (finished) return;

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                finished = true;
                UpdateTimerText();

                if (Stage2TreasureManager.Instance != null)
                {
                    GameSession.Stage2ScoreP1 = Stage2TreasureManager.Instance.GetPoints(PlayerSide.P1);
                    GameSession.Stage2ScoreP2 = Stage2TreasureManager.Instance.GetPoints(PlayerSide.P2);
                }

                if (PPYY.StageTransition.Instance != null)
                {
                    PPYY.StageTransition.Instance.PlayAndLoadScene(nextSceneName);
                }
                else
                {
                    SceneManager.LoadScene(nextSceneName);
                }
                return;
            }

            UpdateTimerText();
        }

        void UpdateTimerText()
        {
            if (timerText) timerText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }
}
