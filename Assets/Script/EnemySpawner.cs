using UnityEngine;

namespace PPYY.Stage1
{
    public class EnemySpawner : MonoBehaviour
    {
        [Tooltip("カラス・ゴースト・ネズミなど、EnemyCharacter を付けたプレハブを登録する")]
        public GameObject[] enemyPrefabs;

        public float minInterval = 1.5f;
        public float maxInterval = 3f;

        [Tooltip("ステージ上に同時に存在できる敵の最大数")]
        public int maxEnemyCount = 20;

        public Vector2 boundsMin = new Vector2(-8f, -4f);
        public Vector2 boundsMax = new Vector2(8f, 4f);

        float timer;
        int activeCount;

        void Start()
        {
            ResetTimer();
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
            Vector3 pos = new Vector3(
                Random.Range(boundsMin.x, boundsMax.x),
                Random.Range(boundsMin.y, boundsMax.y),
                0);

            var enemy = Instantiate(prefab, pos, Quaternion.identity);
            var ec = enemy.GetComponent<EnemyCharacter>();
            if (ec != null)
            {
                ec.boundsMin = boundsMin;
                ec.boundsMax = boundsMax;
                activeCount++;
                ec.OnRemoved += () => activeCount--;
            }
        }
    }
}
