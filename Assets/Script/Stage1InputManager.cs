using UnityEngine;

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

        void Update()
        {
            // 開発用の仮入力（マウスクリック）。
            // 実機のペーパーヨーヨー入力を組み込む際は、検出処理から
            // ProcessHit(worldPos) を直接呼び出せば同じ経路でヒット判定できる。
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                ProcessHit(world);
            }
        }

        public void ProcessHit(Vector2 worldPos)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, hitRadius);
            foreach (var col in hits)
            {
                var hittable = col.GetComponentInParent<IHittable>();
                hittable?.OnHit(worldPos);
            }
        }
    }
}
