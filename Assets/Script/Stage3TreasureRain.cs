using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace PPYY.Stage3
{
    // 画面上部からお宝（コイン・宝石など）を降らせ、地面に積み上がっていく演出。Play()で開始する
    public class Stage3TreasureRain : MonoBehaviour
    {
        public GameObject[] treasurePrefabs;
        public float spawnDuration = 3f;
        public float spawnInterval = 0.1f;
        public Vector2 spawnAreaX = new Vector2(-8f, 8f);
        public float spawnY = 6f;

        [Header("積み上がる地面")]
        [Tooltip("最初の1個が着地する高さ")]
        public float groundY = -4f;
        [Tooltip("1個着地するごとに山の高さがこれだけ上がる")]
        public float pileGrowthPerPiece = 0.03f;
        [Tooltip("着地位置のY方向のばらつき（山を不揃いに見せる）")]
        public float landingYJitter = 0.15f;

        public Vector2 fallDurationRange = new Vector2(1.5f, 3f);
        public Vector2 rotationSpeedRange = new Vector2(-180f, 180f);

        float currentPileHeight;

        public void Play()
        {
            currentPileHeight = 0f;
            StartCoroutine(RainRoutine());
        }

        IEnumerator RainRoutine()
        {
            float elapsed = 0f;
            while (elapsed < spawnDuration)
            {
                SpawnPiece();
                yield return new WaitForSeconds(spawnInterval);
                elapsed += spawnInterval;
            }
        }

        void SpawnPiece()
        {
            if (treasurePrefabs == null || treasurePrefabs.Length == 0) return;

            var prefab = treasurePrefabs[Random.Range(0, treasurePrefabs.Length)];
            float x = Random.Range(spawnAreaX.x, spawnAreaX.y);
            Vector3 startPos = new Vector3(x, spawnY, 0f);
            var piece = Instantiate(prefab, startPos, Quaternion.identity);

            // 山の高さに合わせて着地位置を決め、次はさらに高いところに積もるようにする
            float landY = groundY + currentPileHeight + Random.Range(-landingYJitter, landingYJitter);
            currentPileHeight += pileGrowthPerPiece;

            float fallDuration = Random.Range(fallDurationRange.x, fallDurationRange.y);
            float rotSpeed = Random.Range(rotationSpeedRange.x, rotationSpeedRange.y);

            // 着地後は消さずそのまま残し、山として積み上がっていくようにする
            piece.transform.DOMoveY(landY, fallDuration).SetEase(Ease.InSine)
                .OnComplete(() => piece.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 1, 0));
            piece.transform.DORotate(new Vector3(0f, 0f, rotSpeed * fallDuration), fallDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear);
        }
    }
}
