using UnityEngine;

/// <summary>
/// FloatingMobPlatform - Combat platform carrier for enemy mobs along parkour climbing routes.
/// Supports edge patrol boundary markers for AI navigation and optional floating movement.
/// </summary>
public class FloatingMobPlatform : MonoBehaviour
{
    [Header("Mob Patrol Boundaries")]
    [Tooltip("Left edge patrol offset relative to platform center.")]
    public float leftPatrolOffset = -2.5f;

    [Tooltip("Right edge patrol offset relative to platform center.")]
    public float rightPatrolOffset = 2.5f;

    [Header("Optional Platform Motion")]
    public bool isMovingPlatform = false;
    public Vector2 moveAxis = new Vector2(0f, 1f); // Vertical bobbing by default
    public float moveDistance = 1.5f;
    public float moveSpeed = 1.2f;

    private Vector3 startPosition;

    public Vector3 LeftPatrolPoint => transform.position + new Vector3(leftPatrolOffset, 0f, 0f);
    public Vector3 RightPatrolPoint => transform.position + new Vector3(rightPatrolOffset, 0f, 0f);

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        if (isMovingPlatform && moveSpeed > 0f)
        {
            float pingPong = Mathf.Sin(Time.time * moveSpeed) * moveDistance;
            Vector3 offset = (Vector3)(moveAxis.normalized * pingPong);
            transform.position = startPosition + offset;
        }
    }

    void OnDrawGizmos()
    {
        Vector3 center = Application.isPlaying ? startPosition : transform.position;

        // Draw mob patrol boundary markers (Blue = Left, Red = Right)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center + new Vector3(leftPatrolOffset, 0.5f, 0f), 0.3f);
        Gizmos.DrawLine(center + new Vector3(leftPatrolOffset, 0f, 0f), center + new Vector3(rightPatrolOffset, 0f, 0f));

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center + new Vector3(rightPatrolOffset, 0.5f, 0f), 0.3f);

        // Draw movement range if moving
        if (isMovingPlatform)
        {
            Gizmos.color = Color.yellow;
            Vector3 startPos = center - (Vector3)(moveAxis.normalized * moveDistance);
            Vector3 endPos = center + (Vector3)(moveAxis.normalized * moveDistance);
            Gizmos.DrawLine(startPos, endPos);
        }
    }
}
