using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        public TextMeshProUGUI hpText; // 任意
        [Tooltip("体力ゲージ（任意）。ImageのImage Type=Filled, Fill Method=Horizontal, Fill Origin=Left に設定しておくと、左基準で右側から減っていくバーになる")]
        public Image hpBarImage;

        [Header("弱点（目・手）合計ヒットでコア露出")]
        public int weakPointHitsToExpose = 10;
        [Tooltip("弱点ヒット数の進捗表示（任意、例：3/10）")]
        public TextMeshProUGUI weakPointCounterText;

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
        [Header("口の開閉の効果音")]
        public AudioClip mouthOpenSound;
        public AudioClip mouthCloseSound;
        [Range(0f, 1f)] public float mouthSoundVolume = 1f;

        [Header("コア攻撃1回あたりのダメージ")]
        public int coreDamageMin = 50;
        public int coreDamageMax = 300;


        [Header("雑魚敵召喚")]
        public GameObject[] minionPrefabs;
        public Vector2 minionSpawnIntervalRange = new Vector2(6f, 12f);
        public int minionSpawnCount = 3;
        public Vector2 minionBoundsMin = new Vector2(-8f, -4f);
        public Vector2 minionBoundsMax = new Vector2(8f, 4f);

        [Header("低HP時の強化（体力がこの割合以下になったら発動。1回だけ）")]
        [Range(0f, 1f)] public float enrageHpRatio = 0.2f;
        [Tooltip("左右へ素早く動く頻度の倍率（間隔をこの倍率で割る）")]
        public float enrageMoveFrequencyMultiplier = 1.5f;
        [Tooltip("雑魚敵召喚の頻度の倍率（間隔をこの倍率で割る）")]
        public float enrageMinionFrequencyMultiplier = 3f;

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
        [Tooltip("お宝を強奪した瞬間の効果音")]
        public AudioClip stealSound;
        [Range(0f, 1f)] public float stealSoundVolume = 1f;

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

        [Header("本体：常時ランダムに左右へ素早く移動")]
        public float sideMoveIntervalMin = 1f;
        public float sideMoveIntervalMax = 3f;
        [Tooltip("中央からどれだけ左右に動くか")]
        public float sideMoveRangeX = 2f;
        [Tooltip("移動にかける時間（短いほど素早く動く）")]
        public float sideMoveDuration = 0.25f;
        public Ease sideMoveEase = Ease.OutQuad;

        [Header("ステージ制限時間")]
        public float timeLimit = 90f;
        public TextMeshProUGUI timerText;
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
        [Tooltip("爆発が発生するたびに鳴らす効果音（複数登録するとランダム再生）")]
        public AudioClip[] explosionSounds;
        [Range(0f, 1f)] public float explosionSoundVolume = 1f;
        [Tooltip("爆発が終わってから、断末魔・消滅アニメーションを始めるまでの間の秒数")]
        public float delayAfterExplosion = 0.5f;

        [Header("撃破演出：爆発後、スカッシュ&ストレッチしながら上へ消える")]
        public float squashStretchScaleX = 12f;
        public float squashStretchScaleY = 0.3f;
        public float squashStretchDuration = 0.3f;
        public float pullUpDistance = 10f;
        public float pullUpDuration = 0.5f;
        [Tooltip("伸びて消える瞬間に鳴らす断末魔の声")]
        public AudioClip deathCrySound;
        [Range(0f, 1f)] public float deathCrySoundVolume = 1f;
        [Tooltip("撃破した瞬間に停止する・逃走時にフェードアウトするBGM（StageCountdownが再生しているものと同じAudioSourceを指定）")]
        public AudioSource bgm;
        [Tooltip("逃走時、BGMをフェードアウトする時間")]
        public float bgmFadeOutDuration = 1f;

        [Header("撃破演出：消滅後の画面フラッシュ＋お宝の雨")]
        [Tooltip("画面全体を覆う白いUI Image（任意）")]
        public Image screenFlashImage;
        public float flashFadeInDuration = 0.2f;
        public float flashFadeOutDuration = 1.5f;
        public Stage3TreasureRain treasureRain; // 任意

        [Header("お宝の雨の演出後、ホワイトアウトしてから次のシーンへ")]
        [Tooltip("画面全体を覆う白いUI Image（screenFlashImageとは別。任意）")]
        public Image whiteoutImage;
        public float whiteoutDuration = 1f;

        [Header("お宝の雨が始まってから指定秒後に鳴らす別BGM")]
        public AudioSource victoryBgmSource;
        public AudioClip victoryBgmClip;
        public float victoryBgmDelay = 3f;

        [Header("「ボス撃破」テキスト（お宝の雨と同時に表示）")]
        public TextMeshProUGUI bossDefeatedText;
        public float bossDefeatedTextDuration = 0.5f;
        public Ease bossDefeatedTextEase = Ease.OutBack;

        [Header("撃破演出：背景を明るくする（任意）")]
        [Tooltip("暗くしている背景のSpriteRenderer。複数レイヤーある場合は全て登録")]
        public SpriteRenderer[] backgroundRenderers;
        public Color brightBackgroundColor = Color.white;
        public float backgroundBrightenDuration = 1.5f;

        [Header("倒せなかった場合の逃走演出：①逃走音＋本体が分裂して消える")]
        public AudioClip fleeSound;
        [Range(0f, 1f)] public float fleeSoundVolume = 1f;
        [Tooltip("本体が分裂するゴーストのプレハブ（イントロと同じもので良い）")]
        public GameObject fleeGhostPrefab;
        public int fleeGhostCount = 12;
        [Tooltip("ゴーストが散らばる半径")]
        public float fleeGhostScatterRadius = 4f;
        public float fleeGhostScatterDuration = 1f;
        public float fleeGhostFadeOutDuration = 0.5f;

        [Header("倒せなかった場合の逃走演出：②「ボス逃走」テキスト")]
        public TextMeshProUGUI bossFledText;
        public float bossFledTextDuration = 0.5f;
        public Ease bossFledTextEase = Ease.OutBack;
        [Tooltip("テキスト表示後、次に進むまでの保持時間")]
        public float bossFledTextHoldDuration = 1f;

        [Header("倒せなかった場合の逃走演出：③悲しい効果音")]
        public AudioClip sadSound;
        [Range(0f, 1f)] public float sadSoundVolume = 1f;
        [Tooltip("sadSoundが未設定の場合の待ち時間")]
        public float sadSoundFallbackHold = 1.5f;

        [Header("倒せなかった場合の逃走演出：④ブラックアウトしてから次のステージへ")]
        public Image fleeBlackoutImage;
        public float fleeBlackoutDuration = 1f;

        BossState state = BossState.Active;
        int currentHp;
        int weakPointHitCount;
        float stageRemaining;
        float minionTimer, bombTimer, stealTimer;

        float bodyBaseY;
        Vector3 leftHandBasePos, rightHandBasePos;
        bool leftHandBobPaused, rightHandBobPaused;
        float idleTime;
        bool sideMovePaused;
        bool enraged;
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
            ApplyMouthVisual(false); // 初期化時は音を鳴らさない

            UpdateHpText();
            UpdateTimerText();
            UpdateWeakPointCounterText();

            StartCoroutine(SideMoveLoop());
        }

        // ランダムな間隔で左右へ素早く移動する。強奪アニメーション中(sideMovePaused)は動かない
        IEnumerator SideMoveLoop()
        {
            while (state != BossState.Defeated && state != BossState.Fleeing)
            {
                float wait = Random.Range(sideMoveIntervalMin, sideMoveIntervalMax);
                if (enraged) wait /= enrageMoveFrequencyMultiplier;
                yield return new WaitForSeconds(wait);

                if (state == BossState.Defeated || state == BossState.Fleeing) yield break;
                if (sideMovePaused) continue;

                float targetX = Mathf.Clamp(
                    centerX + Random.Range(-sideMoveRangeX, sideMoveRangeX),
                    -bodyMoveRangeX, bodyMoveRangeX);

                transform.DOKill();
                transform.DOMoveX(targetX, sideMoveDuration).SetEase(sideMoveEase);
            }
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

        void ResetMinionTimer()
        {
            minionTimer = Random.Range(minionSpawnIntervalRange.x, minionSpawnIntervalRange.y);
            if (enraged) minionTimer /= enrageMinionFrequencyMultiplier;
        }
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

            sideMovePaused = true; // 常時のランダム左右移動と衝突しないよう一時停止

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
                SfxPlayer.Play(stealSound, stealSoundVolume);

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
            yield return new WaitForSeconds(bodyMoveDuration);

            sideMovePaused = false;
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
            SfxPlayer.Play(open ? mouthOpenSound : mouthCloseSound, mouthSoundVolume);
            ApplyMouthVisual(open);
        }

        void ApplyMouthVisual(bool open)
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
            NotifyEyes(autoRevert: true);

            if (!enraged && currentHp <= maxHp * enrageHpRatio)
            {
                enraged = true; // 以降、動く頻度・雑魚召喚頻度が上がる（次の間隔リセットから反映）
            }

            if (currentHp <= 0)
            {
                Defeat();
            }
        }

        // 自分の子から目(Eyeロール)のStage3BossWeakPointを自動的に探して反応させる。
        // Inspectorでの個別の参照設定は不要。autoRevert=falseなら変化させたまま戻さない
        void NotifyEyes(bool autoRevert)
        {
            var weakPoints = GetComponentsInChildren<Stage3BossWeakPoint>();
            foreach (var wp in weakPoints)
            {
                if (wp.role == WeakPointRole.Eye) wp.PlayCoreHitReaction(autoRevert);
            }
        }

        void Defeat()
        {
            state = BossState.Defeated;
            StopAllCoroutines();
            ApplyMouthVisual(true); // 倒れた後は口を開けたままにする（音は鳴らさない）
            NotifyEyes(autoRevert: false); // 目も攻撃された時と同じ見た目に変え、そのままにする

            // ApplyMouthVisual(true)は口を開ける際にコアも一緒に表示してしまうため、
            // 爆発が起こる前にコアの画像だけは改めて消しておく
            if (core != null)
            {
                core.gameObject.SetActive(false);
                core.SetVisible(false);
            }

            if (bgm != null) bgm.Stop();

            ClearLingeringObjects();

            int bonus = Mathf.RoundToInt(maxVictoryBonus * Mathf.Clamp01(stageRemaining / timeLimit));
            if (Stage3TreasureManager.Instance != null)
            {
                Stage3TreasureManager.Instance.AddPoints(PlayerSide.P1, bonus);
                Stage3TreasureManager.Instance.AddPoints(PlayerSide.P2, bonus);
            }

            GameSession.BossDefeated = true;
            SaveFinalResultsToGameSession();

            StartCoroutine(DefeatSequence());
        }

        // 変身演出の永続パーティクル（DestroyOnBossDefeatが付いたもの）と、
        // 残っている雑魚キャラクターをまとめて削除する
        void ClearLingeringObjects()
        {
            foreach (var d in FindObjectsOfType<DestroyOnBossDefeat>()) Destroy(d.gameObject);
            foreach (var m in FindObjectsOfType<Stage3Minion>()) Destroy(m.gameObject);
        }

        IEnumerator DefeatSequence()
        {
            yield return StartCoroutine(PlayExplosionBurst());
            yield return new WaitForSeconds(delayAfterExplosion);
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

            if (explosionSounds != null && explosionSounds.Length > 0)
            {
                SfxPlayer.Play(explosionSounds[Random.Range(0, explosionSounds.Length)], explosionSoundVolume);
            }
        }

        // 横に大きく伸びて潰れる（スカッシュ&ストレッチ）→ そのまま上に引っ張られて消える
        void PlayDisappearAnimation()
        {
            SfxPlayer.Play(deathCrySound, deathCrySoundVolume);

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
            if (treasureRain != null)
            {
                treasureRain.Play();
                StartCoroutine(PlayVictoryBgmAfterDelay());
                ShowBossDefeatedText();
            }

            float rainDuration = treasureRain != null ? treasureRain.spawnDuration + 1f : 0f;
            float flashDuration = flashFadeInDuration + flashFadeOutDuration;
            float delay = Mathf.Max(rainDuration, flashDuration, backgroundBrightenDuration);

            StartCoroutine(DelayedGoToNextScene(delay));
        }

        // お宝の雨が始まってから victoryBgmDelay 秒後に別のBGMを鳴らす
        IEnumerator PlayVictoryBgmAfterDelay()
        {
            yield return new WaitForSeconds(victoryBgmDelay);

            if (victoryBgmSource != null && victoryBgmClip != null)
            {
                victoryBgmSource.clip = victoryBgmClip;
                victoryBgmSource.Play();
            }
        }

        // 「ボス撃破」テキストを拡大＋フェードで表示する
        void ShowBossDefeatedText()
        {
            if (bossDefeatedText == null) return;

            bossDefeatedText.gameObject.SetActive(true);

            bossDefeatedText.rectTransform.DOKill();
            bossDefeatedText.DOKill();

            bossDefeatedText.rectTransform.localScale = Vector3.zero;
            Color c = bossDefeatedText.color;
            c.a = 0f;
            bossDefeatedText.color = c;

            bossDefeatedText.rectTransform.DOScale(1f, bossDefeatedTextDuration).SetEase(bossDefeatedTextEase);
            bossDefeatedText.DOFade(1f, bossDefeatedTextDuration).SetEase(bossDefeatedTextEase);
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

            if (whiteoutImage != null)
            {
                whiteoutImage.gameObject.SetActive(true);
                Color c = whiteoutImage.color;
                c.a = 0f;
                whiteoutImage.color = c;

                whiteoutImage.DOKill();
                whiteoutImage.DOFade(1f, whiteoutDuration);
                yield return new WaitForSeconds(whiteoutDuration);
            }

            GoToNextScene();
        }

        void StartFlee()
        {
            state = BossState.Fleeing;
            StopAllCoroutines();
            SetMouthOpen(false);

            GameSession.BossDefeated = false;
            SaveFinalResultsToGameSession();

            transform.DOKill();
            StartCoroutine(FleeSequence());
        }

        // 結果発表画面で使う最終値をGameSessionへ書き込む（撃破・逃走どちらの場合も呼ばれる）
        void SaveFinalResultsToGameSession()
        {
            if (Stage3TreasureManager.Instance == null) return;

            GameSession.Stage3ScoreP1 = Stage3TreasureManager.Instance.GetPoints(PlayerSide.P1);
            GameSession.Stage3ScoreP2 = Stage3TreasureManager.Instance.GetPoints(PlayerSide.P2);

            GameSession.EnemiesDefeatedP1 = Stage3TreasureManager.Instance.GetEnemiesDefeated(PlayerSide.P1);
            GameSession.EnemiesDefeatedP2 = Stage3TreasureManager.Instance.GetEnemiesDefeated(PlayerSide.P2);
        }

        void FadeOutBgm()
        {
            if (bgm == null) return;
            bgm.DOKill();
            bgm.DOFade(0f, bgmFadeOutDuration).OnComplete(() => bgm.Stop());
        }

        IEnumerator FleeSequence()
        {
            FadeOutBgm();

            // ①逃走音とともに、本体が複数のゴーストに分裂してフェードで消える
            SfxPlayer.Play(fleeSound, fleeSoundVolume);
            yield return StartCoroutine(SplitIntoFleeGhosts());

            // ②「ボス逃走」テキスト表示と③悲しい効果音を同時に再生
            ShowBossFledText();
            SfxPlayer.Play(sadSound, sadSoundVolume);

            float textWait = bossFledTextDuration + bossFledTextHoldDuration;
            float sadWait = sadSound != null ? sadSound.length : sadSoundFallbackHold;
            yield return new WaitForSeconds(Mathf.Max(textWait, sadWait));

            // ④ブラックアウト
            yield return StartCoroutine(PlayFleeBlackout());

            // ⑤次のステージへ
            GoToNextScene();
        }

        // 本体を隠し、その場から複数のゴーストが散らばりながらフェードアウトする
        IEnumerator SplitIntoFleeGhosts()
        {
            Vector3 origin = transform.position;

            var bodyRenderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (var r in bodyRenderers) r.enabled = false;

            if (fleeGhostPrefab == null) yield break;

            var ghosts = new List<GameObject>();
            for (int i = 0; i < fleeGhostCount; i++)
            {
                var ghost = Instantiate(fleeGhostPrefab, origin, Quaternion.identity);
                ghosts.Add(ghost);

                Vector2 offset = Random.insideUnitCircle * fleeGhostScatterRadius;
                Vector3 target = origin + new Vector3(offset.x, offset.y, 0f);
                ghost.transform.DOMove(target, fleeGhostScatterDuration).SetEase(Ease.OutQuad);
            }

            yield return new WaitForSeconds(fleeGhostScatterDuration);

            foreach (var ghost in ghosts)
            {
                if (ghost == null) continue;
                var sr = ghost.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.DOFade(0f, fleeGhostFadeOutDuration);
            }

            yield return new WaitForSeconds(fleeGhostFadeOutDuration);

            foreach (var ghost in ghosts)
            {
                if (ghost != null) Destroy(ghost);
            }
        }

        // 「ボス逃走」テキストを拡大＋フェードで表示する
        void ShowBossFledText()
        {
            if (bossFledText == null) return;

            bossFledText.gameObject.SetActive(true);
            bossFledText.rectTransform.DOKill();
            bossFledText.DOKill();

            bossFledText.rectTransform.localScale = Vector3.zero;
            Color c = bossFledText.color;
            c.a = 0f;
            bossFledText.color = c;

            bossFledText.rectTransform.DOScale(1f, bossFledTextDuration).SetEase(bossFledTextEase);
            bossFledText.DOFade(1f, bossFledTextDuration).SetEase(bossFledTextEase);
        }

        IEnumerator PlayFleeBlackout()
        {
            if (fleeBlackoutImage == null) yield break;

            fleeBlackoutImage.gameObject.SetActive(true);
            Color c = fleeBlackoutImage.color;
            c.a = 0f;
            fleeBlackoutImage.color = c;

            fleeBlackoutImage.DOKill();
            fleeBlackoutImage.DOFade(1f, fleeBlackoutDuration);
            yield return new WaitForSeconds(fleeBlackoutDuration);
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
            if (hpBarImage) hpBarImage.fillAmount = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        }

        void UpdateTimerText()
        {
            if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(0f, stageRemaining)).ToString();
        }
    }
}
