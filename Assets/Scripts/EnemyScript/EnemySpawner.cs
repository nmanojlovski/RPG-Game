using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemySpawnEntry
    {
        public GameObject prefab;
        public float weight = 1f;
        public float unlockDifficulty = 0f;
    }

    [SerializeField] private EnemySpawnEntry[] enemyTypes;

    [Header("Spawn Interval")]
    [SerializeField] private float baseInterval = 5f;
    [SerializeField] private float minInterval = 1f;
    [SerializeField] private float maxInterval = 6f;
    [SerializeField] private float intervalPerDifficulty = 0.15f;

    [Header("Population Cap")]
    [SerializeField] private int baseCap = 6;
    [SerializeField] private float capPerDifficulty = 0.5f;
    [SerializeField] private int hardCapCeiling = 20;

    public BoxCollider2D spawnArea;

    [SerializeField] private BoxCollider2D[] forbiddenAreas;

    [Header("Procedural Maps")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 6f;

    void Start()
    {
        StartCoroutine(spawnEnemy());
    }

    private IEnumerator spawnEnemy()
    {
        while (true)
        {
            float difficulty = GetDifficultyScore();

            float interval = Mathf.Clamp(baseInterval - difficulty * intervalPerDifficulty, minInterval, maxInterval);
            yield return new WaitForSeconds(interval);

            int cap = Mathf.Min(baseCap + Mathf.FloorToInt(difficulty * capPerDifficulty), hardCapCeiling);
            if (EnemyPopulationTracker.AliveCount >= cap)
            {
                continue;
            }

            GameObject prefab = ChooseEnemyPrefab(difficulty);
            if (prefab == null)
            {
                continue;
            }

            Vector2 spawnPos = ProceduralMapGenerator.Instance != null
                ? ProceduralMapGenerator.Instance.GetRandomSpawnPoint(minSpawnDistanceFromPlayer)
                : GetRandomPointInBounds();
            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
            EnemyPopulationTracker.Register(enemy);
        }
    }

    private float GetDifficultyScore()
    {
        return DifficultyManager.Instance != null ? DifficultyManager.Instance.GetDifficultyScore() : 1f;
    }

    private GameObject ChooseEnemyPrefab(float difficulty)
    {
        if (enemyTypes == null || enemyTypes.Length == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        foreach (var entry in enemyTypes)
        {
            if (entry.prefab != null && difficulty >= entry.unlockDifficulty)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var entry in enemyTypes)
        {
            if (entry.prefab == null || difficulty < entry.unlockDifficulty)
            {
                continue;
            }

            cumulative += entry.weight;
            if (roll <= cumulative)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    private Vector2 GetRandomPointInBounds()
    {
        Bounds bounds = spawnArea.bounds;

        Vector2 spawnPos;
        int attempts = 0;
        int maxAttempts = 30;

        do
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);

            spawnPos = new Vector2(x, y);
            attempts++;

        }
        while (IsInsideForbidden(spawnPos) && attempts < maxAttempts);

        return spawnPos;
    }

    bool IsInsideForbidden(Vector2 pos)
    {
        foreach (var area in forbiddenAreas)
        {
            if (area != null && area.bounds.Contains(pos))
            {
                return true;
            }
        }
        return false;
    }

}
