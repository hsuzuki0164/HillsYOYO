using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace PPYY
{
    // 制限時間終了時などに使う共通の演出：
    // 画面上部から扉が落ちてきて画面全体を覆う（効果音つき）→ 3秒待つ → ブラックアウト → 次シーンへ。
    // 各ステージのシーンに1つ配置し、Stage1StageManager / Stage2StageManager 等から
    // PlayAndLoadScene(sceneName) を呼び出して使う
    public class StageTransition : MonoBehaviour
    {
        public static StageTransition Instance { get; private set; }

        [Header("扉（画面上部から落ちてきて画面を覆うUI Image）")]
        public RectTransform doorRect;
        [Tooltip("扉が画面を覆い終えたときのY座標（anchoredPosition.y）")]
        public float doorClosedY = 0f;
        [Tooltip("開始時（画面の外・上）のY座標（anchoredPosition.y）。Canvasの高さ等に合わせて調整する")]
        public float doorStartY = 1200f;
        public float doorFallDuration = 0.6f;
        public Ease doorFallEase = Ease.InQuad;

        [Header("扉が落ちる効果音")]
        public AudioSource doorAudioSource;
        public AudioClip doorFallSound;

        [Header("扉が閉まってからの待ち時間")]
        public float holdDuration = 3f;

        [Header("ブラックアウト（扉とは別に画面全体を覆う黒いUI Image）")]
        public Image blackoutImage;
        public float blackoutDuration = 1f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (doorRect != null) doorRect.gameObject.SetActive(false);

            if (blackoutImage != null)
            {
                blackoutImage.gameObject.SetActive(false);
                var c = blackoutImage.color;
                c.a = 0f;
                blackoutImage.color = c;
            }
        }

        public void PlayAndLoadScene(string sceneName)
        {
            StartCoroutine(TransitionRoutine(sceneName));
        }

        IEnumerator TransitionRoutine(string sceneName)
        {
            StopAllStageAudio();
            ClearEnemiesAndSpawners();

            if (doorRect != null)
            {
                doorRect.gameObject.SetActive(true);
                Vector2 pos = doorRect.anchoredPosition;
                pos.y = doorStartY;
                doorRect.anchoredPosition = pos;

                if (doorAudioSource != null && doorFallSound != null)
                {
                    doorAudioSource.clip = doorFallSound;
                    doorAudioSource.Play();
                }

                doorRect.DOKill();
                doorRect.DOAnchorPosY(doorClosedY, doorFallDuration).SetEase(doorFallEase);
                yield return new WaitForSeconds(doorFallDuration);
            }

            yield return new WaitForSeconds(holdDuration);

            if (blackoutImage != null)
            {
                blackoutImage.gameObject.SetActive(true);
                blackoutImage.DOKill();
                blackoutImage.DOFade(1f, blackoutDuration);
                yield return new WaitForSeconds(blackoutDuration);
            }

            SceneManager.LoadScene(sceneName);
        }

        // シーン内で鳴っている全てのBGM・効果音を止める（扉の音はこの後に鳴らすので対象外）
        void StopAllStageAudio()
        {
            var sources = FindObjectsOfType<AudioSource>();
            foreach (var src in sources)
            {
                if (src == doorAudioSource) continue;
                src.Stop();
            }
        }

        // 残っている敵キャラクターを全て削除し、スポナーも止める（新しい敵が湧かないようにする）
        void ClearEnemiesAndSpawners()
        {
            foreach (var e in FindObjectsOfType<PPYY.Stage1.EnemyCharacter>()) Destroy(e.gameObject);
            foreach (var e in FindObjectsOfType<PPYY.Stage2.Stage2EnemyCharacter>()) Destroy(e.gameObject);
            foreach (var e in FindObjectsOfType<PPYY.Stage3.Stage3Minion>()) Destroy(e.gameObject);
            foreach (var e in FindObjectsOfType<PPYY.Stage3.Stage3Bomb>()) Destroy(e.gameObject);

            foreach (var s in FindObjectsOfType<PPYY.Stage1.EnemySpawner>()) s.gameObject.SetActive(false);
            foreach (var s in FindObjectsOfType<PPYY.Stage2.Stage2EnemySpawner>()) s.gameObject.SetActive(false);
        }
    }
}
