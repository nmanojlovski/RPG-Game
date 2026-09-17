using UnityEngine;
using Photon.Pun;
using System.Collections;

public class Playerealth : MonoBehaviourPun
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 10;
    private int currentHealth;

    [Header("Sounds")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float deathVolume = 1f;
    [SerializeField] private float hitVolume = 0.6f;

    private Rigidbody2D rb;
    private bool isDead = false;
    private PlayerMovement2 movement;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement2>();
        currentHealth = maxHealth;
    }

    // =========================
    // This is the only method other objects should call
    // =========================
    [PunRPC]
    public void RPC_TakeDamage(int amount, Vector2 knockDirection, float knockForce, float stunTime)
    {
        if (isDead) return;

        currentHealth += amount;   // amount is negative (-1, -2...)

        // Play hit sound on ALL clients
        PlaySound(hitSound, hitVolume);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Only the owner applies physics knockback
            if (photonView.IsMine)
            {
                StartCoroutine(ApplyKnockback(knockDirection, knockForce, stunTime));
            }
        }
    }

    private IEnumerator ApplyKnockback(Vector2 direction, float force, float stunTime)
    {
        if (movement != null)
            movement.SetKnockback(true);

        rb.linearVelocity = direction.normalized * force;

        yield return new WaitForSeconds(stunTime);

        rb.linearVelocity = Vector2.zero;

        if (movement != null)
            movement.SetKnockback(false);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("Umre1 - Die started");

        PlaySound(deathSound, deathVolume);

        if (photonView.IsMine)
        {
            // 1. First send the RPC
            photonView.RPC(nameof(RPC_PlayerDied), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);

            // 2. Destroy after a tiny delay so the RPC can be sent
            StartCoroutine(DestroyAfterRPC());
        }
    }

    IEnumerator DestroyAfterRPC()
    {
        yield return new WaitForSeconds(0.1f); // small delay
        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    void RPC_PlayerDied(int deadActorNumber)
    {
        Debug.Log("Umre2 - RPC received! Dead ActorNumber: " + deadActorNumber);

        NetworkManager nm = FindObjectOfType<NetworkManager>();
        if (nm != null)
        {
            nm.OnPlayerDied(deadActorNumber);
        }
        else
        {
            Debug.LogError("NetworkManager not found in scene!");
        }
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null) return;

        GameObject go = new GameObject("TempSound");
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0f; // 2D sound
        source.Play();
        Destroy(go, clip.length);
    }

    // Optional: useful for UI later
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;


  
}