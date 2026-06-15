using System;
using UnityEngine;

public class MageCombat : MonoBehaviour
{
    public static MageCombat Instance { get; private set; }

    [Header("Melee Settings")]
    public int meleeDamage = 20;
    public Collider2D meleeAttackCollider;
    public float meleeAttackDuration = 0.2f;
    public float meleeCooldown = 0.3f;

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
    private float meleeTimer;
    private float rangedTimer;
    private float nextMeleeTime;
    private float nextRangedTime;

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

        // Melee Attack (J key)
        if (Input.GetKeyDown(KeyCode.J) && Time.time >= nextMeleeTime)
        {
            PerformMeleeAttack();
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

    private void PerformMeleeAttack()
    {
        nextMeleeTime = Time.time + meleeCooldown;
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        if (meleeAttackCollider != null)
        {
            meleeAttackCollider.enabled = true;
            CancelInvoke(nameof(StopMeleeAttack));
            Invoke(nameof(StopMeleeAttack), meleeAttackDuration);
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
            animator.SetTrigger("Cast"); // Optional: set cast trigger
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

        // Determine direction based on localScale
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
        // Hook for melee attack collider
        if (meleeAttackCollider != null && meleeAttackCollider.enabled)
        {
            IDamageable target = other.GetComponent<IDamageable>();
            if (target == null)
            {
                target = other.GetComponentInParent<IDamageable>();
            }

            if (target != null && other.gameObject != gameObject)
            {
                target.TakeDamage(meleeDamage);
            }
        }
    }
}
