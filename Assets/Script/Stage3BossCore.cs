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

        SpriteRenderer sr;

        void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
        }

        // SpriteRenderer自体のEnabledが編集ミス等でOFFのままでも表示できるよう、
        // GameObjectのアクティブ切り替えとは別にここで明示的にenabledを合わせる
        public void SetVisible(bool visible)
        {
            if (sr != null) sr.enabled = visible;
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

            controller?.OnCoreHit(worldPos);
        }
    }
}
