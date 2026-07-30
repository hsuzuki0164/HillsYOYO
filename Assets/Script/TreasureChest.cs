using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace PPYY.Stage1
{
    public enum ChestSize
    {
        Small,  // 小
        Medium, // 中
        Large,  // 大
    }

    enum ChestState
    {
        Closed,       // 通常状態、ヒット待ち
        MimicEngaged, // ミミック連打受付中
        Resolving,    // 開封演出中（再ヒット無効）
        Vanished,     // 消えていて再出現待ち
    }

    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class TreasureChest : MonoBehaviour, IHittable
    {
        [Header("見た目（配列の並びは ChestSize の順: 小・中・大）")]
        public Sprite[] closedSprites = new Sprite[3];
        public Sprite[] openedSprites = new Sprite[3];
        public Sprite mimicRevealedSprite; // 1回目のヒットでミミックだと判明した直後の見た目
        public Sprite mimicDefeatedSprite; // 規定回数ヒットして倒しきった直後の見た目

        [Header("得点（配列の並びは ChestSize の順: 小・中・大）")]
        public int[] scoreBySize = { 10, 50, 100 };

        [Header("開封に必要な鍵の数（配列の並びは ChestSize の順: 小・中・大）")]
        public int[] keyCostBySize = { 3, 5, 10 };

        [Header("ミミック")]
        [Range(0f, 1f)] public float mimicChance = 0.15f;
        public int mimicKeyCost = 1;
        public int mimicHitsToDefeat = 3;
        public float mimicTimeWindow = 5f;
        public int mimicPenalty = -80;
        public int mimicDefeatBonus = 100;

        [Header("ミミックの見た目（少し赤暗い色味＋カタカタ左右に揺れる）")]
        public Color mimicColorTint = new Color(0.8f, 0.45f, 0.45f, 1f);
        public float mimicShakeDistance = 0.05f;
        public float mimicShakeDuration = 0.05f;
        [Tooltip("揺れる時間")]
        public float mimicShakeActiveDuration = 1f;
        [Tooltip("静止する時間")]
        public float mimicShakeRestDuration = 1f;

        [Header("ミミックが一度もヒットされないまま放置されたら消えるまでの秒数（ランダム）")]
        public Vector2 mimicIdleDisappearRange = new Vector2(4f, 8f);

        [Header("再出現タイミング")]
        public Vector2 respawnDelayRange = new Vector2(2f, 5f);
        public float fadeInDuration = 0.6f;

        [Header("ヒット時アニメーション（DOTween）")]
        public float pullUpDistance = 0.5f;
        public float pullUpDuration = 0.15f;
        public float returnDuration = 0.25f;

        [Header("消滅時の点滅")]
        public int flickerCount = 6;
        public float flickerInterval = 0.06f;

        [Header("効果音")]
        [Tooltip("通常の宝箱を開けたときの音（この後にポイント獲得音がランダムで鳴る）")]
        public AudioClip openSound;
        [Tooltip("ミミックだと判明した瞬間の音")]
        public AudioClip mimicRevealSound;
        [Tooltip("ミミックを倒したときの音（この後にポイント獲得音がランダムで鳴る）")]
        public AudioClip mimicDefeatSound;
        [Tooltip("ポイント獲得時にランダムで選ばれる音（開封・ミミック撃破どちらでも共通）")]
        public AudioClip[] pointsGetSounds;
        [Range(0f, 1f)] public float soundVolume = 1f;

        SpriteRenderer sr;
        Collider2D col;
        ChestState state;
        ChestSize currentSize;
        bool isMimic;

        int mimicHitCount;
        Coroutine mimicTimeoutRoutine;
        Coroutine mimicIdleTimeoutRoutine;
        Coroutine mimicShakeRoutine;
        Sequence mimicShakeSequence;

        Vector3 basePosition;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            basePosition = transform.position;
        }

        void Start()
        {
            SpawnNew();
        }

        void SpawnNew()
        {
            state = ChestState.Closed;
            isMimic = Random.value < mimicChance;
            currentSize = (ChestSize)Random.Range(0, 3);
            mimicHitCount = 0;

            // サイズの違いは画像のみで表現する。transform.localScale は変更しない
            sr.sprite = closedSprites[(int)currentSize];
            sr.color = isMimic ? mimicColorTint : Color.white; // ミミックは少し赤暗い色味にする
            sr.enabled = true;
            col.enabled = true;

            if (isMimic)
            {
                StartMimicShake();
                if (mimicIdleTimeoutRoutine != null) StopCoroutine(mimicIdleTimeoutRoutine);
                mimicIdleTimeoutRoutine = StartCoroutine(MimicIdleDisappearRoutine());
            }
            else
            {
                StopMimicShake();
            }
        }

        // ミミックの間、「mimicShakeActiveDuration秒揺れる → mimicShakeRestDuration秒静止する」を繰り返す
        void StartMimicShake()
        {
            StopMimicShake();
            mimicShakeRoutine = StartCoroutine(MimicShakeLoop());
        }

        void StopMimicShake()
        {
            if (mimicShakeRoutine != null)
            {
                StopCoroutine(mimicShakeRoutine);
                mimicShakeRoutine = null;
            }
            // コルーチンを止めたタイミングによっては、揺れ中の無限ループSequenceが
            // Kill()されずに残ってしまうため、参照を直接持って確実に止める
            if (mimicShakeSequence != null)
            {
                mimicShakeSequence.Kill();
                mimicShakeSequence = null;
            }
            transform.DOKill();
            transform.position = basePosition;
        }

        IEnumerator MimicShakeLoop()
        {
            while (true)
            {
                mimicShakeSequence = DOTween.Sequence();
                mimicShakeSequence.Append(transform.DOMoveX(basePosition.x - mimicShakeDistance, mimicShakeDuration).SetEase(Ease.InOutSine));
                mimicShakeSequence.Append(transform.DOMoveX(basePosition.x + mimicShakeDistance, mimicShakeDuration * 2f).SetEase(Ease.InOutSine));
                mimicShakeSequence.Append(transform.DOMoveX(basePosition.x, mimicShakeDuration).SetEase(Ease.InOutSine));
                mimicShakeSequence.SetLoops(-1);

                yield return new WaitForSeconds(mimicShakeActiveDuration);

                if (mimicShakeSequence != null)
                {
                    mimicShakeSequence.Kill();
                    mimicShakeSequence = null;
                }
                transform.position = basePosition;

                yield return new WaitForSeconds(mimicShakeRestDuration);
            }
        }

        // 一度もヒットされないまま mimicIdleDisappearRange 秒（ランダム）放置されたら、そのまま消える
        IEnumerator MimicIdleDisappearRoutine()
        {
            float wait = Random.Range(mimicIdleDisappearRange.x, mimicIdleDisappearRange.y);
            yield return new WaitForSeconds(wait);

            if (state != ChestState.Closed) yield break; // 既にヒットされている等、対象外なら何もしない

            StopMimicShake();
            state = ChestState.Resolving;
            col.enabled = false;
            StartCoroutine(FlickerThenVanish());
        }

        public void OnHit(Vector2 worldPos)
        {
            if (state == ChestState.Resolving || state == ChestState.Vanished) return;

            PlayerSide side = Stage1ScoreManager.Instance.GetSideFromWorldX(worldPos.x);

            if (isMimic)
            {
                HandleMimicHit(side);
            }
            else
            {
                HandleNormalHit(side);
            }
        }

        void HandleNormalHit(PlayerSide side)
        {
            if (!Stage1ScoreManager.Instance.TryUseKey(side, keyCostBySize[(int)currentSize])) return; // 鍵が足りなければ開かない

            state = ChestState.Resolving;
            col.enabled = false;
            sr.sprite = openedSprites[(int)currentSize];
            Stage1ScoreManager.Instance.AddScore(side, scoreBySize[(int)currentSize]);

            StartCoroutine(PlaySoundThenPointsSound(openSound));
            PlayOpenAnimation();
        }

        // firstSound を鳴らし、その再生時間が終わってからポイント獲得音をランダムで鳴らす
        // （通常開封・ミミック撃破のどちらからも使う共通処理）
        IEnumerator PlaySoundThenPointsSound(AudioClip firstSound)
        {
            float delay = 0f;
            if (firstSound != null)
            {
                SfxPlayer.Play(firstSound, soundVolume);
                delay = firstSound.length;
            }

            if (pointsGetSounds != null && pointsGetSounds.Length > 0)
            {
                yield return new WaitForSeconds(delay);
                var clip = pointsGetSounds[Random.Range(0, pointsGetSounds.Length)];
                SfxPlayer.Play(clip, soundVolume);
            }
        }

        void HandleMimicHit(PlayerSide side)
        {
            // 鍵の消費は最初のヒット（宝箱だと思って手を出した瞬間）の1回のみ。
            // 以降の連打は倒すための攻撃であり、追加の鍵は不要
            if (state != ChestState.MimicEngaged)
            {
                if (!Stage1ScoreManager.Instance.TryUseKey(side, mimicKeyCost)) return;

                if (mimicIdleTimeoutRoutine != null) StopCoroutine(mimicIdleTimeoutRoutine);
                StopMimicShake();

                state = ChestState.MimicEngaged;
                mimicHitCount = 0;
                mimicTimeoutRoutine = StartCoroutine(MimicTimeoutRoutine());

                // 1回目のヒットでミミックだと判明する。その場で見た目が変わり、即座にペナルティが入る
                sr.sprite = mimicRevealedSprite != null ? mimicRevealedSprite : openedSprites[(int)currentSize];
                Stage1ScoreManager.Instance.AddScore(side, mimicPenalty);
                SfxPlayer.Play(mimicRevealSound, soundVolume);
            }

            mimicHitCount++;
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.15f, 0.15f, 1, 0);

            if (mimicHitCount >= mimicHitsToDefeat)
            {
                if (mimicTimeoutRoutine != null) StopCoroutine(mimicTimeoutRoutine);
                DefeatMimic(side);
            }
        }

        // 1回目のヒット（判明の瞬間）から mimicTimeWindow 秒以内に規定回数ヒットできなければ
        // そのまま何も追加されずに消える（ペナルティは判明した瞬間に処理済み）
        IEnumerator MimicTimeoutRoutine()
        {
            yield return new WaitForSeconds(mimicTimeWindow);

            state = ChestState.Resolving;
            col.enabled = false;
            PlayOpenAnimation();
        }

        void DefeatMimic(PlayerSide side)
        {
            state = ChestState.Resolving;
            col.enabled = false;
            sr.sprite = mimicDefeatedSprite != null ? mimicDefeatedSprite : openedSprites[(int)currentSize];
            Stage1ScoreManager.Instance.AddScore(side, mimicDefeatBonus);

            StartCoroutine(PlaySoundThenPointsSound(mimicDefeatSound));
            PlayOpenAnimation();
        }

        void PlayOpenAnimation()
        {
            transform.DOKill();
            Vector3 up = basePosition + Vector3.up * pullUpDistance;

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOMove(up, pullUpDuration).SetEase(Ease.OutQuad));
            seq.Append(transform.DOMove(basePosition, returnDuration).SetEase(Ease.OutBounce));
            seq.OnComplete(() => StartCoroutine(FlickerThenVanish()));
        }

        IEnumerator FlickerThenVanish()
        {
            for (int i = 0; i < flickerCount; i++)
            {
                sr.enabled = !sr.enabled;
                yield return new WaitForSeconds(flickerInterval);
            }
            sr.enabled = false;
            state = ChestState.Vanished;

            yield return new WaitForSeconds(Random.Range(respawnDelayRange.x, respawnDelayRange.y));

            SpawnNew();

            var c = sr.color;
            c.a = 0f;
            sr.color = c;
            sr.enabled = true;
            sr.DOFade(1f, fadeInDuration);
        }
    }
}
