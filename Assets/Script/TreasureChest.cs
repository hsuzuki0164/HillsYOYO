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

        SpriteRenderer sr;
        Collider2D col;
        ChestState state;
        ChestSize currentSize;
        bool isMimic;

        int mimicHitCount;
        Coroutine mimicTimeoutRoutine;

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
            var c = sr.color;
            c.a = 1f;
            sr.color = c;
            sr.enabled = true;
            col.enabled = true;
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

            PlayOpenAnimation();
        }

        void HandleMimicHit(PlayerSide side)
        {
            // 鍵の消費は最初のヒット（宝箱だと思って手を出した瞬間）の1回のみ。
            // 以降の連打は倒すための攻撃であり、追加の鍵は不要
            if (state != ChestState.MimicEngaged)
            {
                if (!Stage1ScoreManager.Instance.TryUseKey(side, mimicKeyCost)) return;
                state = ChestState.MimicEngaged;
                mimicHitCount = 0;
                mimicTimeoutRoutine = StartCoroutine(MimicTimeoutRoutine());

                // 1回目のヒットでミミックだと判明する。その場で見た目が変わり、即座にペナルティが入る
                sr.sprite = mimicRevealedSprite != null ? mimicRevealedSprite : openedSprites[(int)currentSize];
                Stage1ScoreManager.Instance.AddScore(side, mimicPenalty);
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
