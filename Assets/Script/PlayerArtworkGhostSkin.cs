using System.Collections.Generic;
using UnityEngine;

namespace PPYY
{
    // ゴースト系の雑魚キャラ（ステージ1のGhost、ステージ3のミニオン）のプレハブに追加する。
    // EnemyCharacter/Stage3Minionとは別コンポーネントとして持たせることで、
    // カラス・ネズミ等と共有しているスクリプト本体には手を入れずに済む。
    // PlayerArtworkに読み込まれている1P/2Pのゴースト落書きの中からランダムに1枚選んで差し替える
    // （どちらの画面側で出現したかは問わない）。未読み込み時（デバッグプレイ等）は元のプレハブ画像のまま
    public class PlayerArtworkGhostSkin : MonoBehaviour
    {
        SpriteRenderer sr;

        void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
        }

        void Start()
        {
            if (sr == null) return;

            var sprite = PickRandomGhostSprite();
            if (sprite == null) return;

            // スキャン画像の切り抜きサイズは元のプレハブ画像と無関係なため、
            // 差し替え後も元の見た目の大きさを維持できるよう縮尺を補正する
            Vector2 originalSize = sr.sprite != null ? sr.sprite.bounds.size : Vector2.one;
            sr.sprite = sprite;
            ApplySizeCorrection(sr.transform, originalSize, sprite.bounds.size);
        }

        static Sprite PickRandomGhostSprite()
        {
            var candidates = new List<Sprite>(2);
            if (PlayerArtwork.GhostP1 != null) candidates.Add(PlayerArtwork.GhostP1);
            if (PlayerArtwork.GhostP2 != null) candidates.Add(PlayerArtwork.GhostP2);
            if (candidates.Count == 0) return null;

            return candidates[Random.Range(0, candidates.Count)];
        }

        static void ApplySizeCorrection(Transform t, Vector2 originalSize, Vector2 newSize)
        {
            if (newSize.x <= 0.0001f || newSize.y <= 0.0001f) return;

            float scale = ((originalSize.x / newSize.x) + (originalSize.y / newSize.y)) * 0.5f;
            t.localScale = new Vector3(t.localScale.x * scale, t.localScale.y * scale, t.localScale.z);
        }
    }
}
