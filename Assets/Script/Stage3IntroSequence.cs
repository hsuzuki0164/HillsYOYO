using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PPYY; // StageCountdown を流用

namespace PPYY.Stage3
{
    // ステージ3専用の開始演出：
    // 画面内を漂う複数のゴーストが中央に集まり、パーティクルと効果音とともにボスへ変身する。
    // StageJingle の代わりにステージ3のシーンに配置し、演出終了後は StageCountdown を開始する
    public class Stage3IntroSequence : MonoBehaviour
    {
        [Header("演出開始前の暗転からのフェードイン")]
        [Tooltip("画面全体を覆う黒いUI Image（任意）")]
        public Image blackFadeImage;
        public float blackFadeInDuration = 1f;

        [Header("ゴーストの登場（波状に少しずつ増える）")]
        public GameObject ghostPrefab;
        public int ghostCount = 12;
        public Vector2 spawnAreaMin = new Vector2(-8f, -4f);
        public Vector2 spawnAreaMax = new Vector2(8f, 4f);
        [Tooltip("1回の波で登場する数（この範囲でランダム）")]
        public Vector2Int ghostsPerWaveRange = new Vector2Int(2, 3);
        [Tooltip("次の波が登場するまでの間隔")]
        public float waveInterval = 0.4f;
        [Tooltip("1体ずつフェードインする時間")]
        public float ghostFadeInDuration = 0.3f;
        [Tooltip("ゴーストが登場するたびに鳴らす効果音（複数登録するとランダム再生）")]
        public AudioClip[] ghostAppearSounds;
        [Range(0f, 1f)] public float ghostAppearSoundVolume = 1f;

        [Header("漂う動き")]
        [Tooltip("全員登場し終えてから、収束が始まるまでの追加の待ち時間")]
        public float driftDuration = 2f;
        [Tooltip("漂う際のふらつき幅")]
        public float driftAmplitude = 0.6f;

        [Header("中心へ収束")]
        [Tooltip("収束先。未指定（Vector3.zero）かつ Boss が設定されていれば Boss の位置を使う")]
        public Vector3 convergePoint = Vector3.zero;
        public float convergeDuration = 1.2f;
        public Ease convergeEase = Ease.InBack;
        [Tooltip("ゴーストが中心へ集まり始める瞬間に鳴らす効果音")]
        public AudioClip convergeSound;
        [Range(0f, 1f)] public float convergeSoundVolume = 1f;

        [Header("演出中（漂い〜収束の間）に流すパーティクル・音（ステージ開始と同時に再生）")]
        public GameObject introEffectPrefab;
        public AudioSource introAudioSource;
        public AudioClip introSound;

        [Header("変身の瞬間のパーティクル・効果音")]
        public GameObject transformEffectPrefab;
        public AudioClip transformSound;
        [Range(0f, 1f)] public float transformSoundVolume = 1f;

        [Tooltip("ボスのGameObject。演出開始時は非表示にしておき、変身の瞬間に表示する")]
        public GameObject boss;
        [Tooltip("ボスが登場するときのフェード時間")]
        public float bossFadeInDuration = 0.6f;
        [Tooltip("フェード完了後、カウントダウン開始までの余韻の秒数")]
        public float delayAfterTransform = 0.5f;

        [Header("演出終了後に開始するカウントダウン（任意）")]
        [Tooltip("ボスのフェード完了・余韻の後、カウントダウンを開始するまでさらに待つ秒数")]
        public float countdownStartDelay = 0f;
        public StageCountdown countdown;

        readonly List<GameObject> activeGhosts = new List<GameObject>();
        Vector3 convergeTarget;

        void Start()
        {
            Time.timeScale = 0f;
            if (boss != null) boss.SetActive(false);

            convergeTarget = (boss != null) ? boss.transform.position : convergePoint;

            // 間を空けず、Play On Awake相当のタイミングで即座に再生を始める
            if (introAudioSource != null && introSound != null)
            {
                introAudioSource.clip = introSound;
                introAudioSource.Play();
            }

            StartCoroutine(BlackFadeInThenStartIntro());
        }

        // 演出が始まる前に、黒画面から画面をフェードインさせる
        IEnumerator BlackFadeInThenStartIntro()
        {
            if (blackFadeImage != null)
            {
                blackFadeImage.gameObject.SetActive(true);
                Color c = blackFadeImage.color;
                c.a = 1f;
                blackFadeImage.color = c;

                blackFadeImage.DOKill();
                blackFadeImage.DOFade(0f, blackFadeInDuration).SetUpdate(true);
                yield return new WaitForSecondsRealtime(blackFadeInDuration);

                blackFadeImage.gameObject.SetActive(false);
            }

            InstantiateEffect(introEffectPrefab, convergeTarget);

            StartCoroutine(IntroRoutine());
        }

