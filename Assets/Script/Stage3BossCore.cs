using UnityEngine;
using DG.Tweening;
using PPYY.Stage1; // IHittable を流用

namespace PPYY.Stage3
{
    // 口が開いている間だけ有効になるコア。ヒットをコントローラーに知らせる他、
    // 見た目のフィードバック（パンチアニメーション・パーティクル）を出す
    [RequireComponent(typeof(Collider2D))]
    public class Stage3BossCore : MonoBehaviour, IHittable
    {
        public Stage3BossController controller;

        [Header("ヒット時のフィードバック")]
        public GameObject[] hitEffectPrefabs;
        public float punchScale = 0.3f;
        public float punchDuration = 0.15f;
        public AudioClip[] hitSounds;
        [Range(0f, 1f)] public float hitSoundVolume = 1f;

        [Header("連続ヒット防止（1回ヒットしてから、次のヒットを受け付けるまでの秒数）")]
        public float hitCooldown = 0.5f;

        SpriteRenderer sr;
        Vector3 baseScale;
        float lastHitTime = -999f;

        void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        // SpriteRenderer自体のEnabledが編集ミス等でOFFのままでも表示できるよう、
        // GameObjectのアクティブ切り替えとは別にここで明示的にenabledを合わせる
        public void SetVisible(bool visible)
        {
            if (sr != null) sr.enabled = visible;
        }

        public void OnHit(Vector2 worldPos)
        {
            // クールダウン中のヒットは完全に無視する（エフェクト・ダメージとも発生させない）
            if (Time.time - lastHitTime < hitCooldown) return;
            lastHitTime = Time.time;

            transform.DOKill();
            // 連打（LIDARの1タッチで複数回ヒットするケースを含む）でパンチ後の縮小が積み重なって
            // 戻らなくなるのを防ぐため、毎回スケールを基準値に戻してから再生する
            transform.localScale = baseScale;
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

            controller?.OnCoreHit(worldPos);
        }
    }
}
