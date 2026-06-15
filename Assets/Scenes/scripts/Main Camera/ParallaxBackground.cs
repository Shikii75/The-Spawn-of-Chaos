using UnityEngine;

[AddComponentMenu("Environment/Parallax Background")]
public class ParallaxBackground : MonoBehaviour
{
    [Header("Camera Reference")]
    [Tooltip("The main camera transform. If left empty, will auto-detect Camera.main.")]
    public Transform cam;

    [Header("Parallax Coefficients")]
    [Tooltip("How much the layer moves relative to the camera. 0 = moves with camera (infinite distance background), 0.5 = moves at half speed, 1 = static (moves with foreground player), 1.2+ = moves faster (closer foreground silhouettes).")]
    public float parallaxEffectX = 0.5f;
    [Tooltip("Vertical parallax factor. Set to 0 to keep background locked vertically, or low values (e.g. 0.1 - 0.3) for distant layers.")]
    public float parallaxEffectY = 0.1f;

    [Header("Smoothing")]
    [Tooltip("Smoothing/Interpolation rate to prevent jitter. 0 = instant tracking, > 0 = smoother lag/blend (recommended: 0.1 to 5.0).")]
    public float smoothing = 0f;

    [Header("Auto-Scrolling")]
    [Tooltip("Constant scrolling velocity on the X axis, useful for clouds, wind, or dust particles.")]
    public float autoScrollSpeedX = 0f;
    [Tooltip("Constant scrolling velocity on the Y axis, useful for rising mist or falling embers.")]
    public float autoScrollSpeedY = 0f;

    [Header("Infinite Tiling (Horizontal)")]
    [Tooltip("Should this layer automatically wrap around when the camera crosses its length boundary? Requires a SpriteRenderer on this GameObject.")]
    public bool infiniteHorizontalScroll = true;

    [Header("Vertical Constraints")]
    [Tooltip("If true, limits how far the background can scroll vertically relative to its start position.")]
    public bool limitVerticalOffset = false;
    public float maxVerticalOffset = 5f;

    private float length;
    private float startposX;
    private float startposY;
    private float autoScrollOffsetRealX;
    private float autoScrollOffsetRealY;

    void Start()
    {
        if (cam == null)
        {
            if (Camera.main != null)
            {
                cam = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("ParallaxBackground: No Main Camera found! Please assign it in the Inspector.", this);
                enabled = false;
                return;
            }
        }

        startposX = transform.position.x;
        startposY = transform.position.y;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            length = spriteRenderer.bounds.size.x;
        }
        else
        {
            if (infiniteHorizontalScroll)
            {
                Debug.LogWarning($"ParallaxBackground on '{gameObject.name}' has Infinite Scroll enabled but no SpriteRenderer was found. Disabling infinite scroll.", this);
                infiniteHorizontalScroll = false;
            }
        }
    }

    // Run in LateUpdate to sync with Camera movement and eliminate jitter
    void LateUpdate()
    {
        if (cam == null) return;

        // Apply auto-scroll offset over time
        autoScrollOffsetRealX += autoScrollSpeedX * Time.deltaTime;
        autoScrollOffsetRealY += autoScrollSpeedY * Time.deltaTime;

        // Calculate parallax displacement relative to camera movement
        float temp = (cam.position.x * (1 - parallaxEffectX));
        float distX = (cam.position.x * parallaxEffectX) + autoScrollOffsetRealX;
        float distY = (cam.position.y * parallaxEffectY) + autoScrollOffsetRealY;

        float targetX = startposX + distX;
        float targetY = startposY + distY;

        // Apply vertical constraints if enabled
        if (limitVerticalOffset)
        {
            targetY = Mathf.Clamp(targetY, startposY - maxVerticalOffset, startposY + maxVerticalOffset);
        }

        // Target position to move to
        Vector3 targetPos = new Vector3(targetX, targetY, transform.position.z);

        // Move the layer (smoothly or instantly)
        if (smoothing > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, smoothing * Time.deltaTime * 10f);
        }
        else
        {
            transform.position = targetPos;
        }

        // Infinite wrapping
        if (infiniteHorizontalScroll)
        {
            if (temp > startposX + length)
            {
                startposX += length;
            }
            else if (temp < startposX - length)
            {
                startposX -= length;
            }
        }
    }
}
