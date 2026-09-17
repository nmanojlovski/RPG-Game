using UnityEngine;
using System.Collections;

public class EnemyKnockback : MonoBehaviour
{
    private Rigidbody2D rb;
    private EnemyMovement enemyMovement;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyMovement = GetComponent<EnemyMovement>();
    }



    public void Knockback(Transform player, float knockforce, float stunTime)
    {
        Vector2 direction = (transform.position - player.position).normalized;
        rb.linearVelocity = direction * knockforce;

        enemyMovement.ChangeState(EnemyState.Knockedback);
        StartCoroutine(KnockbackCounter(stunTime));
    }

    IEnumerator KnockbackCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        rb.linearVelocity = Vector2.zero;
        enemyMovement.ChangeState(EnemyState.Idle);
    }


}
