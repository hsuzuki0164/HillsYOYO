using UnityEngine;
using PPYY.Stage1; // PlayerSide を流用

namespace PPYY
{
    // タイトル画面でバーコードスキャンして読み込んだ、1P/2Pそれぞれの落書き画像（似顔絵・なまえ・ゴースト・お宝袋）。
    // PlayerArtworkScannerが書き込み、ゲーム側の各所（ゴースト差し替え・ドル袋差し替え・結果画面）が読み出す
    public static class PlayerArtwork
    {
        public static bool LoadedP1;
        public static bool LoadedP2;

        public static string ArtworkIdP1;
        public static string ArtworkIdP2;

        public static Sprite PortraitP1;
        public static Sprite PortraitP2;

        public static Sprite NameP1;
        public static Sprite NameP2;

        public static Sprite GhostP1;
        public static Sprite GhostP2;

        public static Sprite MoneyBagP1;
        public static Sprite MoneyBagP2;

        public static bool IsLoaded(PlayerSide side) => side == PlayerSide.P1 ? LoadedP1 : LoadedP2;

        public static Sprite GetPortrait(PlayerSide side) => side == PlayerSide.P1 ? PortraitP1 : PortraitP2;
        public static Sprite GetName(PlayerSide side) => side == PlayerSide.P1 ? NameP1 : NameP2;
        public static Sprite GetGhost(PlayerSide side) => side == PlayerSide.P1 ? GhostP1 : GhostP2;
        public static Sprite GetMoneyBag(PlayerSide side) => side == PlayerSide.P1 ? MoneyBagP1 : MoneyBagP2;

        // 新しいプレイ（タイトル再訪）のたびに、前回の読み取り結果を残さないよう呼ぶ
        public static void Clear()
        {
            LoadedP1 = LoadedP2 = false;
            ArtworkIdP1 = ArtworkIdP2 = null;
            PortraitP1 = PortraitP2 = null;
            NameP1 = NameP2 = null;
            GhostP1 = GhostP2 = null;
            MoneyBagP1 = MoneyBagP2 = null;
        }

        public static void SetP1(string artworkId, Sprite portrait, Sprite name, Sprite ghost, Sprite moneyBag)
        {
            ArtworkIdP1 = artworkId;
            PortraitP1 = portrait;
            NameP1 = name;
            GhostP1 = ghost;
            MoneyBagP1 = moneyBag;
            LoadedP1 = true;
        }

        public static void SetP2(string artworkId, Sprite portrait, Sprite name, Sprite ghost, Sprite moneyBag)
        {
            ArtworkIdP2 = artworkId;
            PortraitP2 = portrait;
            NameP2 = name;
            GhostP2 = ghost;
            MoneyBagP2 = moneyBag;
            LoadedP2 = true;
        }
    }
}
