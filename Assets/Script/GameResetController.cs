using UnityEngine;
using UnityEngine.SceneManagement;

namespace PPYY
{
    // どのシーンにいてもEscapeキーでゲームを中断し、進行状況(GameSession)をリセットしてタイトルへ戻る。
    // LidarSensorBridgeと同様、シーンをまたいで常駐させる(DontDestroyOnLoad)ことで全ステージ共通の挙動にする
    public class GameResetController : MonoBehaviour
    {
        public static GameResetController Instance { get; private set; }

        public string titleSceneName = "Title";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ResetAndReturnToTitle();
            }
        }

        void ResetAndReturnToTitle()
        {
            ResetGameSession();

            // カウントダウン中(Time.timeScale=0)にEscapeされた場合でも、
            // タイトルへ戻った後の演出が止まったままにならないよう時間を戻しておく
            Time.timeScale = 1f;

            SceneManager.LoadScene(titleSceneName);
        }

        // 歴代ランキング(ScoreHistory)は端末の記録として残し、今回のプレイの進行状況だけをリセットする
        void ResetGameSession()
        {
            GameSession.Stage1ScoreP1 = 0;
            GameSession.Stage1ScoreP2 = 0;
            GameSession.Stage2ScoreP1 = 0;
            GameSession.Stage2ScoreP2 = 0;
            GameSession.Stage3ScoreP1 = 0;
            GameSession.Stage3ScoreP2 = 0;

            GameSession.KeysCollectedP1 = 0;
            GameSession.KeysCollectedP2 = 0;

            GameSession.ChestsOpenedP1 = 0;
            GameSession.ChestsOpenedP2 = 0;

            GameSession.EnemiesDefeatedP1 = 0;
            GameSession.EnemiesDefeatedP2 = 0;

            GameSession.BossDefeated = false;
        }
    }
}
