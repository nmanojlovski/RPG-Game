using UnityEngine;
using Photon.Pun;
using System.Collections;

public class EnemyHealth2 : MonoBehaviourPun
{
    [Header("Health & XP")]
    public int maxHealth = 10;
    public int xpReward = 3;

    private int currentHealth;

    [Header("Sounds")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float deathVolume = 1f;
    [SerializeField] private float hitVolume = 0.6f;

    public delegate void MonsterDefeated(int xp);
    public static event MonsterDefeated OnMonsterDefeated;

    private bool isDead = false;
    private Rigidbody2D rb;

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
    }

    [PunRPC]
    public void RPC_TakeDamage(int amount, Vector2 knockDirection, float knockForce, float stunTime)
    {
        if (isDead) return;

        currentHealth += amount;

        PlaySound(hitSound, hitVolume);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (rb != null)
            {
                StartCoroutine(ApplyKnockback(knockDirection, knockForce, stunTime));

                // Tell the movement script about the knockback
                var movement = GetComponent<EnemyMovement2>();
                if (movement != null)
                {
                    movement.SetKnockback(true, stunTime);
                }
            }
        }
    }

    private IEnumerator ApplyKnockback(Vector2 direction, float force, float stunTime)
    {
        rb.linearVelocity = direction.normalized * force;
        yield return new WaitForSeconds(stunTime);
        rb.linearVelocity = Vector2.zero;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (PhotonNetwork.IsMasterClient)
        {
            OnMonsterDefeated?.Invoke(xpReward);
        }

        PlaySound(deathSound, deathVolume);

        if (photonView.IsMine || PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null) return;

        GameObject go = new GameObject("TempSound");
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.Play();
        Destroy(go, clip.length);
    }
}