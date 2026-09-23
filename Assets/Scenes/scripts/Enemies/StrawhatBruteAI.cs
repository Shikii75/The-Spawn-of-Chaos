using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;

/// <summary>
/// StrawhatBruteAI - Heavy Shadow Brute / Enforcer of the Strawhat Clan.
/// 
/// Visuals & Animations (Facing RIGHT natively -> spriteRenderer.flipX = !right):
/// - Idle: newfat-e5a73ac3 (28 frames, menacing breathing with billowing shadow tendrils)
/// - Walk: newfatwalk-5b766109 (15 frames, heavy lumbering footsteps)
/// - Attack: newfatattack-68451e5a (13 frames, devastating Kama / scythe cleave & ground quake)
/// 
/// Skills & Mechanics:
/// 1. Billowing Shadow Aura: Dark void motes continuously seep from his shadowy silhouette.
/// 2. Heavy Footsteps: Heavy stomping cadence with ground dust and micro-camera rumbles.
/// 3. Shadow Kama Cleave:
///    - Massive two-handed scythe swing with eye flare.
///    - High damage (30 dmg) and massive knockback (9.0 force).
///    - Ground Impact Tremor: Leaves an expanding dark tremor ring on the floor upon slamming down.
/// 4. Super Armor: Resistant to hit stun while actively swinging his heavy weapon.
/// 5. High Durability & Extra Loot: 180 HP tank, drops 4 collectible orbs.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class StrawhatBruteAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Idle,
        Patrol,
        Chase,
        HeavySlam,
        CooldownPacing,
        HitStun,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private State currentState = State.Idle;

    [Header("Health & Combat Stats")]
    public int maxHealth = 180;
    public int slamDamage = 30;
    public float playerKnockbackForce = 9.0f;
    public float hitStunDuration = 0.15f;

    [Header("Movement & Ranges")]
    public float detectionRange = 10f;
    public float attackRange = 3.8f;
    public float chaseSpeed = 3.0f;
    public float patrolSpeed = 1.6f;
    public float patrolDistance = 4.5f;
    public float attackCooldown = 2.8f;

    [Header("Hitbox Setup")]
    public Vector2 cleaveHitboxSize = new Vector2(3.6f, 3.4f);
    public Vector2 cleaveHitboxOffset = new Vector2(1.2f, -0.1f);

    [Header("VFX & Colors")]
    public Color shadowAuraColor = new Color(0.02f, 0.01f, 0.03f, 0.85f);
    public Color tremorColor = new Color(0.04f, 0.02f, 0.06f, 0.95f);
    public Color eyeGlowColor = new Color(1.0f, 0.2f, 0.85f, 1f);
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Loot")]
    public int droppedOrbsCount = 4;

    [Header("Configured Animation Sequences")]
    public Sprite[] idleSprites;
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
    private bool isSuperArmor = false;

    // Ambient Shadow Aura Timer
    private float auraSpawnTimer = 0f;
    private const float AURA_INTERVAL = 0.09f;

    // Direct Frame-by-Frame Animator
    private float animTimer = 0f;
    private int animIndex = 0;
    private Coroutine attackAnimRoutine;
    private const float IDLE_FPS = 12f;
    private const float WALK_FPS = 12f;

    // Animator Hashes
    private static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");

    // Procedural Sprites Cache
    private static Sprite tremorRingSprite;
    private static Sprite smokeSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        if (anim != null && anim.runtimeAnimatorController == null)
        {
#if UNITY_EDITOR
            anim.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Scenes/animations/animators/StrawhatBruteController.controller"
            );
