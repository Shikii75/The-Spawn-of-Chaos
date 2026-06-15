using UnityEngine;

public class EnemyPatrol2D : MonoBehaviour, IDamageable
{
    [Header("Targeting")]
    [Tooltip("Primary target slot. Use this for the player or a destructible object.")]
    public Transform TargetA;

    [Tooltip("Secondary target slot. The enemy chooses the closest live target.")]
    public Transform TargetB;

    [Header("Movement & Combat")]
    public float moveSpeed = 2.5f;
    public float dashSpeed = 9f;
    public float attackRange = 1.4f;
    public float dashRange = 4f;
    public float dashDuration = 0.35f;
    public int attackDamage = 10;
    public int dashDamage = 20;
    public float attackCooldown = 1.0f;
    public float dashCooldown = 2.0f;
    public float knockbackDuration = 0.3f;

    [Header("Patrol")]
    [Tooltip("First patrol waypoint.")]
    public Transform patrolPoint1;
    [Tooltip("Second patrol waypoint.")]
    public Transform patrolPoint2;

    [Header("Animator Parameters")]
    public string strwalkBoolName = "strwalk";
    public string slashTriggerName = "slash";
    public string sDashTriggerName = "sdash";
    public string knockbackTriggerName = "knockback";

    public int maxHealth = 100;
    private int currentHealth;

    private Rigidbody2D rb;
    private Animator animator;
    private Transform currentTarget;
    private bool isDashing;
    private bool isKnockedBack;
    private float dashEndTime;
    private float nextAttackTime;
    private float nextDashTime;
    private Vector2 dashDirection;
    private Transform currentPatrolTarget;
    
    // Animator validation
    private System.Collections.Generic.HashSet<string> _animParamNames = new System.Collections.Generic.HashSet<string>();
    public bool verboseLogs = true;

    [Header("Debug / Fallback")]
    [Tooltip("If true, will use Animator.Play with the provided state names to force a state during testing.")]
    public bool forceDirectPlay = false;
    public string slashStateName = "slash";
    public string sDashStateName = "sdash";
    public string knockbackStateName = "knockback";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (rb == null)
        {
            Debug.LogWarning("EnemyPatrol2D requires a Rigidbody2D component.", this);
        }

