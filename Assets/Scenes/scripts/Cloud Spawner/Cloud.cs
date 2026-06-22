using UnityEngine;

public class Cloud : MonoBehaviour
{
    [Header("Movement Points")]
    public Transform pointA;
    public Transform pointB;

    [Header("Settings")]
    public float speed = 2f;

    private Vector3 target;
    private bool movingToB = true;
    private bool playerOnCloud = false;
    private bool waitingAtPoint = true;

    private Transform playerTransform;

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("Cloud requires pointA and pointB to be assigned.");
            enabled = false;
            return;
        }

        target = pointB.position;
        movingToB = true;
        waitingAtPoint = true;
    }

    private void Update()
    {
        if (pointA == null || pointB == null)
            return;

        if (waitingAtPoint)
        {
            if (!playerOnCloud)
                return;

            waitingAtPoint = false;
        }

        Vector3 currentPosition = transform.position;
        Vector3 newPosition = Vector3.MoveTowards(
            currentPosition,
            target,
            speed * Time.deltaTime
        );

        Vector3 delta = newPosition - currentPosition;
        transform.position = newPosition;

        // Move player along with the cloud if they are standing on it
        if (playerOnCloud && playerTransform != null)
        {
            playerTransform.position += delta;
        }

        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            transform.position = target;
            movingToB = !movingToB;
            target = movingToB ? pointB.position : pointA.position;
            waitingAtPoint = true;
            return;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        playerOnCloud = true;
        playerTransform = collision.transform;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        playerOnCloud = false;
        if (playerTransform == collision.transform)
        {
            playerTransform = null;
        }
    }
}