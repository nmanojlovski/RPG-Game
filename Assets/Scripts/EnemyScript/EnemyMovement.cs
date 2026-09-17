using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private bool isChasing = false;
    private Rigidbody2D rb;
    private Transform player;
    private Animator anim;
    private int moveDirection = -1;
    private EnemyState enemyState;
    private float attackCooldownTimer;

    public int speed = 4;
    public float attackRange = (float)2.0;
    public float attackCooldown = 2;

    public float playerDetectRange = 5;
    public Transform detectionPoint;
    public LayerMask playerLayer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        ChangeState(EnemyState.Idle);
    }

    // Update is called once per frame
    void Update()
    {
        if (enemyState != EnemyState.Knockedback)
        {
            CheckForPlayer();

            if (attackCooldownTimer > 0)
            {
                attackCooldownTimer -= Time.deltaTime;
            }

            if (enemyState == EnemyState.Chasing)
            {
                Chase();
            }

            if (enemyState == EnemyState.Attacking)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    void Chase()
    {
        if (player.position.x - transform.position.x > 0 && moveDirection == -1 ||
            player.position.x - transform.position.x < 0 && moveDirection == 1)
        {
            Flip();
        }


        Vector2 direction = (player.position - transform.position).normalized;
        rb.linearVelocity = direction * speed;
    }

    void Flip()
    {
        moveDirection *= -1;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    void CheckForPlayer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(detectionPoint.position, playerDetectRange, playerLayer);

        if (hits.Length > 0)
        {
            player = hits[0].transform;
            


            if (Vector2.Distance(transform.position, player.transform.position) <= attackRange && attackCooldownTimer <= 0)
            {
                attackCooldownTimer = attackCooldown;
                ChangeState(EnemyState.Attacking);
            }

            else if (Vector2.Distance(transform.position, player.transform.position) > attackRange && enemyState != EnemyState.Attacking)
            {
                ChangeState(EnemyState.Chasing);
            }
        }
        else
        {
            ChangeState(EnemyState.Idle);
            rb.linearVelocity = Vector2.zero;
        }
          
        
    }


    public void ChangeState(EnemyState newState)
    {
        if(enemyState == EnemyState.Idle)
        {
            anim.SetBool("IsIdle", false);
        }
        else if(enemyState == EnemyState.Chasing)
        {
            anim.SetBool("IsChasing", false);
        }
        else if (enemyState == EnemyState.Attacking)
        {
            anim.SetBool("IsAttacking", false);
        }

        enemyState = newState;

        if (enemyState == EnemyState.Idle)
        {
            anim.SetBool("IsIdle", true);
        }
        else if (enemyState == EnemyState.Chasing)
        {
            anim.SetBool("IsChasing", true);
        }
        else if (enemyState == EnemyState.Attacking)
        {
            anim.SetBool("IsAttacking", true);
        }
    }


}

public enum EnemyState
{
    Idle,
    Chasing,
    Attacking,
    Knockedback,
}