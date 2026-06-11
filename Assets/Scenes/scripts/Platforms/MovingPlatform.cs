using UnityEngine;

public class MovingPlatform : PlatformBase
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2.5f;

    private Vector3 targetPosition;

    void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogWarning("MovingPlatform requires both pointA and pointB.");
            enabled = false;
            return;
        }
        targetPosition = pointB.position;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
        {
            targetPosition = targetPosition == pointA.position ? pointB.position : pointA.position;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            collision.transform.SetParent(transform);
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            collision.transform.SetParent(null);
        }
    }
}
