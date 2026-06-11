using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CloudPlatform : PlatformBase
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2.0f;
    public bool oneWay = true;

    private Vector3 currentTarget;
    private bool movingToB = true;

    void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogWarning("CloudPlatform requires pointA and pointB.");
            enabled = false;
            return;
        }
        currentTarget = pointB.position;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, currentTarget, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, currentTarget) < 0.1f)
        {
            movingToB = !movingToB;
            currentTarget = movingToB ? pointB.position : pointA.position;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))
            return;
        collision.transform.SetParent(transform);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))
            return;
        collision.transform.SetParent(null);
    }
}
