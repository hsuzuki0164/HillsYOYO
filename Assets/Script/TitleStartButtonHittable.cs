using UnityEngine;
using UnityEngine.UI;
using PPYY.Stage1; // IHittable を流用

namespace PPYY
{
    // タイトル画面のスタートボタンを、マウスと同じくLIDAR(紙ヨーヨー)でも押せるようにするブリッジ。
    // ワールド座標に置いたCollider2DへのヒットをStage1InputManager経由(既存のIHittableの仕組み)で受け取り、
    // 既存のButton.onClickへそのまま流し込む。スタートボタンの演出・実処理ロジックはTitleScreen側に残し、二重管理しない
    [RequireComponent(typeof(Collider2D))]
    public class TitleStartButtonHittable : MonoBehaviour, IHittable
    {
        public Button startButton;

        [Tooltip("ボタンがまだ表示・有効化されていない間（イントロ演出中等）の誤爆を防ぐためのCanvasGroup。TitleScreenのStart Button Groupと同じものを指定する")]
        public CanvasGroup startButtonGroup;

        public void OnHit(Vector2 worldPos)
        {
            if (startButton == null) return;

            // CanvasGroup.interactableはEventSystem経由のクリックにしか作用しないため、
            // ここではTitleScreenが管理しているフラグを直接見て同じ条件で弾く
            if (startButtonGroup != null && !startButtonGroup.interactable) return;

            startButton.onClick.Invoke();
        }
    }
}
