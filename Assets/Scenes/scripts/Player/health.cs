using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    public int maxHealth = 14;
    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public event Action<int> onDamageTaken;
    public event Action<int> onMaxHealthChanged;
    public event Action onDeath;

    private SpriteRenderer playerSpriteRenderer;
    private Coroutine flashCoroutine;
    private Color originalPlayerColor = Color.white;
    private bool hasOriginalPlayerColor = false;

    void Start()
    {
        if (CompareTag("Player"))
        {
            maxHealth = 100; // Standard 100% health scale
            playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (playerSpriteRenderer != null)
            {
                originalPlayerColor = playerSpriteRenderer.color;
                hasOriginalPlayerColor = true;
            }
        }
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Inflicts damage calculated as a percentage of maximum health (e.g. 25f for 25%).
    /// </summary>
    public void TakeDamagePercent(float percent)
    {
        int damageAmount = Mathf.Max(1, Mathf.RoundToInt(maxHealth * (percent / 100f)));
        TakeDamage(damageAmount);
    }

    public void TakeDamage(int damage)
    {
        // Check if invulnerable (e.g. player is dashing)
        move playerMove = GetComponent<move>();
        if (playerMove != null && playerMove.IsInvulnerable)
        {
            Debug.Log($"{name} is invulnerable (blob/dash)! Ignored damage.");
            return;
        }

        // Check if Lumi's Orb Shield is active (holding 'H')
        if (CompareTag("Player") && LightOrbCompanion.Instance != null && LightOrbCompanion.Instance.isShieldActive)
        {
            float reduction = LightOrbCompanion.Instance.shieldDamageReduction;
            damage = Mathf.RoundToInt(damage * (1f - reduction));
            Debug.Log($"Lumi's Shield mitigated damage! Reduced to {damage}.");
            if (damage <= 0) return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        Debug.Log($"{name} took {damage} damage. Health now {currentHealth}/{maxHealth}.");

        if (CompareTag("Player") && playerSpriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashPlayerRed());
        }

        EnemyPatrol2D enemy = GetComponent<EnemyPatrol2D>();
        if (enemy != null)
        {
            enemy.TriggerKnockback();
        }

        if (!CompareTag("Player"))
        {
            HitFeedbackManager.TriggerHitFeedback(transform, transform.position, damage, damage >= 25, EnemyHitType.PhysicalMelee);
        }

        onDamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
        {
            // 1-in-3 Luck Cheat-Death (DarkBladeSmall)
            if (CompareTag("Player") && SpawnOfChaos.Weapons.WeaponManager.Instance != null && SpawnOfChaos.Weapons.WeaponManager.Instance.TryTriggerLuckCheatDeath(this))
            {
                return;
            }

            Die();
        }
    }

    private System.Collections.IEnumerator FlashPlayerRed()
    {
        if (playerSpriteRenderer == null) yield break;
        if (!hasOriginalPlayerColor)
        {
            originalPlayerColor = playerSpriteRenderer.color;
            hasOriginalPlayerColor = true;
        }

        playerSpriteRenderer.color = Color.red;
        yield return new WaitForSeconds(hitFlashDuration);
        playerSpriteRenderer.color = originalPlayerColor;
        flashCoroutine = null;
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (playerSpriteRenderer != null && hasOriginalPlayerColor)
        {
            playerSpriteRenderer.color = originalPlayerColor;
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (CompareTag("Player") && (currentHealth <= 0 || PlayerSpawnPointManager.isRespawning))
        {
            Resurrect();
        }
    }

    [Header("Hit Flash (Player)")]
    public float hitFlashDuration = 0.15f;

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onMaxHealthChanged?.Invoke(maxHealth);
        onDamageTaken?.Invoke(0); // trigger UI redraw
        Debug.Log($"{name} max health increased by {amount}. Max: {maxHealth}, Current: {currentHealth}");
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onDamageTaken?.Invoke(0); // trigger UI redraw
        Debug.Log($"{name} healed by {amount}. Health now {currentHealth}/{maxHealth}.");
    }

    public void Resurrect()
    {
        currentHealth = maxHealth;
        
        move playerMove = GetComponent<move>();
        if (playerMove != null) playerMove.enabled = true;

        MageCombat playerCombat = GetComponent<MageCombat>();
        if (playerCombat != null) playerCombat.enabled = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector2.zero;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        Debug.Log($"{name} has been resurrected and fully reset.");
        onDamageTaken?.Invoke(0); // redraw UI
    }

    void Die()
    {
        Debug.Log($"{name} died.");
        onDeath?.Invoke();

        if (CompareTag("Player"))
        {
            // Disable movement and combat to prevent input execution after death
            move playerMove = GetComponent<move>();
            if (playerMove != null) playerMove.enabled = false;

            MageCombat playerCombat = GetComponent<MageCombat>();
            if (playerCombat != null) playerCombat.enabled = false;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.isKinematic = true;
            }

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            if (RevivalPromptUI.Instance != null)
            {
                RevivalPromptUI.Instance.PromptRevival(this, () => ExecuteStandardDeathRespawn());
            }
            else
            {
                ExecuteStandardDeathRespawn();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ExecuteStandardDeathRespawn()
    {
        if (SpawnOfChaos.Systems.DrifterSaveManager.Instance != null && SpawnOfChaos.Systems.DrifterSaveManager.Instance.HasCheckpoint())
        {
            SpawnOfChaos.Systems.DrifterSaveManager.Instance.RespawnAtDeathAnchor();
        }
        else
        {
            StartCoroutine(ReloadSceneRoutine(1.5f));
        }
    }

    private System.Collections.IEnumerator ReloadSceneRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayerSpawnPointManager.isRespawning = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}