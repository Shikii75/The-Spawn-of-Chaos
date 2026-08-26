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
    public static MageCombat Instance { get; private set; }

    [Header("Melee Settings")]
    public int meleeDamage = 20;
    public int secondHitDamage = 28;
    public Collider2D meleeAttackCollider;
    public float meleeAttackDuration = 0.25f;
    public float comboWindowDuration = 1.85f; // Window after Attack 1 during which 2nd hit can be chained

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
    private int comboStep = 0; // 0 = Idle, 1 = Attack 1 (playing/completed), 2 = Second Hit (playing)
    private float comboTimer = 0f;
    private bool secondHitQueued = false;
    private float attack1StartTime = -10f;
    private float secondHitStartTime = -10f;

    // Cached clip durations (resolved once at startup to avoid per-frame lookups)
    private float attack1ClipDuration = 0.866f;
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
        get { return comboStep > 0 && (IsAttack1Playing() || IsSecondHitPlaying()); }
    }

    void Awake()
    {
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
        // Regenerate Mana
        currentMana = Mathf.Min(maxMana, currentMana + manaRegenRate * Time.deltaTime);

        // Check if game is paused or UI is active
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused) return;
        if (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf) return;
        if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive) return;
        if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive) return;
        if (move.Instance != null && move.Instance.IsDashing) return;

        bool isAttack1Active = IsAttack1Playing();
        bool isSecondHitActive = IsSecondHitPlaying();

        // 1. Process queued second hit as soon as Attack 1 finishes playing!
        if (comboStep == 1 && secondHitQueued && !isAttack1Active)
        {
            PerformSecondHit();
            return;
        }

        // 2. Manage combo window expiration
        if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;
            if (!isAttack1Active && !isSecondHitActive && comboTimer <= 0f)
            {
                ResetCombo();
            }
        }

        // 3. Melee Attack Input (J Key)
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (comboStep == 0 && !isAttack1Active && !isSecondHitActive)
            {
                // First Attack
                PerformAttack1();
            }
            else if (comboStep == 1)
            {
                if (isAttack1Active)
                {
                    // Attack 1 is still playing -> Queue the second hit!
                    // Do NOT interrupt Attack 1; wait for it to complete.
                    secondHitQueued = true;
                    comboTimer = comboWindowDuration;
                }
                else
                {
                    // Attack 1 already finished -> Execute second hit immediately!
                    PerformSecondHit();
                }
            }
            else if (comboStep == 2 && !isSecondHitActive)
            {
                // After combo finishes, allow starting Attack 1 again
                PerformAttack1();
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
        if (animator == null) return false;
        float elapsed = Time.time - secondHitStartTime;

        // Primary check: has enough real time passed for the clip to have finished?
        if (elapsed < followupClipDuration) return true;

        // Secondary check: is the animator still in the followup/secondhit state?
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if ((state.shortNameHash == hashFollowupAttack || state.shortNameHash == hashSecondHit) && state.normalizedTime < 1.0f)
            return true;

        return false;
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

    private Coroutine shadowVFXCoroutine;

    private void PerformAttack1()
    {
        comboStep = 1;
        secondHitQueued = false;
        comboTimer = comboWindowDuration;
        attack1StartTime = Time.time;

        if (move.Instance != null)
        {
            move.Instance.ResetPlayerScaleToNormal();
        }

        if (animator != null)
        {
            // 1. Clear ALL attack triggers to prevent queued trigger buildup
            ResetAllAttackTriggers();

            // 2. Force-play the Attack state at frame 0 with NO cross-fade blending.
            animator.Play("Attack", 0, 0f);
        }

        if (shadowVFXCoroutine != null) StopCoroutine(shadowVFXCoroutine);
        shadowVFXCoroutine = StartCoroutine(Attack1ShadowFXRoutine());

        EnableMeleeCollider(meleeDamage);
    }

    private IEnumerator Attack1ShadowFXRoutine()
    {
        // Punch 1 apex (Frames 4-5 @ 16 FPS = 0.25s)
        yield return new WaitForSeconds(0.25f);
        if (comboStep == 1 && PlayerCombatJuice.Instance != null)
        {
            float dir = (transform.localScale.x < 0f) ? -1f : 1f;
            Vector3 fist1Pos = transform.position + new Vector3(dir * 1.15f, 0.2f, 0f);
            PlayerCombatJuice.Instance.SpawnShadowPunchVFX(fist1Pos, dir, isHeavy: false);
            PlayerCombatJuice.Instance.TriggerSquashAndStretch(new Vector3(1.2f, 0.85f, 1f), 0.12f);
        }

        // Punch 2 apex (Frames 9-10 @ 16 FPS = 0.56s from start, +0.31s delta)
        yield return new WaitForSeconds(0.31f);
        if (comboStep == 1 && PlayerCombatJuice.Instance != null)
        {
            float dir = (transform.localScale.x < 0f) ? -1f : 1f;
            Vector3 fist2Pos = transform.position + new Vector3(dir * 1.35f, 0.25f, 0f);
            PlayerCombatJuice.Instance.SpawnShadowPunchVFX(fist2Pos, dir, isHeavy: true);
            PlayerCombatJuice.Instance.TriggerSquashAndStretch(new Vector3(1.3f, 0.78f, 1f), 0.15f);
        }
        shadowVFXCoroutine = null;
    }

    private void PerformSecondHit()
    {
        comboStep = 2;
        secondHitQueued = false;
        comboTimer = comboWindowDuration;
        secondHitStartTime = Time.time;

        if (move.Instance != null)
        {
            move.Instance.ResetPlayerScaleToNormal();
        }

        if (animator != null)
        {
            // 1. Clear ALL attack triggers to prevent queued trigger buildup
            ResetAllAttackTriggers();

            // 2. Force-play the followupAttack state at frame 0 with NO cross-fade blending.
            animator.Play("followupAttack", 0, 0f);
        }

        if (shadowVFXCoroutine != null) StopCoroutine(shadowVFXCoroutine);
        shadowVFXCoroutine = StartCoroutine(SecondHitShadowFXRoutine());

        EnableMeleeCollider(secondHitDamage);
    }

    private IEnumerator SecondHitShadowFXRoutine()
    {
        yield return new WaitForSeconds(0.18f);
        if (comboStep == 2 && PlayerCombatJuice.Instance != null)
        {
            float dir = (transform.localScale.x < 0f) ? -1f : 1f;
            Vector3 fistPos = transform.position + new Vector3(dir * 1.4f, 0.3f, 0f);
            PlayerCombatJuice.Instance.SpawnShadowPunchVFX(fistPos, dir, isHeavy: true);
            PlayerCombatJuice.Instance.TriggerSquashAndStretch(new Vector3(1.35f, 0.72f, 1.0f), 0.16f);
        }
        shadowVFXCoroutine = null;
    }

    private void ResetCombo()
    {
        comboStep = 0;
        secondHitQueued = false;
        comboTimer = 0f;

        if (shadowVFXCoroutine != null)
        {
            StopCoroutine(shadowVFXCoroutine);
            shadowVFXCoroutine = null;
        }

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (meleeAttackCollider != null && meleeAttackCollider.enabled)
        {
            IDamageable target = other.GetComponent<IDamageable>();
            if (target == null)
            {
                target = other.GetComponentInParent<IDamageable>();
            }

            if (target != null && other.gameObject != gameObject)
            {
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
}
