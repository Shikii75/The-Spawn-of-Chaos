using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FemaleStrawhatAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Patrolling,
        Chasing,
        Dashing,
        Attacking,
        HitStun,
        Dead
    }

    [Header("State")]
    public State currentState = State.Patrolling;

    [Header("Patrol Settings")]
    public float patrolDistance = 4f;
    public float moveSpeed = 3f;
    public float stopDuration = 1.5f;

    [Header("Combat Settings")]
    public float detectionRange = 12f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1.2f;
    [Range(0f, 1f)]
    public float comboStrikeChance = 0.5f;

    [Header("Phantom Dash Settings (Hornet-Style)")]
    public float dashRange = 8f;             // Min distance to trigger dash
    public float dashMaxRange = 12f;         // Max detection range for dash
    public float phantomDashSpeed = 22f;     // Very fast — feels like a teleport
    public float phantomDashDuration = 0.55f;// Covers ~12 units of ground
    public float dashCooldown = 3f;
    public float dashRecoveryPause = 0.4f;   // Punish window after dash
    public float dashOvershootDistance = 3.5f;// How far past the player she ends up

    [Header("Backdash Settings")]
    public float backdashSpeed = 9f;
    public float backdashDuration = 0.25f;
    public float backdashCooldown = 2.5f;
    [Range(0f, 1f)]
    public float postAttackBackdashChance = 0.5f;
    [Range(0f, 1f)]
    public float feintRedashChance = 0.25f;  // Chance to backdash then immediately re-dash attack

    [Header("Approach Settings")]
    [Tooltip("How close she walks/runs to the player before stopping to plan a move (instead of pushing the player).")]
    public float approachStopRange = 3.5f;

    [Header("Circling Behavior")]
    public float circleRangeMin = 3f;
    public float circleRangeMax = 5f;
    public float circleSpeed = 3.5f;
    public float circleDuration = 1.0f;

    [Header("Health & Damage")]
    public int maxHealth = 50;
    [SerializeField] private int currentHealth;
    public int baseDamage = 16; // 16% damage (Dojo clan standard)
    public float knockbackForce = 2.5f;
    public float hitStunDuration = 0.2f;
    public float deathLaunchForce = 4f;

    // References
    private Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private Collider2D bodyCollider;
    private SpriteJuice spriteJuice;
    private SpriteRenderer spriteRenderer;

    // Internal States
    private Vector3 startingPosition;
    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float lastAttackTime = -999f;
    private float lastDashTime = -999f;
    private float lastBackdashTime = -999f;
    private string currentAnimState = "";
    private bool isCircling = false;
    private float circleTimer = 0f;
    private float circleDirection = 1f;
    private readonly List<Collider2D> ignoredDashColliders = new List<Collider2D>();
    private float planCooldownTimer = 0f;          // Prevents re-rolling maneuver decisions every frame

    [Header("Approach Planning")]
    [Tooltip("How long (seconds) she idles before committing to a maneuver when inside the approach stop range.")]
    public float planDecisionInterval = 0.4f;

    // Animator State Names
    private const string IDLE_STATE = "FemaleStrawhatIdle";
    private const string RUN_STATE = "FSrun";
    private const string DASH_STATE = "FSdash";
    private const string DOUBLE_HIT_STATE = "FSdouble_hit";
    private const string COMBO_STATE = "FScombo";

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
            spriteRenderer.color = new Color(0.16f, 0.2f, 0.24f, 1f);
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
        if (currentState == State.Dead || currentState == State.HitStun || currentState == State.Dashing || currentState == State.Attacking)
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
                    isCircling = false;
                }
            }
            else if (currentState == State.Chasing)
            {
                currentState = State.Patrolling;
                startingPosition = transform.position;
                isCircling = false;
            }
        }
        else if (currentState == State.Chasing)
        {
            currentState = State.Patrolling;
            startingPosition = transform.position;
            isCircling = false;
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
        if (rb == null || currentState == State.Attacking || currentState == State.Dead || currentState == State.HitStun || currentState == State.Dashing)
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
        PlayAnimation(RUN_STATE);
        FlipSprite(direction);
    }

    private void UpdateChasing()
    {
        if (player == null || IsPlayerDead())
        {
            currentState = State.Patrolling;
            startingPosition = transform.position;
            isCircling = false;
            return;
        }

        float horizontalDist = GetHorizontalDistanceToPlayer();
        float playerDirection = player.position.x > transform.position.x ? 1f : -1f;

        // 1. Melee Attack Range — stop and strike
        if (horizontalDist <= GetEffectiveAttackRange())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            isCircling = false;
            planCooldownTimer = 0f;
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

        // 2. Approach Stop Range — stop running, go idle, then plan ONE maneuver
        //    The planCooldownTimer prevents spamming coroutines every FixedUpdate frame.
        if (horizontalDist <= approachStopRange)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            isCircling = false;
            PlayAnimation(IDLE_STATE);
            FlipSprite(playerDirection);

            // Tick down the planning cooldown
            if (planCooldownTimer > 0f)
            {
                planCooldownTimer -= Time.fixedDeltaTime;
                return;
            }

            // Ready to plan — pick ONE maneuver and set a cooldown so we don't re-roll next frame
            if (Time.time >= lastDashTime + dashCooldown)
            {
                planCooldownTimer = planDecisionInterval;

                float choice = Random.value;
                if (choice < 0.35f && Time.time >= lastBackdashTime + backdashCooldown)
                {
                    // Feint Re-dash (backdash then forward dash attack)
                    StartCoroutine(PerformFeintRedashRoutine(playerDirection));
                }
                else if (choice < 0.75f)
                {
                    // Phantom Dash directly at the player
                    StartCoroutine(PerformPhantomDashRoutine(playerDirection));
                }
                else if (Time.time >= lastBackdashTime + backdashCooldown)
                {
                    // Defensive backdash to create space
                    StartCoroutine(PerformBackdashRoutine(-playerDirection));
                }
                else
                {
                    // All maneuvers on cooldown — just wait idle
                    planCooldownTimer = 0.3f;
                }
            }
            else
            {
                // Dash still on cooldown — idle-wait with a short timer to avoid re-checking every frame
                planCooldownTimer = 0.2f;
            }
            return;
        }

        // 3. Phantom Dash — Hornet-style long lunge from far away
        if (horizontalDist >= dashRange && horizontalDist <= dashMaxRange && Time.time >= lastDashTime + dashCooldown)
        {
            isCircling = false;
            planCooldownTimer = 0f;
            StartCoroutine(PerformPhantomDashRoutine(playerDirection));
            return;
        }

        // 4. Circling Behavior — pacing back and forth at mid-range before committing
        if (horizontalDist >= circleRangeMin && horizontalDist <= circleRangeMax
            && Time.time >= lastAttackTime + attackCooldown * 0.5f)
        {
            if (!isCircling)
            {
                isCircling = true;
                circleTimer = circleDuration;
                circleDirection = Random.value > 0.5f ? 1f : -1f;
            }

            if (circleTimer > 0f)
            {
                circleTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = new Vector2(circleDirection * circleSpeed, rb.linearVelocity.y);
                PlayAnimation(RUN_STATE);
                FlipSprite(playerDirection);
                return;
            }
            else
            {
                isCircling = false;
            }
        }

        // 5. Normal Chase Run — reset planning timer when running
        planCooldownTimer = 0f;
        rb.linearVelocity = new Vector2(playerDirection * moveSpeed, rb.linearVelocity.y);
        PlayAnimation(RUN_STATE);
        FlipSprite(playerDirection);
    }

    // ── Phantom Dash (Hollow Knight Hornet Lunge) ─────────────────

    private IEnumerator PerformPhantomDashRoutine(float direction)
    {
        currentState = State.Dashing;
        lastDashTime = Time.time;
        PlayAnimation(DASH_STATE);
        FlipSprite(direction);

        // Match the burst to the actual animation instead of a disconnected timer.
        // The destination is chosen once, so a moving player cannot pull the dash around.
        float dashDuration = GetAnimationDuration(DASH_STATE, phantomDashDuration);
        float playerDistance = GetHorizontalDistanceToPlayer();
        float dashDistance = Mathf.Clamp(playerDistance + dashOvershootDistance * 0.35f, 4.5f, 8.5f);
        float dashSpeed = dashDistance / Mathf.Max(dashDuration, 0.05f);

        // Disable physical collision with player so we pass through
        Collider2D[] playerColliders = player != null ? player.GetComponentsInChildren<Collider2D>() : null;
        if (playerColliders != null && bodyCollider != null)
        {
            foreach (var pCol in playerColliders)
            {
                if (pCol != null && !pCol.isTrigger)
                {
                    Physics2D.IgnoreCollision(bodyCollider, pCol, true);
                    ignoredDashColliders.Add(pCol);
                }
            }
        }

        bool hasDealtDamage = false;

        try
        {
            float elapsed = 0f;
            float graceTime = 0.08f; // Brief grace before wall checks

            while (elapsed < dashDuration)
            {
                if (currentState == State.Dead || currentState == State.HitStun) yield break;

                rb.linearVelocity = new Vector2(direction * dashSpeed, rb.linearVelocity.y);

                // Slash damage on pass-through (AABB overlap check)
                if (!hasDealtDamage && player != null && playerColliders != null)
                {
                    bool overlapping = false;
                    foreach (var pCol in playerColliders)
                    {
                        if (pCol != null && bodyCollider != null && bodyCollider.bounds.Intersects(pCol.bounds))
                        {
                            overlapping = true;
                            break;
                        }
                    }

                    if (overlapping)
                    {
                        Health playerHealth = player.GetComponent<Health>();
                        if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
                        if (playerHealth != null)
                        {
                            int dashDmg = Mathf.RoundToInt(baseDamage * 1.5f);
                            playerHealth.TakeDamage(dashDmg);
                            hasDealtDamage = true;
                        }
                    }
                }

                // Wall safety check (after grace period)
                if (elapsed > graceTime)
                {
                    Vector2 rayOrigin = (Vector2)transform.position + new Vector2(direction * 0.3f, 0f);
                    RaycastHit2D wallHit = Physics2D.Raycast(rayOrigin, Vector2.right * direction, 0.4f,
                        LayerMask.GetMask("Default"));
                    if (wallHit.collider != null && !wallHit.collider.isTrigger && wallHit.collider.gameObject != gameObject
                        && (player == null || wallHit.collider.gameObject != player.gameObject))
                    {
                        break; // Hit a wall, stop early
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            RestoreDashCollisions();
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        if (currentState == State.Dashing)
        {
            // Recovery pause — player's punish window
            PlayAnimation(IDLE_STATE);
            currentAnimState = ""; // Force re-play

            yield return new WaitForSeconds(dashRecoveryPause);

            if (currentState == State.Dashing)
            {
                currentState = State.Chasing;
            }
        }
    }

    private void RestoreDashCollisions()
    {
        if (bodyCollider != null)
        {
            foreach (Collider2D playerCollider in ignoredDashColliders)
            {
                if (playerCollider != null)
                    Physics2D.IgnoreCollision(bodyCollider, playerCollider, false);
            }
        }
        ignoredDashColliders.Clear();
    }

    private float GetAnimationDuration(string stateName, float fallback)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
            return fallback;

        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == stateName && clip.length > 0.01f)
                return clip.length;
        }
        return fallback;
    }

    // ── Backdash (defensive disengage) ────────────────────────────

    private IEnumerator PerformBackdashRoutine(float awayDirection)
    {
        currentState = State.Dashing;
        lastBackdashTime = Time.time;
        PlayAnimation(DASH_STATE);

        float elapsed = 0f;
        while (elapsed < backdashDuration)
        {
            if (currentState == State.Dead || currentState == State.HitStun) yield break;

            rb.linearVelocity = new Vector2(awayDirection * backdashSpeed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        PlayAnimation(IDLE_STATE);
        currentAnimState = "";

        yield return new WaitForSeconds(0.15f);

        if (currentState == State.Dashing)
        {
            currentState = State.Chasing;
        }
    }

    // ── Feint: Backdash then immediately Re-Dash Attack ───────────

    private IEnumerator PerformFeintRedashRoutine(float playerDirection)
    {
        // Step 1: Quick backdash away
        float awayDir = -playerDirection;
        yield return StartCoroutine(PerformBackdashRoutine(awayDir));

        // Step 2: Brief pause (the "feint" — player thinks she's retreating)
        yield return new WaitForSeconds(0.15f);

        // Step 3: Immediately re-dash forward for a surprise attack
        if (currentState != State.Dead && currentState != State.HitStun && player != null)
        {
            float newDir = player.position.x > transform.position.x ? 1f : -1f;
            yield return StartCoroutine(PerformPhantomDashRoutine(newDir));
        }
    }

    // ── Melee Attack ──────────────────────────────────────────────

    private IEnumerator PerformAttackRoutine()
    {
        currentState = State.Attacking;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        float playerDirection = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        FlipSprite(playerDirection);

        // Weighted choice of attack
        bool useCombo = Random.value < comboStrikeChance;
        string attackState = useCombo ? COMBO_STATE : DOUBLE_HIT_STATE;

        PlayAnimation(attackState);

        // Wait one frame to read animation duration
        yield return null;

        float animLength = 0.8f;
        if (anim != null)
        {
            animLength = anim.GetCurrentAnimatorStateInfo(0).length;
        }

        // Damage timings:
        // FSdouble_hit: two quick swings at 0.25s and 0.45s
        // FScombo: quick combo at 0.3s and 0.5s
        float firstHitTime = useCombo ? 0.3f : 0.25f;
        float secondHitTime = useCombo ? 0.5f : 0.45f;

        // Wait for first hit
        yield return new WaitForSeconds(firstHitTime);
        ApplyDamageToPlayer(attackState, useCombo ? baseDamage : Mathf.RoundToInt(baseDamage * 0.8f));

        // Wait for second hit
        yield return new WaitForSeconds(secondHitTime - firstHitTime);
        ApplyDamageToPlayer(attackState, useCombo ? Mathf.RoundToInt(baseDamage * 1.2f) : baseDamage);

        // Wait for the remainder of the animation
        float elapsedSoFar = secondHitTime;
        float remainingTime = animLength - elapsedSoFar;
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        lastAttackTime = Time.time;
        PlayAnimation(IDLE_STATE);

        // Post-attack decision: backdash, feint-redash, or re-engage
        if (player != null)
        {
            float roll = Random.value;
            float dir = player.position.x > transform.position.x ? 1f : -1f;

            if (roll < feintRedashChance && Time.time >= lastBackdashTime + backdashCooldown)
            {
                // Feint: backdash then immediately re-dash for surprise attack
                yield return StartCoroutine(PerformFeintRedashRoutine(dir));
            }
            else if (roll < feintRedashChance + postAttackBackdashChance && Time.time >= lastBackdashTime + backdashCooldown)
            {
                // Normal defensive backdash
                yield return StartCoroutine(PerformBackdashRoutine(-dir));
            }
            else
            {
                currentState = State.Chasing;
            }
        }
        else
        {
            currentState = State.Chasing;
        }
    }

    private void ApplyDamageToPlayer(string attackState, int dmg)
    {
        if (player == null || IsPlayerDead()) return;

        float finalRange = GetEffectiveAttackRange() + 0.6f;
        if (GetHorizontalDistanceToPlayer() <= finalRange)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(dmg);
            }
        }
    }

    // ── Utility ───────────────────────────────────────────────────

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
        if (anim == null) return;

        bool stateChanged = currentAnimState != stateName;
        if (stateChanged)
        {
            anim.Play(stateName);
            currentAnimState = stateName;

            if (stateName == RUN_STATE)
            {
                SetAnimMoving(true);
            }
            else
            {
                SetAnimMoving(false);
            }
        }

        // Fire animator triggers to keep the Animator state machine updated
        if (stateChanged && stateName == DASH_STATE)
        {
            if (AnimatorHasParameter("dash", AnimatorControllerParameterType.Trigger))
            {
                anim.SetTrigger("dash");
            }
        }
        else if (stateChanged && stateName == COMBO_STATE)
        {
            if (AnimatorHasParameter("Attack1", AnimatorControllerParameterType.Trigger))
            {
                anim.SetTrigger("Attack1");
            }
        }
        else if (stateChanged && stateName == DOUBLE_HIT_STATE)
        {
            if (AnimatorHasParameter("attack", AnimatorControllerParameterType.Trigger))
            {
                anim.SetTrigger("attack");
            }
        }
    }

    private void SetAnimMoving(bool moving)
    {
        if (anim != null && AnimatorHasParameter("isMoving", AnimatorControllerParameterType.Bool))
        {
            anim.SetBool("isMoving", moving);
        }
    }

    private bool AnimatorHasParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
            {
                return true;
            }
        }
        return false;
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

    // ── IDamageable ──────────────────────────────────────────────────

    public void TakeDamage(int damageAmount)
    {
        if (currentState == State.Dead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0);

        // Interrupt everything — rewards the player for landing hits
        RestoreDashCollisions();
        Vector2 hitDir = player != null ? (Vector2)(transform.position - player.position).normalized : Vector2.right;
        SpawnHitParticles(hitDir);
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

    private void SpawnHitParticles(Vector2 hitDirection)
    {
        const int sparkCount = 7;
        Vector3 origin = bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        int sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 1;

        for (int i = 0; i < sparkCount; i++)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, Random.Range(-70f, 70f)) * hitDirection;
            Vector2 velocity = direction.normalized * Random.Range(2.2f, 4.4f) + Vector2.up * Random.Range(0.4f, 1.6f);
            Color color = Color.Lerp(new Color(1f, 0.72f, 0.35f), new Color(1f, 0.25f, 0.18f), Random.value);
            HitSpark.Create(origin, velocity, color, sortingOrder);
        }
    }

    private IEnumerator HitStunRoutine()
    {
        currentState = State.HitStun;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        PlayAnimation(IDLE_STATE);
        currentAnimState = ""; // Force re-play on next state change

        yield return new WaitForSeconds(hitStunDuration);

        if (currentState == State.HitStun)
        {
            currentState = State.Chasing;
        }
    }

    private IEnumerator DieRoutine()
    {
        currentState = State.Dead;

        // Disable collider immediately
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
            rb.AddForce(new Vector2(launchDir.x * deathLaunchForce, deathLaunchForce * 1.5f), ForceMode2D.Impulse);
        }

        // Extended death with sprite fade
        float fadeDelay = 1.5f;
        float elapsed = 0f;
        while (elapsed < fadeDelay)
        {
            elapsed += Time.unscaledDeltaTime;

            // Fade sprite alpha in the last 0.6s
            if (spriteRenderer != null && elapsed > fadeDelay - 0.6f)
            {
                float fadeT = (elapsed - (fadeDelay - 0.6f)) / 0.6f;
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                spriteRenderer.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
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

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, dashRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(center, dashMaxRange);
    }
}

/// <summary>Small code-driven impact sparks; no prefab or particle asset required.</summary>
public class HitSpark : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;
    private float lifetime;
    private float age;

    public static void Create(Vector3 position, Vector2 initialVelocity, Color color, int sortingOrder)
    {
        GameObject spark = new GameObject("HitSpark");
        spark.transform.position = position + (Vector3)Random.insideUnitCircle * 0.12f;
        spark.transform.localScale = Vector3.one * Random.Range(0.045f, 0.09f);

        SpriteRenderer renderer = spark.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        HitSpark behaviour = spark.AddComponent<HitSpark>();
        behaviour.spriteRenderer = renderer;
        behaviour.velocity = initialVelocity;
        behaviour.lifetime = Random.Range(0.18f, 0.32f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        velocity += Physics2D.gravity * 0.35f * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        float remaining = 1f - age / lifetime;
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(remaining) * 0.9f;
            spriteRenderer.color = color;
        }

        if (age >= lifetime)
            Destroy(gameObject);
    }
}
