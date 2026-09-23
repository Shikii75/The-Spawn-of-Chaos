using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;

/// <summary>
/// StrawhatStaffAI - Controls the female Strawhat Staff Monk/Inquisitor.
/// 
/// Unique Combat Skills (Distinct from StrawhatTeleportAI):
/// 1. Athletic Sprint & Spacing:
///    - Sprints aggressively at the player with staff ready using fluid run cycle.
///    - Features longer melee reach than sword mobs.
/// 2. Vaulting Staff Slam & Traveling Ground Shockwave:
///    - Plants staff and vaults forward through the air in an aggressive arc.
///    - Slams staff down hard into the earth, triggering screen shake and ground ripples.
///    - Releases a traveling Abyssal Earth Shockwave wave along the ground towards the player.
/// 3. Staff Twirl Deflection / Parry (3 out of 5 times):
///    - Instead of teleporting away, she spins her staff in a high-speed defensive circle,
///      parrying incoming player attacks with metallic clashing sparks and barrier ring VFX.
///    - Negates damage 3 out of 5 times, then retaliates. Allows herself to get hit the other 2 times.
/// 4. Health & Drops:
///    - Implements IDamageable, flashes on hit, and drops collectible orbs on defeat.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class StrawhatStaffAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Patrol,
        Chase,
        VaultSlam,
        Deflecting,
        Cooldown,
        HitStun,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private State currentState = State.Patrol;

    [Header("Health & Combat Stats")]
    public int maxHealth = 120;
    public int directSlamDamage = 24;
    public int shockwaveDamage = 16;
    public float playerKnockbackForce = 7.5f;
    public float hitStunDuration = 0.20f;

    [Header("Movement & Ranges")]
    public float detectionRange = 12f;
    public float attackRange = 5.5f;
    public float runSpeed = 4.2f;
    public float patrolSpeed = 2.0f;
    public float patrolDistance = 4.5f;

    [Header("Vault Slam Skill Parameters")]
    public float vaultForwardVelocity = 5.5f;
    public float vaultJumpForce = 5.0f;
    public float shockwaveTravelSpeed = 7.5f;
    public float shockwaveLifetime = 0.85f;
    public float attackCooldown = 2.4f;

    [Header("Deflection / Parry (3 out of 5)")]
    public bool enableParry = true;
    [Range(0, 5)] public int parryCountPerFive = 3;
    public float parryDuration = 0.35f;

    [Header("VFX & Visuals")]
    public Color shadowWaveColor = new Color(0.015f, 0.01f, 0.02f, 1f); // Pure pitch black abyssal shadow
    public Color staffEnergyColor = new Color(0.85f, 0.25f, 0.95f, 0.95f);
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Loot")]
    public int droppedOrbsCount = 3;

    [Header("Configured Animation Sequences (Exclusive)")]
    public Sprite[] runSprites;
    public Sprite[] attackSprites;

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
    private Coroutine currentActionRoutine;
    private Coroutine flashRoutine;
    private Color originalColor = Color.white;
    private float lastTurnTime = 0f;

    // Desynchronization & Organic Movement
    private float pacePhaseOffset = 0f;
    private float speedMultiplier = 1f;
    private float chaseMicroTimer = 0f;
    private bool isMicroHesitating = false;
    private float microHesitationDuration = 0f;

    // Direct Frame-by-Frame Animation Engine
    private float runAnimTimer = 0f;
    private int runAnimIndex = 0;
    private const float RUN_FRAME_RATE = 14f;
    private Coroutine attackAnimRoutine;

    // Parry Deck (3/5)
    private List<bool> parryDeck = new List<bool>();
    private int parryDeckIndex = 0;
    private bool isParrying = false;

    // Hitbox for staff direct slam
    public Vector2 slamHitboxSize = new Vector2(3.4f, 3.2f);
    public Vector2 slamHitboxOffset = new Vector2(0.8f, -0.2f);

    // Animator Hashes
    private static readonly int AnimIsRunning = Animator.StringToHash("isRunning");
    private static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimParry = Animator.StringToHash("Parry");

    // Procedural Sprites Cache
    private static Sprite shockwaveSprite;
    private static Sprite parryRingSprite;
    private static Sprite sparkSprite;
    public static Sprite SparkSprite => sparkSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        // Desynchronization variables
        pacePhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        speedMultiplier = Random.Range(0.88f, 1.15f);
        chaseMicroTimer = Random.Range(1.2f, 3.0f);

        // Ensure AnimatorController is assigned if missing
        if (anim != null && anim.runtimeAnimatorController == null)
        {
#if UNITY_EDITOR
            anim.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Scenes/animations/animators/StrawhatStaffController.controller"
            );
