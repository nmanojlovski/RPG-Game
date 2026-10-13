using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance;

    [SerializeField] private int killsPerStreakPoint = 3;
    [SerializeField] private int maxStreakBonus = 5;

    public int killStreak { get; private set; } = 0;

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

    void OnEnable()
    {
        EnemyHealth.OnMonsterDefeated += OnEnemyKilled;
        PlayerHealth.OnPlayerDamaged += OnPlayerDamaged;
    }

    void OnDisable()
    {
        EnemyHealth.OnMonsterDefeated -= OnEnemyKilled;
        PlayerHealth.OnPlayerDamaged -= OnPlayerDamaged;
    }

    private void OnEnemyKilled(int xp)
    {
        killStreak++;
    }

    private void OnPlayerDamaged(int amount)
    {
        killStreak = 0;
    }

    public float GetDifficultyScore()
    {
        int level = GameManager.Instance != null ? GameManager.Instance.level : 1;
        int streakBonus = Mathf.Min(killStreak / killsPerStreakPoint, maxStreakBonus);
        return level + streakBonus;
    }
}
