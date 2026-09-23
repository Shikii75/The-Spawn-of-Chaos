using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;

/// <summary>
/// StrawhatSwordAI - Fast, disciplined Katana Swordsman of the Strawhat Clan.
/// 
/// Visuals & Animations (Facing RIGHT natively -> spriteRenderer.flipX = !right):
/// - Idle: newidlestraw-838517e4 (21 frames)
/// - Stance: newstancestraw-5fb4cdc3 (13 frames, alert combat stance)
/// - Walk: newwalkstraw-9edabc38 (23 frames)
/// - Attack: newattackstraw-ba1be727 (19 frames, Iaijutsu blade dash & crescent slash)
/// 
/// Skills & Mechanics:
/// 1. Calm Idle & Patrol: Walks smoothly, turning at patrol limits without jitter.
/// 2. Engage Stance: When player is detected, enters low guard stance to charge attack.
/// 3. Iaijutsu Blade Rush: Surges forward with high-speed katana dash slash and crescent blade VFX.
/// 4. Defensive Deflection / Parry (3 out of 5): Deflects player attacks with spark VFX and defensive back-step.
/// 5. Full Physics Safety: Uses rb.linearVelocity, non-push dynamic attack range, and drops loot orbs on defeat.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class StrawhatSwordAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Idle,
        Patrol,
        EngageStance,
        BladeRush,
        Parry,
        CooldownPacing,
        HitStun,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private State currentState = State.Idle;

    [Header("Health & Combat Stats")]
    public int maxHealth = 110;
    public int slashDamage = 20;
    public float playerKnockbackForce = 6.8f;
    public float hitStunDuration = 0.18f;

    [Header("Movement & Ranges")]
    public float detectionRange = 11f;
    public float attackRange = 4.8f;
    public float runSpeed = 4.2f;
    public float patrolSpeed = 2.0f;
    public float patrolDistance = 4.5f;

    [Header("Blade Rush Parameters")]
    public float rushDashSpeed = 7.5f;
    public float rushDuration = 0.40f;
    public float attackCooldown = 2.2f;
    public Vector2 slashHitboxSize = new Vector2(3.2f, 2.8f);
    public Vector2 slashHitboxOffset = new Vector2(0.9f, 0f);

    [Header("Deflection / Parry (3 out of 5)")]
    public bool enableParry = true;
    [Range(0, 5)] public int parryCountPerFive = 3;
    public float parryDuration = 0.32f;

    [Header("VFX & Colors")]
    public Color bladeArcColor = new Color(0.9f, 0.25f, 0.95f, 0.95f);
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Loot")]
    public int droppedOrbsCount = 3;

    [Header("Configured Animation Sequences")]
    public Sprite[] idleSprites;
    public Sprite[] stanceSprites;
    public Sprite[] walkSprites;
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
    private float lastTurnTime = 0f;
    private Coroutine currentActionRoutine;
    private Coroutine flashRoutine;
    private Color originalColor = Color.white;

    // Desynchronization & Organic Movement
    private float pacePhaseOffset = 0f;
    private float speedMultiplier = 1f;
    private float chaseMicroTimer = 0f;
    private bool isMicroHesitating = false;
    private float microHesitationDuration = 0f;

    // Frame-by-frame direct animator
    private float animTimer = 0f;
    private int animIndex = 0;
    private Coroutine attackAnimRoutine;
    private const float IDLE_FPS = 12f;
    private const float STANCE_FPS = 14f;
    private const float WALK_FPS = 14f;

    // Parry deck
    private List<bool> parryDeck = new List<bool>();
    private int parryDeckIndex = 0;
    private bool isParrying = false;

    // Animator Hashes
    private static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
    private static readonly int AnimIsStance = Animator.StringToHash("isStance");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimParry = Animator.StringToHash("Parry");

    // Procedural Sprites Cache
    private static Sprite slashArcSprite;
    private static Sprite sparkSprite;

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

        if (anim != null && anim.runtimeAnimatorController == null)
        {
#if UNITY_EDITOR
            anim.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Scenes/animations/animators/StrawhatSwordController.controller"
            );