#endif
            if (anim.runtimeAnimatorController == null)
            {
                anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("StrawhatStaffController");
            }
        }

        // Ensure sprite arrays are loaded from the exclusive folders
        EnsureSpritesLoaded();

        // Ensure localScale.x is strictly positive so spriteRenderer.flipX controls facing cleanly
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            if (runSprites != null && runSprites.Length > 0) spriteRenderer.sprite = runSprites[0];
        }

        currentHealth = maxHealth;
        spawnPosition = transform.position;
        gameObject.tag = "enemy";

        RefillParryDeck();
        EnsureVFXSprites();
    }

    void Start()
    {
        FindPlayer();
        currentState = CanSeePlayer() ? State.Chase : State.Patrol;
        stateTimer = Random.Range(2.5f, 4.5f);
        // Stagger initial attack cooldown so mobs don't all strike simultaneously
        lastAttackTime = Time.time - Random.Range(0.4f, attackCooldown * 0.85f);
    }

    void Update()
    {
        if (currentState == State.Dead) return;

        if (player == null || !player.gameObject.activeInHierarchy)
        {
            FindPlayer();
        }

        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  STATE LOGIC (CONTINUOUS ACTIVE LOCOMOTION - NO IDLE)
    // ══════════════════════════════════════════════════════════════════

    private void UpdatePatrol()
    {
        SetRunningAnimation(true);

        float distFromSpawn = transform.position.x - spawnPosition.x;
        bool outOfBounds = Mathf.Abs(distFromSpawn) > patrolDistance && Mathf.Sign(distFromSpawn) == Mathf.Sign(patrolDirection);
        bool wallAhead = IsWallAhead(patrolDirection);

        if ((outOfBounds || wallAhead) && Time.time >= lastTurnTime + 0.5f)
        {
            patrolDirection = -patrolDirection;
            lastTurnTime = Time.time;
        }

        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed * speedMultiplier, rb.linearVelocity.y);
        SetFacing(patrolDirection > 0f);

        if (CanSeePlayer())
        {
            FacePlayer();
            currentState = State.Chase;
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            stateTimer = Random.Range(3.5f, 6.0f);
            if (Random.value > 0.4f)
            {
                patrolDirection = -patrolDirection;
                lastTurnTime = Time.time;
            }
        }
    }

    private float GetCrowdSeparationOffset()
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, 1.8f);
        float separation = 0f;
        foreach (var col in nearby)
        {
            if (col != null && col.gameObject != gameObject && col.CompareTag("enemy"))
            {
                float dx = transform.position.x - col.transform.position.x;
                if (Mathf.Abs(dx) < 1.6f && Mathf.Abs(dx) > 0.01f)
                {
                    separation += Mathf.Sign(dx) * (1.6f - Mathf.Abs(dx)) * 0.7f;
                }
            }
        }
        return Mathf.Clamp(separation, -1.6f, 1.6f);
    }

    private void UpdateChase()
    {
        if (player == null)
        {
            currentState = State.Patrol;
            stateTimer = 3f;
            return;
        }

        FacePlayer();
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (distToPlayer > detectionRange * 1.35f)
        {
            currentState = State.Patrol;
            stateTimer = 3f;
            return;
        }

        // Ready to execute Vaulting Staff Slam
        if (distToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            StartAction(ExecuteVaultSlamSequence());
            return;
        }

        // Sprint towards player with organic cadence and tactical micro-adjustments
        chaseMicroTimer -= Time.deltaTime;
        if (chaseMicroTimer <= 0f)
        {
            chaseMicroTimer = Random.Range(2.0f, 3.8f);
            isMicroHesitating = Random.value < 0.25f;
            microHesitationDuration = Random.Range(0.2f, 0.4f);
        }

        if (isMicroHesitating)
        {
            microHesitationDuration -= Time.deltaTime;
            if (microHesitationDuration <= 0f) isMicroHesitating = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetRunningAnimation(false);
        }
        else
        {
            float dir = player.position.x > transform.position.x ? 1f : -1f;
            float sep = GetCrowdSeparationOffset();
            SetRunningAnimation(true);
            rb.linearVelocity = new Vector2((dir * runSpeed * speedMultiplier) + sep, rb.linearVelocity.y);
        }
    }

    private void UpdateCooldown()
    {
        FacePlayer();
        SetRunningAnimation(true);

        // Active combat footwork during cooldown: stalk or pace around player without freezing
        float distToPlayer = (player != null) ? Vector2.Distance(transform.position, player.position) : 99f;
        float dirToPlayer = (player != null && player.position.x > transform.position.x) ? 1f : -1f;

        if (distToPlayer < attackRange * 0.65f)
        {
            // Back away smoothly to maintain attack pacing
            rb.linearVelocity = new Vector2(-dirToPlayer * patrolSpeed * speedMultiplier, rb.linearVelocity.y);
        }
        else if (distToPlayer > attackRange * 1.15f)
        {
            // Stalk forward
            rb.linearVelocity = new Vector2(dirToPlayer * patrolSpeed * speedMultiplier, rb.linearVelocity.y);
        }
        else
        {
            // Subtle combat bob / pacing desynchronized across mob instances
            float paceDir = Mathf.Sin((Time.time + pacePhaseOffset) * 3f) > 0f ? 1f : -1f;
            rb.linearVelocity = new Vector2(paceDir * patrolSpeed * 0.6f * speedMultiplier, rb.linearVelocity.y);
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            currentState = CanSeePlayer() ? State.Chase : State.Patrol;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  SIGNATURE SKILL: VAULTING STAFF SLAM & GROUND SHOCKWAVE
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator ExecuteVaultSlamSequence()
    {
        currentState = State.VaultSlam;
        SetRunningAnimation(false);
        FacePlayer();

        float dir = isFacingRight ? 1f : -1f;

        // Trigger Attack Animation (15 frames from newfemalestrawstaffattack-1f468d59)
        if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
        attackAnimRoutine = StartCoroutine(PlayAttackSpriteSequence(0.95f));

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetTrigger(AnimAttack);
        }

        // Phase 1: Windup & Staff Plant (Frames 1-3, approx 0.16s)
        yield return new WaitForSeconds(0.16f);

        // Phase 2: Athletic Pole-Vault Leap (Frames 4-7)
        // Propel forward in an aggressive arc
        rb.linearVelocity = new Vector2(dir * vaultForwardVelocity, vaultJumpForce);

        // Vault trail motes
        float vaultElapsed = 0f;
        while (vaultElapsed < 0.28f)
        {
            vaultElapsed += Time.deltaTime;
            SpawnGroundDust(transform.position - Vector3.up * 1.5f);
            yield return null;
        }

        // Phase 3: Downward Crash Slam (Frames 8-10)
        rb.linearVelocity = new Vector2(dir * 1.5f, -8.0f);
        yield return new WaitForSeconds(0.18f);

        // Ground Impact moment (Staff slams into the floor)
        rb.linearVelocity = Vector2.zero;

        // Screen Shake Juice
        try { CameraShakeManager.Shake(0.18f, 0.16f); } catch { }

        // Spawn Ground Impact Ring & Dust Burst
        Vector3 impactPoint = transform.position + new Vector3(dir * 1.2f, -1.5f, 0f);
        StartCoroutine(AnimateGroundImpactRipple(impactPoint));

        // Direct Melee Hitbox check around impact point
        CheckDirectSlamHitbox(dir);

        // Spawn Traveling Abyssal Earth Shockwave along the ground towards player!
        SpawnTravelingGroundShockwave(impactPoint, dir);

        // Phase 4: Staff Recovery & Pose (Frames 11-15, approx 0.35s)
        yield return new WaitForSeconds(0.35f);

        lastAttackTime = Time.time;
        currentState = State.Cooldown;
    }

    private void CheckDirectSlamHitbox(float dir)
    {
        Vector2 center = (Vector2)transform.position + new Vector2(dir * slamHitboxOffset.x, slamHitboxOffset.y);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, slamHitboxSize, 0f);

        foreach (var hit in hits)
        {
            if (hit == null || hit == bodyCollider || hit.transform.IsChildOf(transform)) continue;

            if (hit.CompareTag("Player"))
            {
                Health h = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
                if (h != null)
                {
                    h.TakeDamage(directSlamDamage);
                    try
                    {
                        HitFeedbackManager.TriggerHitFeedback(hit.transform, hit.transform.position, directSlamDamage, true, EnemyHitType.PhysicalMelee);
                    }
                    catch { }

                    // Violent knockback
                    Rigidbody2D pRb = hit.GetComponent<Rigidbody2D>() ?? hit.GetComponentInParent<Rigidbody2D>();
                    if (pRb != null)
                    {
                        pRb.linearVelocity = new Vector2(dir * playerKnockbackForce, playerKnockbackForce * 0.75f);
                    }
                }
                break;
            }
        }
    }

    private void SpawnTravelingGroundShockwave(Vector3 origin, float dir)
    {
        GameObject waveGO = new GameObject("StaffShadowShockwave");
        waveGO.transform.position = origin + Vector3.up * 0.45f;
        waveGO.transform.localScale = new Vector3(dir > 0f ? 1f : -1f, 1f, 1f);

        SpriteRenderer sr = waveGO.AddComponent<SpriteRenderer>();
        sr.sprite = shockwaveSprite;
        sr.color = shadowWaveColor;
        sr.sortingOrder = 32;

        BoxCollider2D col = waveGO.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2.0f, 1.4f);

        StaffShockwaveProjectile projectile = waveGO.AddComponent<StaffShockwaveProjectile>();
        projectile.Init(dir, shockwaveTravelSpeed, shockwaveDamage, shockwaveLifetime, playerKnockbackForce * 0.8f, shadowWaveColor);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DEFENSIVE SKILL: STAFF TWIRL PARRY / DEFLECTION (3/5)
    // ══════════════════════════════════════════════════════════════════

    private void RefillParryDeck()
    {
        parryDeck.Clear();
        for (int i = 0; i < 5; i++)
        {
            parryDeck.Add(i < parryCountPerFive);
        }
        for (int i = 0; i < parryDeck.Count; i++)
        {
            int rand = Random.Range(i, parryDeck.Count);
            bool temp = parryDeck[i];
            parryDeck[i] = parryDeck[rand];
            parryDeck[rand] = temp;
        }
        parryDeckIndex = 0;
    }

    private bool ShouldParryAttack()
    {
        if (!enableParry) return false;
        if (parryDeck == null || parryDeckIndex >= parryDeck.Count)
        {
            RefillParryDeck();
        }
        bool willParry = parryDeck[parryDeckIndex];
        parryDeckIndex++;
        return willParry;
    }

    private IEnumerator ExecuteStaffParryRoutine()
    {
        isParrying = true;
        currentState = State.Deflecting;
        SetRunningAnimation(false);
        rb.linearVelocity = Vector2.zero;
        FacePlayer();

        if (anim != null)
        {
            anim.SetTrigger(AnimParry);
        }

        // Spawn Spinning Staff Barrier Shield Ring
        GameObject barrierGO = new GameObject("StaffParryBarrier");
        barrierGO.transform.position = transform.position + new Vector3(isFacingRight ? 0.6f : -0.6f, 0.2f, 0f);

        SpriteRenderer bSr = barrierGO.AddComponent<SpriteRenderer>();
        bSr.sprite = parryRingSprite;
        bSr.color = staffEnergyColor;
        bSr.sortingOrder = 35;

        // Metallic Deflection Sparks
        for (int i = 0; i < 6; i++)
        {
            Vector2 sparkVel = new Vector2((isFacingRight ? -1f : 1f) * Random.Range(2f, 4.5f), Random.Range(-2f, 3f));
            StartCoroutine(AnimateParrySpark(barrierGO.transform.position, sparkVel));
        }

        try { CameraShakeManager.Shake(0.09f, 0.08f); } catch { }

        // Spin barrier ring
        float elapsed = 0f;
        while (elapsed < parryDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / parryDuration;
            if (barrierGO != null)
            {
                barrierGO.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
                barrierGO.transform.localScale = Vector3.one * (1.2f + Mathf.Sin(t * Mathf.PI) * 0.4f);
                bSr.color = new Color(staffEnergyColor.r, staffEnergyColor.g, staffEnergyColor.b, (1f - t) * 0.95f);
            }
            yield return null;
        }

        if (barrierGO != null) Destroy(barrierGO);

        // Defensive back-hop
        float backDir = isFacingRight ? -1f : 1f;
        rb.linearVelocity = new Vector2(backDir * 3.5f, 2.2f);
        yield return new WaitForSeconds(0.20f);

        isParrying = false;
        currentState = State.Chase;
    }

    // ══════════════════════════════════════════════════════════════════
    //  IDAMAGEABLE & HEALTH
    // ══════════════════════════════════════════════════════════════════

    public void TakeDamage(int amount)
    {
        if (currentState == State.Dead) return;

        // If currently in a parry sequence, ignore extra damage
        if (isParrying) return;

        // 3 out of 5 times: Deflect and parry incoming player attacks!
        if (ShouldParryAttack())
        {
            StartCoroutine(ExecuteStaffParryRoutine());
            return;
        }

        // Other times (2 out of 5 times): Allow herself to get hit
        currentHealth -= amount;

        // Hit flash
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(DamageFlashRoutine());

        try
        {
            HitFeedbackManager.TriggerHitFeedback(transform, transform.position, amount, amount >= 25, EnemyHitType.PhysicalMelee);
        }
        catch { }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (currentState == State.Patrol || currentState == State.Cooldown)
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
        SetRunningAnimation(false);
        yield return new WaitForSeconds(hitStunDuration);
        currentState = State.Chase;
    }

    private void Die()
    {
        currentState = State.Dead;
        if (currentActionRoutine != null) StopCoroutine(currentActionRoutine);

        try
        {
            OrbSpawner.SpawnLootCluster(transform.position + Vector3.up * 0.8f, droppedOrbsCount);
        }
        catch { }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (bodyCollider != null) bodyCollider.enabled = false;
        rb.linearVelocity = new Vector2(0f, 3.5f);

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
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    // ══════════════════════════════════════════════════════════════════
    //  VFX PROCEDURAL ANIMATORS
    // ══════════════════════════════════════════════════════════════════

    private void EnsureVFXSprites()
    {
        if (shockwaveSprite == null) shockwaveSprite = GenerateShockwaveSprite(128, 96);
        if (parryRingSprite == null) parryRingSprite = GenerateParryRingSprite(64);
        if (sparkSprite == null) sparkSprite = GenerateSparkSprite(32);
    }

    private IEnumerator AnimateGroundImpactRipple(Vector3 pos)
    {
        GameObject ripple = new GameObject("GroundImpactRipple");
        ripple.transform.position = pos;
        ripple.transform.localScale = new Vector3(0.5f, 0.15f, 1f);

        SpriteRenderer sr = ripple.AddComponent<SpriteRenderer>();
        sr.sprite = parryRingSprite;
        sr.color = shadowWaveColor;
        sr.sortingOrder = 25;

        float duration = 0.28f;
        float elapsed = 0f;
        Vector3 maxScale = new Vector3(4.2f, 0.85f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (ripple == null) yield break;

            ripple.transform.localScale = Vector3.Lerp(new Vector3(0.5f, 0.15f, 1f), maxScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            sr.color = new Color(shadowWaveColor.r, shadowWaveColor.g, shadowWaveColor.b, (1f - t) * 0.95f);
            yield return null;
        }

        if (ripple != null) Destroy(ripple);
    }

    private IEnumerator AnimateParrySpark(Vector3 pos, Vector2 vel)
    {
        GameObject spark = new GameObject("ParrySpark");
        spark.transform.position = pos;
        spark.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);

        SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
        sr.sprite = sparkSprite;
        sr.color = Color.Lerp(Color.white, staffEnergyColor, Random.value);
        sr.sortingOrder = 36;

        float elapsed = 0f;
        float lifetime = 0.22f;
        while (elapsed < lifetime && spark != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            spark.transform.position += (Vector3)vel * Time.deltaTime;
            vel *= 0.90f;
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
            yield return null;
        }
        if (spark != null) Destroy(spark);
    }

    private void SpawnGroundDust(Vector3 pos)
    {
        GameObject dust = new GameObject("VaultDust");
        dust.transform.position = pos + new Vector3(Random.Range(-0.2f, 0.2f), 0f, 0f);
        dust.transform.localScale = Vector3.one * Random.Range(0.2f, 0.35f);

        SpriteRenderer sr = dust.AddComponent<SpriteRenderer>();
        sr.sprite = sparkSprite;
        sr.color = new Color(0.8f, 0.75f, 0.9f, 0.5f);
        sr.sortingOrder = 22;

        StartCoroutine(FadeAndDestroy(dust, 0.25f));
    }

    private IEnumerator FadeAndDestroy(GameObject go, float lifetime)
    {
        float elapsed = 0f;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        Color start = sr != null ? sr.color : Color.white;
        while (elapsed < lifetime && go != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            if (sr != null) sr.color = new Color(start.r, start.g, start.b, (1f - t) * start.a);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // Procedural Texture Generators
    private static Sprite GenerateShockwaveSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float normY = (float)y / (float)h;
            for (int x = 0; x < w; x++)
            {
                float normX = (float)x / (float)w;
                // Multi-peak jagged shadow wave profile surging forward (normX=1 is front)
                float mainWave = Mathf.Sin(normX * Mathf.PI * 0.9f);
                float jagged1 = Mathf.Sin(normX * 18.0f) * 0.12f;
                float jagged2 = Mathf.Cos(normX * 9.0f) * 0.08f;
                float crestHeight = Mathf.Clamp01(mainWave * 0.85f + jagged1 + jagged2);

                float distFromCrest = normY - crestHeight;
                float alpha = 0f;
                if (normY <= crestHeight)
                {
                    // Solid dark shadow core
                    alpha = Mathf.Clamp01(1f - normY * 0.25f);
                }
                else
                {
                    // Wispy smoke trail rising off the top of the shadow wave
                    alpha = Mathf.Clamp01(Mathf.Exp(-distFromCrest * distFromCrest * 32f) * 0.7f);
                }

                // Front cut-off and back fade
                alpha *= Mathf.SmoothStep(0f, 0.15f, normX);
                alpha *= (1f - normX * 0.2f);

                // Pure pitch black shadow void
                pixels[y * w + x] = new Color(0.015f, 0.01f, 0.02f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

    private static Sprite GenerateParryRingSprite(int size)
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

    private static Sprite GenerateSparkSprite(int size)
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
        if (spriteRenderer != null)
        {
            // The raw frames are drawn facing RIGHT.
            // Therefore:
            // - Facing Right -> flipX = false
            // - Facing Left -> flipX = true
            spriteRenderer.flipX = !right;
        }
    }

    private void SetRunningAnimation(bool isRunning)
    {
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetBool(AnimIsRunning, isRunning);
            anim.SetBool(AnimIsWalking, isRunning);
        }

        // Direct frame animation fallback: continuously cycle run frames during locomotion
        if (currentState != State.VaultSlam && currentState != State.Deflecting && currentState != State.Dead && currentState != State.HitStun)
        {
            AnimateRun();
        }
    }

    private void AnimateRun()
    {
        if (runSprites == null || runSprites.Length == 0) return;
        runAnimTimer += Time.deltaTime;
        if (runAnimTimer >= 1f / RUN_FRAME_RATE)
        {
            runAnimTimer -= 1f / RUN_FRAME_RATE;
            runAnimIndex = (runAnimIndex + 1) % runSprites.Length;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = runSprites[runAnimIndex];
            }
        }
    }

    private IEnumerator PlayAttackSpriteSequence(float duration)
    {
        if (attackSprites == null || attackSprites.Length == 0) yield break;
        float frameTime = duration / attackSprites.Length;
        for (int i = 0; i < attackSprites.Length; i++)
        {
            if (spriteRenderer != null && attackSprites[i] != null)
            {
                spriteRenderer.sprite = attackSprites[i];
            }
            yield return new WaitForSeconds(frameTime);
        }
    }

    public void EnsureSpritesLoaded()
    {
#if UNITY_EDITOR
        if (runSprites == null || runSprites.Length == 0)
        {
            runSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newfemalestrawstaffrun-80c96966");
        }
        if (attackSprites == null || attackSprites.Length == 0)
        {
            attackSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newfemalestrawstaffattack-1f468d59");
        }
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureSpritesLoaded();
    }

    private static Sprite[] LoadEditorSprites(string folder)
    {
        if (!System.IO.Directory.Exists(folder)) return new Sprite[0];
        string[] files = System.IO.Directory.GetFiles(folder, "*.png");
        System.Array.Sort(files);
        List<Sprite> list = new List<Sprite>();
        foreach (var f in files)
        {
            string p = f.Replace('\\', '/');
            int idx = p.IndexOf("Assets/");
            if (idx >= 0) p = p.Substring(idx);
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s == null)
            {
                var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p);
                if (all != null)
                {
                    foreach (var a in all)
                    {
                        if (a is Sprite spr) { s = spr; break; }
                    }
                }
            }
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }
#endif

    private bool IsWallAhead(float direction)
    {
        float myExtentsX = (bodyCollider != null) ? bodyCollider.bounds.extents.x : 0.8f;
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.4f + new Vector2(direction * (myExtentsX + 0.15f), 0f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(direction, 0f), 0.65f, LayerMask.GetMask("Ground", "Terrain", "Platform", "Default"));
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("enemy")) continue;
            if (hit.collider.name.Contains("Orb") || hit.collider.name.Contains("Loot")) continue;
            return true;
        }
        return false;
    }

    private void StartAction(IEnumerator routine)
    {
        if (currentActionRoutine != null) StopCoroutine(currentActionRoutine);
        currentActionRoutine = StartCoroutine(routine);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        float dir = isFacingRight ? 1f : -1f;
        Vector2 center = (Vector2)transform.position + new Vector2(dir * slamHitboxOffset.x, slamHitboxOffset.y);
        Gizmos.DrawWireCube(center, slamHitboxSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}

/// <summary>
/// Projectile script for traveling pitch-black shadow ground shockwave unleashed by the staff slam.
/// Leaves a creeping smoky dark shadow trail hugging the terrain to match the dark game design.
/// </summary>
public class StaffShockwaveProjectile : MonoBehaviour
{
    private float direction;
    private float speed;
    private int damage;
    private float knockback;
    private float lifetime;
    private float elapsed;
    private bool hasHitPlayer;
    private Color shadowColor = new Color(0.015f, 0.01f, 0.02f, 1f);

    private TrailRenderer trail;
    private float smokeSpawnTimer = 0f;
    private const float SMOKE_INTERVAL = 0.035f;

    public void Init(float dir, float moveSpeed, int dmg, float duration, float kb, Color col)
    {
        direction = dir;
        speed = moveSpeed;
        damage = dmg;
        lifetime = duration;
        knockback = kb;
        shadowColor = col;

        SetupShadowTrail();
    }

    private void SetupShadowTrail()
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.32f;
        trail.startWidth = 1.3f;
        trail.endWidth = 0.05f;
        trail.minVertexDistance = 0.05f;
        trail.sortingOrder = 30;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        trail.material = mat;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.02f, 0.01f, 0.03f), 0.0f),
                new GradientColorKey(new Color(0.01f, 0.005f, 0.02f), 0.5f),
                new GradientColorKey(new Color(0.0f, 0.0f, 0.0f), 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.95f, 0.0f),
                new GradientAlphaKey(0.70f, 0.45f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        trail.colorGradient = grad;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += new Vector3(direction * speed * Time.deltaTime, 0f, 0f);

        // Ground check: hug floor closely
        RaycastHit2D ground = Physics2D.Raycast(transform.position + Vector3.up * 0.5f, Vector2.down, 1.2f, LayerMask.GetMask("Ground", "Terrain", "Platform", "Default"));
        if (ground.collider != null && !ground.collider.isTrigger)
        {
            transform.position = new Vector3(transform.position.x, ground.point.y + 0.35f, transform.position.z);
        }

        // Spawn creeping shadow smoke wisps along the ground
        smokeSpawnTimer += Time.deltaTime;
        if (smokeSpawnTimer >= SMOKE_INTERVAL)
        {
            smokeSpawnTimer -= SMOKE_INTERVAL;
            SpawnGroundShadowSmoke(transform.position);
        }

        if (elapsed >= lifetime)
        {
            DissipateShadow();
        }
    }

    private void SpawnGroundShadowSmoke(Vector3 pos)
    {
        GameObject puff = new GameObject("ShadowSmokePuff");
        puff.transform.position = pos + new Vector3(Random.Range(-0.15f, 0.15f), -0.1f + Random.Range(0f, 0.12f), 0f);
        float initScale = Random.Range(0.35f, 0.65f);
        puff.transform.localScale = Vector3.one * initScale;

        SpriteRenderer sr = puff.AddComponent<SpriteRenderer>();
        sr.sprite = StrawhatStaffAI.SparkSprite;
        sr.color = new Color(0.01f, 0.01f, 0.02f, 0.85f);
        sr.sortingOrder = 31;

        StartCoroutine(AnimateShadowSmokePuff(puff, initScale, 0.35f));
    }

    private IEnumerator AnimateShadowSmokePuff(GameObject puff, float startScale, float duration)
    {
        float t = 0f;
        SpriteRenderer sr = puff.GetComponent<SpriteRenderer>();
        Vector3 floatVelocity = new Vector3(-direction * 0.45f, Random.Range(0.35f, 0.75f), 0f);

        while (t < duration && puff != null)
        {
            t += Time.deltaTime;
            float norm = t / duration;

            puff.transform.position += floatVelocity * Time.deltaTime;
            puff.transform.localScale = Vector3.one * Mathf.Lerp(startScale, startScale * 1.35f, norm);

            if (sr != null)
            {
                sr.color = new Color(0.01f, 0.01f, 0.02f, (1f - norm) * 0.85f);
            }
            yield return null;
        }

        if (puff != null) Destroy(puff);
    }

    private void DissipateShadow()
    {
        for (int i = 0; i < 5; i++)
        {
            SpawnGroundShadowSmoke(transform.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0f, 0.4f), 0f));
        }
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHitPlayer) return;

        if (other.CompareTag("Player"))
        {
            Health h = other.GetComponent<Health>() ?? other.GetComponentInParent<Health>();
            if (h != null)
            {
                hasHitPlayer = true;
                h.TakeDamage(damage);

                Rigidbody2D rb = other.GetComponent<Rigidbody2D>() ?? other.GetComponentInParent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(direction * knockback, knockback * 0.6f);
                }

                try
                {
                    HitFeedbackManager.TriggerHitFeedback(other.transform, transform.position, damage, false, EnemyHitType.PhysicalMelee);
                }
                catch { }

                DissipateShadow();
            }
        }
    }
}
