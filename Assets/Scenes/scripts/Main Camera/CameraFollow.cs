using UnityEngine;

/// <summary>
/// CameraFollow - Smooth framerate-independent camera tracking script with boundary clamping.
/// Automatically detects and binds to the player across scene transitions.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Tracking Target")]
    [Tooltip("Player transform to follow. Auto-detected if unassigned.")]
    public Transform player;

    [Header("Follow Settings")]
    [Tooltip("Camera offset relative to player. Y=0.5f places player in the vertical center of screen.")]
    public Vector3 offset = new Vector3(0f, 0.5f, -10f);
    public float smoothSpeed = 6f;

    [Header("Boundary Clamping")]
    [Tooltip("Enable bounding box to clamp camera within level limits.")]
    public bool useBounds = false;
    public Vector2 minBounds = new Vector2(-10f, -20f);
    public Vector2 maxBounds = new Vector2(300f, 20f);

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (player == null)
        {
            FindPlayer();
        }
    }

    void LateUpdate()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        Vector3 shake = (CameraShakeManager.Instance != null) ? CameraShakeManager.Instance.CurrentShakeOffset : Vector3.zero;
        Vector3 targetPosition = player.position + offset + shake;

        if (useBounds)
        {
            targetPosition = ClampPositionToBounds(targetPosition);
        }

        // Exponential decay for framerate-independent smooth tracking
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, t);
    }

    public void FindPlayer()
    {
        if (move.Instance != null)
        {
            player = move.Instance.transform;
        }
        else
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        if (player != null)
        {
            SnapToTarget();
        }
    }

    public void SnapToTarget()
    {
        if (player != null)
        {
            Vector3 pos = player.position + offset;
            if (useBounds)
            {
                pos = ClampPositionToBounds(pos);
            }
            transform.position = pos;
        }
    }

    private Vector3 ClampPositionToBounds(Vector3 target)
    {
        float clampedX = target.x;
        float clampedY = target.y;

        if (cam != null && cam.orthographic)
        {
            float vertExtent = cam.orthographicSize;
            float horzExtent = vertExtent * cam.aspect;

            float minX = minBounds.x + horzExtent;
            float maxX = maxBounds.x - horzExtent;
            float minY = minBounds.y + vertExtent;
            float maxY = maxBounds.y - vertExtent;

            if (minX <= maxX) clampedX = Mathf.Clamp(target.x, minX, maxX);
            else clampedX = (minBounds.x + maxBounds.x) * 0.5f;

            if (minY <= maxY) clampedY = Mathf.Clamp(target.y, minY, maxY);
            else clampedY = (minBounds.y + maxBounds.y) * 0.5f;
        }
        else
        {
            clampedX = Mathf.Clamp(target.x, minBounds.x, maxBounds.x);
            clampedY = Mathf.Clamp(target.y, minBounds.y, maxBounds.y);
        }

        return new Vector3(clampedX, clampedY, target.z);
    }
}
