using UnityEngine;
using Photon.Pun;

public class EnemyDamage1 : MonoBehaviourPun
{
    [Header("Attack Settings")]
    public int damage = 1;
    public float knockbackForce = 8f;
    public float stunTime = 0.3f;
    public Transform attackPoint;
    public float weaponRange = 0.8f;
    public LayerMask playerLayer;

    // Call this from your AI attack animation event or AI logic
    public void Attack()
    {
        // Only the MasterClient (or the owner of the enemy) should detect hits
        if (!PhotonNetwork.IsMasterClient && !photonView.IsMine)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position,
            weaponRange,
            playerLayer
        );

        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger) continue;

            PhotonView targetView = hit.GetComponent<PhotonView>();
            if (targetView == null) continue;

            // Calculate knockback direction
            Vector2 knockDir = (hit.transform.position - transform.position).normalized;

            // Send damage + knockback to the player (works with the PlayerHealth we made earlier)
            targetView.RPC(
                "RPC_TakeDamage",
                RpcTarget.All,
                -damage,
                knockDir,
                knockbackForce,
                stunTime
            );
        }
    }

    // Optional: draw the attack range in the editor
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, weaponRange);
    }
}