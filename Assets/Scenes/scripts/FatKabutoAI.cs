using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// FatKabutoAI - 2D Combat AI for the Fat Kabuto heavy mini-boss/mob in Dojo 2.
/// Features:
/// 1. Spawns in with an Entrance animation (enterancefatkabuto).
/// 2. Heavy Charging Attack: Deals instant collision impact damage & knockback on touch during charge.
/// 3. Ground Bash Strike: High damage close-range melee slam.
/// 4. Heavyweight poise & tanky stats.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FatKabutoAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        SpawningIn,
        Patrolling,
        Chasing,
        ChargingAttack,
        BashAttacking,
        HitStun,
        Dead
    }

    [Header("State")]
    public State currentState = State.SpawningIn;

    [Header("Patrol Settings")]
    public float patrolDistance = 4f;
    public float walkSpeed = 2.8f;
    public float stopDuration = 1.5f;

    [Header("Combat Settings")]
    public float detectionRange = 13f;
    public float bashAttackRange = 2.8f;
    public float attackCooldown = 1.5f;

    [Header("Heavy Charge Settings")]
    public float chargeMinRange = 4.0f;
    public float chargeMaxRange = 14f;
    public float chargeSpeed = 15f;
    public float chargePrepDuration = 0.5f;
    public float maxChargeDuration = 1.4f;
    public float chargeOvershootDistance = 4.0f;
    public float chargeCooldown = 4.0f;

    [Header("Health & Damage")]
    public int maxHealth = 120;
    [SerializeField] private int currentHealth;
    public int bashDamage = 20; // 20% damage (Heavy Dojo brute)
    public int chargeImpactDamage = 20; // 20% damage
    public float knockbackForce = 6f;
    public float hitStunDuration = 0.15f; // Heavy poise reduces hitstun duration

    // Components
    private Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private Collider2D bodyCollider;
    private SpriteJuice spriteJuice;
    private SpriteRenderer spriteRenderer;

    // Internal State
    private Vector3 startingPosition;
    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float lastAttackTime = -999f;
    private float lastChargeTime = -999f;
    private string currentAnimState = "";

    // Animation Clip Names
    private const string ENTRANCE_STATE = "FatKabuto_Entrance";
    private const string IDLE_STATE = "FatKabuto_Idle";
    private const string WALK_STATE = "FatKabuto_Walk";
    private const string CHARGE_STATE = "FatKabuto_Charge";
    private const string BASH_STATE = "FatKabuto_Bash";

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        spriteJuice = GetComponent<SpriteJuice>() ?? GetComponentInChildren<SpriteJuice>();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.2f, 0.16f, 0.12f, 1f);
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        startingPosition = transform.position;
        currentHealth = maxHealth;

        FindPlayer();

        // Start with Entrance animation at spawn point
        StartCoroutine(ExecuteEntranceSequence());
    }

    private IEnumerator ExecuteEntranceSequence()
    {
        currentState = State.SpawningIn;
        rb.linearVelocity = Vector2.zero;

        PlayAnimation(ENTRANCE_STATE);

        // Entrance animation duration (approx 1.2s)
        yield return new WaitForSeconds(1.2f);

        if (currentState != State.Dead)
        {
            currentState = State.Patrolling;
        }
    }

    void Update()
    {
        if (currentState == State.Dead || currentState == State.SpawningIn) return;

        if (player == null) FindPlayer();

        switch (currentState)
        {
            case State.Patrolling:
                UpdatePatrol();
                CheckForPlayer();
                break;

            case State.Chasing:
                UpdateChase();
                break;

            case State.ChargingAttack:
            case State.BashAttacking:
            case State.HitStun:
                // Handled via Coroutine
                break;
        }
    }

    private void FindPlayer()
    {
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) player = pObj.transform;
        else if (move.Instance != null) player = move.Instance.transform;
    }

    private void UpdatePatrol()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            PlayAnimation(IDLE_STATE);

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                movingRight = !movingRight;
            }
            return;
        }

        float targetX = movingRight ? startingPosition.x + patrolDistance : startingPosition.x - patrolDistance;
        float dir = movingRight ? 1f : -1f;

        rb.linearVelocity = new Vector2(dir * walkSpeed, rb.linearVelocity.y);
        FlipSprite(dir);
        PlayAnimation(WALK_STATE);

        if ((movingRight && transform.position.x >= targetX) || (!movingRight && transform.position.x <= targetX))
        {
            isWaiting = true;
            waitTimer = stopDuration;
        }
    }

    private void CheckForPlayer()
    {
        if (player == null) return;
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        if (distToPlayer <= detectionRange)
        {
            currentState = State.Chasing;
        }
    }

    private void UpdateChase()
    {
        if (player == null)
        {
            currentState = State.Patrolling;
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);
        if (distToPlayer > detectionRange * 1.5f)
        {
            currentState = State.Patrolling;
            return;
        }

        float dir = Mathf.Sign(player.position.x - transform.position.x);
        FlipSprite(dir);

        float effectiveBashRange = GetEffectiveBashRange();

        // Trigger heavy charge attack if within range
        if (distToPlayer >= chargeMinRange && distToPlayer <= chargeMaxRange && Time.time >= lastChargeTime + chargeCooldown)
        {
            StartCoroutine(ExecuteHeavyChargeSequence(dir));
            return;
        }

        // Close-range heavy bash attack
        if (distToPlayer <= effectiveBashRange && Time.time >= lastAttackTime + attackCooldown)
        {
            StartCoroutine(ExecuteBashAttack());
            return;
        }

        // Standard pursuit
        if (distToPlayer > effectiveBashRange)
        {
            rb.linearVelocity = new Vector2(dir * walkSpeed * 1.3f, rb.linearVelocity.y);
            PlayAnimation(WALK_STATE);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            PlayAnimation(IDLE_STATE);
        }
    }

    private IEnumerator ExecuteHeavyChargeSequence(float chargeDir)
    {
        currentState = State.ChargingAttack;
        lastChargeTime = Time.time;

        // Prep phase
        PlayAnimation(IDLE_STATE);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        yield return new WaitForSeconds(chargePrepDuration);

        // Charge phase
        PlayAnimation(CHARGE_STATE);
        float elapsed = 0f;
        float targetOvershootX = (player != null ? player.position.x : transform.position.x) + (chargeDir * chargeOvershootDistance);
        bool hasHitPlayer = false;

        while (elapsed < maxChargeDuration)
        {
            rb.linearVelocity = new Vector2(chargeDir * chargeSpeed, rb.linearVelocity.y);

            // Immediate collision damage on player touch during charge
            if (!hasHitPlayer && player != null)
            {
                float dist = Vector2.Distance(transform.position, player.position);
                if (dist <= GetEffectiveBashRange() + 0.6f)
                {
                    hasHitPlayer = true;

                    Health pHealth = player.GetComponent<Health>() ?? player.GetComponentInParent<Health>();
                    if (pHealth != null)
                    {
                        pHealth.TakeDamage(chargeImpactDamage);
                    }

                    Rigidbody2D pRb = player.GetComponent<Rigidbody2D>();
                    if (pRb != null)
                    {
                        pRb.linearVelocity = new Vector2(chargeDir * knockbackForce, 4f);
                    }
                }
            }

            // Check if reached overshoot target
            if ((chargeDir > 0 && transform.position.x >= targetOvershootX) || (chargeDir < 0 && transform.position.x <= targetOvershootX))
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Heavy recovery pause after charge
        rb.linearVelocity = Vector2.zero;
        PlayAnimation(IDLE_STATE);
        yield return new WaitForSeconds(0.6f);

        if (currentState != State.Dead)
        {
            currentState = State.Chasing;
        }
    }

    private IEnumerator ExecuteBashAttack()
    {
        currentState = State.BashAttacking;
        lastAttackTime = Time.time;
        rb.linearVelocity = Vector2.zero;

        PlayAnimation(BASH_STATE);

        // Slam wind-up
        yield return new WaitForSeconds(0.35f);

        // Heavy impact strike
        if (player != null && !IsPlayerDead() && GetHorizontalDistanceToPlayer() <= GetEffectiveBashRange() + 0.6f)
        {
            Health pHealth = player.GetComponent<Health>() ?? player.GetComponentInParent<Health>();
            if (pHealth != null) pHealth.TakeDamage(bashDamage);

            Rigidbody2D pRb = player.GetComponent<Rigidbody2D>();
            if (pRb != null)
            {
                float dir = transform.position.x < player.position.x ? 1f : -1f;
                pRb.linearVelocity = new Vector2(dir * knockbackForce, 4.5f);
            }
        }

        // Heavy slam recovery pause
        yield return new WaitForSeconds(0.5f);

        if (currentState != State.Dead) currentState = State.Chasing;
    }

    private float GetEffectiveBashRange()
    {
        float enemyWidth = bodyCollider != null ? bodyCollider.bounds.extents.x : 0.8f;
        float playerWidth = 0.6f;
        if (player != null)
        {
            Collider2D pCol = player.GetComponent<Collider2D>();
            if (pCol != null) playerWidth = pCol.bounds.extents.x;
        }
        return Mathf.Max(bashAttackRange, enemyWidth + playerWidth + 0.4f);
    }

    private bool IsPlayerDead()
    {
        if (player == null) return true;
        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
        return playerHealth != null && playerHealth.CurrentHealth <= 0;
    }

    public float GetHorizontalDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Mathf.Abs(transform.position.x - player.position.x);
    }

    private void FlipSprite(float dir)
    {
        if (dir == 0) return;
        float scaleX = Mathf.Abs(transform.localScale.x) * (dir > 0 ? 1f : -1f);
        transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
    }

    private void PlayAnimation(string animState)
    {
        if (anim == null || currentAnimState == animState) return;
        anim.Play(animState);
        currentAnimState = animState;
    }

    public void TakeDamage(int damage)
    {
        if (currentState == State.Dead || currentState == State.SpawningIn) return;
        currentHealth -= damage;
        if (spriteJuice != null) spriteJuice.FlashWhite();
        HitFeedbackManager.TriggerHitFeedback(transform, transform.position, damage, damage >= 25, EnemyHitType.PhysicalMelee);

        if (currentHealth <= 0) Die();
        else StartCoroutine(ApplyHitStun());
    }

    private IEnumerator ApplyHitStun()
    {
        currentState = State.HitStun;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(hitStunDuration);
        if (currentState != State.Dead) currentState = State.Chasing;
    }

    private void Die()
    {
        currentState = State.Dead;
        rb.linearVelocity = Vector2.zero;
        if (bodyCollider != null) bodyCollider.enabled = false;

        // Big mob drops large EXP/Coin loot cluster
        OrbSpawner.SpawnLootCluster(transform.position, Random.Range(8, 14));
        Destroy(gameObject, 0.1f);
    }
}
