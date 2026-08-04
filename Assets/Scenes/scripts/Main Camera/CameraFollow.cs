using UnityEngine;

/// <summary>
/// CameraFollow - Smooth framerate-independent camera tracking script.
/// Automatically detects and binds to the player across scene transitions.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Tracking Target")]
    [Tooltip("Player transform to follow. Auto-detected if unassigned.")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 2f, -10f);
    public float smoothSpeed = 6f;

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

        Vector3 targetPosition = player.position + offset;
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
            transform.position = player.position + offset;
        }
    }
}
