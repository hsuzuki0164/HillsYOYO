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
        [Tooltip("元画像が無反転(flipX=false)の状態で右向きに描かれている場合はON")]
        public bool baseFacesRight = false;
        public float fadeOutDuration = 0.3f;
        public GameObject[] hitEffectPrefabs;
        public AudioClip[] hitSounds;
        [Range(0f, 1f)] public float hitSoundVolume = 1f;

        SpriteRenderer sr;
        Collider2D col;
        bool defeated;
        Vector3 target;

        void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            SetFacingRight(false); // 初期状態は左向き
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
            FaceDirection(target.x - transform.position.x);
        }

        void FaceDirection(float dx)
        {
            if (Mathf.Abs(dx) < 0.0001f) return;
            SetFacingRight(dx > 0f);
        }

        // baseFacesRight（元画像が右向きかどうか）を踏まえて、見た目が facingRight を向くように flipX を設定する
        void SetFacingRight(bool facingRight)
        {
            sr.flipX = baseFacesRight ? !facingRight : facingRight;
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
            if (hitSounds != null && hitSounds.Length > 0)
            {
                SfxPlayer.Play(hitSounds[Random.Range(0, hitSounds.Length)], hitSoundVolume);
            }

            transform.DOKill();
            sr.DOFade(0f, fadeOutDuration).OnComplete(() => Destroy(gameObject));
        }
    }
}
