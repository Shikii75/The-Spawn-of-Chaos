using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrolAI : MonoBehaviour, IDamageable
{
    public Transform[] patrolPoints;
    public Transform[] targets;
    public float moveSpeed = 2.5f;
    public float attackRange = 1.5f;
    public float dashRange = 4.0f;
    public float dashSpeed = 8.0f;
    public float dashDuration = 0.35f;
    public float attackCooldown = 1.2f;
    public float dashCooldown = 2.0f;
    public int attackDamage = 12;
    public int dashDamage = 20;
    public int maxHealth = 80;

    [Header("Animator")]
    public Animator animator;
    public string walkBool = "strwalk";
    public string attackTrigger = "slash";
    public string dashTrigger = "sdash";
    public string hitTrigger = "knockback";
    public string dieTrigger = "die";

    private Rigidbody2D rb;
    private int currentHealth;
    private int currentPatrolIndex;
    private bool isDashing;
    private bool isDead;
    private Vector2 dashDirection;
    private float dashEndTime;
    private float nextAttackTime;
    private float nextDashTime;

    private enum EnemyState { Patrol, Chase, Attack, Dash, Stunned }
    private EnemyState state = EnemyState.Patrol;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        animator = animator ?? GetComponent<Animator>();
    }

    void Update()
    {
        if (isDead || animator == null)
            return;

        Transform target = ChooseClosestTarget();
        bool canDash = target != null && Vector2.Distance(transform.position, target.position) <= dashRange;

        if (isDashing)
            return;

        if (target == null || !canDash)
        {
            Patrol();
            return;
        }

        float distance = Vector2.Distance(transform.position, target.position);
        if (distance <= attackRange)
        {
            Attack(target);
            return;
        }

        if (Time.time >= nextDashTime)
        {
            BeginDash(target);
            return;
        }

        Chase(target);
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection.x * dashSpeed, rb.linearVelocity.y);
            if (Time.time >= dashEndTime)
                EndDash();
        }
    }

    private Transform ChooseClosestTarget()
    {
        Transform closest = null;
        float bestDistance = float.MaxValue;
        foreach (Transform candidate in targets)
        {
            if (candidate == null)
                continue;
            float distance = Vector2.Distance(transform.position, candidate.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }
        return closest;
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            SetWalk(false);
            return;
        }

        Transform destination = patrolPoints[currentPatrolIndex];
        float distance = Vector2.Distance(transform.position, destination.position);
        if (distance < 0.3f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            destination = patrolPoints[currentPatrolIndex];
        }

        MoveToward(destination.position);
    }

    private void Chase(Transform target)
    {
        MoveToward(target.position);
    }

    private void MoveToward(Vector2 destination)
    {
        float direction = Mathf.Sign(destination.x - transform.position.x);
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        SetFacing(direction);
        SetWalk(true);
    }

    private void SetWalk(bool active)
    {
        if (animator != null)
            animator.SetBool(walkBool, active);
    }

    private void SetFacing(float directionX)
    {
        if (directionX == 0f)
            return;
        transform.localScale = new Vector3(Mathf.Sign(directionX) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    private void BeginDash(Transform target)
    {
        if (target == null)
            return;

        dashDirection = (target.position - transform.position).normalized;
        dashDirection.y = 0f;
        if (dashDirection == Vector2.zero)
            dashDirection = Vector2.right * Mathf.Sign(transform.localScale.x);

        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;
        SetFacing(dashDirection.x);
        SetWalk(false);

        if (animator != null)
            animator.SetTrigger(dashTrigger);
    }

    private void EndDash()
    {
        isDashing = false;
        SetWalk(true);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void Attack(Transform target)
    {
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;
        SetWalk(false);
        if (animator != null)
            animator.SetTrigger(attackTrigger);

        IDamageable hit = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>();
        if (hit != null)
            hit.TakeDamage(attackDamage);
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);
        if (animator != null)
            animator.SetTrigger(hitTrigger);

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        isDead = true;
        if (animator != null)
            animator.SetTrigger(dieTrigger);
        rb.linearVelocity = Vector2.zero;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
        Destroy(gameObject, 2f);
    }
}

