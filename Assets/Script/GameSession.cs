namespace PPYY
{
    // シーンをまたいで持ち越す進行データ。MonoBehaviour ではない単純な静的クラスなので
    // DontDestroyOnLoad 等の管理が不要で、値は各ステージの終了処理から書き込む
    public static class GameSession
    {
        public static int Stage1ScoreP1;
        public static int Stage1ScoreP2;

        public static int Stage2ScoreP1;
        public static int Stage2ScoreP2;

        public static int Stage3ScoreP1;
        public static int Stage3ScoreP2;

        // 結果発表画面用の集計値。ステージ1終了時点で確定するもの（鍵・宝箱）と、
        // 各ステージ終了時にその時点までの合計で上書きされるもの（倒した敵の数）がある
        public static int KeysCollectedP1;
        public static int KeysCollectedP2;

        public static int ChestsOpenedP1;
        public static int ChestsOpenedP2;

        public static int EnemiesDefeatedP1;
        public static int EnemiesDefeatedP2;

        // ボスは1P/2P共通の敵のため、結果は1つだけ持つ
        public static bool BossDefeated;
    }
}
