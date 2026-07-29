using UnityEngine;

namespace PPYY
{
    // 効果音再生の共通ヘルパー。
    // AudioSource.PlayClipAtPoint は内部で spatialBlend=1（3D音源）として生成されるため、
    // カメラからの距離に応じて音量が減衰してしまう。本作は2Dゲームで距離減衰は不要なので、
    // こちらを使い、指定した音量のまま（距離に関係なく）鳴らす
    public static class SfxPlayer
    {
        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            var go = new GameObject("SFX_" + clip.name);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0f; // 2D再生（距離減衰なし）
            source.Play();
            Object.Destroy(go, clip.length);
        }
    }
}
