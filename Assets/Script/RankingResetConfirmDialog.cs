using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PPYY
{
    // タイトル画面専用。Shift+A+Zを押すと、歴代ランキング(ScoreHistory)をクリアするかどうかの
    // 確認ウインドウを表示する。「はい」を押した場合のみクリアする
    public class RankingResetConfirmDialog : MonoBehaviour
    {
        [Header("確認ウインドウ本体（普段は非表示にしておく）")]
        public GameObject dialogRoot;
        public CanvasGroup dialogCanvasGroup;
        public RectTransform dialogRectTransform;

        [Header("ボタン")]
        public Button yesButton;
        public Button noButton;

        [Header("開くときの演出（DOTweenでフェード＋拡大）")]
        public float openDuration = 0.3f;
        public Ease openEase = Ease.OutBack;
        [Tooltip("開始時のスケール（小さい状態から等倍まで拡大する）")]
        public float openStartScale = 0.8f;

        [Header("閉じるときの演出")]
        public float closeDuration = 0.2f;

        void Awake()
        {
            if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
            if (noButton != null) noButton.onClick.AddListener(OnNoClicked);

            if (dialogRoot != null) dialogRoot.SetActive(false);
        }

        void Update()
        {
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool aHeld = Input.GetKey(KeyCode.A);

            if (shiftHeld && aHeld && Input.GetKeyDown(KeyCode.Z))
            {
                Open();
            }
        }

        void Open()
        {
            if (dialogRoot == null) return;
            if (dialogRoot.activeSelf) return; // 既に開いている場合は何もしない

            dialogRoot.SetActive(true);

            if (dialogCanvasGroup != null)
            {
                dialogCanvasGroup.DOKill();
                dialogCanvasGroup.alpha = 0f;
                dialogCanvasGroup.DOFade(1f, openDuration);
            }

            if (dialogRectTransform != null)
            {
                dialogRectTransform.DOKill();
                dialogRectTransform.localScale = Vector3.one * openStartScale;
                dialogRectTransform.DOScale(1f, openDuration).SetEase(openEase);
            }
        }

        void OnYesClicked()
        {
            ScoreHistory.ClearAll();
            Close();
        }

        void OnNoClicked()
        {
            Close();
        }

        void Close()
        {
            if (dialogRoot == null) return;

            if (dialogCanvasGroup != null)
            {
                dialogCanvasGroup.DOKill();
                dialogCanvasGroup.DOFade(0f, closeDuration)
                    .OnComplete(() => dialogRoot.SetActive(false));
            }
            else
            {
                dialogRoot.SetActive(false);
            }

            if (dialogRectTransform != null)
            {
                dialogRectTransform.DOKill();
                dialogRectTransform.DOScale(openStartScale, closeDuration);
            }
        }
    }
}
