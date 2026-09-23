using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// MageCombat - Player Melee & Magic Combat Engine.
/// Guarantees that attack animations play completely at their native Animation Window frame rates
/// without mid-clip interruptions by using animator.Play() for instant state transitions
/// (no cross-fade blending which corrupts sprite animation timing).
///
/// Features:
/// 1. Spamming 'J' twice queues the second hit ('followupAttack') to execute seamlessly
///    ONLY AFTER the first attack animation completes.
/// 2. Pressing 'J' towards the end or right after the first attack completes seamlessly chains into the follow-up hit.
/// 3. Exposes IsAttacking so movement scripts can suppress walk/run/idle animation overrides during attacks.
/// </summary>
public class MageCombat : MonoBehaviour
{
    [Header("Mage Combat Voice")]
    [Tooltip("Audio clips for Mage attacking.")]
    public AudioClip[] attackVoiceClips;
    private AudioSource combatVoiceSource;

    public static MageCombat Instance { get; private set; }

    [Header("Melee Settings")]
    public int meleeDamage = 20;
    public int secondHitDamage = 28;
    public Collider2D meleeAttackCollider;
    public float meleeAttackDuration = 0.25f;
    public float comboWindowDuration = 1.85f;
    [Tooltip("Maximum delay between J taps to trigger the second attack (follow-up hit).")]
    public float doubleTapThreshold = 0.35f;
    private float lastJTapTime = -10f; // Window after Attack 1 during which 2nd hit can be chained

