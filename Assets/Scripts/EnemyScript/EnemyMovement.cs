using UnityEngine;
using System.Collections.Generic;

public class EnemyMovement : MonoBehaviour
{
    private bool isChasing = false;
    private Rigidbody2D rb;
    private Transform player;
    private Animator anim;
    private EnemyHealth enemyHealth;
    private int moveDirection = -1;
    private EnemyState enemyState;
    private float attackCooldownTimer;

    public int speed = 4;
    public float attackRange = (float)2.0;
    public float attackCooldown = 2;

    public float playerDetectRange = 5;
    public Transform detectionPoint;
    public LayerMask playerLayer;

    [Header("Behavior Profile")]
    public bool canFlee = true;
    public float chasePersistenceTime = 0f;
    private float chaseGiveUpTimer;

    [Header("Patrol")]
    public float patrolRadius = 4f;
    public float patrolWaitTime = 1.5f;
    [Range(0.1f, 1f)] public float patrolSpeedMultiplier = 0.5f;
    private Vector2 spawnPoint;
    private Vector2 currentPatrolTarget;
    private float patrolWaitTimer;

    [Header("Flee & Regroup")]
    public float fleeSpeedMultiplier = 1.3f;
    public float regroupRadius = 6f;
    public float regroupStopDistance = 1.2f;
    public float personalSpaceRadius = 0.7f;
    public float separationStrength = 1f;

    [Header("Pack Alert")]
    public float alertRadius = 5f;

    [Header("Stuck Avoidance")]
    public float stuckCheckInterval = 0.3f;
    public float stuckMoveThreshold = 0.05f;
    public float unstuckDuration = 0.4f;
    private Vector2 lastCheckedPosition;
    private float stuckCheckTimer;
    private float unstuckTimer;
    private Vector2 unstuckDirection;

    private static readonly List<EnemyMovement> activeEnemies = new List<EnemyMovement>();

    void OnEnable()
    {
        activeEnemies.Add(this);
    }

    void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        enemyHealth = GetComponent<EnemyHealth>();

        spawnPoint = transform.position;
        lastCheckedPosition = transform.position;
        PickNewPatrolPoint();

