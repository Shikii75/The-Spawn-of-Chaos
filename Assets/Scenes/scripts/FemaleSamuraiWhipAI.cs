using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FemaleSamuraiWhipAI - 2D Combat AI for the Female Samurai Whip mob in Dojo 2 (Samurai Clan).
/// Features:
/// 1. Start Charge: Plays start charge inbetween frames with an initial acceleration curve.
/// 2. Continue Charge: High-speed running charge locked in the player's direction.
/// 3. Charge Hit / Dodge: If player dodges, she continues past them for overshoot distance before recovery.
/// 4. Whip Attack: Extended reach whip arc strike (attackRange = 3.0 units).
/// 5. Physics Safety: Dynamic contact distance calculation prevents player pushing.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FemaleSamuraiWhipAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Patrolling,
        Chasing,
        StartCharging,
        ContinueCharging,
        WhipAttacking,
        HitStun,
        Dead
    }

    [Header("State")]
    public State currentState = State.Patrolling;

    [Header("Patrol Settings")]
    public float patrolDistance = 4f;
    public float walkSpeed = 4f;
    public float stopDuration = 1.5f;

    [Header("Combat Settings")]
    public float detectionRange = 12f;
    public float whipAttackRange = 3.0f;
    public float attackCooldown = 1.2f;

    [Header("Charge Attack Settings")]
    public float chargeMinRange = 4.5f;       // Min distance to trigger charge
    public float chargeMaxRange = 12f;        // Max detection range to start charge
    public float chargeSpeed = 14f;           // Max charge speed
    public float startChargeDuration = 0.5f;  // Acceleration phase duration
    public float maxChargeDuration = 1.2f;    // Max time spent continuing charge if player dodges
    public float chargeOvershootDistance = 3.5f;
    public float chargeCooldown = 3.0f;
    public float chargeRecoveryPause = 0.4f;

    [Header("Approach & Safety Settings")]
    public float approachStopRange = 3.5f;
    public float planDecisionInterval = 0.4f;

    [Header("Health & Damage")]
    public int maxHealth = 65;
    [SerializeField] private int currentHealth;
    public int baseDamage = 6;
    public float knockbackForce = 2.5f;
    public float hitStunDuration = 0.25f;

    // Components & References
    private Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private Collider2D bodyCollider;
    private SpriteJuice spriteJuice;
    private SpriteRenderer spriteRenderer;

    // Internal State Timers
    private Vector3 startingPosition;
    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float lastAttackTime = -999f;
    private float lastChargeTime = -999f;
    private string currentAnimState = "";
    private float planCooldownTimer = 0f;

    // Animation Names
    private const string IDLE_STATE = "FemaleSamuraiWhip_Idle";
    private const string WALK_STATE = "FemaleSamuraiWhip_Walk";
    private const string RUN_STATE = "FemaleSamuraiWhip_Run";
    private const string START_CHARGE_STATE = "FemaleSamuraiWhip_StartCharge";
    private const string CONTINUE_CHARGE_STATE = "FemaleSamuraiWhip_ContinueCharge";
    private const string WHIP_ATTACK_STATE = "FemaleSamuraiWhip_WhipAttack";

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

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
        if (currentState == State.Dead || currentState == State.HitStun ||
            currentState == State.StartCharging || currentState == State.ContinueCharging ||
            currentState == State.WhipAttacking)
            return;

        if (player == null) FindPlayer();

        if (player != null && !IsPlayerDead())
        {
            float dist = GetDistanceToPlayer();
            if (dist <= detectionRange)
            {
                if (currentState != State.Chasing)
                {
                    currentState = State.Chasing;
                }
            }
            else if (currentState == State.Chasing)
            {
                currentState = State.Patrolling;
                startingPosition = transform.position;
            }
        }
        else if (currentState == State.Chasing)
        {
            currentState = State.Patrolling;
            startingPosition = transform.position;
        }

        if (currentState == State.Patrolling && isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) isWaiting = false;
        }
    }

    void FixedUpdate()
    {
        if (rb == null || currentState == State.Dead || currentState == State.HitStun ||
            currentState == State.StartCharging || currentState == State.ContinueCharging ||
            currentState == State.WhipAttacking)
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

        rb.linearVelocity = new Vector2(direction * walkSpeed, rb.linearVelocity.y);
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

        float horizontalDist = GetHorizontalDistanceToPlayer();
        float playerDirection = player.position.x > transform.position.x ? 1f : -1f;

        // 1. Whip Attack Range — stop and execute whip arc strike
        if (horizontalDist <= GetEffectiveWhipAttackRange())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            planCooldownTimer = 0f;
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(PerformWhipAttackRoutine());
            }
            else
            {
                PlayAnimation(IDLE_STATE);
                FlipSprite(playerDirection);
            }
            return;
        }

        // 2. Charge Attack Range — trigger start charge with initial acceleration
        if (horizontalDist >= chargeMinRange && horizontalDist <= chargeMaxRange && Time.time >= lastChargeTime + chargeCooldown)
        {
            planCooldownTimer = 0f;
            StartCoroutine(PerformChargeSequenceRoutine(playerDirection));
            return;
        }

        // 3. Approach Stop Range — stop running before physically pushing player
        if (horizontalDist <= approachStopRange)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            PlayAnimation(IDLE_STATE);
            FlipSprite(playerDirection);

            if (planCooldownTimer > 0f)
            {
                planCooldownTimer -= Time.fixedDeltaTime;
                return;
            }

            // Decide maneuver when close
            if (Time.time >= lastChargeTime + chargeCooldown)
            {
                planCooldownTimer = planDecisionInterval;
                StartCoroutine(PerformChargeSequenceRoutine(playerDirection));
            }
            else
            {
                planCooldownTimer = 0.3f;
            }
            return;
        }

        // 4. Normal Chase Run
        planCooldownTimer = 0f;
        rb.linearVelocity = new Vector2(playerDirection * walkSpeed, rb.linearVelocity.y);
        PlayAnimation(RUN_STATE);
        FlipSprite(playerDirection);
    }

    // ── Charge Sequence (Start Charge Acceleration -> Continue Charge -> Whip/Overshoot) ────

    private IEnumerator PerformChargeSequenceRoutine(float targetDirection)
    {
        currentState = State.StartCharging;
        lastChargeTime = Time.time;
        PlayAnimation(START_CHARGE_STATE);
        FlipSprite(targetDirection);

        // Phase 1: Start Charge (Initial Acceleration Curve)
        float elapsedStart = 0f;
        float startDuration = GetAnimationDuration(START_CHARGE_STATE, startChargeDuration);

        while (elapsedStart < startDuration)
        {
            if (currentState == State.Dead || currentState == State.HitStun) yield break;

            // SmoothStep acceleration from 0 to chargeSpeed
            float accelProgress = Mathf.SmoothStep(0f, 1f, elapsedStart / Mathf.Max(0.01f, startDuration));
            float currentSpeed = chargeSpeed * accelProgress;

            rb.linearVelocity = new Vector2(targetDirection * currentSpeed, rb.linearVelocity.y);
            elapsedStart += Time.deltaTime;
            yield return null;
        }

        // Phase 2: Continue Charge (Locked Direction Running Charge)
        currentState = State.ContinueCharging;
        PlayAnimation(CONTINUE_CHARGE_STATE);

        float elapsedContinue = 0f;
        float graceTime = 0.1f;
        bool playerHit = false;

        while (elapsedContinue < maxChargeDuration)
        {
            if (currentState == State.Dead || currentState == State.HitStun) yield break;

            rb.linearVelocity = new Vector2(targetDirection * chargeSpeed, rb.linearVelocity.y);

            // Check if player is reached for Whip Attack transition
            if (player != null && !IsPlayerDead())
            {
                float currentDist = GetHorizontalDistanceToPlayer();
                if (currentDist <= GetEffectiveWhipAttackRange())
                {
                    playerHit = true;
                    break;
                }
            }

            // Wall safety raycast
            if (elapsedContinue > graceTime)
            {
                Vector2 rayOrigin = (Vector2)transform.position + new Vector2(targetDirection * 0.3f, 0f);
                RaycastHit2D wallHit = Physics2D.Raycast(rayOrigin, Vector2.right * targetDirection, 0.4f, LayerMask.GetMask("Default"));
                if (wallHit.collider != null && !wallHit.collider.isTrigger && wallHit.collider.gameObject != gameObject)
                {
                    break; // Hit wall, stop charge early
                }
            }

            elapsedContinue += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Phase 3: Transition to Whip Attack if in range, otherwise Recovery Pause
        if (playerHit && currentState == State.ContinueCharging)
        {
            yield return StartCoroutine(PerformWhipAttackRoutine());
        }
        else if (currentState == State.ContinueCharging)
        {
            // Player dodged — overshoot recovery pause
            PlayAnimation(IDLE_STATE);
            currentAnimState = ""; // Force re-trigger next animation
            yield return new WaitForSeconds(chargeRecoveryPause);

            if (currentState == State.ContinueCharging)
            {
                currentState = State.Chasing;
            }
        }
    }

    // ── Whip Attack Routine ───────────────────────────────────────

    private IEnumerator PerformWhipAttackRoutine()
    {
        currentState = State.WhipAttacking;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        float playerDir = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        FlipSprite(playerDir);

        PlayAnimation(WHIP_ATTACK_STATE);

        yield return null;

        float animLength = GetAnimationDuration(WHIP_ATTACK_STATE, 1.2f);
        float damageHitTime = animLength * 0.48f; // Whip arc impact centered at ~48% clip time

        yield return new WaitForSeconds(damageHitTime);

        // Apply whip strike damage in the whip arc
        if (player != null && !IsPlayerDead())
        {
            float hitRange = GetEffectiveWhipAttackRange() + 0.8f;
            if (GetHorizontalDistanceToPlayer() <= hitRange)
            {
                Health playerHealth = player.GetComponent<Health>();
                if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(baseDamage);
                }
            }
        }

        float remaining = animLength - damageHitTime;
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        lastAttackTime = Time.time;
        PlayAnimation(IDLE_STATE);
        yield return new WaitForSeconds(0.2f);

        if (currentState == State.WhipAttacking)
        {
            currentState = State.Chasing;
        }
    }

    // ── Physics & Utility Methods ─────────────────────────────────

    private void FindPlayer()
    {
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) player = pObj.transform;
        else
        {
            move pMove = Object.FindFirstObjectByType<move>();
            if (pMove != null) player = pMove.transform;
        }
    }

    private bool IsPlayerDead()
    {
        if (player == null) return true;
        Health pHealth = player.GetComponent<Health>();
        if (pHealth == null) pHealth = player.GetComponentInParent<Health>();
        return pHealth != null && pHealth.CurrentHealth <= 0;
    }

    private void PlayAnimation(string stateName)
    {
        if (anim == null) return;
        if (currentAnimState != stateName)
        {
            anim.Play(stateName);
            currentAnimState = stateName;
        }
    }

    private void FlipSprite(float dirX)
    {
        if (dirX > 0f)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else if (dirX < 0f)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    public float GetEffectiveWhipAttackRange()
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
        return Mathf.Max(whipAttackRange, touchDistance + 0.2f);
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
        return Vector2.Distance(enemyCenter, playerCenter);
    }

    public float GetHorizontalDistanceToPlayer()
    {
        if (player == null) return 999f;
        float enemyX = bodyCollider != null ? bodyCollider.bounds.center.x : transform.position.x;
        float playerX = player.position.x;
        return Mathf.Abs(enemyX - playerX);
    }

    private float GetAnimationDuration(string stateName, float fallback)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return fallback;
        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == stateName && clip.length > 0.01f)
                return clip.length;
        }
        return fallback;
    }

    // ── IDamageable Interface ──────────────────────────────────────

    public void TakeDamage(int damageAmount)
    {
        if (currentState == State.Dead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0);

        Vector2 hitDir = player != null ? (Vector2)(transform.position - player.position).normalized : Vector2.right;
        StopAllCoroutines();

        if (spriteJuice != null)
        {
            spriteJuice.PlayHitReaction(hitDir, knockbackForce);
        }

        HitFeedbackManager.TriggerHitFeedback(transform, transform.position, damageAmount, damageAmount >= 25, EnemyHitType.PhysicalMelee);

        if (currentHealth <= 0)
        {
            StartCoroutine(DieRoutine());
        }
        else
        {
            StartCoroutine(HitStunRoutine());
        }
    }

    private IEnumerator HitStunRoutine()
    {
        currentState = State.HitStun;
        yield return new WaitForSeconds(hitStunDuration);
        if (currentState == State.HitStun)
        {
            currentState = State.Chasing;
        }
    }

    private IEnumerator DieRoutine()
    {
        currentState = State.Dead;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        PlayAnimation(IDLE_STATE);

        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }
}
