using UnityEngine;
using Photon.Pun;
using System.Collections;

public class EnemySpawner2 : MonoBehaviourPun
{
    [Header("Spawn Settings")]
    public string enemyPrefabName = "Enemy1"; // Name of the prefab in Resources folder
    public Transform[] spawnPoints;
    public float spawnInterval = 5f;
    public int maxEnemies = 10;

    private int currentEnemyCount = 0;

    void Start()
    {
        // Only the MasterClient should spawn enemies
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(SpawnRoutine());
        }
    }

    void OnEnable()
    {
        EnemyHealth.OnMonsterDefeated += HandleEnemyDefeated;
    }

    void OnDisable()
    {
        EnemyHealth.OnMonsterDefeated -= HandleEnemyDefeated;
    }

    private void HandleEnemyDefeated(int xp)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            OnEnemyDied();
        }
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            if (currentEnemyCount < maxEnemies)
            {
                SpawnEnemy();
            }
        }
    }

    void SpawnEnemy()
    {
        if (spawnPoints.Length == 0) return;

        // Pick a random spawn point
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Spawn the networked enemy
        GameObject enemy = PhotonNetwork.Instantiate(
            enemyPrefabName,
            spawnPoint.position,
            spawnPoint.rotation
        );

        currentEnemyCount++;
    }

    // Call this when an enemy dies (from the XP event or Die method)
    public void OnEnemyDied()
    {
        currentEnemyCount = Mathf.Max(0, currentEnemyCount - 1);
    }
}