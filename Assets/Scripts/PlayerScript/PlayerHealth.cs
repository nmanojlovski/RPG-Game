using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float deathVolume = 5f;
    [SerializeField] private float hitVolume = 0.6f;

    public TMP_Text score;
    
    void Start()
    {
        StatsManager.Instance.currentHealth = StatsManager.Instance.maxHealth;
        score.text = "HP: " + StatsManager.Instance.currentHealth + "/" + StatsManager.Instance.maxHealth;
    }

    public void DamageTaken(int amount)
    {
        StatsManager.Instance.currentHealth += amount;
        score.text = "HP: " + StatsManager.Instance.currentHealth + "/" + StatsManager.Instance.maxHealth;


        if (StatsManager.Instance.currentHealth <= 0)
        {
            gameObject.SetActive(false);

            GameObject soundObject = new GameObject("Death Sound");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();

            audioSource.clip = deathSound;
            audioSource.volume = deathVolume;
            audioSource.spatialBlend = 0f; // 2D sound (same volume everywhere)
            audioSource.Play();

            Destroy(soundObject, deathSound.length);
            SceneManager.LoadScene("EndSceneLose");
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
