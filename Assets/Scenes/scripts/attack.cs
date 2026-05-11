using UnityEngine;

public class Attack : MonoBehaviour
{
    public int damage = 20;
    public Collider2D attackCollider;
    public Animator animator;

    void Start()
    {
        attackCollider.enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            animator.SetTrigger("Attack");
            AttackNow();
        }
    }

    void AttackNow()
    {
        attackCollider.enabled = true;
        Invoke(nameof(StopAttack), 0.2f);
    }

    void StopAttack()
    {
        attackCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health target = other.GetComponent<Health>();

        if (target != null)
        {
            target.TakeDamage(damage);
        }
    }
}