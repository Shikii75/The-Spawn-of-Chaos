using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// Fullscreen white flash overlay with black "Saving..." text for The Drifter voluntary death ritual.
    /// Layered at sortingOrder 100 to cover all gameplay, world, and HUD elements.
    /// </summary>
    public class DrifterFlashOverlay : MonoBehaviour
    {
        public static DrifterFlashOverlay Instance { get; private set; }

        private Canvas canvas;
        private Image flashImage;
        private TextMeshProUGUI savingText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlayUI();
        }

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("DrifterFlashOverlay");
                Instance = go.AddComponent<DrifterFlashOverlay>();
            }
        }

        private void BuildOverlayUI()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Always top overlay

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // Flash Image
            GameObject flashGO = new GameObject("FlashImage", typeof(RectTransform));
            flashGO.transform.SetParent(transform, false);
            flashImage = flashGO.AddComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;

            RectTransform flashRT = flashGO.GetComponent<RectTransform>();
            flashRT.anchorMin = Vector2.zero;
            flashRT.anchorMax = Vector2.one;
            flashRT.sizeDelta = Vector2.zero;

            // Saving Text
            GameObject textGO = new GameObject("SavingText", typeof(RectTransform));
            textGO.transform.SetParent(flashGO.transform, false);
            savingText = textGO.AddComponent<TextMeshProUGUI>();
            savingText.text = "Saving...";
            savingText.fontSize = 72f;
            savingText.fontStyle = FontStyles.Bold;
            savingText.alignment = TextAlignmentOptions.Center;
            savingText.color = new Color(0.08f, 0.08f, 0.1f, 0f); // Solid pitch dark text
            savingText.raycastTarget = false;

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.5f, 0.5f);
            textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.sizeDelta = new Vector2(800, 140);
            textRT.anchoredPosition = Vector2.zero;
        }

        public void PlaySaveSequence(Action onSeverBody, Action onRestoreBody, Action onComplete)
        {
            StartCoroutine(SaveSequenceRoutine(onSeverBody, onRestoreBody, onComplete));
        }

        private IEnumerator SaveSequenceRoutine(Action onSeverBody, Action onRestoreBody, Action onComplete)
        {
            if (flashImage == null) BuildOverlayUI();

            // ── STAGE 1: Rapid Flash to Solid White with black "Saving..." text ──
            float t = 0f;
            float flashInDuration = 0.18f;
            while (t < flashInDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / flashInDuration);
                flashImage.color = new Color(1f, 1f, 1f, progress);
                savingText.color = new Color(0.08f, 0.08f, 0.1f, progress);
                yield return null;
            }

            flashImage.color = Color.white;
            savingText.color = new Color(0.08f, 0.08f, 0.1f, 1f);

            // While solid white: sever body, leave only head
            onSeverBody?.Invoke();

            // Hold solid white with "Saving..."
            yield return new WaitForSecondsRealtime(0.75f);

            // Fade out white to reveal the resting severed head on the altar
            float fadeOutDuration = 0.35f;
            t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = 1f - Mathf.Clamp01(t / fadeOutDuration);
                flashImage.color = new Color(1f, 1f, 1f, progress);
                savingText.color = new Color(0.08f, 0.08f, 0.1f, progress);
                yield return null;
            }

            flashImage.color = new Color(1f, 1f, 1f, 0f);
            savingText.color = new Color(0.08f, 0.08f, 0.1f, 0f);

            // ── STAGE 2: Pause on the resting severed head ──
            yield return new WaitForSecondsRealtime(1.0f);

            // ── STAGE 3: Second Flash & Body Restoration ──
            t = 0f;
            float secondFlashDuration = 0.15f;
            while (t < secondFlashDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / secondFlashDuration);
                flashImage.color = new Color(1f, 1f, 1f, progress);
                yield return null;
            }

            flashImage.color = Color.white;

            // While screen is white: restore full body and vitality!
            onRestoreBody?.Invoke();

            yield return new WaitForSecondsRealtime(0.3f);

            // Fade out second white flash to reveal restored Drifter
            t = 0f;
            float secondFadeDuration = 0.45f;
            while (t < secondFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = 1f - Mathf.Clamp01(t / secondFadeDuration);
                flashImage.color = new Color(1f, 1f, 1f, progress);
                yield return null;
            }

            flashImage.color = new Color(1f, 1f, 1f, 0f);
            onComplete?.Invoke();
        }

        public void PlayRespawnAwakenSequence(Action onRestoreBody, Action onComplete)
        {
            StartCoroutine(RespawnAwakenRoutine(onRestoreBody, onComplete));
        }

        private IEnumerator RespawnAwakenRoutine(Action onRestoreBody, Action onComplete)
        {
            if (flashImage == null) BuildOverlayUI();

            // Start solid white
            flashImage.color = Color.white;
            savingText.color = new Color(0.08f, 0.08f, 0.1f, 0f);

            // Reveal resting head at altar
            float t = 0f;
            float fadeOutDuration = 0.4f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = 1f - Mathf.Clamp01(t / fadeOutDuration);
                flashImage.color = new Color(1f, 1f, 1f, progress);
                yield return null;
            }

            flashImage.color = new Color(1f, 1f, 1f, 0f);

            // Pause on resting head
            yield return new WaitForSecondsRealtime(0.85f);

            // Flash white
            t = 0f;
            float flashDuration = 0.15f;
            while (t < flashDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / flashDuration);
                flashImage.color = new Color(1f, 1f, 1f, progress);
                yield return null;
            }

            flashImage.color = Color.white;

            // Restore body & vitality
            onRestoreBody?.Invoke();

            yield return new WaitForSecondsRealtime(0.25f);

            // Fade out
            t = 0f;
            float finalFade = 0.45f;
            while (t < finalFade)
            {
                t += Time.unscaledDeltaTime;
                float progress = 1f - Mathf.Clamp01(t / finalFade);
                flashImage.color = new Color(1f, 1f, 1f, progress);
                yield return null;
            }

            flashImage.color = new Color(1f, 1f, 1f, 0f);
            onComplete?.Invoke();
        }
    }
}