#endif
            if (anim.runtimeAnimatorController == null)
            {
                anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("StrawhatBruteController");
            }
        }

        EnsureSpritesLoaded();

        // Enforce positive scale
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

        EnsureVFXSprites();
    }

    void Start()
    {
        FindPlayer();
        currentState = State.Idle;
        stateTimer = Random.Range(1.0f, 2.0f);
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
            case State.CooldownPacing:
                UpdateCooldownPacing();
                break;
        }

        // Ambient Shadow Aura billowing off brute
        UpdateAmbientShadowAura();

        // Direct animation fallback
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
            currentState = State.Chase;
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = State.Patrol;
            stateTimer = Random.Range(3.5f, 6.0f);
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
            patrolWaitTimer = Random.Range(0.8f, 1.5f);
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
            stateTimer = Random.Range(1.5f, 2.5f);
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

        // Ready to execute brutal scythe slam
        if (distToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            StartAction(ExecuteHeavyCleaveSequence());
            return;
        }

        // Lumbering march towards player
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
    }

    private void UpdateCooldownPacing()
    {
        FacePlayer();
        float distToPlayer = (player != null) ? Vector2.Distance(transform.position, player.position) : 99f;
        float dirToPlayer = (player != null && player.position.x > transform.position.x) ? 1f : -1f;

        if (distToPlayer < attackRange * 0.75f)
        {
            // Back away slowly
            rb.linearVelocity = new Vector2(-dirToPlayer * patrolSpeed * 0.7f, rb.linearVelocity.y);
        }
        else
        {
            // Slow pacing
            float paceDir = Mathf.Sin(Time.time * 2f) > 0f ? 1f : -1f;
            rb.linearVelocity = new Vector2(paceDir * patrolSpeed * 0.6f, rb.linearVelocity.y);
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            currentState = CanSeePlayer() ? State.Chase : State.Idle;
            stateTimer = 1.0f;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  COMBAT SKILL: SHADOW KAMA CLEAVE & GROUND QUAKE
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator ExecuteHeavyCleaveSequence()
    {
        currentState = State.HeavySlam;
        isSuperArmor = true;
        FacePlayer();

        float dir = isFacingRight ? 1f : -1f;

        // Trigger attack animation (13 frames)
        if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
        attackAnimRoutine = StartCoroutine(PlayAttackSpriteSequence(0.92f));

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetTrigger(AnimAttack);
        }

        // Phase 1: Heavy Windup & Eye Flare (frames 1-5, ~0.35s)
        rb.linearVelocity = Vector2.zero;
        SpawnWeaponChargeMotes();
        yield return new WaitForSeconds(0.35f);

        // Phase 2: Violent Downward Cleave (frames 6-8, ~0.20s)
        // Step forward with the heavy momentum
        rb.linearVelocity = new Vector2(dir * 3.5f, 0f);
        yield return new WaitForSeconds(0.18f);

        // Ground Impact moment
        rb.linearVelocity = Vector2.zero;

        // Violent screen shake
        try { CameraShakeManager.Shake(0.24f, 0.20f); } catch { }

        // Spawn Ground Tremor Shock Ring & Dark Impact Dust
        Vector3 impactPoint = transform.position + new Vector3(dir * 1.3f, -1.4f, 0f);
        StartCoroutine(AnimateGroundTremor(impactPoint));

        // Cleave Melee Hitbox check
        CheckCleaveHitbox(dir);

        // Phase 3: Heavy Weapon Recovery (frames 9-13, ~0.38s)
        yield return new WaitForSeconds(0.38f);

        isSuperArmor = false;
        lastAttackTime = Time.time;
        currentState = State.CooldownPacing;
    }

    private void CheckCleaveHitbox(float dir)
    {
        Vector2 center = (Vector2)transform.position + new Vector2(dir * cleaveHitboxOffset.x, cleaveHitboxOffset.y);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, cleaveHitboxSize, 0f);

        foreach (var hit in hits)
        {
            if (hit == null || hit == bodyCollider || hit.transform.IsChildOf(transform)) continue;

            if (hit.CompareTag("Player"))
            {
                Health h = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
                if (h != null)
                {
                    h.TakeDamage(slamDamage);
                    try
                    {
                        HitFeedbackManager.TriggerHitFeedback(hit.transform, hit.transform.position, slamDamage, true, EnemyHitType.PhysicalMelee);
                    }
                    catch { }

                    Rigidbody2D pRb = hit.GetComponent<Rigidbody2D>() ?? hit.GetComponentInParent<Rigidbody2D>();
                    if (pRb != null)
                    {
                        pRb.linearVelocity = new Vector2(dir * playerKnockbackForce, playerKnockbackForce * 0.7f);
                    }
                }
                break;
            }
        }
    }

    private void SpawnWeaponChargeMotes()
    {
        Vector3 weaponPos = transform.position + new Vector3(isFacingRight ? 0.6f : -0.6f, 1.2f, 0f);
        for (int i = 0; i < 4; i++)
        {
            SpawnShadowAuraPuff(weaponPos + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  IDAMAGEABLE & HEALTH
    // ══════════════════════════════════════════════════════════════════

    public void TakeDamage(int amount)
    {
        if (currentState == State.Dead) return;

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
            // Super Armor: Ignore hitstun while executing heavy cleave!
            if (!isSuperArmor && (currentState == State.Idle || currentState == State.Patrol || currentState == State.CooldownPacing))
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

        // Large death burst of shadow motes
        for (int i = 0; i < 8; i++)
        {
            SpawnShadowAuraPuff(transform.position + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.4f, 0.8f), 0f));
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (bodyCollider != null) bodyCollider.enabled = false;
        rb.linearVelocity = new Vector2(0f, 2.5f);

        float elapsed = 0f;
        float duration = 0.50f;
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
        if (currentState == State.HeavySlam || currentState == State.Dead) return;

        bool isWalking = (currentState == State.Patrol && !isPatrolWaiting) ||
                         (currentState == State.Chase) ||
                         (currentState == State.CooldownPacing && Mathf.Abs(rb.linearVelocity.x) > 0.1f);

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetBool(AnimIsWalking, isWalking);
        }

        Sprite[] activeArray = isWalking ? walkSprites : idleSprites;
        float fps = isWalking ? WALK_FPS : IDLE_FPS;

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
    //  VFX PROCEDURAL SPRITES & AMBIENT SHADOW AURA
    // ══════════════════════════════════════════════════════════════════

    private void UpdateAmbientShadowAura()
    {
        auraSpawnTimer += Time.deltaTime;
        if (auraSpawnTimer >= AURA_INTERVAL)
        {
            auraSpawnTimer -= AURA_INTERVAL;
            Vector3 spawnPos = transform.position + new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.2f, 1.2f),
                0f
            );
            SpawnShadowAuraPuff(spawnPos);
        }
    }

    private void SpawnShadowAuraPuff(Vector3 pos)
    {
        GameObject puff = new GameObject("BruteShadowWisp");
        puff.transform.position = pos;
        float initScale = Random.Range(0.25f, 0.5f);
        puff.transform.localScale = Vector3.one * initScale;

        SpriteRenderer sr = puff.AddComponent<SpriteRenderer>();
        sr.sprite = smokeSprite;
        sr.color = shadowAuraColor;
        sr.sortingOrder = 24;

        StartCoroutine(AnimateAuraPuff(puff, initScale, 0.38f));
    }

    private IEnumerator AnimateAuraPuff(GameObject puff, float startScale, float duration)
    {
        float t = 0f;
        SpriteRenderer sr = puff.GetComponent<SpriteRenderer>();
        Vector3 driftVelocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 0.8f), 0f);

        while (t < duration && puff != null)
        {
            t += Time.deltaTime;
            float norm = t / duration;

            puff.transform.position += driftVelocity * Time.deltaTime;
            puff.transform.localScale = Vector3.one * Mathf.Lerp(startScale, startScale * 1.35f, norm);

            if (sr != null)
            {
                sr.color = new Color(shadowAuraColor.r, shadowAuraColor.g, shadowAuraColor.b, (1f - norm) * shadowAuraColor.a);
            }
            yield return null;
        }

        if (puff != null) Destroy(puff);
    }

    private IEnumerator AnimateGroundTremor(Vector3 pos)
    {
        GameObject ring = new GameObject("BruteGroundTremor");
        ring.transform.position = pos;
        ring.transform.localScale = new Vector3(0.6f, 0.2f, 1f);

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = tremorRingSprite;
        sr.color = tremorColor;
        sr.sortingOrder = 25;

        float duration = 0.32f;
        float elapsed = 0f;
        Vector3 maxScale = new Vector3(4.8f, 1.0f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (ring == null) yield break;

            ring.transform.localScale = Vector3.Lerp(new Vector3(0.6f, 0.2f, 1f), maxScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            sr.color = new Color(tremorColor.r, tremorColor.g, tremorColor.b, (1f - t) * 0.95f);
            yield return null;
        }

        if (ring != null) Destroy(ring);
    }

    private void EnsureVFXSprites()
    {
        if (tremorRingSprite == null) tremorRingSprite = GenerateTremorRingSprite(64);
        if (smokeSprite == null) smokeSprite = GenerateSmokeSprite(32);
    }

    private static Sprite GenerateTremorRingSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float center = size * 0.5f;
        float radius = size * 0.44f;
        float halfThickness = size * 0.14f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float dist = Mathf.Abs(d - radius);
                if (dist <= halfThickness)
                {
                    float ring = Mathf.Exp(-dist * dist / (halfThickness * halfThickness * 0.4f));
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

    private static Sprite GenerateSmokeSprite(int size)
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
                float alpha = Mathf.Exp(-t * t * 3.5f);
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
            idleSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newfat-e5a73ac3");
        if (walkSprites == null || walkSprites.Length == 0)
            walkSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newfatwalk-5b766109");
        if (attackSprites == null || attackSprites.Length == 0)
            attackSprites = LoadEditorSprites("Assets/Scenes/animations/frames/newfatattack-68451e5a");
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
        float myExtentsX = (bodyCollider != null) ? bodyCollider.bounds.extents.x : 1.0f;
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
        Vector2 center = (Vector2)transform.position + new Vector2(dir * cleaveHitboxOffset.x, cleaveHitboxOffset.y);
        Gizmos.DrawWireCube(center, cleaveHitboxSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
