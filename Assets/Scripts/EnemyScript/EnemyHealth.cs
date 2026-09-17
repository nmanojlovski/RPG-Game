using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int xpReward = 3;

    public delegate void MonsterDefeated(int xp);
    public static event MonsterDefeated OnMonsterDefeated;

    public int health;
    public int maxHealth = 10;

    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float deathVolume = 5f;
    [SerializeField] private float hitVolume = 0.6f;


    void Start()
    {
        health = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        health += damage;

        if(health <= 0)
        {
            OnMonsterDefeated(xpReward);

            GameObject soundObject = new GameObject("Death Sound");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();

            audioSource.clip = deathSound;
            audioSource.volume = deathVolume;
            audioSource.spatialBlend = 0f; // 2D sound (same volume everywhere)
            audioSource.Play();

            Destroy(soundObject, deathSound.length);

            Destroy(gameObject);
        }
        else
        {
            GameObject soundObject = new GameObject("Hit Sound");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();

            audioSource.clip = hitSound;
            audioSource.volume = hitVolume;
            audioSource.spatialBlend = 0f; // 2D sound (same volume everywhere)
            audioSource.Play();

            Destroy(soundObject, hitSound.length);
        }

    }
}
