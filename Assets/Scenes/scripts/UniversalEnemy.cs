using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class UniversalEnemy : MonoBehaviour, IDamageable
{
    public enum MobBehavior
    {
        SimpleWalkTouchDamage,  // Walks back/forth, contact damage, 1 movement animation
        PatrolChaseAttack,      // Patrols, chases & attacks when player spotted, 3-4 anims
        PatrolIdleWaitAttack,   // Patrols with wait times, has multiple attacks (heavy double damage)
        FlyingChaseAttack       // Flying movement, ignores gravity, dash attacks
    }

    public enum EnemyState
    {
        IdlePatrolling,
        Chasing,
        DashingForward,
        DashingAway,
        Attacking,
        HitStun,
        Dead
    }

    [Header("FSM State")]
    public EnemyState currentState = EnemyState.IdlePatrolling;

    [Header("Behavior Settings")]
    [Tooltip("Choose the AI archetype for this mob.")]
    public MobBehavior behavior = MobBehavior.PatrolChaseAttack;

    [Tooltip("If checked, the mob stands still guarding its starting position until it spots the player (perfect for Dojo/Pantheon rooms).")]
    public bool standStillUntilSpotted = false;

    [Header("Movement & Patrol")]
    public float moveSpeed = 3f;
    public float flySpeed = 2.5f;
    public Transform leftPatrolPoint;
    public Transform rightPatrolPoint;
    [Tooltip("Time (seconds) Patrol Mobs 2 wait at each patrol point before turning.")]
    public float waitTimeAtPoints = 1f;

    [Header("Edge & Wall Detection (Ground Mobs)")]
    [Tooltip("If checked, walking mobs turn around before walking off ledges.")]
    public bool avoidLedges = true;
    public Transform ledgeCheckOrigin;
    public LayerMask groundLayer;

    [Header("Sensors & Combat Ranges")]
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1.5f;

    [Header("Damage & Health Config")]
    public int maxHealth = 30;
    public int baseDamage = 4;
    [Tooltip("Cooldown between contact damage hits for simple touch-damage mobs.")]
    public float contactDamageCooldown = 1f;
    [Tooltip("How much force is applied to push the enemy back when hit.")]
    public float knockbackForce = 2f;

    [Header("Flying Dash Attack Settings")]
    [Tooltip("The speed multiplier during a flying dash attack.")]
    public float flyingDashMultiplier = 2.5f;
    public float flyingDashDuration = 0.4f;

    [Header("Multi-Attack Config (Patrol Mobs 2)")]
    [Tooltip("Number of attack variants this mob has (up to 3).")]
    [Range(1, 3)]
    public int attackVariantCount = 1;
    [Tooltip("Chance (0 to 1) that an attack triggers a Heavy Attack dealing double damage.")]
    [Range(0f, 1f)]
    public float heavyAttackChance = 0.25f;

    [Header("Ground Dash Attack Settings")]
    [Tooltip("Minimum distance to player to trigger a dash.")]
    public float groundDashMinRange = 5f;
    [Tooltip("Maximum distance to player to trigger a dash.")]
    public float groundDashMaxRange = 10f;
    [Tooltip("Speed multiplier during a dash.")]
    public float groundDashSpeedMultiplier = 2.2f;
    [Tooltip("Duration of the ground dash in seconds.")]
    public float groundDashDuration = 0.68f;
    [Tooltip("Cooldown between dashes.")]
    public float groundDashCooldown = 4f;
    [Tooltip("Closest distance where the mob is allowed to dash toward the player. Inside this, she holds space or dashes away.")]
    public float groundDashApproachMinRange = 4f;

    [Header("Animator State Names")]
    [Tooltip("Animator state name for the ground dash animation. FemaleStrawhat uses FSdash.")]
    public string groundDashStateName = "FSdash";
    [Tooltip("Force the dash state immediately after setting the dash trigger. This prevents idle/run transitions from stealing the dash.")]
    public bool forceGroundDashState = true;

    [Header("Sparring Config")]
    public float sparCloseRange = 2f;
    public float sparOptimalRange = 4f;
    [Tooltip("How long the mob idles after creating space with a backward dash.")]
    public float idleAfterDashAway = 1.1f;
    [Tooltip("Extra idle time after a melee attack before chasing again.")]
    public float idleAfterAttack = 0.35f;
    public float hitStunDuration = 0.25f;

    [Header("Post-Attack Decision Weights (PatrolIdleWaitAttack)")]
    [Tooltip("Chance to backdash away after attacking.")]
    [Range(0f, 1f)]
    public float postAttackBackdashChance = 0.35f;
    [Tooltip("Chance to immediately combo a second attack.")]
    [Range(0f, 1f)]
    public float postAttackComboChance = 0.20f;
    [Tooltip("Chance to slowly walk backward after attacking. Remainder = hold ground.")]
    [Range(0f, 1f)]
    public float postAttackWalkBackChance = 0.15f;
    [Tooltip("Duration of the slow walk-back after attacking.")]
    public float walkBackDuration = 0.5f;
    [Tooltip("Speed multiplier for the walk-back (relative to moveSpeed).")]
    public float walkBackSpeedMultiplier = 0.5f;
    [Tooltip("Idle hold time after choosing to hold ground post-attack.")]
    public float holdGroundIdleTime = 0.8f;

    [Header("Approach Variation (PatrolIdleWaitAttack)")]
    [Tooltip("Chance the mob pauses briefly during approach as a feint.")]
    [Range(0f, 1f)]
    public float idleFeintChance = 0.30f;
    [Tooltip("Minimum feint idle duration.")]
    public float idleFeintMinTime = 0.4f;
    [Tooltip("Maximum feint idle duration.")]
    public float idleFeintMaxTime = 1.0f;

    [Header("References")]
    public Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private SpriteJuice spriteJuice;

    // Internal State Timers
    private int currentHealth;
    private bool movingRight = true;
    private float lastAttackTime;
    private float lastContactDamageTime;
    private float lastGroundDashTime;
    private float lastBackdashTime;
    private float holdPositionUntilTime;
    private float waitTimer;
    private float originalGravity;
    private Vector3 startingPosition;
    private bool isDashing = false; // maintained for any external script compatibility
    private Collider2D bodyCollider;
    private float feintUntilTime;       // approach feint: idle until this time
    private int consecutiveAttacks;     // track combo chain length to prevent infinite combos

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        anim = anim ?? GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        spriteJuice = GetComponent<SpriteJuice>();

        if (anim == null)
        {
            Debug.LogError($"[UniversalEnemy] {name} could not find an Animator component on start! Make sure one is attached.", this);
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        currentHealth = maxHealth;
        startingPosition = transform.position;
        originalGravity = rb != null ? rb.gravityScale : 1f;

        // Initialize timers so mobs can act immediately
        lastGroundDashTime = -groundDashCooldown;
        lastBackdashTime = -groundDashCooldown;
        lastAttackTime = -attackCooldown;

        if (behavior == MobBehavior.FlyingChaseAttack && rb != null)
        {
            rb.gravityScale = 0f;
        }

        FindPlayer();
    }

    void Update()
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.HitStun) return;

        // Don't interrupt active dash or melee strike coroutines
        if (currentState == EnemyState.DashingForward || currentState == EnemyState.DashingAway || currentState == EnemyState.Attacking) return;

        if (player == null)
        {
            FindPlayer();
        }

        // FSM State transitions and checks
        switch (currentState)
        {
            case EnemyState.IdlePatrolling:
                UpdateIdlePatrolling();
                break;
            case EnemyState.Chasing:
                UpdateChasing();
                break;
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

    private void UpdateIdlePatrolling()
    {
        // Spot player check
        if (!IsPlayerDead() && behavior != MobBehavior.SimpleWalkTouchDamage)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRange)
            {
                currentState = EnemyState.Chasing;
                return;
            }
        }

        if (standStillUntilSpotted)
        {
            StopMoving();
            return;
        }

        if (behavior == MobBehavior.FlyingChaseAttack)
        {
            FlyingPatrol();
            return;
        }

        // Ground Patrol
        if (leftPatrolPoint == null || rightPatrolPoint == null)
        {
            StopMoving();
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            StopMoving();
            return;
        }

        // Ledge Avoidance
        if (avoidLedges && ledgeCheckOrigin != null)
        {
            bool groundAhead = Physics2D.Raycast(ledgeCheckOrigin.position, Vector2.down, 0.5f, groundLayer);
            if (!groundAhead)
            {
                TurnAround();
            }
        }

        // Wall Avoidance
        float patrolDirection = movingRight ? 1f : -1f;
        if (IsWallAhead(patrolDirection))
        {
            TurnAround();
        }

        Transform targetPoint = movingRight ? rightPatrolPoint : leftPatrolPoint;
        float direction = targetPoint.position.x > transform.position.x ? 1f : -1f;
        if (!CanMoveOnGround(direction))
        {
            TurnAround();
            StopMoving();
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        }
        FlipSprite(direction);
        SetAnimMoving(true);

        if (Mathf.Abs(transform.position.x - targetPoint.position.x) < 0.2f)
        {
            if (behavior == MobBehavior.PatrolIdleWaitAttack)
            {
                waitTimer = waitTimeAtPoints;
            }
            movingRight = !movingRight;
        }
    }

    private void FlyingPatrol()
    {
        if (leftPatrolPoint == null || rightPatrolPoint == null)
        {
            StopMoving();
            return;
        }

        Transform targetPoint = movingRight ? rightPatrolPoint : leftPatrolPoint;
        Vector2 flyDir = (targetPoint.position - transform.position).normalized;

        if (rb != null)
        {
            rb.linearVelocity = flyDir * flySpeed;
        }
        FlipSprite(flyDir.x);
        SetAnimMoving(true);

        if (Vector2.Distance(transform.position, targetPoint.position) < 0.3f)
        {
            movingRight = !movingRight;
        }
    }

    private void UpdateChasing()
    {
        if (player == null || IsPlayerDead())
        {
            currentState = EnemyState.IdlePatrolling;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        float playerDirection = player.position.x > transform.position.x ? 1f : -1f;

        // Lose target if player gets too far
        if (distanceToPlayer > detectionRange * 1.5f)
        {
            currentState = EnemyState.IdlePatrolling;
            return;
        }

        if (behavior == MobBehavior.FlyingChaseAttack)
        {
            if (distanceToPlayer <= GetEffectiveAttackRange())
            {
                StartCoroutine(FlyingDashAttackRoutine());
            }
            else
            {
                SetAnimMoving(true);
                Vector2 chaseDir = (player.position - transform.position).normalized;
                if (rb != null)
                {
                    rb.linearVelocity = chaseDir * flySpeed;
                }
                FlipSprite(chaseDir.x);
            }
            return;
        }

        // Ground Mob Combat Logic (Sparring and Aggressive states)
        bool isGroundCombat = (behavior == MobBehavior.PatrolIdleWaitAttack || behavior == MobBehavior.PatrolChaseAttack);
        bool dashOffCooldown = Time.time >= lastGroundDashTime + groundDashCooldown;
        bool backdashOffCooldown = Time.time >= lastBackdashTime + (groundDashCooldown * 0.5f);
        bool onAttackCooldown = Time.time < lastAttackTime + attackCooldown;

        if (Time.time < holdPositionUntilTime)
        {
            StopMoving();
            FlipSprite(playerDirection);
            return;
        }

        if (onAttackCooldown)
        {
            // Sparring / backing off / repositioning between attacks
            if (isGroundCombat && distanceToPlayer < sparCloseRange)
            {
                if (backdashOffCooldown && CanMoveOnGround(-playerDirection))
                {
                    StartCoroutine(PerformDashRoutine(dashAway: true));
                    return;
                }

                StopMoving();
                FlipSprite(playerDirection);
                return;
            }

            if (distanceToPlayer < sparOptimalRange)
            {
                // Sparring distance: hold ground and let the player breathe.
                StopMoving();
                FlipSprite(playerDirection);
            }
            else
            {
                // Close the gap to re-enter sparring range
                if (behavior == MobBehavior.PatrolIdleWaitAttack && distanceToPlayer < groundDashApproachMinRange)
                {
                    StopMoving();
                    FlipSprite(playerDirection);
                    return;
                }

                if (rb != null && CanMoveOnGround(playerDirection))
                {
                    rb.linearVelocity = new Vector2(playerDirection * (moveSpeed * 0.75f), rb.linearVelocity.y);
                    SetAnimMoving(true);
                }
                else
                {
                    StopMoving();
                }
                FlipSprite(playerDirection);
            }
        }
        else
        {
            // Aggressive actions — ready to strike
            if (distanceToPlayer <= GetEffectiveAttackRange())
            {
                consecutiveAttacks = 0;
                StartCoroutine(PerformAttackRoutine());
            }
            else if (isGroundCombat &&
                     distanceToPlayer >= Mathf.Max(groundDashMinRange, groundDashApproachMinRange) &&
                     distanceToPlayer <= groundDashMaxRange &&
                     dashOffCooldown)
            {
                StartCoroutine(PerformDashRoutine(dashAway: false));
            }
            else
            {
                // Approach with variation (PatrolIdleWaitAttack gets idle feints)
                if (behavior == MobBehavior.PatrolIdleWaitAttack)
                {
                    // Idle feint: occasionally pause during approach to size up the player
                    if (Time.time < feintUntilTime)
                    {
                        StopMoving();
                        FlipSprite(playerDirection);
                        return;
                    }

                    // Roll for a new feint if we're mid-approach and haven't feinted recently
                    if (feintUntilTime <= 0f && Random.value < idleFeintChance * Time.deltaTime)
                    {
                        float feintDuration = Random.Range(idleFeintMinTime, idleFeintMaxTime);
                        feintUntilTime = Time.time + feintDuration;
                        StopMoving();
                        FlipSprite(playerDirection);
                        return;
                    }

                    // Clear expired feint so it can roll again later
                    if (feintUntilTime > 0f && Time.time >= feintUntilTime)
                    {
                        feintUntilTime = 0f;
                    }
                }

                // Chase aggressively
                if (rb != null && CanMoveOnGround(playerDirection))
                {
                    rb.linearVelocity = new Vector2(playerDirection * moveSpeed, rb.linearVelocity.y);
                    SetAnimMoving(true);
                }
                else
                {
                    StopMoving();
                }
                FlipSprite(playerDirection);
            }
        }
    }

    private IEnumerator PerformDashRoutine(bool dashAway)
    {
        currentState = dashAway ? EnemyState.DashingAway : EnemyState.DashingForward;
        isDashing = true;

        float playerDir = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        float dashDir = dashAway ? -playerDir : playerDir;
        FlipSprite(dashDir);
        PlayGroundDashAnimation();

        float elapsed = 0f;
        float graceTime = dashAway ? 0.15f : 0f; // Allow backdash to start moving before ledge/wall checks

        while (elapsed < groundDashDuration)
        {
            if (player == null || IsPlayerDead()) break;

            if (!dashAway)
            {
                // Cancel forward dash early if we reach attack range
                if (Vector2.Distance(transform.position, player.position) <= GetEffectiveAttackRange())
                {
                    break;
                }
            }

            if (elapsed >= graceTime && !CanMoveOnGround(dashDir))
            {
                break;
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(dashDir * (moveSpeed * groundDashSpeedMultiplier), rb.linearVelocity.y);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopMoving();
        if (dashAway)
        {
            lastBackdashTime = Time.time;
        }
        else
        {
            lastGroundDashTime = Time.time;
        }

        if (dashAway)
        {
            float currentLookDir = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
            FlipSprite(currentLookDir);
        }

        float settleTime = dashAway ? idleAfterDashAway : 0.15f;
        holdPositionUntilTime = dashAway ? Time.time + idleAfterDashAway : holdPositionUntilTime;
        yield return new WaitForSeconds(settleTime);

        isDashing = false;
        currentState = EnemyState.Chasing;
    }

    private void PlayGroundDashAnimation()
    {
        if (anim == null)
        {
            Debug.LogWarning($"[{name}] Animator is null inside PerformDashRoutine!", this);
            return;
        }

        SetAnimMoving(true);
        ResetAnimTriggerIfExists("attack");
        ResetAnimTriggerIfExists("Attack1");
        SetAnimTriggerIfExists("dash");

        if (behavior == MobBehavior.PatrolIdleWaitAttack &&
            forceGroundDashState &&
            !string.IsNullOrEmpty(groundDashStateName))
        {
            anim.Play(groundDashStateName, 0, 0f);
        }
    }

    private IEnumerator PerformAttackRoutine()
    {
        currentState = EnemyState.Attacking;
        StopMoving();

        float dirToPlayer = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        FlipSprite(dirToPlayer);

        int dmg = baseDamage;
        bool isHeavy = false;

        if (behavior == MobBehavior.PatrolIdleWaitAttack)
        {
            isHeavy = Random.value < heavyAttackChance;
            if (isHeavy)
            {
                dmg *= 2;
                if (anim != null) anim.SetTrigger("Attack1"); // FScombo
            }
            else
            {
                if (anim != null) anim.SetTrigger("attack"); // FSdouble_hit
            }
        }
        else
        {
            if (anim != null) anim.SetTrigger("attack");
        }

        // Wait for visual attack strike frame
        yield return new WaitForSeconds(0.25f);

        if (player != null && !IsPlayerDead() && Vector2.Distance(transform.position, player.position) <= GetEffectiveAttackRange() + 0.5f)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(dmg);
                Debug.Log($"{name} hit player for {dmg} damage!");
            }
        }

        yield return new WaitForSeconds(0.2f); // Attack settle time
        lastAttackTime = Time.time;
        consecutiveAttacks++;

        // Weighted post-attack decision for PatrolIdleWaitAttack
        if (behavior == MobBehavior.PatrolIdleWaitAttack && player != null && !IsPlayerDead())
        {
            float roll = Random.value;
            float backdashThreshold = postAttackBackdashChance;
            float comboThreshold = backdashThreshold + postAttackComboChance;
            float walkBackThreshold = comboThreshold + postAttackWalkBackChance;

            if (roll < backdashThreshold)
            {
                // BACKDASH: Dash away, idle, then re-engage
                yield return StartCoroutine(PerformDashRoutine(dashAway: true));
                yield break;
            }
            else if (roll < comboThreshold && consecutiveAttacks < 2 &&
                     Vector2.Distance(transform.position, player.position) <= GetEffectiveAttackRange() + 0.3f)
            {
                // COMBO: Chain a second attack immediately (use opposite variant for variety)
                yield return new WaitForSeconds(0.1f); // Tiny gap between combo hits
                yield return StartCoroutine(PerformComboFollowUp());
                yield break;
            }
            else if (roll < walkBackThreshold)
            {
                // WALK BACK: Slowly retreat then idle
                yield return StartCoroutine(WalkBackRoutine());
                yield break;
            }
            else
            {
                // HOLD GROUND: Stand in place facing the player
                holdPositionUntilTime = Time.time + holdGroundIdleTime;
                currentState = EnemyState.Chasing;
                yield break;
            }
        }

        // Default for non-PatrolIdleWaitAttack ground mobs: simple backdash
        bool isGroundCombat = (behavior == MobBehavior.PatrolChaseAttack);
        if (isGroundCombat && player != null && !IsPlayerDead())
        {
            yield return StartCoroutine(PerformDashRoutine(dashAway: true));
            yield break;
        }

        holdPositionUntilTime = Time.time + idleAfterAttack;
        currentState = EnemyState.Chasing;
    }
    private IEnumerator PerformComboFollowUp()
    {
        // Chain a second attack using the opposite variant for visual variety
        currentState = EnemyState.Attacking;
        StopMoving();

        float dirToPlayer = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        FlipSprite(dirToPlayer);

        int dmg = baseDamage;
        bool useHeavy = Random.value < 0.5f; // 50/50 between attack variants for combo

        if (useHeavy)
        {
            dmg *= 2;
            if (anim != null) anim.SetTrigger("Attack1"); // FScombo
        }
        else
        {
            if (anim != null) anim.SetTrigger("attack"); // FSdouble_hit
        }

        yield return new WaitForSeconds(0.25f); // Strike frame

        if (player != null && !IsPlayerDead() && Vector2.Distance(transform.position, player.position) <= GetEffectiveAttackRange() + 0.5f)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(dmg);
            }
        }

        yield return new WaitForSeconds(0.2f);
        lastAttackTime = Time.time;
        consecutiveAttacks++;

        // After a combo, always hold ground or backdash — never triple-combo
        if (Random.value < 0.5f && player != null && !IsPlayerDead())
        {
            yield return StartCoroutine(PerformDashRoutine(dashAway: true));
        }
        else
        {
            holdPositionUntilTime = Time.time + holdGroundIdleTime;
            currentState = EnemyState.Chasing;
        }
    }

    private IEnumerator WalkBackRoutine()
    {
        currentState = EnemyState.DashingAway; // Prevents Update from interrupting
        StopMoving();

        float playerDir = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        float retreatDir = -playerDir;
        FlipSprite(retreatDir); // Face away briefly while walking back

        float elapsed = 0f;
        while (elapsed < walkBackDuration)
        {
            if (player == null || IsPlayerDead()) break;

            if (!CanMoveOnGround(retreatDir))
            {
                break;
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(retreatDir * (moveSpeed * walkBackSpeedMultiplier), rb.linearVelocity.y);
            }
            SetAnimMoving(true);

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopMoving();

        // Turn back to face the player
        float lookDir = player != null ? (player.position.x > transform.position.x ? 1f : -1f) : 1f;
        FlipSprite(lookDir);

        // Idle briefly after walking back
        holdPositionUntilTime = Time.time + idleAfterDashAway * 0.6f;
        currentState = EnemyState.Chasing;
    }

    private IEnumerator FlyingDashAttackRoutine()
    {
        currentState = EnemyState.DashingForward;
        SetAnimMoving(false);
        if (anim != null) anim.SetTrigger("attack");

        Vector2 dashDir = (player.position - transform.position).normalized;
        if (rb != null)
        {
            rb.linearVelocity = dashDir * (flySpeed * flyingDashMultiplier);
        }
        FlipSprite(dashDir.x);

        float elapsed = 0f;
        while (elapsed < flyingDashDuration)
        {
            if (player == null || IsPlayerDead()) break;
            if (Vector2.Distance(transform.position, player.position) < 0.9f)
            {
                Health pH = player.GetComponent<Health>();
                if (pH == null) pH = player.GetComponentInParent<Health>();
                if (pH != null)
                {
                    pH.TakeDamage(baseDamage);
                    break;
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        StopMoving();
        yield return new WaitForSeconds(0.2f);
        lastAttackTime = Time.time;
        currentState = EnemyState.Chasing;
    }

    public void TakeDamage(int damageAmount)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0);

        StopAllCoroutines();
        isDashing = false;
        consecutiveAttacks = 0;
        feintUntilTime = 0f;

        if (anim != null)
        {
            anim.SetTrigger("hit");
        }

        if (spriteJuice != null)
        {
            Vector2 hitDir = player != null ? (Vector2)(transform.position - player.position).normalized : Vector2.right;
            spriteJuice.PlayHitReaction(hitDir, knockbackForce);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(HitStunRoutine());
        }
    }

    private IEnumerator HitStunRoutine()
    {
        currentState = EnemyState.HitStun;
        StopMoving();
        yield return new WaitForSeconds(hitStunDuration);
        currentState = EnemyState.Chasing;
    }

    private void Die()
    {
        currentState = EnemyState.Dead;
        StopMoving();

        if (anim != null)
        {
            anim.SetTrigger("die");
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        Destroy(gameObject, 2f);
    }

    private void StopMoving()
    {
        if (rb != null)
        {
            rb.linearVelocity = (behavior == MobBehavior.FlyingChaseAttack) ? Vector2.zero : new Vector2(0f, rb.linearVelocity.y);
        }
        SetAnimMoving(false);
    }

    private void SetAnimMoving(bool moving)
    {
        if (AnimatorHasParameter("isMoving", AnimatorControllerParameterType.Bool))
        {
            anim.SetBool("isMoving", moving);
        }
    }

    private void SetAnimTriggerIfExists(string triggerName)
    {
        if (AnimatorHasParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            anim.SetTrigger(triggerName);
        }
    }

    private void ResetAnimTriggerIfExists(string triggerName)
    {
        if (AnimatorHasParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            anim.ResetTrigger(triggerName);
        }
    }

    private bool AnimatorHasParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (anim == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanMoveOnGround(float directionX)
    {
        if (behavior == MobBehavior.FlyingChaseAttack || Mathf.Abs(directionX) < 0.01f)
        {
            return true;
        }

        if (IsWallAhead(directionX))
        {
            return false;
        }

        return !avoidLedges || HasGroundAhead(directionX);
    }

    private bool IsWallAhead(float directionX)
    {
        Vector2 origin = GetFeetCenter() + Vector2.up * 0.2f;
        float distance = GetHalfWidth() + 0.18f;
        RaycastHit2D hit = CastEnvironment(origin, Vector2.right * Mathf.Sign(directionX), distance);
        return hit.collider != null;
    }

    private bool HasGroundAhead(float directionX)
    {
        Vector2 origin = GetFeetCenter() + new Vector2(Mathf.Sign(directionX) * (GetHalfWidth() + 0.12f), 0.08f);
        float distance = GetHalfHeight() + 0.75f;
        RaycastHit2D hit = CastEnvironment(origin, Vector2.down, distance);
        return hit.collider != null;
    }

    private RaycastHit2D CastEnvironment(Vector2 origin, Vector2 direction, float distance)
    {
        if (groundLayer.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, groundLayer);
            if (hit.collider != null &&
                hit.collider.gameObject != gameObject &&
                hit.rigidbody != rb &&
                !hit.collider.isTrigger &&
                hit.collider.GetComponentInParent<IDamageable>() == null)
            {
                return hit;
            }
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null &&
                hit.collider.gameObject != gameObject &&
                hit.rigidbody != rb &&
                !hit.collider.isTrigger &&
                hit.collider.GetComponentInParent<IDamageable>() == null)
            {
                return hit;
            }
        }

        return default;
    }

    private Vector2 GetFeetCenter()
    {
        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            return new Vector2(bounds.center.x, bounds.min.y);
        }

        return transform.position;
    }

    private float GetHalfWidth()
    {
        return bodyCollider != null ? bodyCollider.bounds.extents.x : 0.5f;
    }

    private float GetHalfHeight()
    {
        return bodyCollider != null ? bodyCollider.bounds.extents.y : 0.5f;
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
        // Make sure effective range is slightly larger than touch distance to prevent pushing the player
        return Mathf.Max(attackRange, touchDistance + 0.15f);
    }

    private void TurnAround()
    {
        movingRight = !movingRight;
        FlipSprite(movingRight ? 1f : -1f);
    }

    private void FlipSprite(float directionX)
    {
        if (directionX > 0f)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else if (directionX < 0f)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (currentState == EnemyState.Dead || behavior != MobBehavior.SimpleWalkTouchDamage) return;

        if (collision.gameObject.CompareTag("Player") && Time.time >= lastContactDamageTime + contactDamageCooldown)
        {
            Health pH = collision.gameObject.GetComponent<Health>();
            if (pH == null) pH = collision.gameObject.GetComponentInParent<Health>();
            if (pH != null)
            {
                lastContactDamageTime = Time.time;
                pH.TakeDamage(baseDamage);
                Debug.Log($"{name} dealt contact damage to player.");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
