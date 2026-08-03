using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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

        [Header("「結果発表」タイトルの上下浮遊")]
        public TextMeshProUGUI resultTitleText;
        public float titleFloatAmplitude = 10f;
        public float titleFloatFrequency = 1f;

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

        [Header("ランキング演出：ドラムロール（スロット回転中に鳴らし続ける）")]
        public AudioSource drumrollAudioSource;
        public AudioClip drumrollSound;

        [Header("ランキング演出：スロット風の数字めくり")]
        [Tooltip("1行あたりの回転時間")]
        public float slotSpinDuration = 0.8f;
        [Tooltip("回転中、数字が切り替わる間隔")]
        public float slotSpinInterval = 0.05f;
        [Tooltip("下位の行から確定させていく際、次の行の回転を始めるまでの間隔")]
        public float rowRevealInterval = 0.3f;
        [Tooltip("回転中に表示するランダムな数字の範囲（見た目だけで実際のスコアとは無関係）")]
        public int slotRandomMin = 0;
        public int slotRandomMax = 99999;

        [Header("ランキング演出：順位確定時の効果音（行の順位に応じて変える）")]
        public AudioClip rank1ConfirmSound;
        [Tooltip("2位・3位のとき")]
        public AudioClip top3ConfirmSound;
        [Tooltip("4位以下のとき")]
        public AudioClip normalConfirmSound;
        [Range(0f, 1f)] public float confirmSoundVolume = 1f;

        [Header("結果表示から一定時間で自動的にタイトルへ戻る")]
        public float autoReturnDelay = 20f;
        public string titleSceneName = "Title";
        [Tooltip("タイトルへ戻る直前、BGMをフェードアウトする時間")]
        public float bgmFadeOutDuration = 1.5f;
        [Tooltip("タイトルへ戻る直前、画面を白へフェードアウトする時間")]
        public float screenFadeOutDuration = 1.5f;

        [Header("歴代ランキング1位を発表した後に流す通常BGM")]
        public AudioSource bgmSource;
        public AudioClip bgmClip;
        [Tooltip("1位発表の効果音を鳴らしてから、BGMを再生するまでの秒数（ファンファーレの再生時間に合わせて調整する）")]
        public float bgmDelayAfterRank1Sound = 5f;

        Vector2 titleBasePos;
        float titleFloatTime;

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

            if (resultTitleText != null)
            {
                titleBasePos = resultTitleText.rectTransform.anchoredPosition;
            }
        }

        void Update()
        {
            if (resultTitleText == null) return;

            titleFloatTime += Time.deltaTime;
            float y = titleBasePos.y + Mathf.Sin(titleFloatTime * titleFloatFrequency) * titleFloatAmplitude;
            resultTitleText.rectTransform.anchoredPosition = new Vector2(titleBasePos.x, y);
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

            StartCoroutine(ResultSequence());
            StartCoroutine(AutoReturnToTitle());
        }

        // ホワイトインが終わってからランキング演出を始める
        IEnumerator ResultSequence()
        {
            yield return StartCoroutine(WhiteFadeIn());
            yield return StartCoroutine(PlayRankingRevealSequence());
        }

        IEnumerator WhiteFadeIn()
        {
            if (whiteFadeImage == null) yield break;

            whiteFadeImage.DOKill();
            whiteFadeImage.DOFade(0f, whiteFadeInDuration);
            yield return new WaitForSeconds(whiteFadeInDuration);
            whiteFadeImage.gameObject.SetActive(false);
        }

        IEnumerator AutoReturnToTitle()
        {
            yield return new WaitForSeconds(autoReturnDelay);

            FadeOutBgm();
            yield return StartCoroutine(WhiteFadeOut());

            SceneManager.LoadScene(titleSceneName);
        }

        void FadeOutBgm()
        {
            if (bgmSource == null) return;
            bgmSource.DOKill();
            bgmSource.DOFade(0f, bgmFadeOutDuration).OnComplete(() => bgmSource.Stop());
        }

        IEnumerator WhiteFadeOut()
        {
            if (whiteFadeImage == null) yield break;

            whiteFadeImage.gameObject.SetActive(true);
            whiteFadeImage.DOKill();
            whiteFadeImage.DOFade(1f, screenFadeOutDuration);
            yield return new WaitForSeconds(screenFadeOutDuration);
        }

        // 下位の行から順に、ドラムロールを鳴らしながらスロット風に数字を回転させ、
        // 一行ずつ確定させていく。確定時、その行の順位に応じた効果音を鳴らす
        IEnumerator PlayRankingRevealSequence()
        {
            if (rankingSlots == null || rankingSlots.Length == 0) yield break;

            var topEntries = ScoreHistory.GetTopEntries(rankingSlots.Length);

            for (int i = 0; i < rankingSlots.Length; i++)
            {
                var slot = rankingSlots[i];
                if (slot == null) continue;
                if (slot.rankText != null) slot.rankText.text = (i + 1).ToString();
                if (slot.labelText != null) slot.labelText.text = "";
                if (slot.scoreText != null) slot.scoreText.text = "";
            }

            if (drumrollAudioSource != null && drumrollSound != null)
            {
                drumrollAudioSource.clip = drumrollSound;
                drumrollAudioSource.loop = true;
                drumrollAudioSource.Play();
            }

            // 配列の末尾（下位）から先に確定させ、1位を最後に見せることで盛り上げる
            for (int i = rankingSlots.Length - 1; i >= 0; i--)
            {
                var slot = rankingSlots[i];
                if (slot == null) continue;

                yield return StartCoroutine(SpinSlot(slot, slotSpinDuration));

                int rank = i + 1;
                if (i < topEntries.Count)
                {
                    var entry = topEntries[i];
                    if (slot.labelText != null) slot.labelText.text = entry.label;
                    if (slot.scoreText != null) slot.scoreText.text = entry.score.ToString("N0");
                }
                else
                {
                    // 履歴がまだ枠数に満たない場合、余った行は空欄で確定させる
                    if (slot.labelText != null) slot.labelText.text = "-";
                    if (slot.scoreText != null) slot.scoreText.text = "-";
                }

                PlayConfirmPunch(slot);
                PlayConfirmSound(rank);

                if (rank == 1)
                {
                    StartCoroutine(PlayBgmAfterDelay(bgmDelayAfterRank1Sound));
                }

                yield return new WaitForSeconds(rowRevealInterval);
            }

            if (drumrollAudioSource != null) drumrollAudioSource.Stop();
        }

        IEnumerator PlayBgmAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayBgm();
        }

        void PlayBgm()
        {
            if (bgmSource == null || bgmClip == null) return;
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        IEnumerator SpinSlot(RankingSlot slot, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (slot.scoreText != null)
                {
                    slot.scoreText.text = UnityEngine.Random.Range(slotRandomMin, slotRandomMax + 1).ToString("N0");
                }
                yield return new WaitForSeconds(slotSpinInterval);
                elapsed += slotSpinInterval;
            }
        }

        void PlayConfirmPunch(RankingSlot slot)
        {
            if (slot.scoreText == null) return;
            slot.scoreText.rectTransform.DOKill();
            slot.scoreText.rectTransform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 1, 0);
        }

        void PlayConfirmSound(int rank)
        {
            AudioClip clip = rank == 1 ? rank1ConfirmSound
                : rank <= 3 ? top3ConfirmSound
                : normalConfirmSound;

            SfxPlayer.Play(clip, confirmSoundVolume);
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
