using UnityEngine;

/// <summary>
/// Parallax controller for a distant planet.
/// Dynamically locates the camera/player and smoothly follows camera movement
/// when in range, clamped to "a few steps" of displacement.
/// </summary>
[AddComponentMenu("Environment/Planet Parallax")]
public class PlanetParallax : MonoBehaviour
{
    public enum FollowMode
    {
        [Tooltip("Follows camera when the camera/player is near the planet's location.")]
        ProximityZone,
        [Tooltip("Continuously follows camera movement across the scene, clamped to max steps.")]
        GlobalParallax
    }

    [Header("Tracking Target")]
    [Tooltip("Camera to track. Automatically auto-detected if left unassigned.")]
    public Transform targetCamera;

    [Header("Follow Behavior")]
    [Tooltip("Choose whether the planet follows everywhere or only when the camera is in the planet's zone.")]
    public FollowMode followMode = FollowMode.ProximityZone;

    [Tooltip("How strongly the planet moves with the camera (0.1 = subtle, 0.4 = moderate, 1.0 = 1:1 tracking).")]
    [Range(0.01f, 1f)]
    public float followStrengthX = 0.25f;

    [Tooltip("Vertical follow strength.")]
    [Range(0f, 1f)]
    public float followStrengthY = 0.1f;

    [Header("Step Clamping ('A Few Steps')")]
    [Tooltip("Maximum distance (in Unity units / steps) the planet is allowed to shift horizontally from its resting position.")]
    public float maxStepDistanceX = 4.0f;

    [Tooltip("Maximum vertical step distance.")]
    public float maxStepDistanceY = 2.0f;

    [Header("Proximity Range (For ProximityZone Mode)")]
    [Tooltip("Horizontal distance from the planet where camera following activates.")]
    public float activationRangeX = 35.0f;

    [Tooltip("Smooth transition buffer distance when entering/leaving range.")]
    public float smoothEdgeBuffer = 8.0f;

    [Header("Smoothing & Feel")]
    [Tooltip("Interpolation speed for silky smooth movement (higher = faster, 0 = instant).")]
    public float smoothSpeed = 4.0f;

    [Header("Celestial Idle Drift")]
    [Tooltip("Adds a gentle breathing / floating motion to the planet.")]
    public bool enableFloatingDrift = true;
    public float driftAmplitude = 0.2f;
    public float driftSpeed = 0.8f;

    [Header("Live Debug Info (Read Only)")]
    [SerializeField] private bool isInRange;
    [SerializeField] private float currentDistanceToCam;
    [SerializeField] private Vector2 currentOffset;

    // Internal State
    private Vector3 originPosition;
    private Vector3 initialCamPos;
    private bool hasRecordedInitialCam = false;
    private float driftTimer = 0f;

    void Awake()
    {
        originPosition = transform.position;
    }

    void Start()
    {
        FindCameraTarget();
    }

    void LateUpdate()
    {
        // 1. Ensure we have a valid camera target (handles late-spawned cameras)
        if (targetCamera == null)
        {
            FindCameraTarget();
            if (targetCamera == null) return;
        }

        // Record initial camera position once camera is first found
        if (!hasRecordedInitialCam)
        {
            initialCamPos = targetCamera.position;
            hasRecordedInitialCam = true;
        }

        Vector3 camPos = targetCamera.position;
        float targetOffsetX = 0f;
        float targetOffsetY = 0f;

        if (followMode == FollowMode.ProximityZone)
        {
            // Calculate distance between camera and planet origin
            float dx = camPos.x - originPosition.x;
            float dy = camPos.y - originPosition.y;
            currentDistanceToCam = Mathf.Abs(dx);

            // Compute smooth activation weight based on proximity
            float weight = 0f;
            if (currentDistanceToCam <= activationRangeX)
            {
                weight = 1.0f;
                isInRange = true;
            }
            else if (currentDistanceToCam <= activationRangeX + smoothEdgeBuffer)
            {
                weight = Mathf.InverseLerp(activationRangeX + smoothEdgeBuffer, activationRangeX, currentDistanceToCam);
                isInRange = true;
            }
            else
            {
                weight = 0f;
                isInRange = false;
            }

            // Offset follows camera displacement from planet center, clamped to a few steps
            targetOffsetX = Mathf.Clamp(dx * followStrengthX, -maxStepDistanceX, maxStepDistanceX) * weight;
            targetOffsetY = Mathf.Clamp(dy * followStrengthY, -maxStepDistanceY, maxStepDistanceY) * weight;
        }
        else // GlobalParallax
        {
            Vector3 camDelta = camPos - initialCamPos;
            targetOffsetX = Mathf.Clamp(camDelta.x * followStrengthX, -maxStepDistanceX, maxStepDistanceX);
            targetOffsetY = Mathf.Clamp(camDelta.y * followStrengthY, -maxStepDistanceY, maxStepDistanceY);
            isInRange = true;
        }

        currentOffset = new Vector2(targetOffsetX, targetOffsetY);

        // Subtle celestial floating oscillation
        float driftY = 0f;
        if (enableFloatingDrift)
        {
            driftTimer = (driftTimer + Time.deltaTime * driftSpeed) % (Mathf.PI * 200f);
            driftY = Mathf.Sin(driftTimer) * driftAmplitude;
        }

        // Target position to move to
        Vector3 targetPos = new Vector3(
            originPosition.x + targetOffsetX,
            originPosition.y + targetOffsetY + driftY,
            originPosition.z
        );

        // Apply smooth interpolation
        if (smoothSpeed > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = targetPos;
        }
    }

    private void FindCameraTarget()
    {
        if (Camera.main != null)
        {
            targetCamera = Camera.main.transform;
            return;
        }

        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            targetCamera = cam.transform;
            return;
        }

        // Fallback: look for Player GameObject if camera is not yet available
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            targetCamera = playerObj.transform;
        }
    }

    [ContextMenu("Recalibrate Origin Position")]
    public void RecalibrateOrigin()
    {
        originPosition = transform.position;
        if (targetCamera != null)
        {
            initialCamPos = targetCamera.position;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? originPosition : transform.position;

        // Draw maximum movement boundary box (Cyan)
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(center, new Vector3(maxStepDistanceX * 2f, maxStepDistanceY * 2f, 0.2f));

        // Draw proximity activation range (Gold / Yellow)
        if (followMode == FollowMode.ProximityZone)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireCube(center, new Vector3(activationRangeX * 2f, 30f, 0.1f));

            // Outer smooth buffer
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireCube(center, new Vector3((activationRangeX + smoothEdgeBuffer) * 2f, 30f, 0.1f));
        }
    }
#endif
}
