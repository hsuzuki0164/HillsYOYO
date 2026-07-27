using UnityEngine;
using DG.Tweening;
using PPYY.Stage1; // IHittable を流用

namespace PPYY.Stage3
{
    // ボスが召喚する雑魚敵。得点/鍵の付与はなく、画面内をふらふら移動するだけの妨害役
    [RequireComponent(typeof(Collider2D))]
    public class Stage3Minion : MonoBehaviour, IHittable
    {
        public float moveSpeed = 2f;
        public Vector2 boundsMin = new Vector2(-8f, -4f);
        public Vector2 boundsMax = new Vector2(8f, 4f);
        public float fadeOutDuration = 0.3f;
        public GameObject[] hitEffectPrefabs;

        SpriteRenderer sr;
        Collider2D col;
        bool defeated;
        Vector3 target;

        void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            PickNewTarget();
        }

        void Update()
        {
            if (defeated) return;

            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, target) < 0.1f) PickNewTarget();
        }

        void PickNewTarget()
        {
            target = new Vector3(
                Random.Range(boundsMin.x, boundsMax.x),
                Random.Range(boundsMin.y, boundsMax.y),
                0);
        }

        void SpawnHitEffect()
        {
            if (hitEffectPrefabs == null || hitEffectPrefabs.Length == 0) return;
            var effect = hitEffectPrefabs[Random.Range(0, hitEffectPrefabs.Length)];
            if (effect != null) Instantiate(effect, transform.position, Quaternion.identity);
        }

        public void OnHit(Vector2 worldPos)
        {
            if (defeated) return;
            defeated = true;
            col.enabled = false;

            SpawnHitEffect();

            transform.DOKill();
            sr.DOFade(0f, fadeOutDuration).OnComplete(() => Destroy(gameObject));
        }
    }
}
