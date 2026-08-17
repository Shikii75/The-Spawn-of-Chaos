using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// NormalFemaleSamuraiAI - 2D Combat AI for the Normal Female Samurai warrior in Dojo 2.
/// Features high-speed telegraphed charging attacks, slash strike combos, and dynamic evasive overshoots.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class NormalFemaleSamuraiAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Patrolling,
        Chasing,
        StartCharging,
        ContinueCharging,
        SlashAttacking,
        HitStun,
        Dead
    }

    [Header("State")]
    public State currentState = State.Patrolling;

    [Header("Patrol Settings")]
    public float patrolDistance = 5f;
    public float walkSpeed = 4f;
    public float stopDuration = 1.2f;

    [Header("Combat Settings")]
    public float detectionRange = 12f;
    public float slashAttackRange = 2.5f;
    public float attackCooldown = 1.0f;

    [Header("Charge Settings")]
    public float chargeMinRange = 4.5f;
    public float chargeMaxRange = 12f;
    public float chargeSpeed = 13f;
    public float startChargeDuration = 0.45f;
    public float maxChargeDuration = 1.2f;
    public float chargeOvershootDistance = 3f;
    public float chargeCooldown = 3.0f;

    [Header("Health & Damage")]
    public int maxHealth = 60;
    [SerializeField] private int currentHealth;
    public int baseDamage = 16; // 16% damage (Dojo clan standard)
    public float knockbackForce = 3.5f;
    public float hitStunDuration = 0.25f;

    // Components
    private Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private Collider2D bodyCollider;
    private SpriteJuice spriteJuice;
    private SpriteRenderer spriteRenderer;

    // State Timers
    private Vector3 startingPosition;
    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float lastAttackTime = -999f;
    private float lastChargeTime = -999f;
    private string currentAnimState = "";

    // Animation Names
    private const string IDLE_STATE = "NormalFemaleSamurai_Idle";
    private const string WALK_STATE = "NormalFemaleSamurai_Walk";
    private const string RUN_STATE = "NormalFemaleSamurai_Run";
    private const string START_CHARGE_STATE = "NormalFemaleSamurai_StartCharge";
    private const string CONTINUE_CHARGE_STATE = "NormalFemaleSamurai_ContinueCharge";
    private const string SLASH_STATE = "NormalFemaleSamurai_Slash";

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        spriteJuice = GetComponent<SpriteJuice>() ?? GetComponentInChildren<SpriteJuice>();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        startingPosition = transform.position;
        currentHealth = maxHealth;

        FindPlayer();
    }

    void Update()
    {
        if (currentState == State.Dead) return;

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

            case State.StartCharging:
            case State.ContinueCharging:
            case State.SlashAttacking:
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

        float effectiveRange = GetEffectiveSlashRange();

        // Check if charge attack can be triggered
        if (distToPlayer >= chargeMinRange && distToPlayer <= chargeMaxRange && Time.time >= lastChargeTime + chargeCooldown)
        {
            StartCoroutine(ExecuteChargeAttackSequence(dir));
            return;
        }

        // Close range slash attack
        if (distToPlayer <= effectiveRange && Time.time >= lastAttackTime + attackCooldown)
        {
            StartCoroutine(ExecuteSlashAttack());
            return;
        }

        // Standard chase movement
        if (distToPlayer > effectiveRange)
        {
            rb.linearVelocity = new Vector2(dir * walkSpeed * 1.4f, rb.linearVelocity.y);
            PlayAnimation(RUN_STATE);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            PlayAnimation(IDLE_STATE);
        }
    }

    private IEnumerator ExecuteChargeAttackSequence(float chargeDir)
    {
        currentState = State.StartCharging;
        lastChargeTime = Time.time;

        // Phase 1: Start Charge Acceleration Wind-up
        PlayAnimation(START_CHARGE_STATE);
        float elapsed = 0f;
        while (elapsed < startChargeDuration)
        {
            float speedPct = elapsed / startChargeDuration;
            rb.linearVelocity = new Vector2(chargeDir * (chargeSpeed * speedPct * 0.5f), rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: Continue Charge Sprint
        currentState = State.ContinueCharging;
        PlayAnimation(CONTINUE_CHARGE_STATE);

        float chargeElapsed = 0f;
        float targetOvershootX = (player != null ? player.position.x : transform.position.x) + (chargeDir * chargeOvershootDistance);
        bool hasDealtChargeDamage = false;

        while (chargeElapsed < maxChargeDuration)
        {
            rb.linearVelocity = new Vector2(chargeDir * chargeSpeed, rb.linearVelocity.y);

            // Check collision with player during charge
            if (!hasDealtChargeDamage && player != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, player.position);
                if (distToPlayer <= 1.5f)
                {
                    hasDealtChargeDamage = true;
                    Health pHealth = player.GetComponent<Health>() ?? player.GetComponentInParent<Health>();
                    if (pHealth != null) pHealth.TakeDamage(baseDamage);

                    Rigidbody2D pRb = player.GetComponent<Rigidbody2D>();
                    if (pRb != null) pRb.linearVelocity = new Vector2(chargeDir * knockbackForce, 3f);
                }
            }

            // Check if passed overshoot target position
            if ((chargeDir > 0 && transform.position.x >= targetOvershootX) || (chargeDir < 0 && transform.position.x <= targetOvershootX))
            {
                break;
            }

            chargeElapsed += Time.deltaTime;
            yield return null;
        }

        // Recovery pause after charge
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        PlayAnimation(IDLE_STATE);
        yield return new WaitForSeconds(0.35f);

        if (currentState != State.Dead)
        {
            currentState = State.Chasing;
        }
    }

    private IEnumerator ExecuteSlashAttack()
    {
        currentState = State.SlashAttacking;
        lastAttackTime = Time.time;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        PlayAnimation(SLASH_STATE);

        yield return new WaitForSeconds(0.2f);

        float dir = transform.localScale.x > 0 ? 1f : -1f;
        Vector2 attackCenter = (Vector2)transform.position + new Vector2(dir * 1.3f, 0f);
        Collider2D hit = Physics2D.OverlapCircle(attackCenter, 1.3f, LayerMask.GetMask("Default", "Player"));

        if (hit != null && (hit.CompareTag("Player") || hit.GetComponent<move>() != null))
        {
            Health pHealth = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
            if (pHealth != null) pHealth.TakeDamage(baseDamage);

            Rigidbody2D pRb = hit.GetComponent<Rigidbody2D>();
            if (pRb != null) pRb.linearVelocity = new Vector2(dir * knockbackForce, 2f);
        }

        yield return new WaitForSeconds(0.4f);

        if (currentState != State.Dead) currentState = State.Chasing;
    }

    private float GetEffectiveSlashRange()
    {
        float enemyWidth = bodyCollider != null ? bodyCollider.bounds.extents.x : 0.6f;
        float playerWidth = 0.6f;
        if (player != null)
        {
            Collider2D pCol = player.GetComponent<Collider2D>();
            if (pCol != null) playerWidth = pCol.bounds.extents.x;
        }
        return Mathf.Max(slashAttackRange, enemyWidth + playerWidth + 0.3f);
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
        if (currentState == State.Dead) return;
        currentHealth -= damage;
        if (spriteJuice != null) spriteJuice.FlashWhite();
        HitFeedbackManager.TriggerHitFeedback(transform, transform.position, damage, damage >= 25, EnemyHitType.PhysicalMelee);

        if (currentHealth <= 0) Die();
        else StartCoroutine(ApplyHitStun());
    }

    private IEnumerator ApplyHitStun()
    {
        currentState = State.HitStun;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        yield return new WaitForSeconds(hitStunDuration);
        if (currentState != State.Dead) currentState = State.Chasing;
    }

    private void Die()
    {
        currentState = State.Dead;
        rb.linearVelocity = Vector2.zero;
        if (bodyCollider != null) bodyCollider.enabled = false;
        OrbSpawner.SpawnLootCluster(transform.position, Random.Range(4, 7));
        Destroy(gameObject, 0.1f);
    }
}
