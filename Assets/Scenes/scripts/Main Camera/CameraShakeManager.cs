using System.Collections;
using UnityEngine;

/// <summary>
/// CameraShakeManager - Manages micro camera shakes for combat hits and spell impacts.
/// Plays nicely with CameraFollow by applying temporary camera offsets.
/// </summary>
public class CameraShakeManager : MonoBehaviour
{
    private static CameraShakeManager instance;

    public static CameraShakeManager Instance
    {
        get
        {
            if (instance == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    instance = mainCam.GetComponent<CameraShakeManager>();
                    if (instance == null)
                    {
                        instance = mainCam.gameObject.AddComponent<CameraShakeManager>();
                    }
                }
                else
                {
                    Camera cam = FindFirstObjectByType<Camera>();
                    if (cam != null)
                    {
                        instance = cam.gameObject.AddComponent<CameraShakeManager>();
                    }
                }
            }
            return instance;
        }
    }

    private Coroutine shakeCoroutine;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    /// <summary>
    /// Trigger a camera shake with specific duration and magnitude.
    /// </summary>
    public static void Shake(float duration = 0.1f, float magnitude = 0.08f)
    {
        if (Instance != null)
        {
            Instance.TriggerShake(duration, magnitude);
        }
    }

    public void TriggerShake(float duration, float magnitude)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Calculate random offset
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;

            // Apply camera shake translation (preserving Z position)
            transform.position = new Vector3(transform.position.x + offsetX, transform.position.y + offsetY, transform.position.z);

            elapsed += Time.unscaledDeltaTime; // Unscaled time so hitstop doesn't freeze camera shake!
            yield return null;
        }

        shakeCoroutine = null;
    }
}
