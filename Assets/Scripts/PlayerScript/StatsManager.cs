using UnityEngine;
using TMPro;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance;

    [Header("Combat Stats")]
    public int damage;
    public float knockbackForce;
    public float weaponRange;
    public float stunTime;

    [Header("Movement Stats")]
    public int speed;

    [Header("Health Stats")]
    public int maxHealth;
    public int currentHealth;

    void Awake()
    {
        Debug.Log(Instance);

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UpdateMaxHealth(int amount)
    {
        maxHealth += amount;
    }

    public void UpdateDamage(int amount)
    {
        damage += amount;
    }

    public void UpdateSpeed(int amount)
    {
        speed += amount;
    }
}

