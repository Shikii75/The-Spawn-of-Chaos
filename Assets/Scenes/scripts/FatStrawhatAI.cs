using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FatStrawhatAI : MonoBehaviour
{
    public enum State
    {
        Patrolling,
        Chasing,
        Attacking
    }

    [Header("State")]
    public State currentState = State.Patrolling;

    [Header("Patrol Settings")]
    [Tooltip("The distance to walk left and right from the starting position.")]
    public float patrolDistance = 3f;
    [Tooltip("Movement speed.")]
    public float moveSpeed = 2f;
    [Tooltip("How long to wait at each patrol point before turning back.")]
    public float stopDuration = 2f;

    [Header("Combat Settings")]
    public float detectionRange = 6f;
    public float attackRange = 1.5f;
    public float attackCooldown = 2f;
    [Tooltip("Chance (0 to 1) that an attack is a Normal Strike. Remainder is Jump Strike.")]
    [Range(0f, 1f)]
    public float normalStrikeChance = 0.7f;

    // References
    private Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private Collider2D bodyCollider;

    // Internal States
    private Vector3 startingPosition;
    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float lastAttackTime = -999f;
    private string currentAnimState = "";

    // Animator State Names (FatStrawhat controller uses no parameters)
    private const string IDLE_STATE = "FatStrawhat";
    private const string WALK_STATE = "Fatwalk";
    private const string NORMAL_STRIKE_STATE = "Fatstrike";    // lowercase s in controller
    private const string JUMP_STRIKE_STATE = "FatStrike";       // capital S in controller

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        startingPosition = transform.position;
        FindPlayer();
        PlayAnimation(IDLE_STATE);
    }

    void Update()
    {
        if (currentState == State.Attacking) return;

        if (player == null)
        {
            FindPlayer();
            if (player != null)
            {
                Debug.Log($"[{name}] Player found on Update: {player.name} at position {player.position}");
            }
        }

        // Spot player check
        if (player != null && !IsPlayerDead())
        {
            float distanceToPlayer = GetDistanceToPlayer();
            if (distanceToPlayer <= detectionRange)
            {
                if (currentState != State.Chasing)
                {
                    Debug.Log($"[{name}] Player detected! Distance: {distanceToPlayer:F2} (Range: {detectionRange}). Switching to Chasing.");
                    currentState = State.Chasing;
                }
            }
            else if (currentState == State.Chasing)
            {
                Debug.Log($"[{name}] Player went out of range! Distance: {distanceToPlayer:F2}. Returning to Patrolling.");
                // Return to patrolling if player is lost
                currentState = State.Patrolling;
                startingPosition = transform.position; // Reset patrol center
            }
        }
        else if (currentState == State.Chasing)
        {
            Debug.Log($"[{name}] Player is dead or null. Returning to Patrolling.");
            currentState = State.Patrolling;
            startingPosition = transform.position;
        }

        if (currentState == State.Patrolling && isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null || currentState == State.Attacking) return;

        if (currentState == State.Patrolling)
        {
            UpdatePatrolling();
        }
        else if (currentState == State.Chasing)
        {
            UpdateChasing();
        }
    }

    private void UpdatePatrolling()
    {
        if (isWaiting)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            PlayAnimation(IDLE_STATE);
            return;
        }

        float targetX = startingPosition.x + (movingRight ? patrolDistance : -patrolDistance);
        float direction = targetX > transform.position.x ? 1f : -1f;

        if (Mathf.Abs(transform.position.x - targetX) < 0.15f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            PlayAnimation(IDLE_STATE);
            isWaiting = true;
            waitTimer = stopDuration;
            movingRight = !movingRight;
            return;
        }

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        PlayAnimation(WALK_STATE);
        FlipSprite(direction);
    }

    private void UpdateChasing()
    {
        if (player == null || IsPlayerDead())
        {
            currentState = State.Patrolling;
            startingPosition = transform.position;
            return;
        }

        float distanceToPlayer = GetDistanceToPlayer();
        float horizontalDist = GetHorizontalDistanceToPlayer();
        float playerDirection = player.position.x > transform.position.x ? 1f : -1f;

        // Temporarily log values to diagnose pushing
        if (Time.frameCount % 30 == 0) // Log once every 30 frames to avoid spamming
        {
            Debug.Log($"[{name}] HorizontalDist: {horizontalDist:F2}, EffRange: {GetEffectiveAttackRange():F2}, EnemyHalfWidth: {GetHalfWidth():F2}");
        }

        if (horizontalDist <= GetEffectiveAttackRange())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(PerformAttackRoutine());
            }
            else
            {
                PlayAnimation(IDLE_STATE);
                FlipSprite(playerDirection);
            }
            return;
        }

        // Walk towards player
        rb.linearVelocity = new Vector2(playerDirection * moveSpeed, rb.linearVelocity.y);
        PlayAnimation(WALK_STATE);
        FlipSprite(playerDirection);
    }

    private IEnumerator PerformAttackRoutine()
    {
        currentState = State.Attacking;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        float playerDirection = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        FlipSprite(playerDirection);

        // Weighted random choice for attack variation
        bool useNormalStrike = Random.value < normalStrikeChance;
        string attackState = useNormalStrike ? NORMAL_STRIKE_STATE : JUMP_STRIKE_STATE;

        PlayAnimation(attackState);

        // Wait one frame for animator state to update so we get correct duration
        yield return null;

        float animLength = 0.8f; // Default safety fallback
        if (anim != null)
        {
            animLength = anim.GetCurrentAnimatorStateInfo(0).length;
        }

        yield return new WaitForSeconds(animLength);

        // Return to Idle to guarantee animation does not loop
        PlayAnimation(IDLE_STATE);
        lastAttackTime = Time.time;
        currentState = State.Chasing;
    }

    private void FindPlayer()
    {
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null)
        {
            player = pObj.transform;
        }
        else
        {
            move pMove = FindObjectOfType<move>();
            if (pMove != null) player = pMove.transform;
        }
    }

    private bool IsPlayerDead()
    {
        if (player == null) return true;
        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
        return playerHealth != null && playerHealth.CurrentHealth <= 0;
    }

    private void PlayAnimation(string stateName)
    {
        if (anim == null || currentAnimState == stateName) return;

        anim.Play(stateName);
        currentAnimState = stateName;
    }

    private void FlipSprite(float directionX)
    {
        if (directionX > 0f)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (directionX < 0f)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    public float GetEffectiveAttackRange()
    {
        float enemyWidth = GetHalfWidth();
        float playerWidth = 0.5f;
        if (player != null)
        {
            Collider2D pCol = player.GetComponent<Collider2D>();
            if (pCol == null) pCol = player.GetComponentInChildren<Collider2D>();
            if (pCol != null) playerWidth = pCol.bounds.extents.x;
        }
        float touchDistance = enemyWidth + playerWidth;
        // Enforce the rule: ensure attack range is larger than the physical boundaries to avoid pushing
        return Mathf.Max(attackRange, touchDistance + 0.15f);
    }

    private float GetHalfWidth()
    {
        Collider2D col = GetComponent<Collider2D>();
        return col != null ? col.bounds.extents.x : 0.5f;
    }

    public float GetDistanceToPlayer()
    {
        if (player == null) return 999f;
        
        Vector2 enemyCenter = bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        Vector2 playerCenter = player.position;
        
        Collider2D pCol = player.GetComponent<Collider2D>();
        if (pCol == null) pCol = player.GetComponentInChildren<Collider2D>();
        if (pCol != null) playerCenter = pCol.bounds.center;
        
        return Vector2.Distance(enemyCenter, playerCenter);
    }

    public float GetHorizontalDistanceToPlayer()
    {
        if (player == null) return 999f;

        float enemyX = bodyCollider != null ? bodyCollider.bounds.center.x : transform.position.x;
        float playerX = player.position.x;

        Collider2D pCol = player.GetComponent<Collider2D>();
        if (pCol == null) pCol = player.GetComponentInChildren<Collider2D>();
        if (pCol != null) playerX = pCol.bounds.center.x;

        return Mathf.Abs(enemyX - playerX);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Vector3 center = transform.position;
        if (bodyCollider != null)
        {
            center = bodyCollider.bounds.center;
        }
        else
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) center = col.bounds.center;
        }
        Gizmos.DrawWireSphere(center, GetEffectiveAttackRange());
    }
}
