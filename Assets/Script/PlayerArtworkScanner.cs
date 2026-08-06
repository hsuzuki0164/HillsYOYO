using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY
{
    // タイトル画面に置く。バーコードリーダー（キーボード入力として届く機種を想定）で
    // 1P→2Pの順に用紙のバーコードを読み取り、対応するスキャン画像(jpg)を外部フォルダから読み込んで、
    // あらかじめ決めた4か所（なまえ・似顔絵・ゴースト・お宝袋）を切り抜いてPlayerArtworkへ格納する。
    //
    // LidarSensorBridgeと同様にシーンをまたいで常駐させる(DontDestroyOnLoad)。
    // これは結果発表シーンで、歴代ランキングの過去プレイ分の画像を再読み込みするのにも使うため
    public class PlayerArtworkScanner : MonoBehaviour
    {
        public static PlayerArtworkScanner Instance { get; private set; }

        [Header("スキャン画像フォルダのパス設定")]
        [Tooltip("StreamingAssets内のこのファイルの2行目に、スキャン画像フォルダのフルパスを書いておく（1行目はLIDARキャリブレーション用データ）")]
        public string pathConfigFileName = "LidarCalibration.csv";
        [Tooltip("外部ファイルが見つからない場合に使うフォールバックのフォルダパス")]
        public string fallbackScanFolderPath = "";
        public string fileExtension = ".jpg";

        [Header("切り抜き位置（画像左上を原点とした割合 0〜1。実際のスキャン画像を見ながら調整する）")]
        public Rect nameRect = new Rect(0.55f, 0.03f, 0.35f, 0.10f);
        public Rect portraitRect = new Rect(0.55f, 0.16f, 0.35f, 0.24f);
        public Rect ghostRect = new Rect(0.55f, 0.42f, 0.35f, 0.24f);
        public Rect moneyBagRect = new Rect(0.55f, 0.68f, 0.35f, 0.24f);

        [Header("背景の緑色を透過する（クロマキー処理。スキャン画像全体に対して読み込み時に1回だけ行う）")]
        public bool removeGreenBackground = true;
        [Tooltip("緑がこの明るさ未満のピクセルは対象外（黒い線画等を誤って透過しないため）")]
        [Range(0f, 1f)] public float greenMinBrightness = 0.20f;
        [Tooltip("「緑 - 他chの最大値」がこの値以下なら背景ではない（前景のまま）")]
        [Range(0f, 1f)] public float greenKeyLow = 0.05f;
        [Tooltip("「緑 - 他chの最大値」がこの値以上なら完全に背景（透明）。LowとHighの間はエッジをなめらかにフェザリングする")]
        [Range(0f, 1f)] public float greenKeyHigh = 0.25f;

        [Header("1Pの読み取り欄")]
        public TMP_InputField p1Field;
        public TextMeshProUGUI p1StatusText;
        public Image p1PreviewImage;
        public Image p1NamePreviewImage;

        [Header("2Pの読み取り欄（1P読み取り成功後に有効化される）")]
        public TMP_InputField p2Field;
        public TextMeshProUGUI p2StatusText;
        public Image p2PreviewImage;
        public Image p2NamePreviewImage;

        [Header("メッセージ")]
        public string waitingMessage = "バーコードを読み取ってください";
        public string okMessage = "OK";
        public string notFoundMessage = "画像が見つかりません。もう一度読み取ってください";

        [Header("読み取りOK時の演出")]
        public AudioClip okSound;
        [Range(0f, 1f)] public float okSoundVolume = 1f;
        [Tooltip("OK表示（statusText）を拡大→縮小させるパンチの強さ")]
        public float okPunchScale = 0.4f;
        public float okPunchDuration = 0.35f;

        [Header("読み取り失敗時の演出")]
        public AudioClip errorSound;
        [Range(0f, 1f)] public float errorSoundVolume = 1f;
        [Tooltip("入力欄を揺らす強さ・時間")]
        public float errorShakeStrength = 20f;
        public float errorShakeDuration = 0.4f;
        public int errorShakeVibrato = 20;

        [Header("リセット操作（間違えて読み取った場合にShift+このキーでやり直す）")]
        public KeyCode resetKey = KeyCode.C;

        [Header("バーコードの桁数（仕様上の本来の桁数。バーコードリーダーが末尾にチェックデジットを付与してくる場合、超過分は切り捨てる）")]
        public int barcodeDigitCount = 10;

        // 両プレイヤーの読み取りが完了した時に発火。TitleScreenがスタートボタンの有効化に使う
        public event Action OnBothReady;
        // リセットされた時に発火。TitleScreenがスタートボタンの再無効化に使う
        public event Action OnReset;
        public bool BothReady { get; private set; }

        string scanFolderPath;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 2周目以降にタイトルシーンが再読み込みされた際、新しいUI（入力欄・プレビュー画像等）を
                // 持ったこの複製オブジェクトが生成される。参照だけ常駐中のシングルトンへ引き継いでから破棄する
                Instance.RebindUI(this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            scanFolderPath = ReadScanFolderPathFromConfig();
            if (string.IsNullOrEmpty(scanFolderPath)) scanFolderPath = fallbackScanFolderPath;
        }

        void Start()
        {
            // このシーンにUIが無い（=タイトル以外で常駐している）場合は何もしない
            if (p1Field == null) return;

            ResetScan();
        }

        // タイトルシーンが再読み込みされた時に生成される複製オブジェクトから、
        // 新しいシーンのUI参照を引き継ぎ、状態を初期化し直す
        void RebindUI(PlayerArtworkScanner fresh)
        {
            p1Field = fresh.p1Field;
            p1StatusText = fresh.p1StatusText;
            p1PreviewImage = fresh.p1PreviewImage;
            p1NamePreviewImage = fresh.p1NamePreviewImage;

            p2Field = fresh.p2Field;
            p2StatusText = fresh.p2StatusText;
            p2PreviewImage = fresh.p2PreviewImage;
            p2NamePreviewImage = fresh.p2NamePreviewImage;

            ResetScan();
        }

        void Update()
        {
            if (p1Field == null) return; // このシーンにUIが無い場合は何もしない

            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shiftHeld && Input.GetKeyDown(resetKey))
            {
                ResetScan();
            }
        }

        // 1P/2Pの読み取り状態を初期状態に戻す。誤って読み取った場合のやり直しにも使う
        public void ResetScan()
        {
            PlayerArtwork.Clear();
            GameSession.ArtworkIdP1 = null;
            GameSession.ArtworkIdP2 = null;
            BothReady = false;

            SetupField(p1Field, OnP1Submit, true);
            SetupField(p2Field, OnP2Submit, false);
            SetStatus(p1StatusText, waitingMessage);
            SetStatus(p2StatusText, "");

            // 読み込み前は白い矩形が出てしまうため、画像が入るまでは非表示にしておく
            SetPreview(p1PreviewImage, null);
            SetPreview(p2PreviewImage, null);
            SetPreview(p1NamePreviewImage, null);
            SetPreview(p2NamePreviewImage, null);

            OnReset?.Invoke();
        }

        string ReadScanFolderPathFromConfig()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, pathConfigFileName);
                if (!File.Exists(path)) return null;

                var lines = File.ReadAllLines(path);
                if (lines.Length < 2) return null;

                string folder = lines[1].Trim();
                return string.IsNullOrEmpty(folder) ? null : folder;
            }
            catch (Exception e)
            {
                Debug.LogError($"スキャン画像フォルダ設定の読み込みに失敗しました: {e}");
                return null;
            }
        }

        void SetupField(TMP_InputField field, Action<string> onSubmit, bool activate)
        {
            if (field == null) return;

            field.text = "";
            field.onEndEdit.RemoveAllListeners();
            field.onEndEdit.AddListener(value => onSubmit(value));
            field.interactable = activate;

            if (activate)
            {
                // 2周目以降、RebindUI経由(Awake内)でここに来た場合、まだ新しいシーンの
                // TMP_InputField側の初期化(Start等)が終わっていないことがあるため、
                // 1フレーム待ってから選択・アクティブ化する
                StartCoroutine(ActivateFieldDelayed(field));
            }
        }

        IEnumerator ActivateFieldDelayed(TMP_InputField field)
        {
            yield return null;
            if (field != null)
            {
                field.Select();
                field.ActivateInputField();
            }
        }

        void OnP1Submit(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RefocusField(p1Field);
                return;
            }

            value = NormalizeBarcodeValue(value);

            if (TryLoadAndCrop(value, out var portrait, out var name, out var ghost, out var moneyBag))
            {
                PlayerArtwork.SetP1(value, portrait, name, ghost, moneyBag);
                GameSession.ArtworkIdP1 = value;

                SetStatus(p1StatusText, okMessage);
                SetPreview(p1PreviewImage, portrait);
                SetPreview(p1NamePreviewImage, name);
                PlayOkFeedback(p1StatusText);

                p1Field.interactable = false;
                SetupField(p2Field, OnP2Submit, true);
                SetStatus(p2StatusText, waitingMessage);
            }
            else
            {
                SetStatus(p1StatusText, notFoundMessage);
                PlayErrorFeedback(p1Field);
                RefocusField(p1Field);
            }
        }

        void OnP2Submit(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RefocusField(p2Field);
                return;
            }

            value = NormalizeBarcodeValue(value);

            if (TryLoadAndCrop(value, out var portrait, out var name, out var ghost, out var moneyBag))
            {
                PlayerArtwork.SetP2(value, portrait, name, ghost, moneyBag);
                GameSession.ArtworkIdP2 = value;

                SetStatus(p2StatusText, okMessage);
                SetPreview(p2PreviewImage, portrait);
                SetPreview(p2NamePreviewImage, name);
                PlayOkFeedback(p2StatusText);

                p2Field.interactable = false;
                BothReady = true;
                OnBothReady?.Invoke();
            }
            else
            {
                SetStatus(p2StatusText, notFoundMessage);
                PlayErrorFeedback(p2Field);
                RefocusField(p2Field);
            }
        }

        // バーコードリーダーが末尾にチェックデジットを付与してくる場合に、本来の桁数を超えた分を切り捨てる
        string NormalizeBarcodeValue(string value)
        {
            value = value.Trim();
            if (barcodeDigitCount > 0 && value.Length > barcodeDigitCount)
            {
                value = value.Substring(0, barcodeDigitCount);
            }
            return value;
        }

        void RefocusField(TMP_InputField field)
        {
            if (field == null) return;
            field.text = "";
            field.Select();
            field.ActivateInputField();
        }

        void SetStatus(TextMeshProUGUI text, string message)
        {
            if (text != null) text.text = message;
        }

        void SetPreview(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.gameObject.SetActive(sprite != null);
        }

        // 読み取り成功時：OK表示をパンチ拡縮させ、OK音を鳴らす
        void PlayOkFeedback(TextMeshProUGUI statusText)
        {
            SfxPlayer.Play(okSound, okSoundVolume);

            if (statusText == null) return;
            statusText.transform.DOKill();
            statusText.transform.localScale = Vector3.one;
            statusText.transform.DOPunchScale(Vector3.one * okPunchScale, okPunchDuration, 1, 0);
        }

        // 読み取り失敗時：入力欄を揺らし、エラー音を鳴らす
        void PlayErrorFeedback(TMP_InputField field)
        {
            SfxPlayer.Play(errorSound, errorSoundVolume);

            if (field == null) return;
            var rect = field.transform as RectTransform;
            if (rect == null) return;

            rect.DOKill();
            rect.DOShakeAnchorPos(errorShakeDuration, errorShakeStrength, errorShakeVibrato);
        }

        bool TryLoadAndCrop(string artworkId, out Sprite portrait, out Sprite name, out Sprite ghost, out Sprite moneyBag)
        {
            portrait = name = ghost = moneyBag = null;

            if (!TryLoadTextureById(artworkId, out Texture2D tex)) return false;

            portrait = CropSprite(tex, portraitRect);
            name = CropSprite(tex, nameRect);
            ghost = CropSprite(tex, ghostRect);
            moneyBag = CropSprite(tex, moneyBagRect);
            return true;
        }

        // 結果発表画面の歴代ランキング表示など、他のシーンから過去のartworkIdで再読込するのにも使う
        public bool TryLoadTextureById(string artworkId, out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrEmpty(artworkId) || string.IsNullOrEmpty(scanFolderPath)) return false;

            string path = Path.Combine(scanFolderPath, artworkId + fileExtension);
            if (!File.Exists(path)) return false;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var loaded = new Texture2D(2, 2);
                if (!ImageConversion.LoadImage(loaded, bytes))
                {
                    Debug.LogError($"スキャン画像の読み込みに失敗しました: {path}");
                    return false;
                }

                // JPEGはアルファを持たないため、LoadImageはRGB24などアルファ非対応のフォーマットに
                // してしまうことがある。透過処理のためにRGBA32へ明示的に作り直す
                var tex = new Texture2D(loaded.width, loaded.height, TextureFormat.RGBA32, false);
                tex.SetPixels32(loaded.GetPixels32());
                tex.Apply();
                Destroy(loaded);

                if (removeGreenBackground) ApplyGreenKey(tex);

                texture = tex;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"スキャン画像の読み込みに失敗しました: {path} / {e}");
                return false;
            }
        }

        // 緑背景をクロマキーで透過する。JPEGはアルファチャンネルを持たないため、
        // 「緑チャンネル - 他chの最大値」を背景らしさの指標にして、Low〜Highの間はアルファを
        // なめらかに補間することで、圧縮ノイズによるギザギザの縁を防ぐ
        void ApplyGreenKey(Texture2D tex)
        {
            var pixels = tex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                float r = c.r / 255f;
                float g = c.g / 255f;
                float b = c.b / 255f;

                if (g < greenMinBrightness) continue; // 暗い部分（線画など）は対象外

                float greenness = g - Mathf.Max(r, b);
                if (greenness <= greenKeyLow) continue; // 背景ではない

                float alpha;
                if (greenness >= greenKeyHigh)
                {
                    alpha = 0f;
                }
                else
                {
                    float t = (greenness - greenKeyLow) / (greenKeyHigh - greenKeyLow);
                    alpha = 1f - t;
                }

                c.a = (byte)Mathf.RoundToInt(Mathf.Min(c.a, alpha * 255f));
                pixels[i] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
        }

        public Sprite CropPortrait(Texture2D tex) => CropSprite(tex, portraitRect);
        public Sprite CropName(Texture2D tex) => CropSprite(tex, nameRect);
        public Sprite CropGhost(Texture2D tex) => CropSprite(tex, ghostRect);
        public Sprite CropMoneyBag(Texture2D tex) => CropSprite(tex, moneyBagRect);

        // normalizedRect は画像の左上を原点(0,0)とした割合で指定する
        Sprite CropSprite(Texture2D tex, Rect normalizedRect)
        {
            if (tex == null) return null;

            int x = Mathf.RoundToInt(normalizedRect.x * tex.width);
            int topY = Mathf.RoundToInt(normalizedRect.y * tex.height);
            int w = Mathf.RoundToInt(normalizedRect.width * tex.width);
            int h = Mathf.RoundToInt(normalizedRect.height * tex.height);

            // Texture2Dのピクセル原点は左下のため、上端基準で指定した値を下端基準に変換する
            int y = tex.height - topY - h;

            x = Mathf.Clamp(x, 0, tex.width - 1);
            y = Mathf.Clamp(y, 0, tex.height - 1);
            w = Mathf.Clamp(w, 1, tex.width - x);
            h = Mathf.Clamp(h, 1, tex.height - y);

            return Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
