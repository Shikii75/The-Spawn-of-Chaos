using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;

/// <summary>
/// StrawhatTeleportAI - Controls the female Strawhat assassin mob.
/// 
/// Behavior:
/// 1. Stalking & Patrol:
///    - Patrols ground or idles, facing movement direction.
///    - Sprite frames face Left natively; facing is handled cleanly via spriteRenderer.flipX = right.
///    - Features natural pause hysteresis at patrol turn points to eliminate rapid flipping.
///    - Detects the player within detectionRange.
/// 2. Teleport Engage (Strictly on X-Axis):
///    - Strictly preserves grounded Y coordinate (no airborne teleportation, avoiding fall frames).
///    - When ready to attack, teleports near the player with dark-fantasy dimensional rift VFX,
///      expanding ground shockwave, and shadow ghost trails.
///    - Dynamically stops at touchDistance + buffer so she doesn't push the player (per AGENTS.md).
/// 3. Full-Body Hitbox Attack:
///    - Performs a circular whirlwind katana slash.
///    - Her entire body becomes an active lethal hitbox during the attack swing.
///    - Any player touching/overlapping her body takes damage and knockback.
/// 4. Teleport Retreat & Cooldown (Strictly on X-Axis):
///    - Immediately after the attack, teleports backward along the X axis away from the player.
///    - Wall-check raycasts ensure she never teleports into solid geometry.
///    - Paces/idles at safe distance during the cooldown window, giving the player a punish opportunity.
/// 5. Damage & Drops:
///    - Implements IDamageable, flashes on hit, and drops collectible orbs on defeat.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class StrawhatTeleportAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Idle,
        Patrol,
        TeleportToPlayer,
        Attacking,
        TeleportRetreat,
        CooldownPacing,
        HitStun,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private State currentState = State.Idle;

    [Header("Health & Combat Stats")]
    public int maxHealth = 100;
    public int attackDamage = 18;
    public float playerKnockbackForce = 6.5f;
    public float hitStunDuration = 0.18f;

    [Header("Detection & Ranges")]
    public float detectionRange = 12f;
    [Tooltip("Target distance from player when teleporting in to strike.")]
    public float engageBuffer = 0.35f;
    [Tooltip("Distance she teleports backward after an attack.")]
    public float retreatDistance = 5.0f;

    [Header("Timing & Cooldowns")]
    public float attackCooldown = 2.2f;
    public float teleportWindupDelay = 0.12f;
    public float attackActiveStart = 0.20f;
    public float attackActiveEnd = 0.75f;
    public float attackTotalDuration = 1.15f;
    public float postAttackPause = 0.15f;

    [Header("Patrol & Pacing")]
    public float patrolSpeed = 2.2f;
    public float patrolDistance = 4.0f;
    public float paceSpeed = 1.6f;

    [Header("Teleport VFX & Audio")]
    public Color teleportVFXColor = new Color(0.78f, 0.25f, 0.98f, 0.95f);
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Dodge Mechanics")]
    [Tooltip("Whether the mob will dodge player attacks.")]
    public bool enableDodge = true;
    [Tooltip("How many attacks out of 5 the mob will dodge (e.g. 3 = 3 out of 5 times).")]
    [Range(0, 5)] public int dodgeCountPerFive = 3;
    [Tooltip("Cooldown between dodges to avoid rapid spam from multi-hit ticks.")]
    public float dodgeCooldown = 0.35f;

    [Header("Loot")]
    public int droppedOrbsCount = 2;

    // Component References
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Animator anim;
    private Transform player;
    private Health playerHealth;

    // Internal Combat State
    private int currentHealth;
    private bool isFacingRight = true;
    private float lastAttackTime = -999f;
    private float stateTimer = 0f;
    private Vector3 spawnPosition;
    private float patrolDirection = 1f;
    private bool isPatrolWaiting = false;
    private float patrolWaitTimer = 0f;
    private bool isBodyHitboxActive = false;
    private bool hasHitPlayerThisSwing = false;
    private Coroutine currentActionRoutine;
    private Coroutine flashRoutine;
    private Color originalColor = Color.white;
    private float lastTurnTime = 0f;

    // Dodge State
    private List<bool> dodgeDeck = new List<bool>();
    private int dodgeDeckIndex = 0;
    private bool isDodging = false;
    private float lastDodgeTime = -999f;

    // Hitbox bounds for body-attack overlap check
    public Vector2 attackHitboxSize = new Vector2(3.6f, 3.8f);

    // Animator Parameter / State Hashes
    private static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");

    // Procedural VFX Cache
    private static Sprite riftSlitSprite;
    private static Sprite shockwaveRingSprite;
    private static Sprite sparkDotSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        // Keep localScale.x strictly positive so spriteRenderer.flipX handles facing cleanly
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        currentHealth = maxHealth;
        spawnPosition = transform.position;
        gameObject.tag = "enemy";

        RefillDodgeDeck();
        EnsureVFXSprites();
    }

    void Start()
    {
        FindPlayer();
        currentState = State.Idle;
        stateTimer = Random.Range(0.5f, 1.2f);
    }

    void Update()
    {
        if (currentState == State.Dead) return;

        if (player == null || !player.gameObject.activeInHierarchy)
        {
            FindPlayer();
        }

        // Active body-hitbox overlap check during attack swing
        if (isBodyHitboxActive && !hasHitPlayerThisSwing)
        {
            CheckBodyHitboxCollision();
        }

        // State Machine Execution
        switch (currentState)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.CooldownPacing:
                UpdateCooldownPacing();
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  STATE LOGIC
    // ══════════════════════════════════════════════════════════════════

    private void UpdateIdle()
    {
        SetWalkingAnimation(false);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (CanSeePlayer())
        {
            FacePlayer();
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartAction(ExecuteTeleportStrikeSequence());
                return;
            }
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = State.Patrol;
            stateTimer = Random.Range(2f, 4f);
            patrolDirection = Random.value > 0.5f ? 1f : -1f;
        }
    }

    private void UpdatePatrol()
    {
        // Turn-pause hysteresis state: pauses cleanly to avoid rapid flipping
        if (isPatrolWaiting)
        {
            SetWalkingAnimation(false);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                isPatrolWaiting = false;
                patrolDirection = -patrolDirection;
                lastTurnTime = Time.time;
            }
            return;
        }

        SetWalkingAnimation(true);

        // Turn around if strayed too far from spawn or reached a solid wall
        float distFromSpawn = transform.position.x - spawnPosition.x;
        bool outOfBounds = Mathf.Abs(distFromSpawn) > patrolDistance && Mathf.Sign(distFromSpawn) == Mathf.Sign(patrolDirection);
        bool wallAhead = IsWallAhead(patrolDirection);

        if ((outOfBounds || wallAhead) && Time.time >= lastTurnTime + 0.6f)
        {
            // Enter brief natural pause before turning
            isPatrolWaiting = true;
            patrolWaitTimer = 0.45f;
            return;
        }

        // Patrol horizontal movement
        float moveVel = patrolDirection * patrolSpeed;
        rb.linearVelocity = new Vector2(moveVel, rb.linearVelocity.y);
        SetFacing(patrolDirection > 0f);

        // Player detection
        if (CanSeePlayer())
        {
            FacePlayer();
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartAction(ExecuteTeleportStrikeSequence());
                return;
            }
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = State.Idle;
            stateTimer = Random.Range(1f, 2.5f);
        }
    }

    private void UpdateCooldownPacing()
    {
        FacePlayer();

        // During cooldown, she paces/maintains spacing
        if (player != null)
        {
            float dist = Mathf.Abs(player.position.x - transform.position.x);

            // If player chases her too closely, she backs away slowly to maintain space
            if (dist < 3.0f)
            {
                SetWalkingAnimation(true);
                float retreatDir = transform.position.x < player.position.x ? -1f : 1f;
                if (!IsWallAhead(retreatDir))
                {
                    rb.linearVelocity = new Vector2(retreatDir * paceSpeed, rb.linearVelocity.y);
                }
                else
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
            else
            {
                SetWalkingAnimation(false);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        // Check if cooldown has finished
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            if (CanSeePlayer())
            {
                StartAction(ExecuteTeleportStrikeSequence());
            }
            else
            {
                currentState = State.Idle;
                stateTimer = 1.0f;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  TELEPORT ATTACK SEQUENCE (Sequential Coroutine Chain per AGENTS.md)
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator ExecuteTeleportStrikeSequence()
    {
        currentState = State.TeleportToPlayer;
        SetWalkingAnimation(false);
        rb.linearVelocity = Vector2.zero;

        // --- Step 1: Pre-teleport shadow dissolve windup ---
        FacePlayer();
        Vector3 startPos = transform.position;
        Vector3 targetPos = CalculateEngagePosition();

        // Departure VFX with ghost trail extending toward targetPos
        yield return StartCoroutine(PlayTeleportVFX(startPos, targetPos, true));

        if (teleportWindupDelay > 0f)
        {
            yield return new WaitForSeconds(teleportWindupDelay);
        }

        if (player == null)
        {
            currentState = State.Idle;
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            yield break;
        }

        // Instant X-axis relocation (grounded Y preserved)
        transform.position = targetPos;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();

        // Arrival VFX at targetPos
        yield return StartCoroutine(PlayTeleportVFX(targetPos, targetPos, false));

        // --- Step 3: Execute Body Hitbox Whirlwind Attack ---
        currentState = State.Attacking;
        hasHitPlayerThisSwing = false;
        isBodyHitboxActive = false;

        // Trigger Attack Animation
        if (anim != null)
        {
            anim.SetTrigger(AnimAttack);
        }

        // Wait for attack swing active window
        yield return new WaitForSeconds(attackActiveStart);

        // Turn on body lethal hitbox
        isBodyHitboxActive = true;
        CheckBodyHitboxCollision();

        // Active swing window
        float activeDuration = Mathf.Max(0.1f, attackActiveEnd - attackActiveStart);
        float activeElapsed = 0f;
        while (activeElapsed < activeDuration)
        {
            activeElapsed += Time.deltaTime;
            CheckBodyHitboxCollision();
            yield return null;
        }

        // Turn off hitbox
        isBodyHitboxActive = false;

        // Remainder of attack recovery
        float remainingAnimTime = Mathf.Max(0.05f, attackTotalDuration - attackActiveEnd);
        yield return new WaitForSeconds(remainingAnimTime);

        if (postAttackPause > 0f)
        {
            yield return new WaitForSeconds(postAttackPause);
        }

        // --- Step 4: Teleport Retreat to give space and cool down ---
        currentState = State.TeleportRetreat;
        Vector3 preRetreatPos = transform.position;
        Vector3 retreatPos = CalculateRetreatPosition();

        // Departure VFX with ghost trail extending backward
        yield return StartCoroutine(PlayTeleportVFX(preRetreatPos, retreatPos, true));

        // Move backward strictly on X axis (grounded Y preserved)
        transform.position = retreatPos;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();

        // Arrival VFX at retreatPos
        yield return StartCoroutine(PlayTeleportVFX(retreatPos, retreatPos, false));

        // Enter Cooldown & Pacing window
        lastAttackTime = Time.time;
        currentState = State.CooldownPacing;
    }

    // ══════════════════════════════════════════════════════════════════
    //  POSITIONING & TELEPORT SAFETY (STRICTLY ON X AXIS)
    // ══════════════════════════════════════════════════════════════════

    private Vector3 CalculateEngagePosition()
    {
        if (player == null) return transform.position;

        float playerHalfWidth = 0.6f;
        float myHalfWidth = 0.6f;

        Collider2D playerCol = player.GetComponent<Collider2D>();
        if (playerCol != null) playerHalfWidth = playerCol.bounds.extents.x;
        if (bodyCollider != null) myHalfWidth = bodyCollider.bounds.extents.x;

        // Dynamic attack touch range per AGENTS.md
        float effectiveDist = playerHalfWidth + myHalfWidth + engageBuffer;

        // Position on the side of the player facing towards the player
        float side = (player.position.x > transform.position.x) ? -1f : 1f;
        float targetX = player.position.x + (side * effectiveDist);

        // Keep strictly on current grounded Y level (no vertical teleport / no fall animation)
        float currentGroundY = transform.position.y;

        // Check if there is a wall between mob and targetX
        float travelDist = targetX - transform.position.x;
        if (Mathf.Abs(travelDist) > 0.1f)
        {
            float dir = Mathf.Sign(travelDist);
            float dist = Mathf.Abs(travelDist);
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.4f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(dir, 0f), dist, LayerMask.GetMask("Ground", "Terrain", "Platform", "Default"));
            foreach (var h in hits)
            {
                if (IsObstacleIgnored(h.collider)) continue;
                targetX = transform.position.x + dir * Mathf.Max(0.5f, h.distance - (myHalfWidth + 0.2f));
                break;
            }
        }

        return new Vector3(targetX, currentGroundY, transform.position.z);
    }

    private Vector3 CalculateRetreatPosition()
    {
        if (player == null) return transform.position;

        // Retreat strictly along X axis away from player
        float retreatDir = (transform.position.x < player.position.x) ? -1f : 1f;
        float desiredDistance = retreatDistance;
        float myHalfWidth = (bodyCollider != null) ? bodyCollider.bounds.extents.x : 0.6f;

        // Obstacle / wall raycast in retreat direction
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.4f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(retreatDir, 0f), desiredDistance + myHalfWidth, LayerMask.GetMask("Ground", "Terrain", "Platform", "Default"));
        foreach (var h in hits)
        {
            if (IsObstacleIgnored(h.collider)) continue;
            desiredDistance = Mathf.Max(1.0f, h.distance - (myHalfWidth + 0.3f));
            break;
        }

        float targetX = transform.position.x + (retreatDir * desiredDistance);
        // Strictly keep the current grounded Y level (no vertical teleport / no fall animation)
        return new Vector3(targetX, transform.position.y, transform.position.z);
    }

    private bool IsWallAhead(float direction)
    {
        float myExtentsX = (bodyCollider != null) ? bodyCollider.bounds.extents.x : 0.8f;
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.4f + new Vector2(direction * (myExtentsX + 0.15f), 0f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(direction, 0f), 0.65f, LayerMask.GetMask("Ground", "Terrain", "Platform", "Default"));
        foreach (var hit in hits)
        {
            if (!IsObstacleIgnored(hit.collider))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsObstacleIgnored(Collider2D col)
    {
        if (col == null || col.isTrigger) return true;
        if (col.gameObject == gameObject || col.transform.IsChildOf(transform)) return true;
        if (col.CompareTag("Player") || col.CompareTag("enemy")) return true;
        if (col.name.Contains("Orb") || col.name.Contains("Loot") || col.name.Contains("Spawn") || col.name.Contains("Trigger")) return true;
        if (col.GetComponent<UniversalEnemy>() != null || col.GetComponentInParent<UniversalEnemy>() != null) return true;
        if (col.GetComponent<IDamageable>() != null) return true;
        return false;
    }

    // ══════════════════════════════════════════════════════════════════
    //  FULL-BODY HITBOX ATTACK COLLISION
    // ══════════════════════════════════════════════════════════════════

    private void CheckBodyHitboxCollision()
    {
        if (hasHitPlayerThisSwing) return;

        Vector2 center = (bodyCollider != null) ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, attackHitboxSize, 0f);

        foreach (var hit in hits)
        {
            if (hit == null || hit == bodyCollider) continue;

            if (hit.CompareTag("Player") || hit.GetComponent<Health>() != null)
            {
                Health h = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
                if (h != null && hit.CompareTag("Player"))
                {
                    hasHitPlayerThisSwing = true;
                    h.TakeDamage(attackDamage);

                    // Apply physical knockback to player
                    Rigidbody2D playerRb = hit.GetComponent<Rigidbody2D>() ?? hit.GetComponentInParent<Rigidbody2D>();
                    if (playerRb != null)
                    {
                        float knockDir = hit.transform.position.x >= transform.position.x ? 1f : -1f;
                        playerRb.linearVelocity = new Vector2(knockDir * playerKnockbackForce, playerKnockbackForce * 0.6f);
                    }
                    break;
                }
            }
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (isBodyHitboxActive && !hasHitPlayerThisSwing)
        {
            if (other.CompareTag("Player"))
            {
                CheckBodyHitboxCollision();
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DODGE MECHANICS (3/5 Teleport Retreat Evasion)
    // ══════════════════════════════════════════════════════════════════

    private void RefillDodgeDeck()
    {
        dodgeDeck.Clear();
        for (int i = 0; i < 5; i++)
        {
            dodgeDeck.Add(i < dodgeCountPerFive);
        }
        // Fisher-Yates shuffle to randomize order within each 5-attack set
        for (int i = 0; i < dodgeDeck.Count; i++)
        {
            int rand = Random.Range(i, dodgeDeck.Count);
            bool temp = dodgeDeck[i];
            dodgeDeck[i] = dodgeDeck[rand];
            dodgeDeck[rand] = temp;
        }
        dodgeDeckIndex = 0;
    }

    private bool ShouldDodgeAttack()
    {
        if (!enableDodge) return false;
        if (Time.time < lastDodgeTime + dodgeCooldown) return false;

        if (dodgeDeck == null || dodgeDeckIndex >= dodgeDeck.Count)
        {
            RefillDodgeDeck();
        }

        bool willDodge = dodgeDeck[dodgeDeckIndex];
        dodgeDeckIndex++;
        return willDodge;
    }

    private IEnumerator ExecuteDodgeTeleport()
    {
        isDodging = true;
        lastDodgeTime = Time.time;
        currentState = State.TeleportRetreat;
        SetWalkingAnimation(false);
        rb.linearVelocity = Vector2.zero;

        // Cancel any pending strike or flinch routine
        if (currentActionRoutine != null)
        {
            StopCoroutine(currentActionRoutine);
            currentActionRoutine = null;
        }
        isBodyHitboxActive = false;

        FacePlayer();

        Vector3 startPos = transform.position;
        Vector3 retreatPos = CalculateRetreatPosition();

        // Departure VFX: Leave an evasive ghost phantom and dimensional rift at starting position
        yield return StartCoroutine(PlayTeleportVFX(startPos, retreatPos, true));

        // Instantly relocate backward strictly along the X axis
        transform.position = retreatPos;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();

        // Arrival VFX at retreat position
        yield return StartCoroutine(PlayTeleportVFX(retreatPos, retreatPos, false));

        // Reset attack timer so player has a tactical pacing window before she re-engages
        lastAttackTime = Time.time;
        currentState = State.CooldownPacing;
        isDodging = false;
    }

    // ══════════════════════════════════════════════════════════════════
    //  IDAMAGEABLE & HEALTH
    // ══════════════════════════════════════════════════════════════════

    public void TakeDamage(int amount)
    {
        if (currentState == State.Dead) return;

        // Ignore hits while currently executing a dodge or teleport maneuver
        if (isDodging || currentState == State.TeleportRetreat || currentState == State.TeleportToPlayer)
        {
            return;
        }

        // 3 out of 5 times: Dodge incoming attack by teleporting backward!
        if (ShouldDodgeAttack())
        {
            StartCoroutine(ExecuteDodgeTeleport());
            return;
        }

        // Other times (2 out of 5 times): Allow herself to get hit
        currentHealth -= amount;

        // Hit flash
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(DamageFlashRoutine());

        // Hit feedback juice
        try
        {
            HitFeedbackManager.TriggerHitFeedback(transform, transform.position, amount, amount >= 25, EnemyHitType.PhysicalMelee);
        }
        catch { }

        // Hit stun / flinch
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // If attacked during pacing/patrol/idle, flinch briefly
            if (currentState == State.CooldownPacing || currentState == State.Idle || currentState == State.Patrol)
            {
                StartAction(ExecuteHitStunRoutine());
            }
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(0.12f);
            spriteRenderer.color = originalColor;
        }
    }

    private IEnumerator ExecuteHitStunRoutine()
    {
        currentState = State.HitStun;
        SetWalkingAnimation(false);
        yield return new WaitForSeconds(hitStunDuration);
        currentState = State.CooldownPacing;
    }

    private void Die()
    {
        currentState = State.Dead;
        if (currentActionRoutine != null) StopCoroutine(currentActionRoutine);
        isBodyHitboxActive = false;

        // Spawn Loot Collectible Orbs per AGENTS.md
        try
        {
            OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 0.8f, droppedOrbsCount);
        }
        catch (System.Exception) { }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Disable collisions
        if (bodyCollider != null) bodyCollider.enabled = false;
        rb.linearVelocity = new Vector2(0f, 3.5f); // slight death pop

        // Fade dissolve
        float elapsed = 0f;
        float duration = 0.45f;
        Color startCol = spriteRenderer != null ? spriteRenderer.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(startCol.r, startCol.g, startCol.b, 1f - t);
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, t * 0.5f);
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    // ══════════════════════════════════════════════════════════════════
    //  COOL PROCEDURAL TELEPORT VFX (DARK FANTASY RIFT & SHADOW GHOSTS)
    // ══════════════════════════════════════════════════════════════════

    private static void EnsureVFXSprites()
    {
        if (riftSlitSprite == null) riftSlitSprite = GenerateRiftSlitSprite(32, 128);
        if (shockwaveRingSprite == null) shockwaveRingSprite = GenerateShockwaveRingSprite(64);
        if (sparkDotSprite == null) sparkDotSprite = GenerateSparkDotSprite(32);
    }

    private IEnumerator PlayTeleportVFX(Vector3 fromPos, Vector3 toPos, bool isDeparture)
    {
        EnsureVFXSprites();

        // Subtle combat camera shake
        try { CameraShakeManager.Shake(0.08f, 0.07f); } catch { }

        float feetOffsetY = (bodyCollider != null) ? bodyCollider.bounds.extents.y : 1.7f;
        Vector3 feetPos = fromPos - new Vector3(0f, feetOffsetY, 0f);

        if (isDeparture)
        {
            // 1. Spawn Vertical Dimensional Rift Slit at departure point
            StartCoroutine(AnimateRiftSlit(fromPos, true));

            // 2. Spawn Expanding Ground Shockwave Ring at feet
            StartCoroutine(AnimateShockwave(feetPos));

            // 3. Spawn 3 Intermediate Shadow Ghost Afterimages along X path if traveling distance
            float deltaX = toPos.x - fromPos.x;
            if (Mathf.Abs(deltaX) > 0.8f && spriteRenderer != null && spriteRenderer.sprite != null)
            {
                int ghostCount = 3;
                for (int i = 1; i <= ghostCount; i++)
                {
                    float t = (float)i / (ghostCount + 1);
                    Vector3 ghostPos = Vector3.Lerp(fromPos, toPos, t);
                    StartCoroutine(AnimateGhostShadow(ghostPos, spriteRenderer.sprite, spriteRenderer.flipX, 0.22f, 0.65f - t * 0.2f));
                }
            }

            // 4. Vanish mob sprite cleanly during transit
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            }

            yield return new WaitForSeconds(0.08f);
        }
        else
        {
            // Arrival:
            Vector3 arrivalFeet = toPos - new Vector3(0f, feetOffsetY, 0f);

            // 1. Vertical Dimensional Rift Slit rips open at destination
            StartCoroutine(AnimateRiftSlit(toPos, false));

            // 2. Ground Shockwave Ring expands at destination
            StartCoroutine(AnimateShockwave(arrivalFeet));

            // 3. Radial void energy sparks
            for (int i = 0; i < 6; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StartCoroutine(AnimateVoidSpark(toPos + (Vector3)dir * 0.2f, dir * Random.Range(1.8f, 3.8f)));
            }

            // 4. Flash mob in neon violet silhouette, smoothly returning to normal
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(teleportVFXColor.r, teleportVFXColor.g, teleportVFXColor.b, 1f);
                float flashElapsed = 0f;
                float flashDur = 0.14f;
                while (flashElapsed < flashDur)
                {
                    flashElapsed += Time.deltaTime;
                    float t = flashElapsed / flashDur;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = Color.Lerp(teleportVFXColor, originalColor, t);
                    }
                    yield return null;
                }
                if (spriteRenderer != null) spriteRenderer.color = originalColor;
            }
        }
    }

    private IEnumerator AnimateRiftSlit(Vector3 pos, bool closing)
    {
        GameObject slit = new GameObject("TeleportRiftSlit");
        slit.transform.position = pos;
        slit.transform.localScale = closing ? new Vector3(0.35f, 4.0f, 1f) : new Vector3(0.05f, 0.4f, 1f);

        SpriteRenderer sr = slit.AddComponent<SpriteRenderer>();
        sr.sprite = riftSlitSprite;
        sr.color = teleportVFXColor;
        sr.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 5) + 2;

        float duration = 0.18f;
        float elapsed = 0f;
        Vector3 initialScale = slit.transform.localScale;
        Vector3 targetScale = closing ? new Vector3(0.02f, 4.5f, 1f) : new Vector3(0.32f, 4.2f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (slit == null) yield break;

            slit.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            sr.color = new Color(teleportVFXColor.r, teleportVFXColor.g, teleportVFXColor.b, (1f - t) * 0.95f);
            yield return null;
        }

        if (slit != null) Destroy(slit);
    }

    private IEnumerator AnimateShockwave(Vector3 feetPos)
    {
        GameObject ring = new GameObject("TeleportShockwave");
        ring.transform.position = feetPos;
        ring.transform.localScale = new Vector3(0.4f, 0.12f, 1f);

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = shockwaveRingSprite;
        sr.color = new Color(teleportVFXColor.r, teleportVFXColor.g, teleportVFXColor.b, 0.9f);
        sr.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 5) - 1;

        float duration = 0.22f;
        float elapsed = 0f;
        Vector3 maxScale = new Vector3(3.6f, 0.85f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (ring == null) yield break;

            ring.transform.localScale = Vector3.Lerp(new Vector3(0.4f, 0.12f, 1f), maxScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            sr.color = new Color(teleportVFXColor.r, teleportVFXColor.g, teleportVFXColor.b, (1f - t) * 0.85f);
            yield return null;
        }

        if (ring != null) Destroy(ring);
    }

    private IEnumerator AnimateGhostShadow(Vector3 pos, Sprite sprite, bool flipX, float lifetime, float startAlpha)
    {
        GameObject ghost = new GameObject("StrawhatGhostAfterimage");
        ghost.transform.position = pos;
        ghost.transform.localScale = transform.localScale;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.flipX = flipX;
        sr.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 5) - 1;
        Color ghostCol = new Color(teleportVFXColor.r, teleportVFXColor.g, teleportVFXColor.b, startAlpha);
        sr.color = ghostCol;

        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            if (ghost == null) yield break;

            sr.color = new Color(ghostCol.r, ghostCol.g, ghostCol.b, Mathf.Lerp(startAlpha, 0f, t));
            ghost.transform.localScale += Vector3.one * (Time.deltaTime * 0.15f);
            yield return null;
        }

        if (ghost != null) Destroy(ghost);
    }

    private IEnumerator AnimateVoidSpark(Vector3 pos, Vector2 velocity)
    {
        GameObject spark = new GameObject("VoidSpark");
        spark.transform.position = pos;
        spark.transform.localScale = Vector3.one * Random.Range(0.2f, 0.45f);

        SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
        sr.sprite = sparkDotSprite;
        sr.color = new Color(teleportVFXColor.r, teleportVFXColor.g, teleportVFXColor.b, 1f);
        sr.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 5) + 3;

        float duration = Random.Range(0.18f, 0.28f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (spark == null) yield break;

            spark.transform.position += (Vector3)velocity * Time.deltaTime;
            velocity *= 0.92f;
            sr.color = new Color(teleportVFXColor.r, teleportVFXColor.g, teleportVFXColor.b, 1f - t);
            yield return null;
        }

        if (spark != null) Destroy(spark);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PROCEDURAL SPRITE GENERATORS
    // ══════════════════════════════════════════════════════════════════

    private static Sprite GenerateRiftSlitSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float cx = w * 0.5f;
        float cy = h * 0.5f;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float dy = Mathf.Abs(y - cy) / cy; // 0 at center, 1 at ends
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - cx) / cx;
                float maxDx = Mathf.Pow(Mathf.Clamp01(1f - dy), 0.55f);
                if (dx <= maxDx && maxDx > 0.001f)
                {
                    float u = dx / maxDx;
                    float core = Mathf.Exp(-u * u * 8f);
                    float edge = 1f - u;
                    float alpha = Mathf.Clamp01(core * 0.9f + edge * 0.4f) * Mathf.Clamp01(1f - dy * dy);
                    float brightness = Mathf.Lerp(0.85f, 1.0f, core);
                    pixels[y * w + x] = new Color(brightness, brightness, brightness, alpha);
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite GenerateShockwaveRingSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float center = size * 0.5f;
        float radius = size * 0.44f;
        float halfThickness = size * 0.12f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float dist = Mathf.Abs(d - radius);
                if (dist <= halfThickness)
                {
                    float ring = Mathf.Exp(-dist * dist / (halfThickness * halfThickness * 0.45f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, ring);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite GenerateSparkDotSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float center = size * 0.5f;
        float radius = size * 0.45f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float t = Mathf.Clamp01(d / radius);
                float alpha = Mathf.Exp(-t * t * 4f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ══════════════════════════════════════════════════════════════════
    //  UTILITIES & SENSING
    // ══════════════════════════════════════════════════════════════════

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("Player");
        if (p == null) p = GameObject.Find("BasePlayer");

        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<Health>();
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;
        float dist = Vector2.Distance(transform.position, player.position);
        return dist <= detectionRange;
    }

    private void FacePlayer()
    {
        if (player == null) return;
        SetFacing(player.position.x > transform.position.x);
    }

    private void SetFacing(bool right)
    {
        isFacingRight = right;
        // The raw sprite asset is drawn facing Left.
        // Therefore:
        // - To face Right, flipX must be TRUE.
        // - To face Left, flipX must be FALSE.
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = right;
        }
    }

    private void SetWalkingAnimation(bool isWalking)
    {
        if (anim != null)
        {
            anim.SetBool(AnimIsWalking, isWalking);
        }
    }

    private void StartAction(IEnumerator routine)
    {
        if (currentActionRoutine != null) StopCoroutine(currentActionRoutine);
        currentActionRoutine = StartCoroutine(routine);
    }

    void OnDrawGizmosSelected()
    {
        // Draw body attack hitbox in red
        Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.4f);
        Vector2 center = (bodyCollider != null) ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        Gizmos.DrawWireCube(center, attackHitboxSize);

        // Draw detection range in yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
