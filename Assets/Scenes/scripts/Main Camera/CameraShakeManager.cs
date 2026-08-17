using System.Collections;
using UnityEngine;

/// <summary>
/// CameraShakeManager - Manages micro camera shakes for combat hits and spell impacts.
/// Provides non-accumulating shake offsets to prevent camera drift or infinite position bugs.
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

    public Vector3 CurrentShakeOffset { get; private set; } = Vector3.zero;
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
        if (duration <= 0f || magnitude <= 0f) return;

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
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;
            CurrentShakeOffset = new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.unscaledDeltaTime; // Unscaled time so hitstop doesn't freeze camera shake
            yield return null;
        }

        CurrentShakeOffset = Vector3.zero;
        shakeCoroutine = null;
    }

    private void OnDisable()
    {
        CurrentShakeOffset = Vector3.zero;
    }
}
