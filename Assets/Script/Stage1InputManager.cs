using UnityEngine;
using PPYY.Lidar;

namespace PPYY.Stage1
{
    // 宝箱・敵など、当たり判定を受け取るオブジェクトが実装するインターフェース
    public interface IHittable
    {
        void OnHit(Vector2 worldPos);
    }

    public class Stage1InputManager : MonoBehaviour
    {
        [Tooltip("ヒット判定の許容半径。ペーパーヨーヨーの当たりの太さに合わせて調整する")]
        public float hitRadius = 0.3f;

        void OnEnable()
        {
            // LidarSensorBridgeはシーンをまたいで常駐しているため、このシーンのInputManagerから毎回購読する
            if (LidarSensorBridge.Instance != null)
            {
                LidarSensorBridge.Instance.OnHit += ProcessHit;
            }
        }

        void OnDisable()
        {
            if (LidarSensorBridge.Instance != null)
            {
                LidarSensorBridge.Instance.OnHit -= ProcessHit;
            }
        }

        void Update()
        {
            // 開発用の仮入力（マウスクリック）。実機ではLidarSensorBridge経由のProcessHit呼び出しがこれと同じ経路を通る。
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                ProcessHit(world);
            }
        }

        public void ProcessHit(Vector2 worldPos)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, hitRadius);

            if (hits.Length == 0)
            {
                Debug.Log($"[Stage1InputManager] ProcessHit worldPos={worldPos} radius={hitRadius} -> ヒットしたコライダーなし");
            }

            foreach (var col in hits)
            {
                var hittable = col.GetComponentInParent<IHittable>();
                Debug.Log($"[Stage1InputManager] ProcessHit worldPos={worldPos} radius={hitRadius} -> collider={col.name} IHittable={(hittable != null)}");
                hittable?.OnHit(worldPos);
            }
        }
    }
}
