using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpawnOfChaos.Systems;

/// <summary>
/// NormalMaleSamuraiAI - 2D Combat AI for the Normal Male Samurai foot soldier in Dojo 2.
/// Basic frontline warrior that patrols, chases the player, and executes slash attacks.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class NormalMaleSamuraiAI : MonoBehaviour, IDamageable
{
    public enum State
    {
        Patrolling,
        Chasing,
        Attacking,
        HitStun,
        Dead
    }

    [Header("State")]
    public State currentState = State.Patrolling;

    [Header("Patrol Settings")]
    public float patrolDistance = 5f;
    public float walkSpeed = 3.5f;
    public float stopDuration = 1.2f;

    [Header("Combat Settings")]
    public float detectionRange = 10f;
    public float runSpeed = 6.5f;
    public float attackRange = 2.2f;
    public float attackCooldown = 1.2f;

    [Header("Health & Damage")]
    public int maxHealth = 50;
    [SerializeField] private int currentHealth;
    public int baseDamage = 12;
    public float knockbackForce = 3f;
    public float hitStunDuration = 0.25f;

    // Components
    private Animator anim;
    private Rigidbody2D rb;
    private Transform player;
    private Collider2D bodyCollider;
    private SpriteJuice spriteJuice;
    private SpriteRenderer spriteRenderer;

    // Timers & State
    private Vector3 startingPosition;
    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private float lastAttackTime = -999f;
    private string currentAnimState = "";

    // Animation Clip Names
    private const string IDLE_STATE = "NormalMaleSamurai_Idle";
    private const string WALK_STATE = "NormalMaleSamurai_Walk";
    private const string RUN_STATE = "NormalMaleSamurai_Run";
    private const string SLASH_STATE = "NormalMaleSamurai_Slash";

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

        if (player == null)
        {
            FindPlayer();
        }

        switch (currentState)
        {
            case State.Patrolling:
                UpdatePatrol();
                CheckForPlayer();
                break;

            case State.Chasing:
                UpdateChase();
                break;

            case State.Attacking:
                // Handled via Coroutine
                break;

            case State.HitStun:
                // Handled via Coroutine
                break;
        }
    }

    private void FindPlayer()
    {
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null)
        {
            player = pObj.transform;
        }
        else if (move.Instance != null)
        {
            player = move.Instance.transform;
        }
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

        // Calculate dynamic touch range to avoid pushing the player
        float effectiveRange = GetEffectiveAttackRange();

        if (distToPlayer <= effectiveRange && Time.time >= lastAttackTime + attackCooldown)
        {
            StartCoroutine(ExecuteSlashAttack());
            return;
        }

        if (distToPlayer > effectiveRange)
        {
            rb.linearVelocity = new Vector2(dir * runSpeed, rb.linearVelocity.y);
            PlayAnimation(RUN_STATE);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            PlayAnimation(IDLE_STATE);
        }
    }

    private IEnumerator ExecuteSlashAttack()
    {
        currentState = State.Attacking;
        lastAttackTime = Time.time;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        PlayAnimation(SLASH_STATE);

        // Wind-up pause before strike
        yield return new WaitForSeconds(0.25f);

        // Strike phase: Check hit collider
        float dir = transform.localScale.x > 0 ? 1f : -1f;
        Vector2 attackCenter = (Vector2)transform.position + new Vector2(dir * 1.2f, 0f);
        Collider2D hit = Physics2D.OverlapCircle(attackCenter, 1.2f, LayerMask.GetMask("Default", "Player"));

        if (hit != null && (hit.CompareTag("Player") || hit.GetComponent<move>() != null))
        {
            Health pHealth = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
            if (pHealth != null)
            {
                pHealth.TakeDamage(baseDamage);
            }

            Rigidbody2D pRb = hit.GetComponent<Rigidbody2D>();
            if (pRb != null)
            {
                pRb.linearVelocity = new Vector2(dir * knockbackForce, 2f);
            }
        }

        // Recovery phase
        yield return new WaitForSeconds(0.45f);

        if (currentState != State.Dead)
        {
            currentState = State.Chasing;
        }
    }

    private float GetEffectiveAttackRange()
    {
        float enemyWidth = bodyCollider != null ? bodyCollider.bounds.extents.x : 0.6f;
        float playerWidth = 0.6f;
        if (player != null)
        {
            Collider2D pCol = player.GetComponent<Collider2D>();
            if (pCol != null) playerWidth = pCol.bounds.extents.x;
        }
        return Mathf.Max(attackRange, enemyWidth + playerWidth + 0.3f);
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

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(ApplyHitStun());
        }
    }

    private IEnumerator ApplyHitStun()
    {
        State prevState = currentState;
        currentState = State.HitStun;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        yield return new WaitForSeconds(hitStunDuration);

        if (currentState != State.Dead)
        {
            currentState = State.Chasing;
        }
    }

    private void Die()
    {
        currentState = State.Dead;
        rb.linearVelocity = Vector2.zero;
        if (bodyCollider != null) bodyCollider.enabled = false;

        // Spawn loot or drop orb
        OrbSpawner.SpawnLootCluster(transform.position, Random.Range(3, 6));

        Destroy(gameObject, 0.1f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        float dir = transform.localScale.x > 0 ? 1f : -1f;
        Gizmos.DrawWireSphere(transform.position + new Vector3(dir * 1.2f, 0f, 0f), 1.2f);
    }
}
