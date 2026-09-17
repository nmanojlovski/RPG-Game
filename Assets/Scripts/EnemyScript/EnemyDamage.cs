using UnityEngine;

public class EnemyDamage : MonoBehaviour
{
    public float stunTime;
    public int damage;
    public int knocbakcForce; 
    public Transform attackPoint;
    public float weaponRange;
    public LayerMask playerLayer;
    

 
    public void Attack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, weaponRange, playerLayer);

        if (hits.Length > 0)
        {
            hits[0].GetComponent<PlayerHealth>().DamageTaken(-damage);
            hits[0].GetComponent<PlayerMovement>().Knockback(transform, knocbakcForce, stunTime);
        }
    }
}
