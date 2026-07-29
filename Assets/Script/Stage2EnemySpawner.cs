using System.Collections;
using UnityEngine;

namespace PPYY.Stage2
{
    public class Stage2EnemySpawner : MonoBehaviour
    {
        [Tooltip("カラス・ネズミ・ドローンなど、Stage2EnemyCharacter を付けたプレハブを登録する")]
        public GameObject[] enemyPrefabs;

        [Header("通常出現")]
        public float minInterval = 1.5f;
        public float maxInterval = 3f;

        [Tooltip("ステージ上に同時に存在できる敵の最大数")]
        public int maxEnemyCount = 20;

        [Header("出現位置")]
        [Tooltip("このスポナー自身の位置(transform.position)を中心とした半径内にランダムで出現させる")]
        public float spawnRadius = 6f;

        [Header("徘徊範囲（お宝を盗んだ後にうろつく範囲。ワールド座標のAABB）")]
        public Vector2 boundsMin = new Vector2(-8f, -4f);
        public Vector2 boundsMax = new Vector2(8f, 4f);

        [Header("群れ（スウォーム）")]
        public float swarmIntervalMin = 20f;
        public float swarmIntervalMax = 45f;
        public int swarmSizeMin = 6;
        public int swarmSizeMax = 10;
        public float swarmWarningDuration = 2f;

        [Tooltip("群れ襲来の警告として表示するUIオブジェクト（任意、SetActiveで表示/非表示）")]
        public GameObject swarmWarningUI;

        [Header("警告音（UI表示と同時に再生し、UIが消えると同時に停止）")]
        public AudioSource warningAudioSource;
        public AudioClip warningSound;

        float timer;
        int activeCount;

        void Start()
        {
            ResetTimer();
            if (swarmWarningUI != null) swarmWarningUI.SetActive(false);
            StartCoroutine(SwarmLoop());
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                SpawnRandomEnemy();
                ResetTimer();
            }
        }

        void ResetTimer()
        {
            timer = Random.Range(minInterval, maxInterval);
        }

        void SpawnRandomEnemy()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
            if (activeCount >= maxEnemyCount) return; // 上限に達している間は出現させない

            var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = transform.position + new Vector3(offset.x, offset.y, 0);

            var enemy = Instantiate(prefab, pos, Quaternion.identity);
            var ec = enemy.GetComponent<Stage2EnemyCharacter>();
            if (ec != null)
            {
                ec.boundsMin = boundsMin;
                ec.boundsMax = boundsMax;
                activeCount++;
                ec.OnRemoved += () => activeCount--;
            }
        }

        // ランダムな間隔で警告UI・警告音を出し、少し待ってからまとめて敵を出現させる
        IEnumerator SwarmLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(swarmIntervalMin, swarmIntervalMax));

                if (swarmWarningUI != null) swarmWarningUI.SetActive(true);
                PlayWarningSound();

                yield return new WaitForSeconds(swarmWarningDuration);
                if (swarmWarningUI != null) swarmWarningUI.SetActive(false);
                if (warningAudioSource != null) warningAudioSource.Stop();

                int swarmSize = Random.Range(swarmSizeMin, swarmSizeMax + 1);
                for (int i = 0; i < swarmSize; i++)
                {
                    SpawnRandomEnemy(); // 上限を超える分は自動的にスキップされる
                }
            }
        }

        void PlayWarningSound()
        {
            if (warningAudioSource == null || warningSound == null) return;
            warningAudioSource.clip = warningSound;
            warningAudioSource.loop = true; // 表示中は鳴らし続けたいのでループ再生にする
            warningAudioSource.Play();
        }
    }
}
