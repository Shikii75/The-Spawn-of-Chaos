using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpiderPatrol : MonoBehaviour
{
    public float speed = 2f;
    public Transform leftPoint;
    public Transform rightPoint;
    public float flipCooldownDuration = 0.35f;

    private Rigidbody2D rb;
    private bool movingRight = true;
    private float nextFlipTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (leftPoint == null || rightPoint == null || rb == null)
            return;

        Transform target = movingRight ? rightPoint : leftPoint;
        float dir = movingRight ? 1f : -1f;

        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);

        float horizontalDistance = Mathf.Abs(transform.position.x - target.position.x);
        if (horizontalDistance < 0.3f && Time.time >= nextFlipTime)
        {
            nextFlipTime = Time.time + flipCooldownDuration;
            movingRight = !movingRight;
            Flip();
        }
    }

    void Flip()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (movingRight ? 1f : -1f);
        transform.localScale = scale;
    }
}