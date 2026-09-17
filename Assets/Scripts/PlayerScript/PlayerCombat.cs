using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Animator anim;

    public float timer = 0;
    public float cooldown = 2;

    public Transform attackPoint;
    public LayerMask enemyLayer;




    void Update()
    {
        if(timer > 0)
        {
            timer -= Time.deltaTime;
        }

        Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint.position, StatsManager.Instance.weaponRange, enemyLayer);

        
    }

    public void Attacking()
    {
        if(timer <= 0)
        {
            anim.SetBool("Attack", true);
            timer = cooldown;
        }
    }

    public void DealDamage()
    {
        Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint.position, StatsManager.Instance.weaponRange, enemyLayer);
       
        foreach (Collider2D enemy in enemies)
        {
            if (enemy.isTrigger) continue; 
                                           
            enemy.GetComponent<EnemyHealth>()?.TakeDamage(-StatsManager.Instance.damage);
            enemy.GetComponent<EnemyKnockback>()?.Knockback(transform, StatsManager.Instance.knockbackForce, StatsManager.Instance.stunTime);
        }
    }

    public void StopAttacking()
    {
        anim.SetBool("Attack", false);
    }

  
}
