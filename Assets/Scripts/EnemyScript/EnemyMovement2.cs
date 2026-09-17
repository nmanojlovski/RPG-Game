using UnityEngine;
using Photon.Pun;
using System.Collections;

public class EnemyMovement2 : MonoBehaviourPun
{
    [Header("Movement Settings")]
    public int speed = 4;
    public float attackRange = 2f;
    public float attackCooldown = 2f;
    public float playerDetectRange = 5f;
    public Transform detectionPoint;
    public LayerMask playerLayer;

    private Rigidbody2D rb;
    private Animator anim;
    private Transform player;
    private int moveDirection = -1;
    private EnemyAIState enemyState;
    private float attackCooldownTimer;
    private bool isKnockedback = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        ChangeState(EnemyAIState.Idle);
    }

    void Update()
    {
        

        if (!PhotonNetwork.IsMasterClient)
        {
            // On non-master clients we should NOT control the enemy
            return;
        }


        // Only MasterClient runs the AI
        if (!PhotonNetwork.IsMasterClient) return;

        if (isKnockedback) return;

        CheckForPlayer();

        if (attackCooldownTimer > 0)
            attackCooldownTimer -= Time.deltaTime;

        if (enemyState == EnemyAIState.Chasing)
        {
            Chase();
        }
        else if (enemyState == EnemyAIState.Attacking)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void Chase()
    {
        if (player == null) return;

        if ((player.position.x > transform.position.x && moveDirection == -1) ||
            (player.position.x < transform.position.x && moveDirection == 1))
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
            float distance = Vector2.Distance(transform.position, player.position);

            if (distance <= attackRange && attackCooldownTimer <= 0)
            {
                attackCooldownTimer = attackCooldown;
                ChangeState(EnemyAIState.Attacking);
            }
            else if (distance > attackRange && enemyState != EnemyAIState.Attacking)
            {
                ChangeState(EnemyAIState.Chasing);
            }
        }
        else
        {
            player = null;
            ChangeState(EnemyAIState.Idle);
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void ChangeState(EnemyAIState newState)
    {
        if (enemyState == EnemyAIState.Idle) anim.SetBool("IsIdle", false);
        else if (enemyState == EnemyAIState.Chasing) anim.SetBool("IsChasing", false);
        else if (enemyState == EnemyAIState.Attacking) anim.SetBool("IsAttacking", false);

        enemyState = newState;

        if (enemyState == EnemyAIState.Idle) anim.SetBool("IsIdle", true);
        else if (enemyState == EnemyAIState.Chasing) anim.SetBool("IsChasing", true);
        else if (enemyState == EnemyAIState.Attacking) anim.SetBool("IsAttacking", true);
    }

    public void SetKnockback(bool value, float duration = 0.35f)
    {
        isKnockedback = value;

        if (value)
        {
            ChangeState(EnemyAIState.Knockedback);
            StartCoroutine(KnockbackRoutine(duration));
        }
    }

    private IEnumerator KnockbackRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isKnockedback = false;

        if (PhotonNetwork.IsMasterClient)
            ChangeState(EnemyAIState.Idle);
    }
}

public enum EnemyAIState
{
    Idle,
    Chasing,
    Attacking,
    Knockedback
}