using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// BlobMageIntroSequence - Cinematic entrance to Level 1.
    /// The player arrives in Level 1 (SampleScene) as a primordial shadow blob,
    /// oozing forth from the void, before absorbing ambient ether and undergoing
    /// a majestic metamorphosis into the humanoid Mage form.
    /// </summary>
    public class BlobMageIntroSequence : MonoBehaviour
    {
        public static BlobMageIntroSequence Instance { get; private set; }

        private const string PREF_BLOB_INTRO_PLAYED = "SpawnOfChaos_Level1BlobIntroPlayed";

        [Header("Cinematic Timing")]
        public float blobCrawlDuration = 2.4f;
        public float metamorphosisDuration = 1.2f;

        [Header("FX & Visuals")]
        public Color voidEnergyColor = new Color(0.65f, 0.15f, 0.95f, 1f);

        private Canvas introCanvas;
        private CanvasGroup introGroup;
        private TextMeshProUGUI subtitleText;
        private GameObject shockwaveRing;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            BuildCinematicUI();
        }

        private void Start()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            // Only trigger in SampleScene (Level 1) or if forced
            if (sceneName == "SampleScene")
            {
                bool alreadyPlayed = PlayerPrefs.GetInt(PREF_BLOB_INTRO_PLAYED, 0) == 1;
                if (!alreadyPlayed)
                {
                    StartCoroutine(ExecuteIntroSequenceRoutine());
                }
            }
        }

        [ContextMenu("Force Play Blob Intro")]
        public void ForcePlayIntro()
        {
            StartCoroutine(ExecuteIntroSequenceRoutine());
        }

        private IEnumerator ExecuteIntroSequenceRoutine()
        {
            // Find player
            GameObject player = null;
            while (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player") ?? GameObject.Find("BasePlayer");
                yield return null;
            }

            move playerMove = player.GetComponent<move>();
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

            // Lock controls and force Blob form
            move.ExternalMovementLock = true;
            move.forceBlobIntro = true;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            // Subtitle 1: Primordial blob emergence
            SetSubtitle("From the primordial void... a formless shadow crawls forth.", true);

            // Gentle crawl forward
            float elapsed = 0f;
            while (elapsed < blobCrawlDuration)
            {
                elapsed += Time.deltaTime;
                if (rb != null)
                {
                    // Crawl slowly rightward
                    rb.linearVelocity = new Vector2(0.8f, rb.linearVelocity.y);
                }
                yield return null;
            }

            if (rb != null) rb.linearVelocity = Vector2.zero;

            // Subtitle 2: Metamorphosis
            SetSubtitle("Ancient mana coalesces. The vessel of the Mage takes shape!", true);

            // Spawn arcane shockwave / aura burst
            SpawnArcaneBurst(player.transform.position);

            // Screen shake / juice
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Shake(1.2f, 0.6f);
            }

            yield return new WaitForSeconds(0.4f);

            // Release blob form -> Mage morphs!
            move.forceBlobIntro = false;

            // Metamorphosis burst duration
            yield return new WaitForSeconds(metamorphosisDuration);

            // Fade out subtitle
            SetSubtitle("", false);

            // Restore player controls
            move.ExternalMovementLock = false;
            PlayerPrefs.SetInt(PREF_BLOB_INTRO_PLAYED, 1);
            PlayerPrefs.Save();

            Debug.Log("<color=#D47BFF>[BlobMageIntroSequence] Cinematic metamorphosis complete! Mage body awakened.</color>");
        }

        private void SpawnArcaneBurst(Vector3 position)
        {
            GameObject burstGO = new GameObject("ArcaneBurstRing");
            burstGO.transform.position = position;

            SpriteRenderer sr = burstGO.AddComponent<SpriteRenderer>();
            sr.color = voidEnergyColor;
            sr.sortingOrder = 30;

            // Procedural ring sprite
            Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                    if (dist >= 24 && dist <= 31)
                    {
                        tex.SetPixel(x, y, voidEnergyColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32f);

            StartCoroutine(ExpandAndFadeRing(burstGO, sr));
        }

        private IEnumerator ExpandAndFadeRing(GameObject ring, SpriteRenderer sr)
        {
            float elapsed = 0f;
            float duration = 0.8f;
            Vector3 startScale = Vector3.one * 0.2f;
            Vector3 endScale = Vector3.one * 6.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                ring.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                sr.color = new Color(voidEnergyColor.r, voidEnergyColor.g, voidEnergyColor.b, 1f - t);
                yield return null;
            }

            Destroy(ring);
        }

        private void BuildCinematicUI()
        {
            GameObject canvasGO = new GameObject("BlobIntroCanvas");
            canvasGO.transform.SetParent(transform, false);

            introCanvas = canvasGO.AddComponent<Canvas>();
            introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            introCanvas.sortingOrder = 950;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Subtitle Text
            GameObject textGO = new GameObject("IntroSubtitleText");
            textGO.transform.SetParent(canvasGO.transform, false);
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.1f, 0.12f);
            textRT.anchorMax = new Vector2(0.9f, 0.22f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            introGroup = textGO.AddComponent<CanvasGroup>();
            introGroup.alpha = 0f;

            subtitleText = textGO.AddComponent<TextMeshProUGUI>();
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.fontSize = 22;
            subtitleText.fontStyle = FontStyles.Italic;
            subtitleText.color = new Color(0.92f, 0.85f, 1.0f, 0.95f);
        }

        private void SetSubtitle(string text, bool visible)
        {
            if (subtitleText != null) subtitleText.text = text;
            if (introGroup != null)
            {
                StopCoroutine("FadeSubtitleRoutine");
                StartCoroutine(FadeSubtitleRoutine(visible ? 1f : 0f));
            }
        }

        private IEnumerator FadeSubtitleRoutine(float targetAlpha)
        {
            float startAlpha = introGroup.alpha;
            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.deltaTime;
                introGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / 0.4f);
                yield return null;
            }
            introGroup.alpha = targetAlpha;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "SampleScene")
            {
                if (FindFirstObjectByType<BlobMageIntroSequence>() == null)
                {
                    GameObject go = new GameObject("[BlobMageIntroSequence]");
                    go.AddComponent<BlobMageIntroSequence>();
                }
            }
        }
    }
}