        // 2〜3体ずつフェードで登場させ、登場したものから漂い始める。既定数に達するまで繰り返す
        IEnumerator SpawnGhostsInWaves()
        {
            if (ghostPrefab == null) yield break;

            int spawned = 0;
            while (spawned < ghostCount)
            {
                int waveSize = Random.Range(ghostsPerWaveRange.x, ghostsPerWaveRange.y + 1);
                waveSize = Mathf.Min(waveSize, ghostCount - spawned);

                SfxPlayer.Play(
                    (ghostAppearSounds != null && ghostAppearSounds.Length > 0)
                        ? ghostAppearSounds[Random.Range(0, ghostAppearSounds.Length)]
                        : null,
                    ghostAppearSoundVolume);

                for (int i = 0; i < waveSize; i++)
                {
                    SpawnOneGhostWithFade();
                }

                spawned += waveSize;

                if (spawned < ghostCount)
                {
                    yield return new WaitForSecondsRealtime(waveInterval);
                }
            }
        }

        void SpawnOneGhostWithFade()
        {
            Vector3 pos = new Vector3(
                Random.Range(spawnAreaMin.x, spawnAreaMax.x),
                Random.Range(spawnAreaMin.y, spawnAreaMax.y),
                0f);

            var ghost = Instantiate(ghostPrefab, pos, Quaternion.identity);
            activeGhosts.Add(ghost);

            // フェードインで登場
            var sr = ghost.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 0f;
                sr.color = c;
                sr.DOFade(1f, ghostFadeInDuration).SetUpdate(true);
            }

            // 登場した瞬間から漂い始める：近くのランダムな位置へゆらゆら往復する
            Vector3 driftTarget = pos + (Vector3)(Random.insideUnitCircle * driftAmplitude);
            ghost.transform.DOMove(driftTarget, driftDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true); // timeScale=0 の間も動かす
        }

        IEnumerator IntroRoutine()
        {
            yield return StartCoroutine(SpawnGhostsInWaves());

            // timeScale=0 の間も進む実時間ベースの待機を使う
            yield return new WaitForSecondsRealtime(driftDuration);

            SfxPlayer.Play(convergeSound, convergeSoundVolume);

            foreach (var ghost in activeGhosts)
            {
                if (ghost == null) continue;
                ghost.transform.DOKill();
                ghost.transform.DOMove(convergeTarget, convergeDuration).SetEase(convergeEase).SetUpdate(true);
                ghost.transform.DOScale(0.3f, convergeDuration).SetUpdate(true);
            }

            yield return new WaitForSecondsRealtime(convergeDuration);

            // 変身：ゴーストを消し、パーティクルと効果音を出してボスを表示する
            foreach (var ghost in activeGhosts)
            {
                if (ghost != null) Destroy(ghost);
            }
            activeGhosts.Clear();

            InstantiateEffect(transformEffectPrefab, convergeTarget);
            SfxPlayer.Play(transformSound, transformSoundVolume);

            ShowBossWithFade();
            yield return new WaitForSecondsRealtime(bossFadeInDuration);

            yield return new WaitForSecondsRealtime(delayAfterTransform);
            yield return new WaitForSecondsRealtime(countdownStartDelay);

            if (countdown != null)
            {
                countdown.BeginCountdown();
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

        // Time.timeScale=0 の間はパーティクルシステムが既定(Scaled Delta Time)のままだと
        // 再生されない(止まって見える)ため、生成後に Unscaled Time を使うよう切り替える
        void InstantiateEffect(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;

            var effect = Instantiate(prefab, position, Quaternion.identity);
            var systems = effect.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }
        }

        // ボス配下の全SpriteRenderer（本体・目・手など）を透明な状態からフェードインさせる
        void ShowBossWithFade()
        {
            if (boss == null) return;

            boss.SetActive(true);

            var renderers = boss.GetComponentsInChildren<SpriteRenderer>();
            foreach (var r in renderers)
            {
                r.DOKill();
                Color c = r.color;
                c.a = 0f;
                r.color = c;
                r.DOFade(1f, bossFadeInDuration).SetUpdate(true); // timeScale=0 の間も動かす
            }
        }

        void OnDestroy()
        {
            // シーン遷移等でこのオブジェクトが消える際、timeScaleが0のまま残らないようにする保険
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }
}
