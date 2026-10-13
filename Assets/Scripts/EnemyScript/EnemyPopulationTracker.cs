using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyPopulationTracker
{
    private static readonly List<GameObject> aliveEnemies = new List<GameObject>();
    private static bool initialized;

    public static int AliveCount
    {
        get
        {
            EnsureInitialized();
            CleanUp();
            return aliveEnemies.Count;
        }
    }

    public static void Register(GameObject enemy)
    {
        EnsureInitialized();
        CleanUp();
        aliveEnemies.Add(enemy);
    }

    private static void EnsureInitialized()
    {
        if (initialized) return;
        SceneManager.sceneLoaded += (scene, mode) => aliveEnemies.Clear();
        initialized = true;
    }

    private static void CleanUp()
    {
        aliveEnemies.RemoveAll(e => e == null);
    }
}
