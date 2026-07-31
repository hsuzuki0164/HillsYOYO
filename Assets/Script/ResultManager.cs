using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY
{
    // 結果発表シーン用。GameSessionに蓄積された最終値を読み出してUIに表示するだけの担当。
    // 各ステージのマネージャーがシーン終了時にGameSessionへ書き込んだ値を、このシーンの開始時に読み出す
    public class ResultManager : MonoBehaviour
    {
        [Serializable]
        public class RankingSlot
        {
            [Tooltip("例：1位, 2位…（未設定でも動作する）")]
            public TextMeshProUGUI rankText;
            [Tooltip("記録した側（1P/2P）")]
            public TextMeshProUGUI labelText;
            public TextMeshProUGUI scoreText;
        }

        [Header("画面のホワイトイン（シーン開始時、白画面から表示される）")]
        public Image whiteFadeImage;
        public float whiteFadeInDuration = 1f;

        [Header("最終スコア（お宝ポイント＋鍵・宝箱・撃破数のボーナスを加味した合計）")]
        public TextMeshProUGUI p1ScoreText;
        public TextMeshProUGUI p2ScoreText;

        [Header("手に入れた鍵の数")]
        public TextMeshProUGUI p1KeysText;
        public TextMeshProUGUI p2KeysText;

        [Header("開けた宝箱の数")]
        public TextMeshProUGUI p1ChestsText;
        public TextMeshProUGUI p2ChestsText;

        [Header("ゲットしたお宝ポイント（ステージ3終了時点の素のお宝ポイント。最終スコアとは別）")]
        public TextMeshProUGUI p1PointsGetText;
        public TextMeshProUGUI p2PointsGetText;

        [Header("倒した敵の数")]
        public TextMeshProUGUI p1EnemiesText;
        public TextMeshProUGUI p2EnemiesText;

        [Header("ボス撃破有無（1P/2P共通のボスのため、表示は1つだけ）")]
        public TextMeshProUGUI bossResultText;
        public string bossDefeatedLabel = "ボス撃破！";
        public string bossFledLabel = "ボス撃破ならず…";

        [Header("最終スコアのボーナス倍率")]
        public int pointsPerKey = 100;
        public int pointsPerChest = 100;
        public int pointsPerEnemyDefeated = 30;

        [Header("ランキング（左側。過去の全プレイ履歴の中から上位を表示）")]
        public RankingSlot[] rankingSlots = new RankingSlot[5];

        [Header("今回のプレイが履歴全体の中で何位か（1P/2Pそれぞれ）")]
        public TextMeshProUGUI p1RankText;
        public TextMeshProUGUI p2RankText;
        [Tooltip("{0}に順位の数字が入る")]
        public string rankFormat = "{0}位";

        void Awake()
        {
            // 最初のフレームが描画される前に白で覆っておく（データが揃う前の一瞬の表示を防ぐ）
            if (whiteFadeImage != null)
            {
                whiteFadeImage.gameObject.SetActive(true);
                Color c = whiteFadeImage.color;
                c.a = 1f;
                whiteFadeImage.color = c;
            }
        }

        void Start()
        {
            int p1Score = CalculateFinalScore(PlayerSide.P1);
            int p2Score = CalculateFinalScore(PlayerSide.P2);

            SetText(p1ScoreText, p1Score);
            SetText(p2ScoreText, p2Score);

            SetText(p1KeysText, GameSession.KeysCollectedP1);
            SetText(p2KeysText, GameSession.KeysCollectedP2);

            SetText(p1ChestsText, GameSession.ChestsOpenedP1);
            SetText(p2ChestsText, GameSession.ChestsOpenedP2);

            SetText(p1PointsGetText, GameSession.Stage3ScoreP1);
            SetText(p2PointsGetText, GameSession.Stage3ScoreP2);

            SetText(p1EnemiesText, GameSession.EnemiesDefeatedP1);
            SetText(p2EnemiesText, GameSession.EnemiesDefeatedP2);

            if (bossResultText != null)
            {
                bossResultText.text = GameSession.BossDefeated ? bossDefeatedLabel : bossFledLabel;
            }

            // 今回の1P/2Pの最終スコアを履歴に記録し、その順位・全体ランキングを表示する
            var p1Entry = ScoreHistory.AddEntry("1P", p1Score);
            var p2Entry = ScoreHistory.AddEntry("2P", p2Score);

            SetRankText(p1RankText, ScoreHistory.GetRank(p1Entry));
            SetRankText(p2RankText, ScoreHistory.GetRank(p2Entry));

            PopulateRanking();

            StartCoroutine(WhiteFadeIn());
        }

        IEnumerator WhiteFadeIn()
        {
            if (whiteFadeImage == null) yield break;

            whiteFadeImage.DOKill();
            whiteFadeImage.DOFade(0f, whiteFadeInDuration);
            yield return new WaitForSeconds(whiteFadeInDuration);
            whiteFadeImage.gameObject.SetActive(false);
        }

        void PopulateRanking()
        {
            if (rankingSlots == null) return;

            var topEntries = ScoreHistory.GetTopEntries(rankingSlots.Length);

            for (int i = 0; i < rankingSlots.Length; i++)
            {
                var slot = rankingSlots[i];
                if (slot == null) continue;

                if (i < topEntries.Count)
                {
                    var entry = topEntries[i];
                    if (slot.rankText != null) slot.rankText.text = (i + 1).ToString();
                    if (slot.labelText != null) slot.labelText.text = entry.label;
                    if (slot.scoreText != null) slot.scoreText.text = entry.score.ToString("N0");
                }
                else
                {
                    // 履歴がまだ5件に満たない場合、余った枠は空欄にする
                    if (slot.rankText != null) slot.rankText.text = (i + 1).ToString();
                    if (slot.labelText != null) slot.labelText.text = "-";
                    if (slot.scoreText != null) slot.scoreText.text = "-";
                }
            }
        }

        void SetRankText(TextMeshProUGUI text, int rank)
        {
            if (text != null) text.text = string.Format(rankFormat, rank);
        }

        // お宝ポイントに、鍵・宝箱・撃破数それぞれのボーナスを加算した最終スコアを算出する
        int CalculateFinalScore(PlayerSide side)
        {
            int treasurePoints = side == PlayerSide.P1 ? GameSession.Stage3ScoreP1 : GameSession.Stage3ScoreP2;
            int keys = side == PlayerSide.P1 ? GameSession.KeysCollectedP1 : GameSession.KeysCollectedP2;
            int chests = side == PlayerSide.P1 ? GameSession.ChestsOpenedP1 : GameSession.ChestsOpenedP2;
            int enemiesDefeated = side == PlayerSide.P1 ? GameSession.EnemiesDefeatedP1 : GameSession.EnemiesDefeatedP2;

            return treasurePoints
                + keys * pointsPerKey
                + chests * pointsPerChest
                + enemiesDefeated * pointsPerEnemyDefeated;
        }

        void SetText(TextMeshProUGUI text, int value)
        {
            if (text != null) text.text = value.ToString("N0");
        }
    }
}
