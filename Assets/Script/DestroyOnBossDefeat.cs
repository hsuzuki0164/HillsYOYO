using UnityEngine;

namespace PPYY.Stage3
{
    // このコンポーネントが付いたオブジェクトは、ボス撃破時にまとめて削除される。
    // 変身演出の永続パーティクルなど、そのままだとボス撃破後も残り続けてしまうものに付ける
    public class DestroyOnBossDefeat : MonoBehaviour
    {
    }
}
