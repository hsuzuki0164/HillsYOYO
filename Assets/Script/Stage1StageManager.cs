using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

namespace PPYY.Stage1
{
    public class Stage1StageManager : MonoBehaviour
    {
        [Header("制限時間（秒）")]
        public float timeLimit = 60f;

        [Header("制限時間終了後に遷移するシーン名")]
        public string nextSceneName = "Stage2";

        [Header("UI（任意）")]
        public TextMeshProUGUI timerText;

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

                if (Stage1ScoreManager.Instance != null)
                {
                    GameSession.Stage1ScoreP1 = Stage1ScoreManager.Instance.GetScore(PlayerSide.P1);
                    GameSession.Stage1ScoreP2 = Stage1ScoreManager.Instance.GetScore(PlayerSide.P2);
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
