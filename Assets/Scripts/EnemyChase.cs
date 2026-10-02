using UnityEngine;

public class EnemyChase : MonoBehaviour
{
    [Header("Targeting")]
    public Transform player;
    public float detectionRadius = 5f;
    public float playerAttackRange = 2f;
    
    [Header("Movement Speeds")]
    public float runSpeed = 4f;
    public float walkSpeed = 2f;

    [Header("Line of Sight")]
    public LayerMask obstacleLayer;

    [Header("Wandering")]
    public float wanderRadius = 3f;
    public float maxWalkTime = 4f; // Fail-safe: max time to spend walking before giving up
    
    [Header("Idle/Waiting")]
    public float minWaitTime = 1f;
    public float maxWaitTime = 5f;

    [Header("Separation (Flocking)")]
    public float separationRadius = 1f;
    public float separationWeight = 1.5f;

    private Vector2 anchorPoint; 
    private Vector2 wanderTarget;
    private float walkTimer;
    
    // New variables for the waiting logic
    private bool isWaiting;
    private float waitTimer;
    private float currentWaitDuration;

    private void Start()
    {
        anchorPoint = transform.position;
        StartWaiting(); // Start the game by standing still, then pick a point
    }

    private void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= playerAttackRange)
        {
            FleePlayer();
        }
        else if (distanceToPlayer <= detectionRadius && HasLineOfSight())
        {
            ChasePlayer();
        }
        else
        {
            Wander();
        }
    }

    private bool HasLineOfSight()
    {
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleLayer);
        return hit.collider == null; 
    }

    private void ChasePlayer()
    {
        // Cancel waiting if we get aggro'd
        isWaiting = false; 
        Move(player.position, runSpeed);
    }

    private void FleePlayer()
    {
        isWaiting = false;
        Vector2 directionAway = (transform.position - player.position).normalized;
        Vector2 escapeTarget = (Vector2)transform.position + directionAway;
        Move(escapeTarget, runSpeed);
    }

    private void Wander()
    {
        if (isWaiting)
        {
            // Stand still and count up the timer
            waitTimer += Time.deltaTime;
            
            // Once the random wait time is finished, pick a new spot
            if (waitTimer >= currentWaitDuration)
            {
                isWaiting = false;
                PickNewWanderTarget();
            }
        }
        else
        {
            walkTimer += Time.deltaTime;
            
            // Check if we arrived AT the target, OR if we've been walking too long (stuck on a wall)
            if (Vector2.Distance(transform.position, wanderTarget) < 0.1f || walkTimer >= maxWalkTime)
            {
                StartWaiting();
            }
            else
            {
                Move(wanderTarget, walkSpeed);
            }
        }
    }

    private void StartWaiting()
    {
        isWaiting = true;
        waitTimer = 0f;
        // Pick a random wait time between 1 and 5 seconds
        currentWaitDuration = Random.Range(minWaitTime, maxWaitTime); 
    }

    private void PickNewWanderTarget()
    {
        walkTimer = 0f;
        Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
        wanderTarget = anchorPoint + randomOffset;
    }

    private void Move(Vector2 targetPosition, float speed)
    {
        Vector2 currentPos = transform.position;
        Vector2 directionToTarget = (targetPosition - currentPos).normalized;
        Vector2 separationForce = GetSeparation();
        
        Vector2 finalDirection = (directionToTarget + separationForce).normalized;

        if (Vector2.Distance(currentPos, targetPosition) > 0.05f)
        {
            transform.position += (Vector3)(finalDirection * speed * Time.deltaTime);
        }
    }

    private Vector2 GetSeparation()
    {
        Vector2 separationForce = Vector2.zero;
        int neighborCount = 0;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, separationRadius);

        foreach (Collider2D col in colliders)
        {
            if (col.gameObject != gameObject && col.CompareTag("Enemy"))
            {
                Vector2 directionAway = (Vector2)transform.position - (Vector2)col.transform.position;
                float distance = directionAway.magnitude;
                if (distance > 0) 
                {
                    separationForce += (directionAway.normalized / distance);
                    neighborCount++;
                }
            }
        }

        if (neighborCount > 0)
        {
            separationForce /= neighborCount;
            separationForce *= separationWeight;
        }

        return separationForce;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, playerAttackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(anchorPoint, wanderRadius);
            
            // Draw line to target only if we are actually walking towards it
            if (!isWaiting)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, wanderTarget);
            }
        }
    }
}