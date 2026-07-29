using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace PPYY
{
    // 各ステージ開始時、カウントダウンより前に表示する説明ジングル。
    // シーン開始と同時に Time.timeScale を0にして表示・音を鳴らし、
    // 指定秒数（Inspectorで調整可）が経ったらDOTween(InBack)で上へ移動して消え、
    // StageCountdown を開始する。
    // タイトル→ステージ1、ステージ1→2、ステージ2→3、それぞれの受け側シーンに1つ配置して使う
    public class StageJingle : MonoBehaviour
    {
        [Header("説明画面（画像・テキストなどをまとめた1つのオブジェクト）")]
        public GameObject jingleVisual;

        [Tooltip("説明画面を表示しておく秒数")]
        public float jingleDuration = 3f;

        [Header("消えるときのアニメーション（上へ移動しながらDOTween Ease.InBackで消える）")]
        public float exitMoveDistance = 1200f;
        public float exitDuration = 0.5f;

        [Header("表示中に鳴らす音")]
        public AudioSource jingleAudioSource;
        public AudioClip jingleSound;

        [Header("ジングル終了後に開始するカウントダウン（任意。未設定ならジングル終了と同時にゲーム再開）")]
        public StageCountdown countdown;

        void Start()
        {
            Time.timeScale = 0f;
            if (jingleVisual != null) jingleVisual.SetActive(true);

            if (jingleAudioSource != null && jingleSound != null)
            {
                jingleAudioSource.clip = jingleSound;
                jingleAudioSource.Play();
            }

            StartCoroutine(JingleRoutine());
        }

        IEnumerator JingleRoutine()
        {
            // timeScale=0 の間も進む実時間ベースの待機を使う
            yield return new WaitForSecondsRealtime(jingleDuration);

            if (jingleAudioSource != null) jingleAudioSource.Stop();
            PlayExitAnimation();
            yield return new WaitForSecondsRealtime(exitDuration);

            if (jingleVisual != null) jingleVisual.SetActive(false);

            if (countdown != null)
            {
                countdown.BeginCountdown();
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

        // 上へ移動しながら消える。Time.timeScale=0 の間も再生されるよう SetUpdate(true) を使う
        void PlayExitAnimation()
        {
            if (jingleVisual == null) return;

            var rect = jingleVisual.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.DOKill();
                rect.DOAnchorPosY(rect.anchoredPosition.y + exitMoveDistance, exitDuration)
                    .SetEase(Ease.InBack).SetUpdate(true);
            }
            else
            {
                jingleVisual.transform.DOKill();
                jingleVisual.transform.DOMoveY(jingleVisual.transform.position.y + exitMoveDistance, exitDuration)
                    .SetEase(Ease.InBack).SetUpdate(true);
            }
        }

        void OnDestroy()
        {
            // シーン遷移等でこのオブジェクトが消える際、timeScaleが0のまま残らないようにする保険
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }
}
