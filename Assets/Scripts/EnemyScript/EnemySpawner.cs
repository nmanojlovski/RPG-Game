using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject spawnerPrefab;
    [SerializeField] private float spawnerInterval = 3.5f;

    public BoxCollider2D spawnArea;

    [SerializeField] private BoxCollider2D[] forbiddenAreas;

    void Start()
    {
        StartCoroutine(spawnEnemy(spawnerInterval, spawnerPrefab));
    }


    private IEnumerator spawnEnemy(float interval, GameObject enemy)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);

            Vector2 spawnPos = GetRandomPointInBounds();

            Instantiate(enemy, spawnPos, Quaternion.identity);

        }
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