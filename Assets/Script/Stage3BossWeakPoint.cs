using System.Collections;
using UnityEngine;
using DG.Tweening;
using PPYY.Stage1; // IHittable, PlayerSide を流用

namespace PPYY.Stage3
{
    public enum WeakPointRole
    {
        Eye,
        Hand,
    }

    // ボスの目・手に付ける当たり判定。ヒットしたことをコントローラーに知らせる他、
    // 見た目のフィードバック（パンチアニメーション・パーティクル）を出す
    [RequireComponent(typeof(Collider2D))]
    public class Stage3BossWeakPoint : MonoBehaviour, IHittable
    {
        public WeakPointRole role;
        public Stage3BossController controller;

        [Header("ヒット時のフィードバック（目・手で別々の音を設定できる）")]
        public GameObject[] hitEffectPrefabs;
        public float punchScale = 0.2f;
        public float punchDuration = 0.15f;
        public AudioClip[] hitSounds;
        [Range(0f, 1f)] public float hitSoundVolume = 1f;

        [Header("お宝を持った状態でヒットして取り返した時の効果音")]
        public AudioClip lootRecoverSound;

        [Header("常時のゆらぎ（Eyeロールのみ有効。Handはコントローラー側の動きと衝突するため対象外）")]
        public float idleMoveAmplitude = 0.08f;
        public float idleMoveFrequency = 1.5f;

        [Header("コアが攻撃された時の見た目（Eyeロールのみ有効）")]
        public Sprite eyeHurtSprite;
        [Tooltip("この秒数後に元の見た目へ戻す")]
        public float eyeHurtDuration = 0.3f;

        [Header("お宝を持っている間の見た目（Handロール用、子オブジェクトのSpriteRendererを指定）")]
        public SpriteRenderer lootRenderer;
        public float lootFadeOutDuration = 0.4f;

        Vector3 baseLocalPos;
        float idleTime;
        float idlePhaseOffset;

        bool hasLoot;
        int lootAmount;
        PlayerSide lootSide;
        Coroutine lootTimeoutRoutine;

        SpriteRenderer sr;
        Sprite eyeOriginalSprite;
        Coroutine eyeHurtRoutine;

        void Awake()
        {
            baseLocalPos = transform.localPosition;
            idlePhaseOffset = Random.Range(0f, Mathf.PI * 2f); // 左右の目で動きをずらす

            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) eyeOriginalSprite = sr.sprite;

            if (lootRenderer != null) lootRenderer.gameObject.SetActive(false);
        }

        void Update()
        {
            if (role != WeakPointRole.Eye) return;

            idleTime += Time.deltaTime;
            float offsetX = Mathf.Sin(idleTime * idleMoveFrequency + idlePhaseOffset) * idleMoveAmplitude;
            float offsetY = Mathf.Cos(idleTime * idleMoveFrequency * 0.8f + idlePhaseOffset) * idleMoveAmplitude;
            transform.localPosition = baseLocalPos + new Vector3(offsetX, offsetY, 0f);
        }

        // コアが攻撃された時・撃破された時にコントローラーから呼ばれる（Eyeロールのみ意味を持つ）。
        // autoRevert=false にすると、しばらくしても元に戻さずそのままにする（撃破時の見た目維持用）
        public void PlayCoreHitReaction(bool autoRevert = true)
        {
            if (role != WeakPointRole.Eye) return;
            if (sr == null || eyeHurtSprite == null) return;

            sr.sprite = eyeHurtSprite;

            if (eyeHurtRoutine != null)
            {
                StopCoroutine(eyeHurtRoutine);
                eyeHurtRoutine = null;
            }

            if (autoRevert)
            {
                eyeHurtRoutine = StartCoroutine(RevertEyeSpriteAfterDelay());
            }
        }

        IEnumerator RevertEyeSpriteAfterDelay()
        {
            yield return new WaitForSeconds(eyeHurtDuration);
            if (sr != null) sr.sprite = eyeOriginalSprite;
        }

        // ボスが盗んだお宝を、この手が持っている見た目にする。returnWindow 秒以内にこの手を
        // ヒットすればお宝を取り返せる。ヒットされないまま時間切れになると消滅（盗まれたままになる）
        public void ReceiveLoot(int amount, PlayerSide side, Sprite sprite, float returnWindow)
        {
            hasLoot = true;
            lootAmount = amount;
            lootSide = side;

            if (lootRenderer != null)
            {
                if (sprite != null) lootRenderer.sprite = sprite;
                lootRenderer.DOKill();
                var c = lootRenderer.color;
                c.a = 1f;
                lootRenderer.color = c;
                lootRenderer.gameObject.SetActive(true);
            }

            if (lootTimeoutRoutine != null) StopCoroutine(lootTimeoutRoutine);
            lootTimeoutRoutine = StartCoroutine(LootTimeoutRoutine(returnWindow));
        }

        IEnumerator LootTimeoutRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            ClearLoot();
        }

        void ClearLoot()
        {
            hasLoot = false;
            lootTimeoutRoutine = null;

            if (lootRenderer != null)
            {
                lootRenderer.DOKill();
                var renderer = lootRenderer;
                renderer.DOFade(0f, lootFadeOutDuration).OnComplete(() =>
                {
                    if (renderer != null) renderer.gameObject.SetActive(false);
                });
            }
        }

        public void OnHit(Vector2 worldPos)
        {
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 1, 0);

            if (hitEffectPrefabs != null && hitEffectPrefabs.Length > 0)
            {
                var effect = hitEffectPrefabs[Random.Range(0, hitEffectPrefabs.Length)];
                if (effect != null) Instantiate(effect, transform.position, Quaternion.identity);
            }
            if (hitSounds != null && hitSounds.Length > 0)
            {
                SfxPlayer.Play(hitSounds[Random.Range(0, hitSounds.Length)], hitSoundVolume);
            }

            PlayCoreHitReaction(); // Eyeロールなら自分がヒットされた時も見た目を切り替える（Handは内部で無視される）

            if (hasLoot)
            {
                if (lootTimeoutRoutine != null) StopCoroutine(lootTimeoutRoutine);
                Stage3TreasureManager.Instance?.AddPoints(lootSide, lootAmount);
                SfxPlayer.Play(lootRecoverSound, hitSoundVolume);
                ClearLoot();
            }

            controller?.OnWeakPointHit(this, worldPos);
        }
    }
}
