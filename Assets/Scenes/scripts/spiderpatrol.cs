using UnityEngine;

public class SpiderPatrol : MonoBehaviour
{
    public float speed = 2f;
    public Transform leftPoint;
    public Transform rightPoint;

    private bool movingRight = true;

    void Update()
    {
        if (movingRight)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                rightPoint.position,
                speed * Time.deltaTime
            );

            if (Vector2.Distance(transform.position, rightPoint.position) < 0.3f)
            {
                movingRight = false;
                Flip();
            }
        }
        else
{
    transform.position = Vector2.MoveTowards(
        transform.position,
        leftPoint.position,
        speed * Time.deltaTime
    );

    if (Vector2.Distance(transform.position, leftPoint.position) < 1f)
    {
        movingRight = true;
        Flip();
    }
}
    }

    void Flip()
    {
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}