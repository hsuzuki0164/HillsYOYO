using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace PPYY
{
    // ステージ開始前に「3, 2, 1, スタート」のカウントダウンを表示する共通コンポーネント。
    // カウントダウン中は Time.timeScale を0にしてゲーム内の動き（敵のスポーン・タイマー・
    // DOTweenアニメーション等）を止め、終了後に時間を戻してBGMを再生する。
    // 各ステージのシーンに1つずつ配置するだけで使える（個々のスクリプトを直す必要はない）
    public class StageCountdown : MonoBehaviour
    {
        [Header("カウントダウン表示")]
        public TextMeshProUGUI countdownText;
        public string[] countdownLabels = { "3", "2", "1", "スタート" };

        [Tooltip("countdownLabelsと同じ並び。各数字を表示しておく秒数（音声ファイルの間合いに合わせて個別に調整する）")]
        public float[] labelDurations = { 1f, 1f, 1f, 0.6f };

        [Header("数字が出るときのフェード拡大演出")]
        [Tooltip("表示開始時のスケール（小さい状態から等倍まで拡大する）")]
        public float popStartScale = 0.5f;
        public float popDuration = 0.3f;

        [Header("カウントダウン音声（「3,2,1,スタート」等をまとめた1本の音声ファイルを想定。カウントダウン開始と同時に1回だけ再生）")]
        public AudioSource countdownAudioSource;
        public AudioClip countdownVoiceClip;

        [Header("BGM（カウントダウン終了後に再生）")]
        public AudioSource bgm;

        [Tooltip("ONならシーン開始と同時に自動でカウントダウンを始める。StageJingle等から呼び出す場合はOFFにする")]
        public bool autoStart = true;

        void Start()
        {
            if (autoStart) BeginCountdown();
        }

        // StageJingle など、カウントダウンの前に何か挟みたい場合はこちらを呼び出す
        public void BeginCountdown()
        {
            Time.timeScale = 0f;
            if (countdownText != null) countdownText.gameObject.SetActive(true);
            StartCoroutine(CountdownRoutine());
        }

        IEnumerator CountdownRoutine()
        {
            if (countdownAudioSource != null && countdownVoiceClip != null)
            {
                countdownAudioSource.PlayOneShot(countdownVoiceClip);
            }

            for (int i = 0; i < countdownLabels.Length; i++)
            {
                if (countdownText != null)
                {
                    countdownText.text = countdownLabels[i];
                    PlayPopAnimation();
                }

                float wait = (labelDurations != null && i < labelDurations.Length) ? labelDurations[i] : 1f;
                // timeScale=0 の間も進む実時間ベースの待機を使う
                yield return new WaitForSecondsRealtime(wait);
            }

            if (countdownText != null) countdownText.gameObject.SetActive(false);

            Time.timeScale = 1f;

            if (bgm != null) bgm.Play();
        }

        // 小さく透明な状態から、等倍・不透明までフェードしながら拡大する。
        // Time.timeScale=0 の間も再生されるよう SetUpdate(true) で時間停止の影響を受けないようにする
        void PlayPopAnimation()
        {
            countdownText.rectTransform.DOKill();
            countdownText.DOKill();

            countdownText.rectTransform.localScale = Vector3.one * popStartScale;
            Color c = countdownText.color;
            c.a = 0f;
            countdownText.color = c;

            countdownText.rectTransform.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetUpdate(true);
            countdownText.DOFade(1f, popDuration).SetUpdate(true);
        }

        void OnDestroy()
        {
            // シーン遷移等でこのオブジェクトが消える際、timeScaleが0のまま残らないようにする保険
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }
}
