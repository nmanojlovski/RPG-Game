using UnityEngine;
using Photon.Pun;

public class PlayerCombat2 : MonoBehaviourPun
{
    public Animator anim;
    public float cooldown = 0.8f;
    public Transform attackPoint;
    public LayerMask enemyLayer;          // Must include Players + AI Enemies
    public float attackRadius = 0.8f;

    private float timer = 0f;

    void Update()
    {
        if (!photonView.IsMine) return;

        if (timer > 0f)
            timer -= Time.deltaTime;
    }

    public void Attacking()
    {
        if (!photonView.IsMine) return;

        if (timer <= 0f)
        {
            anim.SetBool("Attack", true);   // Photon Animator View will sync this
            timer = cooldown;
        }
    }

    // === Call this from Animation Event (at the hit frame) ===
    public void DealDamage()
    {
        if (!photonView.IsMine) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger) continue;

            // Better self-check (works even if collider is on a child)
            PhotonView targetView = hit.GetComponentInParent<PhotonView>();
            if (targetView == null) continue;
            if (targetView == photonView) continue;   // ? don't hit yourself

            Vector2 knockDir = (hit.transform.position - transform.position).normalized;

            targetView.RPC(
                "RPC_TakeDamage",
                RpcTarget.All,
                -1,
                knockDir,
                10f,
                0.35f
            );
        }
    }

    // === Call this from Animation Event (at the end of the attack animation) ===
    public void StopAttacking()
    {
        anim.SetBool("Attack", false);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}