    [Header("Ranged Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float rangedCooldown = 0.5f;
    public bool isProjectileUnlocked = false;

    [Header("Mana Settings")]
    public float maxMana = 100f;
    public float currentMana;
    public float manaRegenRate = 0f;
    public float projectileManaCost = 25f;

    private Animator animator;
    private float nextRangedTime;

    // Combo & Queue State
    private int comboStep = 0; // 0 = Idle, 1 = Spear Crescent Slash, 2 = Spear Rising Slash, 3 = Sonic Thrust Finisher
    private float comboTimer = 0f;
    private bool secondHitQueued = false;
    private float attack1StartTime = -10f;
    private float secondHitStartTime = -10f;
    private float stepStartTime = -10f;

    // Cached clip durations (resolved once at startup to avoid per-frame lookups)
    private float attack1ClipDuration = 0.45f;
    private float followupClipDuration = 1.716f;

    // Attack state hash cache
    private int hashAttack;
    private int hashFollowupAttack;
    private int hashSecondHit;

    [Header("Combo Cooldown & Expanded Combat Settings")]
    public float comboRecoveryCooldown = 0.40f;
    private float comboRecoveryTimer = 0f;
    public bool IsInRecoveryCooldown => comboRecoveryTimer > 0f;

    // Aerial 2-Hit Combo State
    private int airComboStep = 0; // 0 = Idle, 1 = Air Strike 1 (Hover Damped), 2 = Air Strike 2 (Rising Finisher)
    private float airComboTimer = 0f;
    public int AirComboStep => airComboStep;
    public float AirComboTimer => airComboTimer;
    public bool IsAirborne => move.Instance != null && !move.Instance.IsGrounded;

    // Post-Dash Heavy Single Strike State
    private bool isPostDashStriking = false;
    public bool IsPostDashStriking => isPostDashStriking;

    private bool isFinisherActive = false;
    private int currentAttackDamage = 20;

    /// <summary>
    /// True while any melee attack animation is actively playing.
    /// Used by move.cs to suppress walk/run/idle animation overrides.
    /// </summary>
    public bool IsAttacking
    {
        get { return (comboStep > 0 || airComboStep > 0 || isPostDashStriking || isFinisherActive) && (Time.time - stepStartTime < 0.35f); }
    }

    void Awake()
    {
        SpawnOfChaos.Entities.PlayerMageVoiceController.EnsureAttached(gameObject);
        if (Instance == null)
        {
            Instance = this;
        }

        animator = GetComponent<Animator>();
        manaRegenRate = 0f;
        currentMana = maxMana;

        if (GetComponent<PlayerCombatJuice>() == null)
        {
            gameObject.AddComponent<PlayerCombatJuice>();
        }

        if (GetComponent<SpearSlashVFX>() == null)
        {
            gameObject.AddComponent<SpearSlashVFX>();
        }

        if (meleeAttackCollider == null)
        {
            Transform hb = transform.Find("hitbox") ?? transform.Find("Hitbox");
            if (hb != null) meleeAttackCollider = hb.GetComponent<Collider2D>();
        }
        if (meleeAttackCollider != null)
        {
            meleeAttackCollider.enabled = false;
        }

        // Pre-cache state name hashes for efficient comparison
        hashAttack = Animator.StringToHash("Attack");
        hashFollowupAttack = Animator.StringToHash("followupAttack");
        hashSecondHit = Animator.StringToHash("secondhit");
    }

    void Start()
    {
        InitCombatVoice();
        // Resolve actual clip durations from the animator controller at startup
        // so we don't rely on potentially stale GetCurrentAnimatorClipInfo during transitions
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == "Attack")
                {
                    attack1ClipDuration = clip.length;
                }
                else if (clip.name == "followupAttack")
                {
                    followupClipDuration = clip.length;
                }
            }
        }
    }

    void Update()
    {
        // 1. Tick down recovery cooldown and combo timers every frame
        if (comboRecoveryTimer > 0f)
        {
            comboRecoveryTimer -= Time.deltaTime;
        }

        if (airComboStep > 0)
        {
            airComboTimer -= Time.deltaTime;
            if (airComboTimer <= 0f)
            {
                ResetAirCombo();
            }
        }

        // Landing on ground resets aerial combo if not actively attacking
        if (move.Instance != null && move.Instance.IsGrounded && airComboStep > 0 && !IsAttacking)
        {
            ResetAirCombo();
        }

        if (isPostDashStriking && Time.time - stepStartTime >= 0.35f)
        {
            isPostDashStriking = false;
        }

        if (isFinisherActive && Time.time - stepStartTime >= 0.35f)
        {
            isFinisherActive = false;
        }

        if (HUDManager.IsInMainMenu()) return;

        // Check if game is paused or UI is active
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return;
        if (move.ExternalMovementLock || SpeechBubbleDialogue.IsAnyDialogueOpen) return;
        if (NyxarisManager.IsChatActive) return;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return;

        // R7 Component 3: Strict Combat Lockout While in Blob Form or Dashing
        if (move.Instance != null && (move.Instance.IsDashing || move.Instance.IsBlobForm)) return;

        // Manage ground combo window expiration
        if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                ResetCombo();
            }
        }

        // Melee Attack Input (J Key or Left Mouse Click)
        bool attackPressed = Input.GetKeyDown(KeyCode.J) ||
            (Input.GetMouseButtonDown(0) && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()));
        if (attackPressed)
        {
            ExecuteMeleeAttack();
        }

        // 4. Ranged Attack (K key, if unlocked)
        if (Input.GetKeyDown(KeyCode.K) && isProjectileUnlocked && Time.time >= nextRangedTime)
        {
            if (move.Instance != null && (move.Instance.IsDashing || move.Instance.IsBlobForm)) return;
            if (currentMana >= projectileManaCost)
            {
                PerformRangedAttack();
            }
            else
            {
                Debug.Log("Not enough mana for magic projectile!");
            }
        }
    }

    private bool IsAttack1Playing()
    {
        if (animator == null) return false;
        float elapsed = Time.time - attack1StartTime;

        // Primary check: has enough real time passed for the clip to have finished?
        if (elapsed < attack1ClipDuration) return true;

        // Secondary check: is the animator still in the Attack state?
        // (catches edge cases where the clip plays slightly longer due to frame timing)
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == hashAttack && state.normalizedTime < 1.0f)
            return true;

        return false;
    }

    private bool IsSecondHitPlaying()
    {
        float elapsed = Time.time - secondHitStartTime;
        return comboStep == 2 && elapsed < 0.35f;
    }

    /// <summary>
    /// Resets ALL attack-related triggers to prevent queued trigger buildup
    /// that causes duplicate/glitchy transitions.
    /// </summary>
    private void ResetAllAttackTriggers()
    {
        if (animator == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger)
            {
                string n = p.name;
                if (n == "Attack" || n == "attack" ||
                    n == "Attack2" || n == "attack2" ||
                    n == "followattack" || n == "FollowAttack")
                {
                    animator.ResetTrigger(p.name);
                }
            }
        }
    }

    /// <summary>
    /// Executes a dynamic 3-hit spear and swordsmanship combo with GPU graphics slash VFX and mage vocal effort.
    /// Step 1: Sweeping horizontal crescent slash.
    /// Step 2: Ascending reverse diagonal upper cut.
    /// Step 3: Sonic Piercing Thrust & Finisher Blast.
    /// </summary>
    public float GetCurrentFacing()
    {
        if (move.Instance != null && Mathf.Abs(move.Instance.LastFacingSign) > 0.1f)
        {
            return (move.Instance.LastFacingSign < 0f) ? -1f : 1f;
        }
        return (transform.localScale.x < 0f) ? -1f : 1f;
    }

    /// <summary>
    /// Unified melee attack input handler supporting:
    /// 1. Post-Dash Heavy Single Strike (R7 Component 1)
    /// 2. Aerial 2-Hit Combo with mid-air buoyancy and rising finisher (R7 Component 2)
    /// 3. Ground 3-Hit Spear Swordsmanship Combo
    /// Strict lockout enforced during recovery cooldown and Blob Form (R7 Components 3 & 4).
    /// </summary>
    public void ExecuteMeleeAttack(float? overrideFacing = null)
    {
        if (HUDManager.IsInMainMenu()) return;
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return;
        if (NyxarisManager.IsChatActive) return;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return;

        // R7 Component 3: Strict Combat Lockout While in Blob Form or Dashing
        if (move.Instance != null && (move.Instance.IsDashing || move.Instance.IsBlobForm)) return;

        // R7 Component 4: End-of-Combo Recovery Cooldown Lockout
        if (comboRecoveryTimer > 0f) return;

        float facing = overrideFacing ?? GetCurrentFacing();
        if (overrideFacing == null)
        {
            float h = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(h) > 0.1f)
            {
                facing = Mathf.Sign(h);
                if (move.Instance != null)
                {
                    move.Instance.lastFacingSign = facing;
                    move.Instance.FaceTarget(transform.position + new Vector3(facing * 5f, 0f, 0f));
                }
            }
        }

        // R7 Component 1: Post-Dash Heavy Single Strike
        if (move.Instance != null && move.Instance.IsInPostDashWindow && comboRecoveryTimer <= 0f)
        {
            if (move.Instance.TryConsumePostDashStrike())
            {
                PerformPostDashHeavyStrike(facing);
                return;
            }
        }

        // R7 Component 2: Aerial 2-Hit Combo
        bool isAirborne = move.Instance != null && !move.Instance.IsGrounded;
        if (isAirborne)
        {
            if (airComboStep == 0)
            {
                PerformAirComboHit1(facing);
            }
            else if (airComboStep == 1 && airComboTimer > 0f)
            {
                PerformAirComboHit2(facing);
            }
            else
            {
                PerformAirComboHit1(facing);
            }
            return;
        }

        // Ground 3-Hit Spear Swordsmanship Combo
        if (comboStep == 0)
        {
            PerformSpearComboStep(1, facing);
        }
        else if (comboStep == 1)
        {
            PerformSpearComboStep(2, facing);
        }
        else if (comboStep == 2)
        {
            PerformSpearComboStep(3, facing);
        }
        else
        {
            PerformSpearComboStep(1, facing);
        }
    }

    /// <summary>
    /// R7 Component 1: Post-Dash Heavy Single Strike.
    /// Consumes post-dash window, executes forward kinetic impulse (facing * 6.5f),
    /// deals 75 heavy damage, plays attack animation, thrust VFX, screen shake, impact frame,
    /// and triggers the end-of-combo recovery cooldown (0.40f).
    /// </summary>
    public void PerformPostDashHeavyStrike(float facing)
    {
        ResetCombo();
        ResetAirCombo();
        isPostDashStriking = true;
        stepStartTime = Time.time;

        if (move.Instance != null)
        {
            move.Instance.lastFacingSign = facing;
            move.Instance.FaceTarget(transform.position + new Vector3(facing * 5f, 0f, 0f));
            move.Instance.ResetPlayerScaleToNormal();
        }

        PlayRandomAttackVoice();

        // Forward kinetic impulse (facing * 6.5f)
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(facing * 6.5f, rb.linearVelocity.y);
        }

        // High-damage physical spear thrust (75 damage)
        if (LumiSpearWeapon.Instance != null)
        {
            LumiSpearWeapon.Instance.ExecuteMeleeSpearThrust(facing);
        }
        else
        {
            SpearSlashVFX slashVfx = SpearSlashVFX.GetOrCreate(transform);
            if (slashVfx != null)
            {
                slashVfx.SpawnThrustFinisher(transform.position, facing, 7.0f);
            }
        }

        // Screen shake and impact frame feedback
        SpawnOfChaos.Systems.ImpactFrameFX.Trigger(transform.position + new Vector3(facing * 2f, 0f, 0f), 0.06f, false);
        SpearSlashVFX.TryShake(0.18f, 0.35f);

        hitsThisSwing.Clear();
        if (animator != null)
        {
            ResetAllAttackTriggers();
            if (animator.HasState(0, Animator.StringToHash("Attack")))
            {
                animator.Play("Attack", 0, 0f);
            }
            else if (animator.HasState(0, Animator.StringToHash("attack")))
            {
                animator.Play("attack", 0, 0f);
            }
        }

        EnableMeleeCollider(75);

        // R7 Component 4: Trigger end-of-combo recovery cooldown (0.40f)
        comboRecoveryTimer = comboRecoveryCooldown;
    }

    /// <summary>
    /// R7 Component 2: Aerial Combo Hit 1.
    /// Deals 40 damage, applies air hover/fall-damping to preserve aerial feel
    /// (rb.linearVelocity = new Vector2(facing * 3.5f, Mathf.Max(rb.linearVelocity.y * 0.35f, 1.8f))),
    /// plays animation and slash VFX, and opens the 0.65s follow-up window.
    /// </summary>
    public void PerformAirComboHit1(float facing)
    {
        ResetCombo();
        airComboStep = 1;
        airComboTimer = 0.65f;
        stepStartTime = Time.time;

        if (move.Instance != null)
        {
            move.Instance.lastFacingSign = facing;
            move.Instance.FaceTarget(transform.position + new Vector3(facing * 5f, 0f, 0f));
            move.Instance.ResetPlayerScaleToNormal();
        }

        PlayRandomAttackVoice();

        // Air hover & fall-damping
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(facing * 3.5f, Mathf.Max(rb.linearVelocity.y * 0.35f, 1.8f));
        }

        // Melee spear slash 1 (40 damage)
        if (LumiSpearWeapon.Instance != null)
        {
            LumiSpearWeapon.Instance.ExecuteMeleeSpearSlash1(facing);
        }

        SpearSlashVFX slashVfx = SpearSlashVFX.GetOrCreate(transform);
        if (slashVfx != null)
        {
            slashVfx.BeginComboSlash(1, facing, transform.position);
        }

        hitsThisSwing.Clear();
        if (animator != null)
        {
            ResetAllAttackTriggers();
            if (animator.HasState(0, Animator.StringToHash("Attack")))
            {
                animator.Play("Attack", 0, 0f);
            }
            else if (animator.HasState(0, Animator.StringToHash("attack")))
            {
                animator.Play("attack", 0, 0f);
            }
        }

        EnableMeleeCollider(40);
    }

    /// <summary>
    /// R7 Component 2: Aerial Combo Hit 2 (Rising Finisher).
    /// Deals 55 damage, applies upward lift (rb.linearVelocity = new Vector2(facing * 4.2f, 2.6f)),
    /// plays finisher animation and VFX, concludes the air combo, and triggers end-of-combo recovery cooldown (0.45f).
    /// </summary>
    public void PerformAirComboHit2(float facing)
    {
        airComboStep = 2;
        stepStartTime = Time.time;

        if (move.Instance != null)
        {
            move.Instance.lastFacingSign = facing;
            move.Instance.FaceTarget(transform.position + new Vector3(facing * 5f, 0f, 0f));
            move.Instance.ResetPlayerScaleToNormal();
        }

        PlayRandomAttackVoice();

        // Upward lift for rising finisher
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(facing * 4.2f, 2.6f);
        }

        // Melee spear slash 2 (55 damage)
        if (LumiSpearWeapon.Instance != null)
        {
            LumiSpearWeapon.Instance.ExecuteMeleeSpearSlash2(facing);
        }

        SpearSlashVFX slashVfx = SpearSlashVFX.GetOrCreate(transform);
        if (slashVfx != null)
        {
            slashVfx.BeginComboSlash(2, facing, transform.position);
        }

        hitsThisSwing.Clear();
        if (animator != null)
        {
            ResetAllAttackTriggers();
            if (animator.HasState(0, Animator.StringToHash("Attack")))
            {
                animator.Play("Attack", 0, 0f);
            }
            else if (animator.HasState(0, Animator.StringToHash("attack")))
            {
                animator.Play("attack", 0, 0f);
            }
        }

        EnableMeleeCollider(55);

        // Conclude air combo and trigger recovery cooldown (0.45f)
        ResetAirCombo();
        comboRecoveryTimer = 0.45f;
    }

    public void PerformSpearComboStep(int step, float facing)
    {
        stepStartTime = Time.time;

        if (step == 3)
        {
            comboStep = 0;
            comboTimer = 0f;
            isFinisherActive = true;
            // R7 Component 4: Trigger end-of-combo recovery cooldown (0.40f)
            comboRecoveryTimer = comboRecoveryCooldown;
        }
        else
        {
            comboStep = step;
            comboTimer = 0.75f; // Generous combo window to chain strikes
            isFinisherActive = false;
        }

        if (move.Instance != null)
        {
            move.Instance.ResetPlayerScaleToNormal();
        }
        // Attack sound when attacking
        PlayRandomAttackVoice();

        // Apply swordsmanship forward micro-lunge for satisfying momentum
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float lunge = (step == 3) ? 4.2f : (step == 2 ? 3.0f : 2.4f);
            rb.linearVelocity = new Vector2(facing * lunge, rb.linearVelocity.y);
        }

        // 1. Old slash arc VFX removed in favor of stunning dynamic Ghost Spear afterimage trail graphics

        // 2. Physical Spear Swordsmanship Strike
        if (LumiSpearWeapon.Instance != null)
        {
            if (step == 1)
            {
                LumiSpearWeapon.Instance.ExecuteMeleeSpearSlash1(facing);
            }
            else if (step == 2)
            {
                LumiSpearWeapon.Instance.ExecuteMeleeSpearSlash2(facing);
            }
            else
            {
                LumiSpearWeapon.Instance.ExecuteMeleeSpearThrust(facing);
            }
        }

        // 3. Player Character Attack Pose (no locking)
        hitsThisSwing.Clear();
        if (animator != null)
        {
            ResetAllAttackTriggers();
            if (animator.HasState(0, Animator.StringToHash("Attack")))
            {
                animator.Play("Attack", 0, 0f);
            }
            else if (animator.HasState(0, Animator.StringToHash("attack")))
            {
                animator.Play("attack", 0, 0f);
            }
        }

        // 4. Hitbox Collider Enable
        int dmg = (step == 3) ? 75 : (step == 2 ? secondHitDamage : meleeDamage);
        EnableMeleeCollider(dmg);
    }

    public void PerformAttack1()
    {
        float facing = GetCurrentFacing();
        PerformSpearComboStep(1, facing);
    }

    public void PerformSecondHit()
    {
        float facing = GetCurrentFacing();
        PerformSpearComboStep(2, facing);
    }

    public void ResetAirCombo()
    {
        airComboStep = 0;
        airComboTimer = 0f;
    }

    public void ResetComboPublic()
    {
        ResetCombo();
        ResetAirCombo();
    }

    private void ResetCombo()
    {
        comboStep = 0;
        secondHitQueued = false;
        comboTimer = 0f;
        isPostDashStriking = false;
        isFinisherActive = false;

        // Clean up any lingering triggers when combo window expires
        ResetAllAttackTriggers();
    }

    private void EnableMeleeCollider(int damage)
    {
        currentAttackDamage = damage;
        if (meleeAttackCollider != null)
        {
            meleeAttackCollider.enabled = true;
            CancelInvoke(nameof(StopMeleeAttack));

            // Use the pre-cached clip duration for accurate collider timing
            float clipLength = (comboStep == 2 || airComboStep == 2) ? followupClipDuration : attack1ClipDuration;
            Invoke(nameof(StopMeleeAttack), clipLength);
        }
    }

    private void StopMeleeAttack()
    {
        if (meleeAttackCollider != null)
        {
            meleeAttackCollider.enabled = false;
        }
    }

    private void PerformRangedAttack()
    {
        PlayRandomAttackVoice();
        currentMana -= projectileManaCost;
        nextRangedTime = Time.time + rangedCooldown;

        if (animator != null)
        {
            // Force-play cast animation cleanly, no cross-fade
            ResetAllAttackTriggers();
            if (animator.HasState(0, Animator.StringToHash("Cast")))
                animator.Play("Cast", 0, 0f);
            else if (animator.HasState(0, Animator.StringToHash("cast")))
                animator.Play("cast", 0, 0f);
        }

        if (PlayerCombatJuice.Instance != null)
        {
            PlayerCombatJuice.Instance.TriggerSquashAndStretch(new Vector3(0.85f, 1.25f, 1.0f), 0.12f);
        }

        SpawnProjectile();
    }

    private void SpawnProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("MageCombat: No projectile prefab assigned.");
            return;
        }

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        GameObject projObj = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);

        float direction = Mathf.Sign(transform.localScale.x);

        MagicProjectile projectile = projObj.GetComponent<MagicProjectile>();
        if (projectile != null)
        {
            projectile.Launch(direction);
        }
    }

    public void UnlockProjectile()
    {
        isProjectileUnlocked = true;
        Debug.Log("Magic projectile unlocked!");
    }

    private readonly System.Collections.Generic.HashSet<IDamageable> hitsThisSwing = new System.Collections.Generic.HashSet<IDamageable>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (meleeAttackCollider != null && meleeAttackCollider.enabled)
        {
            IDamageable target = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();

            if (target != null && other.gameObject != gameObject && !hitsThisSwing.Contains(target))
            {
                hitsThisSwing.Add(target);
                bool isHeavyCombo = (comboStep == 2 || isPostDashStriking || airComboStep == 2 || isFinisherActive);
                int currentDamage = currentAttackDamage;
                target.TakeDamage(currentDamage);

                Vector3 contactPoint = other.bounds.ClosestPoint(transform.position);

                // 1. Core Game Feel Hit Feedback
                HitFeedbackManager.TriggerHitFeedback(other.transform, contactPoint, currentDamage, isHeavyCombo, isHeavyCombo ? EnemyHitType.HeavyCombo : EnemyHitType.PhysicalMelee);

                // 2. High Impact Collision Particles & Player Juice
                if (PlayerCombatJuice.Instance != null)
                {
                    PlayerCombatJuice.Instance.SpawnHitCollisionParticles(contactPoint, isHeavyCombo);
                }
            }
        }
    }

    /// <summary>
    /// Invoked by on-screen touch attack button.
    /// Strict lockout enforced while in Blob Form, Dashing, or during combo recovery cooldown.
    /// </summary>
    public void TriggerMeleeAttackFromTouch()
    {
        if (move.Instance != null && (move.Instance.IsDashing || move.Instance.IsBlobForm)) return;
        if (comboRecoveryTimer > 0f) return;
        ExecuteMeleeAttack();
    }

    /// <summary>
    /// Invoked by on-screen touch magic projectile button.
    /// Strict lockout enforced while in Blob Form or Dashing.
    /// </summary>
    public void TriggerRangedAttackFromTouch()
    {
        if (move.Instance != null && (move.Instance.IsDashing || move.Instance.IsBlobForm)) return;
        if (isProjectileUnlocked && Time.time >= nextRangedTime)
        {
            if (currentMana >= projectileManaCost)
            {
                PerformRangedAttack();
            }
            else
            {
                Debug.Log("Not enough mana for magic projectile!");
            }
        }
    }

    private void InitCombatVoice()
    {
        combatVoiceSource = gameObject.GetComponent<AudioSource>();
        if (combatVoiceSource == null)
        {
            combatVoiceSource = gameObject.AddComponent<AudioSource>();
        }
        combatVoiceSource.playOnAwake = false;
        combatVoiceSource.spatialBlend = 0f;

        if (attackVoiceClips == null || attackVoiceClips.Length == 0)
        {
            attackVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/attack");
        }
    }

    public void PlayRandomAttackVoice()
    {
        var vc = GetComponent<SpawnOfChaos.Entities.PlayerMageVoiceController>() ?? GetComponentInParent<SpawnOfChaos.Entities.PlayerMageVoiceController>();
        if (vc != null) { vc.PlayAttackVoice(); return; }
        if (attackVoiceClips == null || attackVoiceClips.Length == 0)
        {
            attackVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/attack");
        }
        if (attackVoiceClips == null || attackVoiceClips.Length == 0) return;
        if (combatVoiceSource == null) InitCombatVoice();
        AudioClip clip = attackVoiceClips[UnityEngine.Random.Range(0, attackVoiceClips.Length)];
        if (clip != null)
        {
            float sfxVol = AudioManager.Instance != null ? AudioManager.Instance.GetRealSFXVolume() : 1.0f;
            combatVoiceSource.PlayOneShot(clip, sfxVol);
        }
    }

}
