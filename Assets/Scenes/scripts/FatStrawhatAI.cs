using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FatStrawhatAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Patrolling,
        Chasing,
        Attacking,
        HitStun,
        Dead
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
    public float attackCooldown = 1.6f;
    [Tooltip("Chance (0 to 1) that an attack is a Normal Strike. Remainder is Jump Strike.")]
    [Range(0f, 1f)]
    public float normalStrikeChance = 0.6f;
    public float chargeSpeedMultiplier = 1.5f;
    public float chargeRangeMin = 3f;
    public float chargeRangeMax = 5f;
    public float postAttackVulnerabilityDuration = 0.5f;

    [Header("Health & Damage")]
    public int maxHealth = 60;
    [SerializeField] private int currentHealth;
    public int baseDamage = 4;
    public float knockbackForce = 3f;
    public float hitStunDuration = 0.25f;
    public float heavyStaggerDuration = 0.4f;
    public float deathLaunchForce = 5f;
    private SpriteJuice spriteJuice;

    // References
    private Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;

    // Internal States
    private Vector3 startingPosition;
    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float lastAttackTime = -999f;
    private string currentAnimState = "";
    private bool isCharging = false;
    private float chargeTimer = 0f;

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
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.72f, 0.2f, 0.1f, 1f);
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        currentHealth = maxHealth;
        spriteJuice = GetComponent<SpriteJuice>();

        startingPosition = transform.position;
        FindPlayer();
        PlayAnimation(IDLE_STATE);
    }

    void Update()
    {
        if (currentState == State.Attacking || currentState == State.Dead || currentState == State.HitStun)
            return;

        if (player == null)
        {
            FindPlayer();
        }

        // Spot player check
        if (player != null && !IsPlayerDead())
        {
            float distanceToPlayer = GetDistanceToPlayer();
            if (distanceToPlayer <= detectionRange)
            {
                if (currentState != State.Chasing)
                {
                    currentState = State.Chasing;
                }
            }
            else if (currentState == State.Chasing)
            {
                // Return to patrolling if player is lost
                currentState = State.Patrolling;
                startingPosition = transform.position; // Reset patrol center
                isCharging = false;
            }
        }
        else if (currentState == State.Chasing)
        {
            currentState = State.Patrolling;
            startingPosition = transform.position;
            isCharging = false;
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
        if (rb == null || currentState == State.Attacking || currentState == State.Dead || currentState == State.HitStun)
            return;

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
            isCharging = false;
            return;
        }

        float horizontalDist = GetHorizontalDistanceToPlayer();
        float playerDirection = player.position.x > transform.position.x ? 1f : -1f;

        // 1. Attack Range — stop and swing
        if (horizontalDist <= GetEffectiveAttackRange())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            isCharging = false;
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

        // 2. Charge Walk — menacing approach at 1.5x speed when 3–5 units away
        float currentMoveSpeed = moveSpeed;
        if (horizontalDist >= chargeRangeMin && horizontalDist <= chargeRangeMax)
        {
            if (!isCharging)
            {
                isCharging = true;
                chargeTimer = 1.0f; // charge for 1 second
            }

            if (chargeTimer > 0f)
            {
                currentMoveSpeed = moveSpeed * chargeSpeedMultiplier;
                chargeTimer -= Time.fixedDeltaTime;
            }
            else
            {
                isCharging = false;
            }
        }
        else
        {
            isCharging = false;
        }

        // Walk towards player
        rb.linearVelocity = new Vector2(playerDirection * currentMoveSpeed, rb.linearVelocity.y);
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
        // If very close, bias toward jump strike for a punishing slam
        float effectiveRange = GetEffectiveAttackRange();
        float dist = GetHorizontalDistanceToPlayer();
        float strikeChance = normalStrikeChance;
        if (dist < effectiveRange * 0.8f)
        {
            strikeChance = 0.4f; // More likely to jump strike when right on top of player
        }

        bool useNormalStrike = Random.value < strikeChance;
        string attackState = useNormalStrike ? NORMAL_STRIKE_STATE : JUMP_STRIKE_STATE;

        PlayAnimation(attackState);

        // Wait one frame for animator state to update so we get correct duration
        yield return null;

        float animLength = 0.8f; // Default safety fallback
        if (anim != null)
        {
            animLength = anim.GetCurrentAnimatorStateInfo(0).length;
        }

        // Wait until the strike impact point
        float damageDelay = useNormalStrike ? 0.35f : 0.45f;
        damageDelay = Mathf.Min(damageDelay, animLength * 0.8f);

        yield return new WaitForSeconds(damageDelay);

        // Check range and apply damage to the player
        if (player != null && !IsPlayerDead())
        {
            float finalRange = GetEffectiveAttackRange() + 0.5f;
            if (GetHorizontalDistanceToPlayer() <= finalRange)
            {
                Health playerHealth = player.GetComponent<Health>();
                if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
                if (playerHealth != null)
                {
                    int dmg = useNormalStrike ? baseDamage : Mathf.RoundToInt(baseDamage * 1.5f);
                    playerHealth.TakeDamage(dmg);
                }
            }
        }

        float remainingTime = animLength - damageDelay;
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // Post-attack vulnerability window — idle pause where player can punish
        PlayAnimation(IDLE_STATE);
        lastAttackTime = Time.time;

        yield return new WaitForSeconds(postAttackVulnerabilityDuration);

        if (currentState == State.Attacking)
        {
            currentState = State.Chasing;
        }
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
            move pMove = Object.FindFirstObjectByType<move>();
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

    // ── IDamageable ──────────────────────────────────────────────────

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0 || currentState == State.Dead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        // Interrupt whatever he's doing — rewards the player for landing hits
        StopAllCoroutines();

        // SpriteJuice visual feedback (flash + shake + knockback)
        if (spriteJuice != null)
        {
            Vector2 hitDir = player != null ? (Vector2)(transform.position - player.position).normalized : Vector2.right;
            spriteJuice.PlayHitReaction(hitDir, knockbackForce);
        }

        HitFeedbackManager.TriggerHitFeedback(transform, transform.position, damage, damage >= 25, EnemyHitType.PhysicalMelee);

        if (currentHealth <= 0)
        {
            StartCoroutine(DieRoutine());
        }
        else
        {
            // Heavy stagger when below 30% HP for dramatic feel
            bool heavyStagger = currentHealth < maxHealth * 0.3f;
            StartCoroutine(HitStunRoutine(heavyStagger ? heavyStaggerDuration : hitStunDuration));
        }
    }

    private IEnumerator HitStunRoutine(float duration)
    {
        currentState = State.HitStun;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        PlayAnimation(IDLE_STATE);
        currentAnimState = ""; // Force re-play on next state change

        yield return new WaitForSeconds(duration);

        if (currentState == State.HitStun)
        {
            currentState = State.Chasing;
        }
    }

    private IEnumerator DieRoutine()
    {
        currentState = State.Dead;

        // Disable collider immediately to prevent ghost interactions
        if (bodyCollider != null) bodyCollider.enabled = false;

        PlayAnimation(IDLE_STATE);
        currentAnimState = "";

        // Dramatic upward + backward launch
        if (rb != null)
        {
            rb.isKinematic = false;
            Vector2 launchDir = player != null
                ? (Vector2)(transform.position - player.position).normalized
                : Vector2.right;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(launchDir.x * deathLaunchForce, deathLaunchForce * 1.2f), ForceMode2D.Impulse);
        }

        // Fade out the sprite over the death delay
        float fadeDelay = 1.2f;
        float elapsed = 0f;
        while (elapsed < fadeDelay)
        {
            elapsed += Time.unscaledDeltaTime;

            // Fade sprite alpha in the last 0.5s
            if (spriteRenderer != null && elapsed > fadeDelay - 0.5f)
            {
                float fadeT = (elapsed - (fadeDelay - 0.5f)) / 0.5f;
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                spriteRenderer.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