        if (animator == null)
        {
            Debug.LogWarning("EnemyPatrol2D requires an Animator component.", this);
        }
        else
        {
            // Cache animator parameter names for quick checks
            foreach (var p in animator.parameters)
            {
                _animParamNames.Add(p.name);
            }

            ValidateAnimatorParameters();
        }

        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isKnockedBack)
        {
            return;
        }

        currentTarget = ChooseClosestTarget();

        bool targetInDashRange = currentTarget != null && Vector2.Distance(transform.position, currentTarget.position) <= dashRange;

        if (!targetInDashRange)
        {
            PerformPatrol();
            return;
        }

        if (isDashing)
        {
            return;
        }

        float distance = Vector2.Distance(transform.position, currentTarget.position);

        if (distance <= attackRange)
        {
            BeginAttack();
            return;
        }

        if (Time.time >= nextDashTime)
        {
            BeginDash();
            return;
        }

        ChaseTarget(currentTarget.position);
    }

    private void FixedUpdate()
    {
        if (isKnockedBack)
        {
            return;
        }

        if (isDashing)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(dashDirection.x * dashSpeed, rb.linearVelocity.y);
            }

            if (Time.time >= dashEndTime || currentTarget == null ||
                Vector2.Distance(transform.position, currentTarget.position) <= attackRange)
            {
                EndDash();
            }
        }
    }

    private Transform ChooseClosestTarget()
    {
        Transform closest = null;
        float bestDistance = float.MaxValue;

        foreach (Transform candidate in new[] { TargetA, TargetB })
        {
            if (candidate == null)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, candidate.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private bool IsTargetInDashRange()
    {
        if (TargetA != null && Vector2.Distance(transform.position, TargetA.position) <= dashRange)
            return true;
        if (TargetB != null && Vector2.Distance(transform.position, TargetB.position) <= dashRange)
            return true;
        return false;
    }

    private void PerformPatrol()
    {
        if (rb == null)
        {
            return;
        }

        if (patrolPoint1 == null || patrolPoint2 == null)
        {
            // If patrol points are not configured, do not move horizontally
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetWalkState(false);
            return;
        }

        if (currentPatrolTarget == null)
        {
            currentPatrolTarget = patrolPoint1;
        }

        float xDistance = currentPatrolTarget.position.x - transform.position.x;
        float absDistance = Mathf.Abs(xDistance);

        if (absDistance < 0.25f)
        {
            currentPatrolTarget = currentPatrolTarget == patrolPoint1 ? patrolPoint2 : patrolPoint1;
            xDistance = currentPatrolTarget.position.x - transform.position.x;
        }

        float xDirection = Mathf.Sign(xDistance);
        rb.linearVelocity = new Vector2(xDirection * moveSpeed, rb.linearVelocity.y);
        SetWalkState(true);
        UpdateFacing(xDirection);
    }

    private void PerformWalkMovement(Vector2 destination)
    {
        if (rb == null)
        {
            return;
        }

        float xDistance = destination.x - transform.position.x;
        float xDirection = Mathf.Sign(xDistance);
        rb.linearVelocity = new Vector2(xDirection * moveSpeed, rb.linearVelocity.y);
        SetWalkState(true);
        UpdateFacing(xDirection);
    }

    private void ChaseTarget(Vector2 destination)
    {
        if (rb == null)
        {
            return;
        }

        float xDistance = destination.x - transform.position.x;
        float xDirection = Mathf.Sign(xDistance);
        if (xDirection == 0f)
        {
            xDirection = transform.localScale.x >= 0 ? 1f : -1f;
        }

        rb.linearVelocity = new Vector2(xDirection * moveSpeed, rb.linearVelocity.y);
        SetWalkState(true);
        UpdateFacing(xDirection);
    }

    private void BeginAttack()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        SetWalkState(false);

        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        if (animator != null && (_animParamNames.Contains(slashTriggerName) || forceDirectPlay))
        {
            if (verboseLogs) Debug.Log($"[Enemy] {name} triggering attack: {slashTriggerName}");
            if (forceDirectPlay)
                animator.Play(slashStateName);
            else
                animator.SetTrigger(slashTriggerName);
        }
        else if (verboseLogs)
        {
            Debug.LogWarning($"Animator trigger '{slashTriggerName}' not found on {name}");
        }

        ApplyDamageToTarget(currentTarget, attackDamage);
    }

    private void BeginDash()
    {
        if (animator == null || isDashing || currentTarget == null)
        {
            return;
        }

        float xDistance = currentTarget.position.x - transform.position.x;
        float xDirection = Mathf.Sign(xDistance);
        if (xDirection == 0f)
        {
            xDirection = transform.localScale.x >= 0 ? 1f : -1f;
        }

        dashDirection = new Vector2(xDirection, 0f);
        dashEndTime = Time.time + dashDuration;
        isDashing = true;
        if (animator != null && (_animParamNames.Contains(sDashTriggerName) || forceDirectPlay))
        {
            if (verboseLogs) Debug.Log($"[Enemy] {name} starting dash: {sDashTriggerName}");
            if (forceDirectPlay)
                animator.Play(sDashStateName);
            else
                animator.SetTrigger(sDashTriggerName);
        }
        else if (verboseLogs)
        {
            Debug.LogWarning($"Animator trigger '{sDashTriggerName}' not found on {name}");
        }
        UpdateFacing(dashDirection.x);
    }

    private void EndDash()
    {
        if (!isDashing)
        {
            return;
        }

        isDashing = false;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        nextDashTime = Time.time + dashCooldown;
        SetWalkState(true);

        if (currentTarget != null && Vector2.Distance(transform.position, currentTarget.position) <= attackRange)
        {
            ApplyDamageToTarget(currentTarget, dashDamage);
        }
    }

    private void UpdateFacing(float directionX)
    {
        if (directionX > 0.1f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (directionX < -0.1f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    private void SetWalkState(bool isWalking)
    {
        if (animator == null)
        {
            return;
        }

        if (_animParamNames.Contains(strwalkBoolName))
        {
            animator.SetBool(strwalkBoolName, isWalking);
        }
        else if (verboseLogs)
        {
            Debug.LogWarning($"Animator bool '{strwalkBoolName}' not found on {name}");
        }
    }

    private void ValidateAnimatorParameters()
    {
        if (animator == null)
            return;

        System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
        if (!_animParamNames.Contains(strwalkBoolName)) missing.Add(strwalkBoolName + "(bool)");
        if (!_animParamNames.Contains(slashTriggerName)) missing.Add(slashTriggerName + "(trigger)");
        if (!_animParamNames.Contains(sDashTriggerName)) missing.Add(sDashTriggerName + "(trigger)");
        if (!_animParamNames.Contains(knockbackTriggerName)) missing.Add(knockbackTriggerName + "(trigger)");

        if (missing.Count > 0)
        {
            Debug.LogWarning($"Animator on {name} is missing parameters: {string.Join(", ", missing)}. Check Animator parameters and names.");
        }
        else if (verboseLogs)
        {
            Debug.Log($"Animator parameters validated on {name}.");
        }
    }

    private void ApplyDamageToTarget(Transform target, int damage)
    {
        if (target == null)
        {
            return;
        }

        IDamageable damageable = GetDamageableComponent(target);
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }

    private IDamageable GetDamageableComponent(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        return target.GetComponentInParent<IDamageable>();
    }

    public void TriggerKnockback()
    {
        if (animator != null)
        {
            animator.SetTrigger(knockbackTriggerName);
        }

        if (!isKnockedBack)
        {
            StartCoroutine(KnockbackRoutine());
        }
    }
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (verboseLogs)
        {
            Debug.Log($"[Enemy] {name} took {damage} damage. Health now {currentHealth}/{maxHealth}.");
        }

        if (animator != null && (_animParamNames.Contains(knockbackTriggerName) || forceDirectPlay))
        {
            if (forceDirectPlay)
                animator.Play(knockbackStateName);
            else
                animator.SetTrigger(knockbackTriggerName);
        }
        else if (verboseLogs)
        {
            Debug.LogWarning($"Animator trigger '{knockbackTriggerName}' not found on {name}");
        }

        if (!isKnockedBack)
        {
            StartCoroutine(KnockbackRoutine());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"[Enemy] {name} died.");
        Destroy(gameObject);
    }
    private System.Collections.IEnumerator KnockbackRoutine()
    {
        isKnockedBack = true;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        SetWalkState(false);
        yield return new WaitForSeconds(knockbackDuration);
        isKnockedBack = false;
        SetWalkState(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dashRange);

        if (TargetA != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, TargetA.position);
        }

        if (TargetB != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, TargetB.position);
        }

        if (patrolPoint1 != null && patrolPoint2 != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(patrolPoint1.position, patrolPoint2.position);
            Gizmos.DrawWireSphere(patrolPoint1.position, 0.3f);
            Gizmos.DrawWireSphere(patrolPoint2.position, 0.3f);
        }
    }
}

