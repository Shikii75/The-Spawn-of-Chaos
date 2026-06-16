using UnityEngine;

public class SpiderPatrol : MonoBehaviour
{
    public float speed = 2f;
    public Transform leftPoint;
    public Transform rightPoint;

    private bool movingRight = true;

    void Update()
    {
        if (leftPoint == null || rightPoint == null)
            return;

        Transform target = movingRight ? rightPoint : leftPoint;
        
        // Move towards target's X coordinate while preserving current Y coordinate
        Vector2 targetXPosition = new Vector2(target.position.x, transform.position.y);
        transform.position = Vector2.MoveTowards(
            transform.position,
            targetXPosition,
            speed * Time.deltaTime
        );

        float horizontalDistance = Mathf.Abs(transform.position.x - target.position.x);
        if (horizontalDistance < 0.3f)
        {
            movingRight = !movingRight;
            Flip();
        }
    }

    void Flip()
    {
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}