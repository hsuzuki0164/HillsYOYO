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
    }
}
