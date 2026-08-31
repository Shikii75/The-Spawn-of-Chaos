using UnityEngine;

public class Attack : MonoBehaviour
{
    public int damage = 20;
    public Collider2D attackCollider;
    public Animator animator;

    void Start()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }

        // If MageCombat handles combat on this player, disable this legacy script to avoid input conflicts
        if (GetComponent<MageCombat>() != null || GetComponentInParent<MageCombat>() != null)
        {
            enabled = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (animator != null)
            {
                animator.SetTrigger("Attack");
                animator.SetTrigger("attack");
                if (animator.HasState(0, Animator.StringToHash("attack")))
                    animator.Play("attack", 0, 0f);
                else if (animator.HasState(0, Animator.StringToHash("Attack")))
                    animator.Play("Attack", 0, 0f);
            }

            if (PlayerCombatJuice.Instance != null)
            {
                float dir = Mathf.Sign(transform.localScale.x);
                // Lunge removed
                // Attack squash removed
                PlayerCombatJuice.Instance.SpawnSlashArc(transform.position, dir, false);
            }

            AttackNow();
        }
    }

    void AttackNow()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = true;
            CancelInvoke(nameof(StopAttack));
            Invoke(nameof(StopAttack), 0.2f);
        }
    }

    void StopAttack()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null) target = other.GetComponentInParent<IDamageable>();

        if (target != null && other.gameObject != gameObject)
        {
            target.TakeDamage(damage);
            Vector3 contactPoint = other.bounds.ClosestPoint(transform.position);

            HitFeedbackManager.TriggerHitFeedback(other.transform, contactPoint, damage, false, EnemyHitType.PhysicalMelee);

            if (PlayerCombatJuice.Instance != null)
            {
                PlayerCombatJuice.Instance.SpawnHitCollisionParticles(contactPoint, false);
            }
        }
    }
}