        ChangeState(EnemyState.Patrolling);
    }

    // Update is called once per frame
    void Update()
    {
        if (enemyState != EnemyState.Knockedback)
        {
            CheckForPlayer();
            UpdateStuckState();

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

            if (enemyState == EnemyState.Patrolling)
            {
                Patrol();
            }

            if (enemyState == EnemyState.Fleeing)
            {
                Flee();
            }
        }
    }

    void Chase()
    {
        Vector2 direction = (player.position - transform.position).normalized;
        MoveInDirection(direction, 1f);
    }

    void Flip()
    {
        moveDirection *= -1;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    void Patrol()
    {
        Vector2 toTarget = currentPatrolTarget - (Vector2)transform.position;

        if (toTarget.magnitude <= 0.2f)
        {
            rb.linearVelocity = Vector2.zero;
            patrolWaitTimer += Time.deltaTime;

            if (patrolWaitTimer >= patrolWaitTime)
            {
                patrolWaitTimer = 0f;
                PickNewPatrolPoint();
            }

            return;
        }

        MoveInDirection(toTarget.normalized, patrolSpeedMultiplier);
    }

    void PickNewPatrolPoint()
    {
        currentPatrolTarget = spawnPoint + Random.insideUnitCircle * patrolRadius;
    }

    void Flee()
    {
        // Only rally toward allies that aren't themselves fleeing, otherwise two low-health
        // enemies just run straight at each other and their colliders deadlock.
        EnemyMovement ally = FindNearestAlly(regroupRadius, excludeFleeing: true);
        Vector2 separation = GetSeparationVector(personalSpaceRadius) * separationStrength;

        if (ally != null)
        {
            float distanceToAlly = Vector2.Distance(transform.position, ally.transform.position);

            if (distanceToAlly <= regroupStopDistance)
            {
                // Close enough to the group - stop closing in so we don't push into the ally.
                rb.linearVelocity = separation;
                return;
            }

            Vector2 toAlly = ((Vector2)ally.transform.position - (Vector2)transform.position).normalized;
            MoveInDirection((toAlly + separation).normalized, fleeSpeedMultiplier);
            return;
        }

        if (player != null)
        {
            Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)player.position).normalized;
            MoveInDirection((awayFromPlayer + separation).normalized, fleeSpeedMultiplier);
        }
    }

    void MoveInDirection(Vector2 direction, float speedMultiplier)
    {
        // While breaking free from a wall we ignore the requested direction entirely
        // and keep sliding sideways until the unstuck window expires.
        if (unstuckTimer > 0f)
        {
            direction = unstuckDirection;
        }

        if (direction == Vector2.zero) return;

        if ((direction.x > 0 && moveDirection == -1) || (direction.x < 0 && moveDirection == 1))
        {
            Flip();
        }

        rb.linearVelocity = direction * speed * speedMultiplier;
    }

    void UpdateStuckState()
    {
        if (unstuckTimer > 0f)
        {
            unstuckTimer -= Time.deltaTime;
            return;
        }

        stuckCheckTimer += Time.deltaTime;
        if (stuckCheckTimer < stuckCheckInterval) return;

        float movedSinceLastCheck = Vector2.Distance(transform.position, lastCheckedPosition);
        bool wasTryingToMove = rb.linearVelocity.sqrMagnitude > 0.01f;

        if (wasTryingToMove && movedSinceLastCheck < stuckMoveThreshold)
        {
            // Barely moved despite trying to - deflect sideways off whatever we hit.
            Vector2 currentDirection = rb.linearVelocity.normalized;
            Vector2 perpendicular = new Vector2(-currentDirection.y, currentDirection.x);
            unstuckDirection = Random.value < 0.5f ? perpendicular : -perpendicular;
            unstuckTimer = unstuckDuration;

            if (enemyState == EnemyState.Patrolling)
            {
                PickNewPatrolPoint();
            }
        }

        stuckCheckTimer = 0f;
        lastCheckedPosition = transform.position;
    }

    Vector2 GetSeparationVector(float radius)
    {
        Vector2 push = Vector2.zero;

        foreach (EnemyMovement other in activeEnemies)
        {
            if (other == this || other == null) continue;

            Vector2 offset = (Vector2)transform.position - (Vector2)other.transform.position;
            float distance = offset.magnitude;

            if (distance > 0f && distance < radius)
            {
                push += offset.normalized * (radius - distance);
            }
        }

        return push;
    }

    EnemyMovement FindNearestAlly(float radius, bool excludeFleeing = false)
    {
        EnemyMovement nearest = null;
        float nearestDistance = radius;

        foreach (EnemyMovement other in activeEnemies)
        {
            if (other == this || other == null) continue;
            if (excludeFleeing && other.enemyState == EnemyState.Fleeing) continue;

            float distance = Vector2.Distance(transform.position, other.transform.position);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = other;
            }
        }

        return nearest;
    }

    void AlertNearbyAllies(float radius)
    {
        foreach (EnemyMovement other in activeEnemies)
        {
            if (other == this || other == null) continue;
            if (other.enemyState != EnemyState.Patrolling) continue;

            if (Vector2.Distance(transform.position, other.transform.position) <= radius)
            {
                other.ForceAggro(player);
            }
        }
    }

    public void ForceAggro(Transform target)
    {
        if (enemyState == EnemyState.Fleeing || enemyState == EnemyState.Knockedback) return;

        player = target;
        ChangeState(EnemyState.Chasing);
    }

    void CheckForPlayer()
    {
        if (canFlee && enemyHealth != null && enemyHealth.IsLowHealth)
        {
            if (enemyState != EnemyState.Fleeing)
            {
                ChangeState(EnemyState.Fleeing);
            }
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(detectionPoint.position, playerDetectRange, playerLayer);

        if (hits.Length > 0)
        {
            chaseGiveUpTimer = 0f;
            player = hits[0].transform;

            if (Vector2.Distance(transform.position, player.transform.position) <= attackRange && attackCooldownTimer <= 0)
            {
                attackCooldownTimer = attackCooldown;
                ChangeState(EnemyState.Attacking);
            }
            else if (Vector2.Distance(transform.position, player.transform.position) > attackRange && enemyState != EnemyState.Attacking)
            {
                if (enemyState != EnemyState.Chasing)
                {
                    AlertNearbyAllies(alertRadius);
                }

                ChangeState(EnemyState.Chasing);
            }
        }
        else
        {
            if (enemyState == EnemyState.Chasing || enemyState == EnemyState.Attacking)
            {
                chaseGiveUpTimer += Time.deltaTime;
                if (chaseGiveUpTimer < chasePersistenceTime)
                {
                    return;
                }
            }

            chaseGiveUpTimer = 0f;
            player = null;

            if (enemyState != EnemyState.Patrolling)
            {
                ChangeState(EnemyState.Patrolling);
                PickNewPatrolPoint();
            }
        }
    }


    public void ChangeState(EnemyState newState)
    {
        if(enemyState == EnemyState.Idle)
        {
            anim.SetBool("IsIdle", false);
        }
        else if(enemyState == EnemyState.Chasing || enemyState == EnemyState.Fleeing || enemyState == EnemyState.Patrolling)
        {
            anim.SetBool("IsChasing", false);
        }
        else if (enemyState == EnemyState.Attacking)
        {
            anim.SetBool("IsAttacking", false);
        }

        enemyState = newState;

        // Patrolling and Fleeing reuse the existing "IsChasing" (movement) anim bool since
        // they're both walk/run states, rather than the standing-still "IsIdle" bool.
        if (enemyState == EnemyState.Idle)
        {
            anim.SetBool("IsIdle", true);
        }
        else if (enemyState == EnemyState.Chasing || enemyState == EnemyState.Fleeing || enemyState == EnemyState.Patrolling)
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
    Patrolling,
    Chasing,
    Attacking,
    Fleeing,
    Knockedback,
}