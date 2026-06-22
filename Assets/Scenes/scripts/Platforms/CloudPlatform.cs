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

    private Transform playerTransform;
    private bool playerOnPlatform = false;

    void Update()
    {
        Vector3 currentPosition = transform.position;
        Vector3 newPosition = Vector3.MoveTowards(currentPosition, currentTarget, speed * Time.deltaTime);
        Vector3 delta = newPosition - currentPosition;
        transform.position = newPosition;

        if (playerOnPlatform && playerTransform != null)
        {
            playerTransform.position += delta;
        }

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
        playerOnPlatform = true;
        playerTransform = collision.transform;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))
            return;
        playerOnPlatform = false;
        if (playerTransform == collision.transform)
        {
            playerTransform = null;
        }
    }
}
