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
        Idle,
        Patrol,
        Chase,
        VaultSlam,
        Deflecting,
        Cooldown,
        HitStun,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private State currentState = State.Idle;

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
    public float shockwaveLifetime = 0.8f;
    public float attackCooldown = 2.4f;

    [Header("Deflection / Parry (3 out of 5)")]
    public bool enableParry = true;
    [Range(0, 5)] public int parryCountPerFive = 3;
    public float parryDuration = 0.35f;

    [Header("VFX & Visuals")]
    public Color staffEnergyColor = new Color(0.85f, 0.25f, 0.95f, 0.95f);
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Loot")]
    public int droppedOrbsCount = 3;

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

    // Parry Deck (3/5)
    private List<bool> parryDeck = new List<bool>();
    private int parryDeckIndex = 0;
    private bool isParrying = false;

    // Hitbox for staff direct slam
    public Vector2 slamHitboxSize = new Vector2(3.4f, 3.2f);
    public Vector2 slamHitboxOffset = new Vector2(0.8f, -0.2f);

    // Animator Hashes
    private static readonly int AnimIsRunning = Animator.StringToHash("isRunning");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimParry = Animator.StringToHash("Parry");

    // Procedural Sprites Cache
    private static Sprite shockwaveSprite;
    private static Sprite parryRingSprite;
    private static Sprite sparkSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        // Ensure localScale.x is strictly positive so spriteRenderer.flipX controls facing cleanly
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
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
        currentState = State.Idle;
        stateTimer = Random.Range(0.6f, 1.2f);
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
            case State.Idle:
                UpdateIdle();
                break;
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
    //  STATE LOGIC
    // ══════════════════════════════════════════════════════════════════

    private void UpdateIdle()
    {
        SetRunningAnimation(false);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (CanSeePlayer())
        {
            FacePlayer();
            currentState = State.Chase;
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = State.Patrol;
            stateTimer = Random.Range(2.5f, 4.5f);
            patrolDirection = Random.value > 0.5f ? 1f : -1f;
        }
    }

    private void UpdatePatrol()
    {
        if (isPatrolWaiting)
        {
            SetRunningAnimation(false);
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

        SetRunningAnimation(true);

        float distFromSpawn = transform.position.x - spawnPosition.x;
        bool outOfBounds = Mathf.Abs(distFromSpawn) > patrolDistance && Mathf.Sign(distFromSpawn) == Mathf.Sign(patrolDirection);
        bool wallAhead = IsWallAhead(patrolDirection);

        if ((outOfBounds || wallAhead) && Time.time >= lastTurnTime + 0.6f)
        {
            isPatrolWaiting = true;
            patrolWaitTimer = 0.5f;
            return;
        }

        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);
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
            currentState = State.Idle;
            stateTimer = Random.Range(1f, 2.5f);
        }
    }

    private void UpdateChase()
    {
        if (player == null)
        {
            currentState = State.Idle;
            return;
        }

        FacePlayer();
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (distToPlayer > detectionRange * 1.35f)
        {
            currentState = State.Idle;
            stateTimer = 1.5f;
            return;
        }

        // Ready to execute Vaulting Staff Slam
        if (distToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            StartAction(ExecuteVaultSlamSequence());
            return;
        }

        // Sprint towards player
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        if (!IsWallAhead(dir))
        {
            SetRunningAnimation(true);
            rb.linearVelocity = new Vector2(dir * runSpeed, rb.linearVelocity.y);
        }
        else
        {
            SetRunningAnimation(false);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartAction(ExecuteVaultSlamSequence());
            }
        }
    }

    private void UpdateCooldown()
    {
        FacePlayer();
        SetRunningAnimation(false);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            if (CanSeePlayer())
            {
                currentState = State.Chase;
            }
            else
            {
                currentState = State.Idle;
                stateTimer = 1.0f;
            }
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

        // Trigger Attack Animation (15 frames total)
        if (anim != null)
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
        GameObject waveGO = new GameObject("StaffGroundShockwave");
        waveGO.transform.position = origin + Vector3.up * 0.45f;
        waveGO.transform.localScale = new Vector3(dir > 0f ? 1f : -1f, 1f, 1f);

        SpriteRenderer sr = waveGO.AddComponent<SpriteRenderer>();
        sr.sprite = shockwaveSprite;
        sr.color = staffEnergyColor;
        sr.sortingOrder = 30;

        BoxCollider2D col = waveGO.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.8f, 1.4f);

        StaffShockwaveProjectile projectile = waveGO.AddComponent<StaffShockwaveProjectile>();
        projectile.Init(dir, shockwaveTravelSpeed, shockwaveDamage, shockwaveLifetime, playerKnockbackForce * 0.8f);
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
            if (currentState == State.Idle || currentState == State.Patrol || currentState == State.Cooldown)
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
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, t * 0.5f);
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    // ══════════════════════════════════════════════════════════════════
    //  VFX & PROCEDURAL SPRITES
    // ══════════════════════════════════════════════════════════════════

    private static void EnsureVFXSprites()
    {
        if (shockwaveSprite == null) shockwaveSprite = GenerateShockwaveSprite(128, 64);
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
        sr.color = staffEnergyColor;
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
            sr.color = new Color(staffEnergyColor.r, staffEnergyColor.g, staffEnergyColor.b, (1f - t) * 0.9f);
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
            float normY = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                float normX = (float)x / w;
                // Forward crest crescent profile
                float crest = Mathf.Sin(normX * Mathf.PI);
                float wave = Mathf.Exp(-Mathf.Pow(normY - crest * 0.75f, 2f) * 12f);
                float alpha = Mathf.Clamp01(wave * (1f - normX * 0.35f));
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
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
            spriteRenderer.flipX = right;
        }
    }

    private void SetRunningAnimation(bool isRunning)
    {
        if (anim != null)
        {
            anim.SetBool(AnimIsRunning, isRunning);
        }
    }

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
/// Projectile script for traveling ground shockwave unleashed by the staff slam.
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

    public void Init(float dir, float moveSpeed, int dmg, float duration, float kb)
    {
        direction = dir;
        speed = moveSpeed;
        damage = dmg;
        lifetime = duration;
        knockback = kb;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += new Vector3(direction * speed * Time.deltaTime, 0f, 0f);

        // Ground check: keep hugging floor
        RaycastHit2D ground = Physics2D.Raycast(transform.position + Vector3.up * 0.5f, Vector2.down, 1.2f, LayerMask.GetMask("Ground", "Terrain", "Platform", "Default"));
        if (ground.collider != null && !ground.collider.isTrigger)
        {
            transform.position = new Vector3(transform.position.x, ground.point.y + 0.35f, transform.position.z);
        }

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
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

                Destroy(gameObject, 0.05f);
            }
        }
    }
}
