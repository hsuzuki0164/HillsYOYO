using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace PPYY
{
    // タイトル画面。
    // 0.BGM再生 → 1.ホワイトイン → 2.タイトル画像が上からスライドイン → 3.以降タイトルが上下に浮遊
    // → 4.中央オブジェクトがフェードイン → 5/6.左右のオブジェクトが同時にスライドイン
    // → 7.スタートボタンがフェードイン → 8.スタートボタンを押すと点滅+効果音 → 9.ホワイトアウトして次シーンへ
    public class TitleScreen : MonoBehaviour
    {
        [Header("0. BGM（開始と同時に再生、スタートボタンを押すとフェードアウト）")]
        public AudioSource bgmSource;
        public AudioClip bgmClip;
        public float bgmFadeOutDuration = 1f;

        [Header("1. 画面のホワイトイン")]
        public Image whiteFadeImage;
        public float whiteFadeInDuration = 1f;

        [Header("2. タイトル画像が上からスライドイン")]
        public RectTransform titleImage;
        public float titleSlideInDuration = 0.8f;
        public Ease titleSlideInEase = Ease.OutBack;
        [Tooltip("スライドイン開始位置（最終位置からのYオフセット）")]
        public float titleSlideStartOffsetY = 500f;

        [Header("3. タイトル画像の上下浮遊（スライドイン後、常時）")]
        public float titleFloatAmplitude = 15f;
        public float titleFloatFrequency = 1f;

        [Header("4. 中央のオブジェクト（フェードイン。各オブジェクトにCanvasGroupが必要）")]
        public CanvasGroup[] centerObjects;
        public float centerFadeInDuration = 0.5f;

        [Header("5・6. 左から3つ／右から4つ（同時にスライドイン）")]
        public RectTransform[] leftObjects;
        public RectTransform[] rightObjects;
        public float sideSlideInDuration = 0.6f;
        public Ease sideSlideInEase = Ease.OutBack;
        public float sideSlideDistance = 600f;

        [Header("7. スタートボタンのフェードイン")]
        public CanvasGroup startButtonGroup;
        public Button startButton;
        public float startButtonFadeInDuration = 0.5f;

        [Header("8. スタートボタンを押した時（点滅＋効果音）")]
        public AudioClip startSound;
        [Range(0f, 1f)] public float startSoundVolume = 1f;
        public int blinkCount = 4;
        public float blinkInterval = 0.08f;

        [Header("9. ブラックアウトして次のシーンへ")]
        [Tooltip("画面全体を覆う黒いUI Image（1.のWhite Fade Imageとは別に用意する）")]
        public Image blackFadeImage;
        public float blackFadeOutDuration = 1f;
        public string nextSceneName = "Stage1";

        Vector2 titleBasePos;
        Vector2[] leftOriginalPos;
        Vector2[] rightOriginalPos;

        bool titleFloating;
        float floatTime;
        bool started;

        void Start()
        {
            // 0. BGM
            if (bgmSource != null && bgmClip != null)
            {
                bgmSource.clip = bgmClip;
                bgmSource.Play();
            }

            HideAllForIntro();

            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
            }

            StartCoroutine(IntroSequence());
        }

        void Update()
        {
            if (!titleFloating || titleImage == null) return;

            floatTime += Time.deltaTime;
            float y = titleBasePos.y + Mathf.Sin(floatTime * titleFloatFrequency) * titleFloatAmplitude;
            titleImage.anchoredPosition = new Vector2(titleImage.anchoredPosition.x, y);
        }

        // 演出開始前に、各オブジェクトを初期状態（非表示・画面外）にしておく
        void HideAllForIntro()
        {
            if (whiteFadeImage != null)
            {
                whiteFadeImage.gameObject.SetActive(true);
                Color c = whiteFadeImage.color;
                c.a = 1f;
                whiteFadeImage.color = c;
            }

            if (titleImage != null)
            {
                titleBasePos = titleImage.anchoredPosition;
                titleImage.anchoredPosition = titleBasePos + new Vector2(0f, titleSlideStartOffsetY);
            }

            SetGroupsAlpha(centerObjects, 0f);

            leftOriginalPos = CaptureAndOffset(leftObjects, -sideSlideDistance);
            rightOriginalPos = CaptureAndOffset(rightObjects, sideSlideDistance);

            if (startButtonGroup != null)
            {
                startButtonGroup.alpha = 0f;
                startButtonGroup.interactable = false;
                startButtonGroup.blocksRaycasts = false;
            }
        }

        Vector2[] CaptureAndOffset(RectTransform[] objects, float offsetX)
        {
            if (objects == null) return null;

            var original = new Vector2[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null) continue;
                original[i] = objects[i].anchoredPosition;
                objects[i].anchoredPosition = original[i] + new Vector2(offsetX, 0f);
            }
            return original;
        }

        void SetGroupsAlpha(CanvasGroup[] groups, float alpha)
        {
            if (groups == null) return;
            foreach (var g in groups)
            {
                if (g != null) g.alpha = alpha;
            }
        }

        IEnumerator IntroSequence()
        {
            // 1. ホワイトイン
            if (whiteFadeImage != null)
            {
                whiteFadeImage.DOKill();
                whiteFadeImage.DOFade(0f, whiteFadeInDuration);
                yield return new WaitForSeconds(whiteFadeInDuration);
                whiteFadeImage.gameObject.SetActive(false);
            }

            // 2. タイトル画像が上からスライドイン
            if (titleImage != null)
            {
                titleImage.DOKill();
                titleImage.DOAnchorPos(titleBasePos, titleSlideInDuration).SetEase(titleSlideInEase);
                yield return new WaitForSeconds(titleSlideInDuration);
            }

            // 3. 以降、上下に浮遊させ続ける
            titleFloating = true;

            // 4. 中央オブジェクトがフェードイン
            FadeInGroups(centerObjects);
            yield return new WaitForSeconds(centerFadeInDuration);

            // 5・6. 左右のオブジェクトが同時にスライドイン
            SlideInObjects(leftObjects, leftOriginalPos);
            SlideInObjects(rightObjects, rightOriginalPos);
            yield return new WaitForSeconds(sideSlideInDuration);

            // 7. スタートボタンがフェードイン
            if (startButtonGroup != null)
            {
                startButtonGroup.DOKill();
                startButtonGroup.DOFade(1f, startButtonFadeInDuration);
                startButtonGroup.interactable = true;
                startButtonGroup.blocksRaycasts = true;
            }
        }

        void FadeInGroups(CanvasGroup[] groups)
        {
            if (groups == null) return;
            foreach (var g in groups)
            {
                if (g == null) continue;
                g.DOKill();
                g.DOFade(1f, centerFadeInDuration);
            }
        }

        void SlideInObjects(RectTransform[] objects, Vector2[] originalPositions)
        {
            if (objects == null || originalPositions == null) return;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null) continue;
                objects[i].DOKill();
                objects[i].DOAnchorPos(originalPositions[i], sideSlideInDuration).SetEase(sideSlideInEase);
            }
        }

        // 8. スタートボタンを押した時：点滅＋効果音 → 9. ホワイトアウトして次のシーンへ
        void OnStartButtonClicked()
        {
            if (started) return;
            started = true;

            if (startButtonGroup != null)
            {
                startButtonGroup.interactable = false;
                startButtonGroup.blocksRaycasts = false;
            }

            StartCoroutine(StartButtonSequence());
        }

        void FadeOutBgm()
        {
            if (bgmSource == null) return;
            bgmSource.DOKill();
            bgmSource.DOFade(0f, bgmFadeOutDuration).OnComplete(() => bgmSource.Stop());
        }

        IEnumerator StartButtonSequence()
        {
            FadeOutBgm();
            SfxPlayer.Play(startSound, startSoundVolume);

            for (int i = 0; i < blinkCount; i++)
            {
                if (startButtonGroup != null) startButtonGroup.alpha = 0f;
                yield return new WaitForSeconds(blinkInterval);
                if (startButtonGroup != null) startButtonGroup.alpha = 1f;
                yield return new WaitForSeconds(blinkInterval);
            }

            if (blackFadeImage != null)
            {
                blackFadeImage.gameObject.SetActive(true);
                Color c = blackFadeImage.color;
                c.a = 0f;
                blackFadeImage.color = c;

                blackFadeImage.DOKill();
                blackFadeImage.DOFade(1f, blackFadeOutDuration);
                yield return new WaitForSeconds(blackFadeOutDuration);
            }

            SceneManager.LoadScene(nextSceneName);
        }
    }
}