#endif
            if (anim.runtimeAnimatorController == null)
            {
                anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("StrawhatSwordController");
            }
        }

        EnsureSpritesLoaded();

        // Ensure scale is positive
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            if (idleSprites != null && idleSprites.Length > 0) spriteRenderer.sprite = idleSprites[0];
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
        stateTimer = Random.Range(0.6f, 1.8f);
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
            case State.Idle:
                UpdateIdle();
                break;
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.EngageStance:
                UpdateEngageStance();
                break;
            case State.CooldownPacing:
                UpdateCooldownPacing();
                break;
        }

        // Direct animation driver
        DriveAnimationFrames();
    }

    // ══════════════════════════════════════════════════════════════════
    //  STATE LOGIC
    // ══════════════════════════════════════════════════════════════════

    private void UpdateIdle()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (CanSeePlayer())
        {
            FacePlayer();
            currentState = State.EngageStance;
            stateTimer = Random.Range(0.4f, 0.7f);
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = State.Patrol;
            stateTimer = Random.Range(3.0f, 5.0f);
            patrolDirection = Random.value > 0.5f ? 1f : -1f;
        }
    }

    private void UpdatePatrol()
    {
        if (isPatrolWaiting)
        {
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

        float distFromSpawn = transform.position.x - spawnPosition.x;
        bool outOfBounds = Mathf.Abs(distFromSpawn) > patrolDistance && Mathf.Sign(distFromSpawn) == Mathf.Sign(patrolDirection);
        bool wallAhead = IsWallAhead(patrolDirection);

        if ((outOfBounds || wallAhead) && Time.time >= lastTurnTime + 0.6f)
        {
            isPatrolWaiting = true;
            patrolWaitTimer = Random.Range(0.6f, 1.2f);
            return;
        }

        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed * speedMultiplier, rb.linearVelocity.y);
        SetFacing(patrolDirection > 0f);

        if (CanSeePlayer())
        {
            FacePlayer();
            currentState = State.EngageStance;
            stateTimer = Random.Range(0.35f, 0.75f);
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = State.Idle;
            stateTimer = Random.Range(1.2f, 2.5f);
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

    private void UpdateEngageStance()
    {
        FacePlayer();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (player == null)
        {
            currentState = State.Idle;
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (distToPlayer > detectionRange * 1.3f)
        {
            currentState = State.Idle;
            stateTimer = 1.5f;
            return;
        }

        // Ready to strike!
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f && Time.time >= lastAttackTime + attackCooldown)
        {
            StartAction(ExecuteBladeRushSequence());
            return;
        }

        // If player is outside attack range, stalk forward with desynchronized cadence
        if (distToPlayer > attackRange)
        {
            chaseMicroTimer -= Time.deltaTime;
            if (chaseMicroTimer <= 0f)
            {
                chaseMicroTimer = Random.Range(2.0f, 3.8f);
                isMicroHesitating = Random.value < 0.28f;
                microHesitationDuration = Random.Range(0.2f, 0.45f);
            }

            if (isMicroHesitating)
            {
                microHesitationDuration -= Time.deltaTime;
                if (microHesitationDuration <= 0f) isMicroHesitating = false;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            else
            {
                float dir = player.position.x > transform.position.x ? 1f : -1f;
                float sep = GetCrowdSeparationOffset();
                rb.linearVelocity = new Vector2((dir * runSpeed * speedMultiplier) + sep, rb.linearVelocity.y);
            }
        }
    }

    private void UpdateCooldownPacing()
    {
        FacePlayer();
        float distToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : 99f;
        float dirToPlayer = (player != null && player.position.x > transform.position.x) ? 1f : -1f;

        // Maintain safe tactical distance
        if (distToPlayer < attackRange * 0.7f)
        {
            rb.linearVelocity = new Vector2(-dirToPlayer * patrolSpeed * speedMultiplier, rb.linearVelocity.y);
        }
        else
        {
            float paceDir = Mathf.Sin((Time.time + pacePhaseOffset) * 2.5f) > 0f ? 1f : -1f;
            rb.linearVelocity = new Vector2(paceDir * patrolSpeed * 0.5f * speedMultiplier, rb.linearVelocity.y);
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            currentState = CanSeePlayer() ? State.EngageStance : State.Idle;
            stateTimer = Random.Range(0.35f, 0.75f);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  COMBAT SKILL: IAIJUTSU BLADE RUSH & CRESCENT SLASH
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator ExecuteBladeRushSequence()
    {
        currentState = State.BladeRush;
        FacePlayer();

        float dir = isFacingRight ? 1f : -1f;

        // Trigger attack animation
        if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
        attackAnimRoutine = StartCoroutine(PlayAttackSpriteSequence(0.95f));

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetTrigger(AnimAttack);
        }

        // Windup & sword unsheath (frames 1-4, ~0.18s)
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.18f);

        // Lethal Blade Rush Forward Surge! (frames 5-11, ~0.35s)
        rb.linearVelocity = new Vector2(dir * rushDashSpeed, 0.5f);

        // Spawn crescent slash arc effect & afterimages
        Vector3 slashPos = transform.position + new Vector3(dir * 1.0f, 0f, 0f);
        StartCoroutine(AnimateSlashArc(slashPos, dir));

        // Screen Shake
        try { CameraShakeManager.Shake(0.15f, 0.12f); } catch { }

        // Melee hitbox check during active swing
        bool hitLanded = false;
        float rushElapsed = 0f;
        while (rushElapsed < rushDuration)
        {
            rushElapsed += Time.deltaTime;
            if (!hitLanded)
            {
                hitLanded = CheckSlashHitbox(dir);
            }
            yield return null;
        }

        // Deceleration & Sheathing Recovery (frames 12-19, ~0.40s)
        rb.linearVelocity = new Vector2(dir * 1.0f, rb.linearVelocity.y);
        yield return new WaitForSeconds(0.40f);

        rb.linearVelocity = Vector2.zero;
        lastAttackTime = Time.time;
        currentState = State.CooldownPacing;
    }

    private bool CheckSlashHitbox(float dir)
    {
        Vector2 center = (Vector2)transform.position + new Vector2(dir * slashHitboxOffset.x, slashHitboxOffset.y);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, slashHitboxSize, 0f);

        foreach (var hit in hits)
        {
            if (hit == null || hit == bodyCollider || hit.transform.IsChildOf(transform)) continue;

            if (hit.CompareTag("Player"))
            {
                Health h = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
                if (h != null)
                {
                    h.TakeDamage(slashDamage);
                    try
                    {
                        HitFeedbackManager.TriggerHitFeedback(hit.transform, hit.transform.position, slashDamage, true, EnemyHitType.PhysicalMelee);
                    }
                    catch { }

                    Rigidbody2D pRb = hit.GetComponent<Rigidbody2D>() ?? hit.GetComponentInParent<Rigidbody2D>();
                    if (pRb != null)
                    {
                        pRb.linearVelocity = new Vector2(dir * playerKnockbackForce, playerKnockbackForce * 0.6f);
                    }
                    return true;
                }
            }
        }
        return false;
    }

    // ══════════════════════════════════════════════════════════════════
    //  DEFENSIVE PARRY / DEFLECTION (3/5)
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
        if (parryDeck.Count == 0 || parryDeckIndex >= parryDeck.Count)
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
        currentState = State.Parry;
        FacePlayer();

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetTrigger(AnimParry);
        }

        // Sword blade deflect flash & sparks
        Vector3 sparkPos = transform.position + new Vector3((isFacingRight ? 0.8f : -0.8f), 0.2f, 0f);
        for (int i = 0; i < 6; i++)
        {
            Vector2 sparkVel = new Vector2((isFacingRight ? -1f : 1f) * Random.Range(2f, 5f), Random.Range(-2f, 3f));
            StartCoroutine(AnimateParrySpark(sparkPos, sparkVel));
        }

        try { CameraShakeManager.Shake(0.08f, 0.08f); } catch { }

        // Defensive back-dash
        float backDir = isFacingRight ? -1f : 1f;
        rb.linearVelocity = new Vector2(backDir * 4.2f, 1.5f);
        yield return new WaitForSeconds(parryDuration);

        isParrying = false;
        currentState = State.EngageStance;
        stateTimer = 0.4f;
    }

    // ══════════════════════════════════════════════════════════════════
    //  IDAMAGEABLE & HEALTH
    // ══════════════════════════════════════════════════════════════════

    public void TakeDamage(int amount)
    {
        if (currentState == State.Dead) return;
        if (isParrying) return;

        if (ShouldParryAttack())
        {
            StartCoroutine(ExecuteStaffParryRoutine());
            return;
        }

        currentHealth -= amount;

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
            if (currentState == State.Idle || currentState == State.Patrol || currentState == State.CooldownPacing)
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
        yield return new WaitForSeconds(hitStunDuration);
        currentState = State.EngageStance;
        stateTimer = 0.3f;
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
    //  DIRECT ANIMATION ENGINE
    // ══════════════════════════════════════════════════════════════════

    private void DriveAnimationFrames()
    {
        if (currentState == State.BladeRush || currentState == State.Parry || currentState == State.Dead) return;

        bool isWalking = (currentState == State.Patrol && !isPatrolWaiting) ||
                         (currentState == State.EngageStance && Mathf.Abs(rb.linearVelocity.x) > 0.1f) ||
                         (currentState == State.CooldownPacing && Mathf.Abs(rb.linearVelocity.x) > 0.1f);

        bool isStance = (currentState == State.EngageStance && Mathf.Abs(rb.linearVelocity.x) <= 0.1f) ||
                        (currentState == State.CooldownPacing && Mathf.Abs(rb.linearVelocity.x) <= 0.1f);

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetBool(AnimIsWalking, isWalking);
            anim.SetBool(AnimIsStance, isStance);
        }

        // Direct frame-by-frame fallback
        Sprite[] activeArray = idleSprites;
        float fps = IDLE_FPS;

        if (isWalking && walkSprites != null && walkSprites.Length > 0)
        {
            activeArray = walkSprites;
            fps = WALK_FPS;
        }
        else if (isStance && stanceSprites != null && stanceSprites.Length > 0)
        {
            activeArray = stanceSprites;
            fps = STANCE_FPS;
        }

        if (activeArray == null || activeArray.Length == 0) return;

        animTimer += Time.deltaTime;
        if (animTimer >= 1f / fps)
        {
            animTimer -= 1f / fps;
            animIndex = (animIndex + 1) % activeArray.Length;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = activeArray[animIndex];
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

    // ══════════════════════════════════════════════════════════════════
    //  VFX PROCEDURAL SPRITES & ANIMATORS
    // ══════════════════════════════════════════════════════════════════

    private void EnsureVFXSprites()
    {
        if (slashArcSprite == null) slashArcSprite = GenerateSlashArcSprite(128, 64);
        if (sparkSprite == null) sparkSprite = GenerateSparkSprite(32);
    }

    private IEnumerator AnimateSlashArc(Vector3 pos, float dir)
    {
        GameObject arc = new GameObject("KatanaSlashArc");
        arc.transform.position = pos;
        arc.transform.localScale = new Vector3(dir > 0f ? 1.5f : -1.5f, 1.4f, 1f);

        SpriteRenderer sr = arc.AddComponent<SpriteRenderer>();
        sr.sprite = slashArcSprite;
        sr.color = bladeArcColor;
        sr.sortingOrder = 35;

        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (arc == null) yield break;

            arc.transform.localScale = new Vector3((dir > 0f ? 1.5f : -1.5f) * (1f + t * 0.4f), 1.4f * (1f + t * 0.3f), 1f);
            sr.color = new Color(bladeArcColor.r, bladeArcColor.g, bladeArcColor.b, (1f - t) * 0.95f);
            yield return null;
        }

        if (arc != null) Destroy(arc);
    }

    private IEnumerator AnimateParrySpark(Vector3 pos, Vector2 vel)
    {
        GameObject spark = new GameObject("SwordParrySpark");
        spark.transform.position = pos;
        spark.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);

        SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
        sr.sprite = sparkSprite;
        sr.color = Color.Lerp(Color.white, bladeArcColor, Random.value);
        sr.sortingOrder = 36;

        float elapsed = 0f;
        float lifetime = 0.20f;
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

    private static Sprite GenerateSlashArcSprite(int w, int h)
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
                float arc = Mathf.Sin(normX * Mathf.PI);
                float dist = Mathf.Abs(normY - arc * 0.8f);
                float alpha = Mathf.Clamp01(Mathf.Exp(-dist * dist * 35f) * normX);
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
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
            // Raw frames face RIGHT -> flipX = !right
            spriteRenderer.flipX = !right;
        }
    }

    public void EnsureSpritesLoaded()
    {
#if UNITY_EDITOR
        if (idleSprites == null || idleSprites.Length == 0)
            idleSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newidlestraw-838517e4");
        if (stanceSprites == null || stanceSprites.Length == 0)
            stanceSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newstancestraw-5fb4cdc3");
        if (walkSprites == null || walkSprites.Length == 0)
            walkSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newwalkstraw-9edabc38");
        if (attackSprites == null || attackSprites.Length == 0)
            attackSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newattackstraw-ba1be727");
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
        Vector2 center = (Vector2)transform.position + new Vector2(dir * slashHitboxOffset.x, slashHitboxOffset.y);
        Gizmos.DrawWireCube(center, slashHitboxSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
