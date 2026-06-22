using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DojoTransitionFader : MonoBehaviour
{
    public static DojoTransitionFader Instance { get; private set; }

    private Image overlay;

    public static DojoTransitionFader Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject faderObject = new GameObject("DojoTransitionFader");
        return faderObject.AddComponent<DojoTransitionFader>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        Canvas canvas = UIFactory.CreateCanvas("DojoTransitionCanvas", 60);
        canvas.transform.SetParent(transform, false);

        RectTransform overlayRect = UIFactory.CreateFullScreenPanel(canvas.transform, "DojoFadeOverlay", Color.clear);
        overlay = overlayRect.GetComponent<Image>();
        overlay.raycastTarget = true;
    }

    public IEnumerator FadeOutIn(float fadeOutDuration, float holdDuration, float fadeInDuration, System.Action onHidden)
    {
        yield return FadeTo(1f, fadeOutDuration);
        onHidden?.Invoke();
        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }
        yield return FadeTo(0f, fadeInDuration);
    }

    public void PlayFadeOutIn(float fadeOutDuration, float holdDuration, float fadeInDuration, System.Action onHidden, System.Action onComplete)
    {
        StartCoroutine(FadeOutInRoutine(fadeOutDuration, holdDuration, fadeInDuration, onHidden, onComplete));
    }

    private IEnumerator FadeOutInRoutine(float fadeOutDuration, float holdDuration, float fadeInDuration, System.Action onHidden, System.Action onComplete)
    {
        yield return FadeOutIn(fadeOutDuration, holdDuration, fadeInDuration, onHidden);
        onComplete?.Invoke();
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (overlay == null)
        {
            yield break;
        }

        Color color = overlay.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            color.a = targetAlpha;
            overlay.color = color;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            overlay.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        overlay.color = color;
    }
}
