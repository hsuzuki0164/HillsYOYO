using System.Collections;
using UnityEngine;
using PPYY.Stage1; // PlayerSide, IHittable を流用

namespace PPYY.Stage3
{
    // ボスが投げる爆弾。着弾前にヒットすれば不発、着弾すると狙った側のお宝を減らす
    [RequireComponent(typeof(Collider2D))]
    public class Stage3Bomb : MonoBehaviour, IHittable
    {
        public float flightDuration = 1.2f;
        public float arcHeight = 1.5f;
        public int minStealOnExplode = 30;
        public int maxStealOnExplode = 80;
        public GameObject explodeEffectPrefab;
        public GameObject defuseEffectPrefab;

        Collider2D col;
        bool resolved;
        PlayerSide targetSide;
        Vector3 landPos;

        void Awake()
        {
            col = GetComponent<Collider2D>();
        }

        public void Init(Vector3 startPos, Vector3 targetPos, PlayerSide side)
        {
            transform.position = startPos;
            landPos = targetPos;
            targetSide = side;
            StartCoroutine(Flight());
        }

        IEnumerator Flight()
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < flightDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / flightDuration);
                Vector3 pos = Vector3.Lerp(start, landPos, p);
                pos.y += Mathf.Sin(p * Mathf.PI) * arcHeight; // 山なりの軌道
                transform.position = pos;
                yield return null;
            }
            Explode();
        }

        void Explode()
        {
            if (resolved) return;
            resolved = true;
            col.enabled = false;

            if (Stage3TreasureManager.Instance != null)
            {
                int amount = Random.Range(minStealOnExplode, maxStealOnExplode + 1);
                Stage3TreasureManager.Instance.StealPoints(targetSide, amount);
            }

            if (explodeEffectPrefab != null) Instantiate(explodeEffectPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }

        public void OnHit(Vector2 worldPos)
        {
            if (resolved) return;
            resolved = true;
            col.enabled = false;
            StopAllCoroutines();

            if (defuseEffectPrefab != null) Instantiate(defuseEffectPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
