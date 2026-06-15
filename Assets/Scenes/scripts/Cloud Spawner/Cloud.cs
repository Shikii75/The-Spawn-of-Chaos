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

        if (!playerOnCloud)
            return;

        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            transform.position = target;
            movingToB = !movingToB;
            target = movingToB ? pointB.position : pointA.position;
            waitingAtPoint = true;
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            speed * Time.deltaTime
        );
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        playerOnCloud = true;
        collision.transform.SetParent(transform);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        playerOnCloud = false;
        collision.transform.SetParent(null);
    }
}