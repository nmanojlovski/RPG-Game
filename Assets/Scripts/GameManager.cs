using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int level = 1;
    public int availablePoints = 5;

    // Kept here so XP and the next-level threshold survive the scene reload on every new map.
    public int xp = 0;
    public int xpToLevel = 10;

    // ?? NEW: store skill levels
    public Dictionary<string, int> skillLevels = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    public void UpdateLevel(int amount)
    {
        level += amount;
    }

    public void UpdateAvailablePoints(int amount)
    {
        availablePoints += amount;
    }
}

//public class GameManager : MonoBehaviour
//{
//    public static GameManager Instance;
//
//    public int level = 1;
//    public int availablePoints = 5;
//
//
//    void Awake()
//    {
//        Debug.Log(Instance);
//
//        if (Instance == null)
//        {
//            Instance = this;
//            DontDestroyOnLoad(gameObject);
//        }
//        else
//        {
//            Destroy(gameObject);
//        }
//    }
//
//    public void UpdateLevel(int amount)
//    {
//        level += amount;
//    }
//
//    public void UpdateAvailablePoints(int amount)
//    {
//        level += amount;
//    }
//
//
//}
