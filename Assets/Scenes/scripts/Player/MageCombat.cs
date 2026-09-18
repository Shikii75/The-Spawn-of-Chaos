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
    public float manaRegenRate = 15f;
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

    /// <summary>
    /// True while any melee attack animation is actively playing.
    /// Used by move.cs to suppress walk/run/idle animation overrides.
    /// </summary>
    public bool IsAttacking
    {
        get { return comboStep > 0 && (Time.time - stepStartTime < 0.35f); }
    }

    void Awake()
    {
        SpawnOfChaos.Entities.PlayerMageVoiceController.EnsureAttached(gameObject);
        if (Instance == null)
        {
            Instance = this;
        }

        animator = GetComponent<Animator>();
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
        if (HUDManager.IsInMainMenu()) return;

        // Regenerate Mana
        currentMana = Mathf.Min(maxMana, currentMana + manaRegenRate * Time.deltaTime);

        // Check if game is paused or UI is active
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return;
        if (NyxarisManager.IsChatActive) return;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return;
        if (move.Instance != null && move.Instance.IsDashing) return;

        // Manage combo window expiration
        if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                ResetCombo();
            }
        }

        // Melee Attack Input (J Key or Left Mouse Click) - Fluid 3-Hit Spear Swordsmanship Combo
        bool attackPressed = Input.GetKeyDown(KeyCode.J) ||
            (Input.GetMouseButtonDown(0) && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()));
        if (attackPressed)
        {
            float facing = GetCurrentFacing();
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

        // 4. Ranged Attack (K key, if unlocked)
        if (Input.GetKeyDown(KeyCode.K) && isProjectileUnlocked && Time.time >= nextRangedTime)
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

    public void PerformSpearComboStep(int step, float facing)
    {
        comboStep = step;
        comboTimer = (step == 3) ? 0.45f : 0.75f; // Generous combo window to chain strikes
        stepStartTime = Time.time;

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

    private void ResetCombo()
    {
        comboStep = 0;
        secondHitQueued = false;
        comboTimer = 0f;

        // Clean up any lingering triggers when combo window expires
        ResetAllAttackTriggers();
    }

    private void EnableMeleeCollider(int damage)
    {
        if (meleeAttackCollider != null)
        {
            meleeAttackCollider.enabled = true;
            CancelInvoke(nameof(StopMeleeAttack));

            // Use the pre-cached clip duration for accurate collider timing
            float clipLength = (comboStep == 2) ? followupClipDuration : attack1ClipDuration;
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
                bool isHeavyCombo = (comboStep == 2);
                int currentDamage = isHeavyCombo ? secondHitDamage : meleeDamage;
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
    /// Invoked by on-screen touch attack button. Performs Attack 1 or chains into Spear Finisher on double-tap.
    /// </summary>
    public void TriggerMeleeAttackFromTouch()
    {
        if (NyxarisManager.IsChatActive) return;
        float currentTime = Time.time;
        float tapDelta = currentTime - lastJTapTime;
        lastJTapTime = currentTime;

        bool isAttack1Active = IsAttack1Playing();
        bool isSecondHitActive = IsSecondHitPlaying();

        if (comboStep == 0 && !isAttack1Active && !isSecondHitActive)
        {
            PerformAttack1();
        }
        else if (comboStep == 1)
        {
            if (tapDelta <= doubleTapThreshold)
            {
                if (isAttack1Active)
                {
                    secondHitQueued = true;
                    comboTimer = comboWindowDuration;
                }
                else
                {
                    PerformSecondHit();
                }
            }
            else
            {
                if (!isAttack1Active)
                {
                    ResetCombo();
                    PerformAttack1();
                }
            }
        }
        else if (comboStep == 2 && !isSecondHitActive)
        {
            PerformAttack1();
        }
    }

    /// <summary>
    /// Invoked by on-screen touch magic projectile button.
    /// </summary>
    public void TriggerRangedAttackFromTouch()
    {
        if (isProjectileUnlocked && Time.time >= nextRangedTime)
        {
            if (currentMana >= projectileManaCost)
            {
                PerformRangedAttack();
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
