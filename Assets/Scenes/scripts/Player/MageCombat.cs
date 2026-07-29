using System;
using UnityEngine;

/// <summary>
/// MageCombat - Player Melee & Magic Combat Engine.
/// Features fluid 2-hit melee combo mechanics with double-tap and combo input buffering.
/// If the player double-taps 'J' or taps 'J' before Attack 1 finishes, it seamlessly queues
/// and executes the second hit ('secondhit') animation and combo damage!
/// </summary>
public class MageCombat : MonoBehaviour
{
    public static MageCombat Instance { get; private set; }

    [Header("Melee Settings")]
    public int meleeDamage = 20;
    public int secondHitDamage = 28;
    public Collider2D meleeAttackCollider;
    public float meleeAttackDuration = 0.25f;
    public float meleeCooldown = 0.35f;
    public float comboWindowDuration = 0.6f; // Window during which a 2nd 'J' press executes second hit

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
    private float nextMeleeTime;
    private float nextRangedTime;

    // Combo & Double-Tap Buffer state
    private int comboStep = 0; // 0 = Idle, 1 = Attack 1, 2 = Second Hit
    private float comboTimer = 0f;
    private bool secondHitQueued = false;
    private float lastJPressTime = -10f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        animator = GetComponent<Animator>();
        currentMana = maxMana;

        if (meleeAttackCollider != null)
        {
            meleeAttackCollider.enabled = false;
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

        // Update Combo Window Timer
        if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                ResetCombo();
            }
        }

        // Melee Attack Input (J Key)
        if (Input.GetKeyDown(KeyCode.J))
        {
            float timeSinceLastPress = Time.time - lastJPressTime;
            lastJPressTime = Time.time;

            if (comboStep == 0 && Time.time >= nextMeleeTime)
            {
                // First Attack
                PerformAttack1();
            }
            else if (comboStep == 1)
            {
                // Double-tap or tap 'J' before first attack finishes -> Trigger/Queue Second Hit!
                secondHitQueued = true;
                PerformSecondHit();
            }
            else if (comboStep == 2 && Time.time >= nextMeleeTime)
            {
                // Reset to Attack 1 after combo completes
                PerformAttack1();
            }
        }

        // Check queued second hit execution if transition window reached
        if (secondHitQueued && comboStep == 1 && comboTimer > 0f)
        {
            PerformSecondHit();
        }

        // Ranged Attack (K key, if unlocked)
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

    private void PerformAttack1()
    {
        comboStep = 1;
        secondHitQueued = false;
        comboTimer = comboWindowDuration;
        nextMeleeTime = Time.time + 0.2f;

        if (animator != null)
        {
            animator.ResetTrigger("Attack2");
            animator.SetTrigger("Attack");
        }

        EnableMeleeCollider(meleeDamage, meleeAttackDuration);
    }

    private void PerformSecondHit()
    {
        comboStep = 2;
        secondHitQueued = false;
        comboTimer = comboWindowDuration;
        nextMeleeTime = Time.time + meleeCooldown;

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack2");
            animator.Play("secondhit", 0, 0f); // Force instant playback of secondhit
        }

        EnableMeleeCollider(secondHitDamage, meleeAttackDuration + 0.1f);
    }

    private void ResetCombo()
    {
        comboStep = 0;
        secondHitQueued = false;
        comboTimer = 0f;
    }

    private void EnableMeleeCollider(int damage, float duration)
    {
        if (meleeAttackCollider != null)
        {
            meleeAttackCollider.enabled = true;
            CancelInvoke(nameof(StopMeleeAttack));
            Invoke(nameof(StopMeleeAttack), duration);
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
            animator.SetTrigger("Cast");
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
                int currentDamage = (comboStep == 2) ? secondHitDamage : meleeDamage;
                target.TakeDamage(currentDamage);
            }
        }
    }
}
