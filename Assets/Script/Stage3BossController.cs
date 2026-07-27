using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY.Stage3
{
    public class Stage3BossController : MonoBehaviour
    {
        enum BossState { Active, CoreExposed, Fleeing, Defeated }

        [Header("ライフ")]
        public int maxHp = 3000;
        public Text hpText; // 任意

        [Header("弱点（目・手）合計ヒットでコア露出")]
        public int weakPointHitsToExpose = 10;
        [Tooltip("弱点ヒット数の進捗表示（任意、例：3/10）")]
        public Text weakPointCounterText;

        [Header("コア露出")]
        public float coreExposureDuration = 5f;
        [Tooltip("方式A：口を閉じている間だけ表示する別オブジェクト（任意）")]
        public GameObject mouthClosedVisual;
        [Tooltip("方式A：口が開いてコアが出ている間だけ表示する別オブジェクト（任意）")]
        public GameObject mouthOpenVisual;
        [Tooltip("方式B：本体の1枚絵を差し替えたい場合、対象のSpriteRendererを指定（任意）")]
        public SpriteRenderer bodyRenderer;
        [Tooltip("方式B：口を閉じているときの見た目")]
        public Sprite mouthClosedSprite;
        [Tooltip("方式B：口が開いてコアが出ているときの見た目")]
        public Sprite mouthOpenSprite;
        public Stage3BossCore core;

        [Header("コア攻撃1回あたりのダメージ")]
        public int coreDamageMin = 50;
        public int coreDamageMax = 300;

        [Header("雑魚敵召喚")]
        public GameObject[] minionPrefabs;
        public Vector2 minionSpawnIntervalRange = new Vector2(6f, 12f);
        public int minionSpawnCount = 3;
        public Vector2 minionBoundsMin = new Vector2(-8f, -4f);
        public Vector2 minionBoundsMax = new Vector2(8f, 4f);

        [Header("爆弾")]
        public GameObject bombPrefab;
        public Vector2 bombIntervalRange = new Vector2(4f, 8f);
        [Tooltip("爆弾を投げ始める位置（ワールド座標）")]
        public Vector3 bombThrowOrigin = new Vector3(0f, 3f, 0f);

        [Header("手によるお宝強奪")]
        public Transform leftHand;
        public Transform rightHand;
        public Vector2 stealIntervalRange = new Vector2(8f, 14f);
        public int stealAmountMin = 50;
        public int stealAmountMax = 150;
        public float handReachDuration = 0.6f;
        public float bodyMoveDuration = 1f;

        [Header("盗んだお宝を手に持たせる見た目・お返し猶予")]
        public Sprite lootSprite;
        [Tooltip("この秒数以内に手をヒットするとお宝を取り返せる。経過すると消滅（盗まれたまま）")]
        public float lootReturnWindow = 3f;

        [Header("本体の中央位置（左右移動の復帰先）")]
        public float centerX = 0f;

        [Tooltip("お宝を取りに行く際、本体が中央からこの値の範囲（-x〜+x）でしか動かないようにする")]
        public float bodyMoveRangeX = 2f;

        [Header("本体：常時上下にふわふわ浮遊")]
        public float bodyBobAmplitude = 0.3f;
        public float bodyBobFrequency = 1f;

        [Header("手：常時上下にバタバタ（左右で位相をずらす）")]
        public float handBobAmplitude = 0.3f;
        public float handBobFrequency = 1.2f;

        [Header("ステージ制限時間")]
        public float timeLimit = 90f;
        public Text timerText;
        [Tooltip("撃破/逃走後に遷移するシーン名。空なら遷移しない")]
        public string nextSceneName = "";

        [Header("撃破時ボーナス（残り秒数に応じて0〜この値。1P/2P双方に加算）")]
        public int maxVictoryBonus = 5000;

        [Header("撃破演出：爆発（画面中に連続で発生）")]
        public GameObject explosionEffectPrefab;
        public float explosionDuration = 5f;
        public float explosionSpawnInterval = 0.3f;
        public Vector2 explosionAreaMin = new Vector2(-6f, -3f);
        public Vector2 explosionAreaMax = new Vector2(6f, 3f);

        [Header("撃破演出：爆発後、スカッシュ&ストレッチしながら上へ消える")]
        public float squashStretchScaleX = 12f;
        public float squashStretchScaleY = 0.3f;
        public float squashStretchDuration = 0.3f;
        public float pullUpDistance = 10f;
        public float pullUpDuration = 0.5f;

        [Header("撃破演出：消滅後の画面フラッシュ＋お宝の雨")]
        [Tooltip("画面全体を覆う白いUI Image（任意）")]
        public Image screenFlashImage;
        public float flashFadeInDuration = 0.2f;
        public float flashFadeOutDuration = 1.5f;
        public Stage3TreasureRain treasureRain; // 任意

        [Header("撃破演出：背景を明るくする（任意）")]
        [Tooltip("暗くしている背景のSpriteRenderer。複数レイヤーある場合は全て登録")]
        public SpriteRenderer[] backgroundRenderers;
        public Color brightBackgroundColor = Color.white;
        public float backgroundBrightenDuration = 1.5f;

        BossState state = BossState.Active;
        int currentHp;
        int weakPointHitCount;
        float stageRemaining;
        float minionTimer, bombTimer, stealTimer;

        float bodyBaseY;
        Vector3 leftHandBasePos, rightHandBasePos;
        bool leftHandBobPaused, rightHandBobPaused;
        float idleTime;

        void Start()
        {
            currentHp = maxHp;
            stageRemaining = timeLimit;
            transform.position = new Vector3(centerX, transform.position.y, transform.position.z);
            bodyBaseY = transform.position.y;

            if (leftHand != null) leftHandBasePos = leftHand.localPosition;
            if (rightHand != null) rightHandBasePos = rightHand.localPosition;

            ResetMinionTimer();
            ResetBombTimer();
            ResetStealTimer();
            SetMouthOpen(false);

            UpdateHpText();
            UpdateTimerText();
            UpdateWeakPointCounterText();
        }

        void Update()
        {
            if (state == BossState.Defeated || state == BossState.Fleeing) return;

            idleTime += Time.deltaTime;
            UpdateIdleMotion();

            stageRemaining -= Time.deltaTime;
            UpdateTimerText();
            if (stageRemaining <= 0f)
            {
                StartFlee();
                return;
            }

            if (state == BossState.Active)
            {
                TickAttackTimers();
            }
        }

        // 本体は上下にふわふわ浮遊（X方向はお宝強奪時のDOTweenに任せるため触らない）。
        // 手は常時上下にバタバタ（強奪アニメーション中は一時停止し、DOTweenと衝突しないようにする）
        void UpdateIdleMotion()
        {
            float bodyY = bodyBaseY + Mathf.Sin(idleTime * bodyBobFrequency) * bodyBobAmplitude;
            transform.position = new Vector3(transform.position.x, bodyY, transform.position.z);

            if (leftHand != null && !leftHandBobPaused)
            {
                float y = leftHandBasePos.y + Mathf.Sin(idleTime * handBobFrequency) * handBobAmplitude;
                leftHand.localPosition = new Vector3(leftHandBasePos.x, y, leftHandBasePos.z);
            }

            if (rightHand != null && !rightHandBobPaused)
            {
                // 右手は左手と半周期ずらして、同じ動きにならないようにする
                float y = rightHandBasePos.y + Mathf.Sin(idleTime * handBobFrequency + Mathf.PI) * handBobAmplitude;
                rightHand.localPosition = new Vector3(rightHandBasePos.x, y, rightHandBasePos.z);
            }
        }

        void TickAttackTimers()
        {
            minionTimer -= Time.deltaTime;
            if (minionTimer <= 0f)
            {
                SummonMinions();
                ResetMinionTimer();
            }

            bombTimer -= Time.deltaTime;
            if (bombTimer <= 0f)
            {
                ThrowBomb();
                ResetBombTimer();
            }

            stealTimer -= Time.deltaTime;
            if (stealTimer <= 0f)
            {
                StartCoroutine(HandStealRoutine());
                ResetStealTimer();
            }
        }

        void ResetMinionTimer() => minionTimer = Random.Range(minionSpawnIntervalRange.x, minionSpawnIntervalRange.y);
        void ResetBombTimer() => bombTimer = Random.Range(bombIntervalRange.x, bombIntervalRange.y);
        void ResetStealTimer() => stealTimer = Random.Range(stealIntervalRange.x, stealIntervalRange.y);

        void SummonMinions()
        {
            if (minionPrefabs == null || minionPrefabs.Length == 0) return;

            for (int i = 0; i < minionSpawnCount; i++)
            {
                var prefab = minionPrefabs[Random.Range(0, minionPrefabs.Length)];
                Vector3 pos = new Vector3(
                    Random.Range(minionBoundsMin.x, minionBoundsMax.x),
                    Random.Range(minionBoundsMin.y, minionBoundsMax.y),
                    0);
                Instantiate(prefab, pos, Quaternion.identity);
            }
        }

        void ThrowBomb()
        {
            if (bombPrefab == null || Stage3TreasureManager.Instance == null) return;

            PlayerSide side = Random.value < 0.5f ? PlayerSide.P1 : PlayerSide.P2;
            Vector3 landPos = Stage3TreasureManager.Instance.GetTreasureWorldPosition(side);

            var obj = Instantiate(bombPrefab);
            var bomb = obj.GetComponentInChildren<Stage3Bomb>();
            if (bomb != null) bomb.Init(bombThrowOrigin, landPos, side);
        }

        IEnumerator HandStealRoutine()
        {
            if (Stage3TreasureManager.Instance == null) yield break;

            PlayerSide side = Random.value < 0.5f ? PlayerSide.P1 : PlayerSide.P2;
            Vector3 pileTarget = Stage3TreasureManager.Instance.GetTreasureWorldPosition(side);
            Transform hand = side == PlayerSide.P1 ? leftHand : rightHand;

            float clampedX = Mathf.Clamp(pileTarget.x, -bodyMoveRangeX, bodyMoveRangeX);

            // 本体がその方向へ寄る（X方向のみ。Y方向は常時の浮遊アニメーションに任せる）
            transform.DOKill();
            transform.DOMoveX(clampedX, bodyMoveDuration).SetEase(Ease.InOutQuad);
            yield return new WaitForSeconds(bodyMoveDuration);

            if (hand != null)
            {
                SetHandBobPaused(side, true); // 常時バタバタと強奪アニメーションが衝突しないよう一時停止
                Vector3 handHome = hand.position;

                hand.DOKill();
                hand.DOMove(pileTarget, handReachDuration).SetEase(Ease.OutQuad);
                yield return new WaitForSeconds(handReachDuration);

                int amount = Random.Range(stealAmountMin, stealAmountMax + 1);
                Stage3TreasureManager.Instance.StealPoints(side, amount);

                var handWeakPoint = hand.GetComponent<Stage3BossWeakPoint>();
                if (handWeakPoint != null) handWeakPoint.ReceiveLoot(amount, side, lootSprite, lootReturnWindow);

                hand.DOMove(handHome, handReachDuration).SetEase(Ease.InQuad);
                yield return new WaitForSeconds(handReachDuration);

                UpdateHandBaseFromCurrent(side); // 戻った位置を新しいバタバタの基準にする
                SetHandBobPaused(side, false);
            }

            // 本体は中央へ戻る
            transform.DOKill();
            transform.DOMoveX(centerX, bodyMoveDuration).SetEase(Ease.InOutQuad);
        }

        void SetHandBobPaused(PlayerSide side, bool paused)
        {
            if (side == PlayerSide.P1) leftHandBobPaused = paused;
            else rightHandBobPaused = paused;
        }

        void UpdateHandBaseFromCurrent(PlayerSide side)
        {
            if (side == PlayerSide.P1 && leftHand != null) leftHandBasePos = leftHand.localPosition;
            else if (side == PlayerSide.P2 && rightHand != null) rightHandBasePos = rightHand.localPosition;
        }

        public void OnWeakPointHit(Stage3BossWeakPoint point, Vector2 worldPos)
        {
            if (state != BossState.Active) return;

            weakPointHitCount++;
            UpdateWeakPointCounterText();

            if (weakPointHitCount >= weakPointHitsToExpose)
            {
                ExposeCore();
            }
        }

        void UpdateWeakPointCounterText()
        {
            if (weakPointCounterText) weakPointCounterText.text = weakPointHitCount + " / " + weakPointHitsToExpose;
        }

        void ExposeCore()
        {
            state = BossState.CoreExposed;
            SetMouthOpen(true);
            StartCoroutine(CoreExposureRoutine());
        }

        IEnumerator CoreExposureRoutine()
        {
            yield return new WaitForSeconds(coreExposureDuration);

            if (state == BossState.CoreExposed)
            {
                CloseMouth();
            }
        }

        void CloseMouth()
        {
            state = BossState.Active;
            weakPointHitCount = 0;
            UpdateWeakPointCounterText();
            SetMouthOpen(false);
        }

        void SetMouthOpen(bool open)
        {
            // 方式A：別オブジェクトの表示切り替え
            if (mouthClosedVisual != null) mouthClosedVisual.SetActive(!open);
            if (mouthOpenVisual != null) mouthOpenVisual.SetActive(open);

            // 方式B：1枚のSpriteRendererの画像差し替え
            if (bodyRenderer != null)
            {
                Sprite sprite = open ? mouthOpenSprite : mouthClosedSprite;
                if (sprite != null) bodyRenderer.sprite = sprite;
            }

            if (core != null)
            {
                core.gameObject.SetActive(open);
                core.SetVisible(open);
            }
        }

        public void OnCoreHit(Vector2 worldPos)
        {
            if (state != BossState.CoreExposed) return;

            int damage = Random.Range(coreDamageMin, coreDamageMax + 1);
            currentHp = Mathf.Max(0, currentHp - damage);
            UpdateHpText();

            if (currentHp <= 0)
            {
                Defeat();
            }
        }

        void Defeat()
        {
            state = BossState.Defeated;
            StopAllCoroutines();
            SetMouthOpen(false);

            int bonus = Mathf.RoundToInt(maxVictoryBonus * Mathf.Clamp01(stageRemaining / timeLimit));
            if (Stage3TreasureManager.Instance != null)
            {
                Stage3TreasureManager.Instance.AddPoints(PlayerSide.P1, bonus);
                Stage3TreasureManager.Instance.AddPoints(PlayerSide.P2, bonus);
            }

            StartCoroutine(DefeatSequence());
        }

        IEnumerator DefeatSequence()
        {
            yield return StartCoroutine(PlayExplosionBurst());
            PlayDisappearAnimation();
        }

        // 画面中に爆発エフェクトを連続で発生させる
        IEnumerator PlayExplosionBurst()
        {
            float elapsed = 0f;
            while (elapsed < explosionDuration)
            {
                SpawnExplosionAtRandomPosition();
                yield return new WaitForSeconds(explosionSpawnInterval);
                elapsed += explosionSpawnInterval;
            }
        }

        void SpawnExplosionAtRandomPosition()
        {
            if (explosionEffectPrefab == null) return;

            Vector3 pos = new Vector3(
                Random.Range(explosionAreaMin.x, explosionAreaMax.x),
                Random.Range(explosionAreaMin.y, explosionAreaMax.y),
                0f);
            Instantiate(explosionEffectPrefab, pos, Quaternion.identity);
        }

        // 横に大きく伸びて潰れる（スカッシュ&ストレッチ）→ そのまま上に引っ張られて消える
        void PlayDisappearAnimation()
        {
            transform.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOScale(new Vector3(squashStretchScaleX, squashStretchScaleY, 1f), squashStretchDuration).SetEase(Ease.OutQuad));
            seq.Append(transform.DOMoveY(transform.position.y + pullUpDistance, pullUpDuration).SetEase(Ease.InBack));
            seq.Join(transform.DOScale(Vector3.zero, pullUpDuration).SetEase(Ease.InBack));
            seq.OnComplete(PlayVictoryCelebration);
        }

        // ボスが消えた後、画面フラッシュ・背景の明転・お宝の雨を演出してからシーン遷移する
        void PlayVictoryCelebration()
        {
            PlayScreenFlash();
            BrightenBackground();
            if (treasureRain != null) treasureRain.Play();

            float rainDuration = treasureRain != null ? treasureRain.spawnDuration + 1f : 0f;
            float flashDuration = flashFadeInDuration + flashFadeOutDuration;
            float delay = Mathf.Max(rainDuration, flashDuration, backgroundBrightenDuration);

            StartCoroutine(DelayedGoToNextScene(delay));
        }

        void BrightenBackground()
        {
            if (backgroundRenderers == null) return;

            foreach (var r in backgroundRenderers)
            {
                if (r == null) continue;
                r.DOKill();
                r.DOColor(brightBackgroundColor, backgroundBrightenDuration);
            }
        }

        void PlayScreenFlash()
        {
            if (screenFlashImage == null) return;

            screenFlashImage.gameObject.SetActive(true);
            Color c = screenFlashImage.color;
            c.a = 0f;
            screenFlashImage.color = c;

            screenFlashImage.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.Append(screenFlashImage.DOFade(1f, flashFadeInDuration));
            seq.Append(screenFlashImage.DOFade(0f, flashFadeOutDuration));
            seq.OnComplete(() => screenFlashImage.gameObject.SetActive(false));
        }

        IEnumerator DelayedGoToNextScene(float delay)
        {
            yield return new WaitForSeconds(delay);
            GoToNextScene();
        }

        void StartFlee()
        {
            state = BossState.Fleeing;
            StopAllCoroutines();
            SetMouthOpen(false);

            transform.DOKill();
            transform.DOMoveY(transform.position.y + 6f, 1.2f).SetEase(Ease.InQuad)
                .OnComplete(GoToNextScene);
        }

        void GoToNextScene()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }

        void UpdateHpText()
        {
            if (hpText) hpText.text = currentHp + " / " + maxHp;
        }

        void UpdateTimerText()
        {
            if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(0f, stageRemaining)).ToString();
        }
    }
}
