using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// TsuchigumoBossController - 3-Phase Combat AI for the Tsuchigumo Giant Spider Boss.
/// 
/// Behaviors:
/// 1. Stalks the player with dynamic spacing and directional facing.
/// 2. Telegraph Warning (tsubeforeanyattack) with glowing red/purple danger cue before every strike.
/// 3. Ground Slam (tsuSlam) with cave tremor shockwave + falling ceiling shadow boulders.
/// 4. Lunge Pounce (tsulungeattack) for mid-range airborne leap strikes.
/// 5. Heavy Charge Rush (tsustartcharge -> tsucharge):
///    - Dodging into a wall triggers Wall Crash Stun (tsuendcharge -> tsuknockdownstart -> tsuknockdownstay)
///      giving the player a 3-second vulnerable damage window.
/// 6. Phase Roar & Mob Summon (tsugrowl):
///    - Triggers at Phase 2 (70% HP) and Phase 3 (35% HP), summoning burrowing venom spiderlings.
/// 7. Cinematic Defeat:
///    - On 0 HP, collapses with knockdown animation, violet smoke dissolve, and massive loot drop.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class TsuchigumoBossController : MonoBehaviour, IDamageable
{
    public enum BossState
    {
        DisguisedWarrior,
        Idle,
        Stalking,
        Telegraphing,
        Lunging,
        GroundSlamming,
        StartCharge,
        Charging,
        EndCharge,
        KnockdownStart,
        KnockdownStay,
        Roaring,
        Dead
    }

    [Header("Disguise Settings")]
    [Tooltip("Whether Tsuchigumo starts in human samurai warrior disguise in the Pantheon.")]
    public bool startAsDisguisedWarrior = true;
    public Sprite disguisedWarriorSprite;
    private Sprite originalSpiderSprite;
    private RuntimeAnimatorController originalSpiderAnimController;
    private bool hasTransformed = false;

    [Header("Boss Identity & Health")]
    public string bossName = "Tsuchigumo, The Shape-Shifter";
    public int maxHealth = 450;
    [SerializeField] private int currentHealth;

    [Header("Current Phase")]
    public int currentPhase = 1; // 1: 100-70%, 2: 70-35%, 3: 35-0%
    public BossState currentState = BossState.Idle;

    [Header("Movement & Positioning")]
    public float walkSpeed = 3.2f;
    public float lungeSpeed = 12f;
    public float chargeSpeed = 15f;
    public float idealCombatDistance = 4.5f;

    [Header("Attack Settings")]
    public int meleeDamage = 25;
    public int slamDamage = 25;
    public int chargeDamage = 25;
    [Tooltip("Base rest time in seconds between attack executions (allows boss to idle, stalk, and pace).")]
    public float attackCooldown = 3.8f;
    public float telegraphDuration = 0.75f;
    public float knockdownStunDuration = 3.0f;

    [Header("Hazards & Summons")]
    public GameObject spiderlingPrefab;
    public int bouldersPerSlam = 3;
    public float boulderSpawnHeight = 10f;
    public int boulderDamage = 25;

    [Header("Visual Feedback & Components")]
    public Color telegraphGlowColor = new Color(0.95f, 0.2f, 0.35f, 1f);
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public Rigidbody2D rb;
    public Collider2D bodyCollider;

    [Header("Audio SFX")]
    public AudioClip roarSFX;
    public AudioClip slamSFX;
    public AudioClip chargeSFX;
    public AudioClip hitSFX;
    public AudioClip deathSFX;

    // Events
    public System.Action<int, int> onHealthChanged;
    public System.Action onBossDefeated;

    private Transform player;
    private bool hasTriggeredPhase2Roar = false;
    private bool hasTriggeredPhase3Roar = false;
    private float nextActionTime = 0f;
    private float initialFacingScaleX = 1f;
    private Coroutine activeAttackRoutine;
    private Vector3 startingPosition;
    private Color originalSpriteColor = Color.white;
    private int currentAnimHash = 0;

    // Animation State Hash Names
    private static readonly int AnimIdle = Animator.StringToHash("Idle");
    private static readonly int AnimWalk = Animator.StringToHash("Walk");
    private static readonly int AnimTelegraph = Animator.StringToHash("Telegraph");
    private static readonly int AnimLunge = Animator.StringToHash("LungeAttack");
    private static readonly int AnimSlam = Animator.StringToHash("Slam");
    private static readonly int AnimStartCharge = Animator.StringToHash("StartCharge");
    private static readonly int AnimCharge = Animator.StringToHash("Charge");
    private static readonly int AnimEndCharge = Animator.StringToHash("EndCharge");
    private static readonly int AnimKnockdownStart = Animator.StringToHash("KnockdownStart");
    private static readonly int AnimKnockdownStay = Animator.StringToHash("KnockdownStay");
    private static readonly int AnimGrowl = Animator.StringToHash("Growl");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (rb != null)
        {
            rb.mass = 50000f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            PhysicsMaterial2D noFriction = new PhysicsMaterial2D("TsuchigumoMaterial");
            noFriction.friction = 0f;
            noFriction.bounciness = 0f;
            rb.sharedMaterial = noFriction;
        }

        if (spriteRenderer != null)
        {
            originalSpriteColor = spriteRenderer.color;
        }

        initialFacingScaleX = Mathf.Abs(transform.localScale.x);
        if (initialFacingScaleX < 0.1f) initialFacingScaleX = 1f;
    }

    private void FixedUpdate()
    {
        // Prevent player from physically pushing the heavy boss when stationary or stunned
        if (currentState == BossState.Idle || currentState == BossState.KnockdownStay || currentState == BossState.Dead || currentState == BossState.Telegraphing)
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
    }

    private void EnsureDisguisedSpriteAssigned()
    {
        if (disguisedWarriorSprite == null)
        {
#if UNITY_EDITOR
            disguisedWarriorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Scenes/animations/frames/normalmalesamurai/normalmalesamuraistopandstep-45da2e11/frame_001.png"
            );
#endif
        }
    }

    private void Start()
    {
        currentHealth = maxHealth;
        startingPosition = transform.position;
        FindPlayer();
        EnsureDisguisedSpriteAssigned();

        if (startAsDisguisedWarrior && !hasTransformed)
        {
            currentState = BossState.DisguisedWarrior;
            if (spriteRenderer != null) originalSpiderSprite = spriteRenderer.sprite;
            if (animator != null)
            {
                originalSpiderAnimController = animator.runtimeAnimatorController;
                animator.enabled = false;
            }
            if (spriteRenderer != null && disguisedWarriorSprite != null)
            {
                spriteRenderer.sprite = disguisedWarriorSprite;
            }
            // Temporarily hide boss health bar while disguised
            BossHealthBar.Instance?.HideBossBar();
        }
        else
        {
            PlayAnimation(AnimIdle, force: true);
            nextActionTime = Time.time + Random.Range(2.5f, 4.0f);
        }
    }

    private void Update()
    {
        if (currentState == BossState.Dead) return;

        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        // Phase Evaluation
        UpdateCombatPhase();

        // State Loop
        switch (currentState)
        {
            case BossState.DisguisedWarrior:
                UpdateDisguisedWarrior();
                break;
            case BossState.Idle:
            case BossState.Stalking:
                UpdateStalking();
                break;
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    private void UpdateCombatPhase()
    {
        float hpPercent = (float)currentHealth / maxHealth;

        if (hpPercent <= 0.35f && !hasTriggeredPhase3Roar)
        {
            hasTriggeredPhase3Roar = true;
            currentPhase = 3;
            TriggerPhaseRoar(spiderlingCount: 3);
        }
        else if (hpPercent <= 0.70f && !hasTriggeredPhase2Roar)
        {
            hasTriggeredPhase2Roar = true;
            currentPhase = 2;
            TriggerPhaseRoar(spiderlingCount: 2);
        }
    }


    private void UpdateDisguisedWarrior()
    {
        if (player == null) return;

        FaceTarget(player.position);
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        // Player approaches close or attacks the disguised warrior
        if (distToPlayer <= 5.5f || currentHealth < maxHealth)
        {
            StartCoroutine(ExecuteTransformationRoutine());
        }
    }

    private IEnumerator ExecuteTransformationRoutine()
    {
        hasTransformed = true;
        currentState = BossState.Roaring;
        rb.linearVelocity = Vector2.zero;

        // Briefly freeze player movement for dramatic standoff
        move.ExternalMovementLock = true;

        // Violet warning glow
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.9f, 0.3f, 1f, 1f);
        }

        if (CameraShakeManager.Instance != null)
        {
            CameraShakeManager.Shake(1.2f, 0.8f);
        }

        yield return new WaitForSeconds(0.8f);

        // Restore spider visuals and animator
        if (animator != null)
        {
            animator.enabled = true;
            if (originalSpiderAnimController != null)
            {
                animator.runtimeAnimatorController = originalSpiderAnimController;
            }
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalSpriteColor;
            if (originalSpiderSprite != null)
            {
                spriteRenderer.sprite = originalSpiderSprite;
            }
        }

        // Show Boss Health Bar
        if (BossHealthBar.Instance != null)
        {
            Health h = GetComponent<Health>();
            if (h != null)
            {
                BossHealthBar.Instance.ShowBossBar(h, bossName);
            }
        }

        // Release player movement
        move.ExternalMovementLock = false;

        // Play initial menacing Growl/Roar and summon spiderlings
        TriggerPhaseRoar(spiderlingCount: 2);
    }

    private void UpdateStalking()
    {
        if (Time.time < nextActionTime)
        {
            // Gentle spacing / stalking
            float distToPlayer = Vector2.Distance(transform.position, player.position);
            FaceTarget(player.position);

            if (distToPlayer > idealCombatDistance + 1.2f)
            {
                // Actively walk closer
                Vector2 moveDir = (player.position.x > transform.position.x) ? Vector2.right : Vector2.left;
                rb.linearVelocity = new Vector2(moveDir.x * walkSpeed, rb.linearVelocity.y);
                PlayAnimation(AnimWalk);
                currentState = BossState.Stalking;
            }
            else if (distToPlayer < idealCombatDistance - 1.2f)
            {
                // Back up slightly with walk animation
                Vector2 moveDir = (player.position.x > transform.position.x) ? Vector2.left : Vector2.right;
                rb.linearVelocity = new Vector2(moveDir.x * (walkSpeed * 0.75f), rb.linearVelocity.y);
                PlayAnimation(AnimWalk);
                currentState = BossState.Stalking;
            }
            else
            {
                // In sweet spot -> idle breathing
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                PlayAnimation(AnimIdle);
                currentState = BossState.Idle;
            }
            return;
        }

        // Ready to attack! Choose best attack based on distance & probability
        DecideAndExecuteAttack();
    }

    private void DecideAndExecuteAttack()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);

        float roll = Random.value;

        // Long or Medium range (dist >= 4.5f):
        if (dist >= 5.0f)
        {
            // 50% Charge Rush, 35% Lunge Pounce, 15% Walk In + Slam
            if (roll < 0.50f)
            {
                activeAttackRoutine = StartCoroutine(ExecuteChargeAttackRoutine());
            }
            else if (roll < 0.85f)
            {
                activeAttackRoutine = StartCoroutine(ExecuteLungeAttackRoutine());
            }
            else
            {
                activeAttackRoutine = StartCoroutine(ExecuteGroundSlamRoutine());
            }
        }
        else // Close range (dist < 5.0f):
        {
            // 45% Ground Slam, 30% Lunge Pounce, 25% Back Up into Charge
            if (roll < 0.45f)
            {
                activeAttackRoutine = StartCoroutine(ExecuteGroundSlamRoutine());
            }
            else if (roll < 0.75f)
            {
                activeAttackRoutine = StartCoroutine(ExecuteLungeAttackRoutine());
            }
            else
            {
                activeAttackRoutine = StartCoroutine(ExecuteChargeAttackRoutine());
            }
        }
    }

    #region Attack Coroutines

    /// <summary>
    /// Telegraphs with glowing eye warning, then leaps forward into the player.
    /// </summary>
    private IEnumerator ExecuteLungeAttackRoutine()
    {
        currentState = BossState.Telegraphing;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        FaceTarget(player.position);

        yield return StartCoroutine(PlayTelegraphWarning(telegraphDuration));

        currentState = BossState.Lunging;
        PlayAnimation(AnimLunge);

        float dir = (player.position.x > transform.position.x) ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * lungeSpeed, 6.5f);

        float lungeTimer = 0f;
        bool hasHitPlayer = false;

        while (lungeTimer < 0.65f)
        {
            lungeTimer += Time.deltaTime;

            if (!hasHitPlayer && player != null)
            {
                float dist = Vector2.Distance(transform.position, player.position);
                if (dist < 2.2f)
                {
                    hasHitPlayer = true;
                    DamagePlayer(meleeDamage, dir * 8f);
                }
            }
            yield return null;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        PlayAnimation(AnimIdle);
        currentState = BossState.Idle;
        nextActionTime = Time.time + attackCooldown * (currentPhase == 3 ? 0.75f : 1.0f);
    }

    /// <summary>
    /// Smashes the ground with shockwave, triggering ceiling boulder rubble.
    /// </summary>
    private IEnumerator ExecuteGroundSlamRoutine()
    {
        currentState = BossState.Telegraphing;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        FaceTarget(player.position);

        yield return StartCoroutine(PlayTelegraphWarning(telegraphDuration * 0.9f));

        currentState = BossState.GroundSlamming;
        PlayAnimation(AnimSlam);

        // Wait for impact frame in animation (~0.6s)
        yield return new WaitForSeconds(0.6f);

        // Ground shockwave impact
        PlaySound(slamSFX);
        CameraShakeManager.Shake(0.35f, 0.18f);

        // Damage close-range player
        if (player != null && Vector2.Distance(transform.position, player.position) < 4.2f)
        {
            float dir = (player.position.x > transform.position.x) ? 1f : -1f;
            DamagePlayer(slamDamage, dir * 10f);
        }

        // Drop ceiling boulders
        SpawnCeilingBoulders();

        // Finish animation recovery
        yield return new WaitForSeconds(0.9f);

        PlayAnimation(AnimIdle);
        currentState = BossState.Idle;
        nextActionTime = Time.time + attackCooldown * (currentPhase == 3 ? 0.75f : 1.0f);
    }

    /// <summary>
    /// Rushes across the arena. Crashing into a wall triggers a 3-second vulnerable stun.
    /// </summary>
    private IEnumerator ExecuteChargeAttackRoutine()
    {
        currentState = BossState.StartCharge;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        FaceTarget(player.position);

        PlayAnimation(AnimStartCharge);
        yield return StartCoroutine(PlayTelegraphWarning(0.7f));

        currentState = BossState.Charging;
        PlayAnimation(AnimCharge);
        PlaySound(chargeSFX);

        float dir = (player.position.x > transform.position.x) ? 1f : -1f;
        float chargeTime = 0f;
        float maxChargeDuration = 2.2f;
        isChargeBlockedByWall = false;
        bool hitWall = false;

        while (chargeTime < maxChargeDuration)
        {
            chargeTime += Time.deltaTime;
            rb.linearVelocity = new Vector2(dir * chargeSpeed, rb.linearVelocity.y);

            // Check if player is hit
            if (player != null && Vector2.Distance(transform.position, player.position) < 2.8f)
            {
                DamagePlayer(chargeDamage, dir * 12f);
            }

            if (isChargeBlockedByWall)
            {
                hitWall = true;
                break;
            }

            // Check if hit wall or obstacle
            RaycastHit2D wallCheck = Physics2D.Raycast(transform.position, Vector2.right * dir, 2.5f, ~LayerMask.GetMask("Player", "Ignore Raycast"));
            if (wallCheck.collider != null && !wallCheck.collider.isTrigger && !wallCheck.collider.CompareTag("enemy") && wallCheck.collider.gameObject != gameObject)
            {
                hitWall = true;
                break;
            }

            yield return null;
        }

        if (hitWall)
        {
            // Wall Crash Stun sequence!
            yield return StartCoroutine(ExecuteWallCrashStun());
        }
        else
        {
            // Smooth skidding stop
            currentState = BossState.EndCharge;
            PlayAnimation(AnimEndCharge);
            rb.linearVelocity = new Vector2(dir * (chargeSpeed * 0.3f), rb.linearVelocity.y);
            yield return new WaitForSeconds(0.6f);

            PlayAnimation(AnimIdle);
            currentState = BossState.Idle;
            nextActionTime = Time.time + attackCooldown;
        }
    }

    /// <summary>
    /// Wall crash stumble and 3-second vulnerable opening for the player.
    /// </summary>
    private IEnumerator ExecuteWallCrashStun()
    {
        currentState = BossState.KnockdownStart;
        rb.linearVelocity = Vector2.zero;

        CameraShakeManager.Shake(0.4f, 0.2f);

        PlayAnimation(AnimEndCharge);
        yield return new WaitForSeconds(0.3f);

        PlayAnimation(AnimKnockdownStart);
        yield return new WaitForSeconds(1.2f);

        // Enter sustained vulnerable state
        currentState = BossState.KnockdownStay;
        PlayAnimation(AnimKnockdownStay);

        // Flash vulnerable outline / visual
        float stunElapsed = 0f;
        while (stunElapsed < knockdownStunDuration)
        {
            stunElapsed += Time.deltaTime;
            if (spriteRenderer != null)
            {
                float pulse = 0.7f + Mathf.Sin(stunElapsed * 10f) * 0.3f;
                spriteRenderer.color = Color.Lerp(Color.yellow, Color.white, pulse);
            }
            yield return null;
        }

        if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;

        // Recovery
        PlayAnimation(AnimIdle);
        currentState = BossState.Idle;
        nextActionTime = Time.time + 1.0f;
    }

    /// <summary>
    /// Roars to mark a phase change and summons burrowing venom spiderlings.
    /// </summary>
    public void TriggerPhaseRoar(int spiderlingCount)
    {
        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        activeAttackRoutine = StartCoroutine(ExecuteRoarRoutine(spiderlingCount));
    }

    private IEnumerator ExecuteRoarRoutine(int spiderlingCount)
    {
        currentState = BossState.Roaring;
        rb.linearVelocity = Vector2.zero;

        PlayAnimation(AnimGrowl);
        PlaySound(roarSFX);

        CameraShakeManager.Shake(0.5f, 0.25f);

        yield return new WaitForSeconds(0.4f);

        // Spawn venom spiderlings around the arena
        SpawnSpiderlings(spiderlingCount);

        yield return new WaitForSeconds(0.8f);

        PlayAnimation(AnimIdle);
        currentState = BossState.Idle;
        nextActionTime = Time.time + 1.2f;
    }

    private IEnumerator PlayTelegraphWarning(float duration)
    {
        PlayAnimation(AnimTelegraph);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (spriteRenderer != null)
            {
                float flash = Mathf.PingPong(elapsed * 8f, 1f);
                spriteRenderer.color = Color.Lerp(originalSpriteColor, telegraphGlowColor, flash);
            }
            yield return null;
        }

        if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;
    }

    #endregion

    #region Hazards & Spawns

    private void SpawnCeilingBoulders()
    {
        int count = (currentPhase == 3) ? bouldersPerSlam + 1 : bouldersPerSlam;

        for (int i = 0; i < count; i++)
        {
            Vector2 targetPos;
            if (i == 0 && player != null)
            {
                // Direct player targeting
                targetPos = new Vector2(player.position.x + Random.Range(-0.8f, 0.8f), transform.position.y);
            }
            else
            {
                // Spread across arena
                float offsetX = (i == 1) ? Random.Range(3f, 7f) : Random.Range(-7f, -3f);
                targetPos = new Vector2(transform.position.x + offsetX, transform.position.y);
            }

            FallingBoulder.Spawn(targetPos, spawnHeight: boulderSpawnHeight, boulderDamage: boulderDamage);
        }
    }

    private void SpawnSpiderlings(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float offsetX = (i - (count - 1) * 0.5f) * 2.5f;
            Vector3 spawnPos = new Vector3(transform.position.x + offsetX, transform.position.y - 0.5f, 0f);

            GameObject spiderObj = null;
            if (spiderlingPrefab != null)
            {
                spiderObj = Instantiate(spiderlingPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Fallback runtime spawn
                GameObject fallbackPrefab = Resources.Load<GameObject>("Prefabs/Spiderling");
                if (fallbackPrefab != null)
                {
                    spiderObj = Instantiate(fallbackPrefab, spawnPos, Quaternion.identity);
                }
            }

            if (spiderObj != null)
            {
                EnemySpawnFX fx = spiderObj.GetComponent<EnemySpawnFX>();
                if (fx == null) fx = spiderObj.AddComponent<EnemySpawnFX>();
                fx.spawnStyle = EnemySpawnFX.SpawnStyle.GroundRise;
            }
        }
    }

    [Header("Hitstop & Pure White Flash")]
    public float hitstopDuration = 0.75f;
    private Coroutine hitstopCoroutine;

    #endregion

    #region Damage & IDamageable

    public void TakeDamage(int damage)
    {
        if (currentState == BossState.Dead) return;

        // Knockdown vulnerability: Takes 1.5x damage during stun!
        if (currentState == BossState.KnockdownStay)
        {
            damage = Mathf.RoundToInt(damage * 1.5f);
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        onHealthChanged?.Invoke(currentHealth, maxHealth);

        // Feedback
        HitFeedbackManager.TriggerHitFeedback(transform, transform.position, damage, damage >= 25, EnemyHitType.SpiderVenom);
        SpawnOfChaos.Systems.ImpactFrameFX.Trigger(transform.position, 0.08f, isNegativeInversion: true);
        PlaySound(hitSFX);

        // Trigger pure white flash and hitstop freeze
        if (currentHealth > 0)
        {
            if (hitstopCoroutine != null) StopCoroutine(hitstopCoroutine);
            hitstopCoroutine = StartCoroutine(ExecuteHitstopAndWhiteFlash(hitstopDuration));
        }
        else
        {
            Die();
        }
    }

    private IEnumerator ExecuteHitstopAndWhiteFlash(float duration)
    {
        float prevAnimSpeed = (animator != null) ? animator.speed : 1f;
        if (animator != null) animator.speed = 0f;

        // Flash pure white
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            SpriteJuice sj = GetComponent<SpriteJuice>();
            if (sj != null) sj.FlashWhite();
        }

        yield return new WaitForSeconds(duration);

        if (animator != null && currentState != BossState.Dead)
        {
            animator.speed = (currentPhase == 3) ? 1.25f : 1.0f;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalSpriteColor;
        }
        hitstopCoroutine = null;
    }

    private void DamagePlayer(int damage, float knockbackX)
    {
        if (player == null) return;
        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }

        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = new Vector2(knockbackX, 5f);
        }
    }

    private void Die()
    {
        if (currentState == BossState.Dead) return;
        currentState = BossState.Dead;

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        if (hitstopCoroutine != null) StopCoroutine(hitstopCoroutine);
        StartCoroutine(ExecuteDeathSequence());
    }

    private IEnumerator ExecuteDeathSequence()
    {
        if (animator != null) animator.speed = 1f;

        // Remove and disable all colliders so player can walk freely through the defeated boss
        if (bodyCollider != null) bodyCollider.enabled = false;
        Collider2D[] allCols = GetComponentsInChildren<Collider2D>();
        foreach (var c in allCols) c.enabled = false;

        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;

        PlayAnimation(AnimKnockdownStart, force: true);
        PlaySound(deathSFX);

        // Slow motion impact
        Time.timeScale = 0.4f;
        yield return new WaitForSecondsRealtime(0.6f);
        Time.timeScale = 1.0f;

        // Spawn massive loot & EXP cluster
        OrbSpawner.SpawnLootCluster(transform.position, 14);

        // Violet Smoke & Dissolve Particle Burst
        CreateDeathSmokeFX();

        // Wait for KnockdownStart animation to complete
        yield return new WaitForSeconds(2.0f);

        // Transition to KnockdownStay and stay permanently defeated on the ground!
        currentState = BossState.Dead;
        PlayAnimation(AnimKnockdownStay, force: true);

        onBossDefeated?.Invoke();
    }

    private void CreateDeathSmokeFX()
    {
        GameObject fx = new GameObject("BossDeathSmokeFX");
        fx.transform.position = transform.position;

        ParticleSystem ps = fx.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.2f;
        main.startSpeed = 5f;
        main.startSize = 1.2f;
        main.startColor = new Color(0.7f, 0.2f, 0.95f, 0.85f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.5f;

        ps.Play();
    }

    private bool isChargeBlockedByWall = false;
    private float lastContactDamageTime = 0f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == BossState.Charging)
        {
            if (!collision.gameObject.CompareTag("Player") && !collision.gameObject.CompareTag("enemy") && !collision.collider.isTrigger)
            {
                // Hit a solid wall / ground obstacle during charge!
                isChargeBlockedByWall = true;
            }
        }
        HandleCombatContact(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleCombatContact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCombatContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleCombatContact(other);
    }

    private void HandleCombatContact(Collider2D other)
    {
        if (currentState == BossState.Dead) return;

        if (other != null && other.CompareTag("Player") && Time.time - lastContactDamageTime >= 0.45f)
        {
            float dir = (other.transform.position.x > transform.position.x) ? 1f : -1f;

            if (currentState == BossState.Charging)
            {
                lastContactDamageTime = Time.time;
                DamagePlayer(chargeDamage, dir * 14f);
            }
            else if (currentState == BossState.Lunging)
            {
                lastContactDamageTime = Time.time;
                DamagePlayer(meleeDamage, dir * 10f);
            }
            else if (currentState == BossState.GroundSlamming)
            {
                lastContactDamageTime = Time.time;
                DamagePlayer(slamDamage, dir * 10f);
            }
        }
    }

    #endregion

    #region Helpers

    private void PlayAnimation(int stateHash, bool force = false)
    {
        if (animator != null && animator.isActiveAndEnabled)
        {
            if (currentAnimHash == stateHash && !force) return;
            currentAnimHash = stateHash;
            animator.Play(stateHash, 0, 0f);
        }
    }

    private void FaceTarget(Vector3 targetPos)
    {
        float diff = targetPos.x - transform.position.x;
        if (Mathf.Abs(diff) > 0.2f)
        {
            float sign = Mathf.Sign(diff);
            transform.localScale = new Vector3(-sign * initialFacingScaleX, transform.localScale.y, transform.localScale.z);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXAtPosition(clip, transform.position);
        }
    }

    #endregion
}
