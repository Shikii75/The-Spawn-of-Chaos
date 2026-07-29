using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// MainMenuRainingBlackOrbsFX - Plain black raining chaos orbs for the Main Menu.
/// Features sleek, solid plain black orbs raining continuously across the screen
/// with responsive mouse parallax depth and subtle click bursts.
/// </summary>
public class MainMenuRainingBlackOrbsFX : MonoBehaviour, IPointerDownHandler
{
    [Header("Orb Configuration")]
    public int orbCount = 85;
    public float minFallSpeed = 140f;
    public float maxFallSpeed = 380f;
    public float minOrbSize = 8f;
    public float maxOrbSize = 34f; // Reduced size for sleek, refined visual balance

    [Header("Visual Color")]
    public Color plainBlackColor = new Color(0.0f, 0.0f, 0.0f, 0.92f); // Pure Solid Plain Black

    private RectTransform canvasRT;
    private RectTransform orbContainer;
    private Image ambientGlowVignette;

    private struct PlainBlackOrb
    {
        public RectTransform rt;
        public Image img;
        public Vector2 position;
        public float fallSpeed;
        public float wobbleFrequency;
        public float wobbleAmplitude;
        public float size;
        public float phase;
        public float baseAlpha;
    }

    private PlainBlackOrb[] orbs;
    private static Sprite circleSprite;

    public void Initialize(Canvas rootCanvas, RectTransform parentPanel = null)
    {
        canvasRT = rootCanvas.GetComponent<RectTransform>();
        Transform targetParent = parentPanel != null ? (Transform)parentPanel : rootCanvas.transform;

        // Create Container under targetParent
        GameObject containerGO = new GameObject("BlackOrbRainContainer");
        containerGO.transform.SetParent(targetParent, false);

        orbContainer = containerGO.AddComponent<RectTransform>();
        orbContainer.anchorMin = Vector2.zero;
        orbContainer.anchorMax = Vector2.one;
        orbContainer.offsetMin = Vector2.zero;
        orbContainer.offsetMax = Vector2.zero;

        // Place at sibling index 0 of MainPanel so it renders right in front of background art
        orbContainer.SetAsFirstSibling();

        // Background Raycast Target to catch clicks for particle explosions
        Image clickCatcher = containerGO.AddComponent<Image>();
        clickCatcher.color = Color.clear;
        clickCatcher.raycastTarget = true;

        // Subtle Ambient Dark Vignette Image
        GameObject glowGO = new GameObject("AmbientVoidGlow");
        glowGO.transform.SetParent(orbContainer, false);
        RectTransform glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = Vector2.zero;
        glowRT.offsetMax = Vector2.zero;

        ambientGlowVignette = glowGO.AddComponent<Image>();
        ambientGlowVignette.color = new Color(0.02f, 0.01f, 0.04f, 0.35f);
        ambientGlowVignette.raycastTarget = false;

        EnsureCircleSprite();

        // Spawn Plain Black Orb Rain
        orbs = new PlainBlackOrb[orbCount];
        for (int i = 0; i < orbCount; i++)
        {
            SpawnOrb(i, true);
        }
    }

    void Update()
    {
        if (orbs == null || canvasRT == null) return;

        float dt = Time.unscaledDeltaTime;
        float canvasWidth = canvasRT.rect.width > 0 ? canvasRT.rect.width : Screen.width;
        float canvasHeight = canvasRT.rect.height > 0 ? canvasRT.rect.height : Screen.height;
        float halfWidth = canvasWidth * 0.5f;
        float halfHeight = canvasHeight * 0.5f;

        // Interactive mouse parallax tilt
        Vector2 mouseNorm = (Input.mousePosition - new Vector3(Screen.width * 0.5f, Screen.height * 0.5f)) / (Screen.width * 0.5f);
        float parallaxX = mouseNorm.x * 25f;

        // Pulse ambient vignette background
        if (ambientGlowVignette != null)
        {
            float pulse = 0.30f + 0.10f * Mathf.Sin(Time.unscaledTime * 1.5f);
            ambientGlowVignette.color = new Color(0.02f, 0.01f, 0.04f, pulse);
        }

        for (int i = 0; i < orbs.Length; i++)
        {
            orbs[i].phase += dt * orbs[i].wobbleFrequency;
            float wobbleX = Mathf.Sin(orbs[i].phase) * orbs[i].wobbleAmplitude;

            // Fall downward
            orbs[i].position.y -= orbs[i].fallSpeed * dt;
            orbs[i].position.x += wobbleX * dt;

            // Apply position with depth parallax
            float depthFactor = (orbs[i].size / maxOrbSize);
            Vector2 finalPos = orbs[i].position + new Vector2(parallaxX * depthFactor, 0f);
            orbs[i].rt.anchoredPosition = finalPos;

            // Pulse alpha
            if (orbs[i].img != null)
            {
                float currentAlpha = orbs[i].baseAlpha * (0.85f + 0.15f * Mathf.Sin(orbs[i].phase * 2f));
                orbs[i].img.color = new Color(0f, 0f, 0f, currentAlpha);
            }

            // Reset when falling past bottom of screen
            if (orbs[i].position.y < -halfHeight - 60f)
            {
                SpawnOrb(i, false);
            }
        }
    }

    private void SpawnOrb(int index, bool randomizeY)
    {
        float canvasWidth = canvasRT != null && canvasRT.rect.width > 0 ? canvasRT.rect.width : 1920f;
        float canvasHeight = canvasRT != null && canvasRT.rect.height > 0 ? canvasRT.rect.height : 1080f;
        float halfWidth = canvasWidth * 0.5f;
        float halfHeight = canvasHeight * 0.5f;

        if (orbs[index].rt == null)
        {
            // Single Plain Black Circle Image
            GameObject orbGO = new GameObject($"PlainBlackOrb_{index}");
            orbGO.transform.SetParent(orbContainer, false);

            RectTransform rt = orbGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = orbGO.AddComponent<Image>();
            img.sprite = circleSprite;
            img.color = plainBlackColor;
            img.raycastTarget = false;

            orbs[index].rt = rt;
            orbs[index].img = img;
        }

        float size = Random.Range(minOrbSize, maxOrbSize);
        orbs[index].size = size;

        // Sleek droplet proportioning
        orbs[index].rt.sizeDelta = new Vector2(size, size * Random.Range(1.1f, 1.4f));

        float startX = Random.Range(-halfWidth - 60f, halfWidth + 60f);
        float startY = randomizeY ? Random.Range(-halfHeight, halfHeight + 100f) : halfHeight + Random.Range(20f, 100f);

        orbs[index].position = new Vector2(startX, startY);
        orbs[index].fallSpeed = Random.Range(minFallSpeed, maxFallSpeed);
        orbs[index].wobbleFrequency = Random.Range(1.2f, 3.5f);
        orbs[index].wobbleAmplitude = Random.Range(8f, 22f);
        orbs[index].phase = Random.Range(0f, Mathf.PI * 2f);
        orbs[index].baseAlpha = Random.Range(0.6f, 0.95f);

        orbs[index].rt.anchoredPosition = orbs[index].position;
    }

    /// <summary>
    /// Interactive click burst: Spawns plain black droplets on click!
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(orbContainer, eventData.position, eventData.pressEventCamera, out localPoint);
        TriggerBlackBurst(localPoint);
    }

    private void TriggerBlackBurst(Vector2 position)
    {
        for (int i = 0; i < 14; i++)
        {
            GameObject particle = new GameObject("BlackBurstParticle");
            particle.transform.SetParent(orbContainer, false);

            RectTransform rt = particle.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            float sz = Random.Range(10f, 26f);
            rt.sizeDelta = new Vector2(sz, sz);

            Image img = particle.AddComponent<Image>();
            img.sprite = circleSprite;
            img.color = new Color(0f, 0f, 0f, 0.9f);
            img.raycastTarget = false;

            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(180f, 450f);
            particle.AddComponent<BurstParticleAnim>().Initialize(dir, 0.45f);
        }
    }

    private static void EnsureCircleSprite()
    {
        if (circleSprite != null) return;

        Texture2D tex = new Texture2D(64, 64);
        Vector2 center = new Vector2(32f, 32f);
        float radius = 30f;

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = Mathf.SmoothStep(1f, 0f, dist / radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64);
    }

    /// <summary>
    /// Helper animation component for burst particles on click.
    /// </summary>
    public class BurstParticleAnim : MonoBehaviour
    {
        private RectTransform rt;
        private Image img;
        private Vector2 velocity;
        private float lifetime;
        private float maxLifetime;

        public void Initialize(Vector2 vel, float duration)
        {
            rt = GetComponent<RectTransform>();
            img = GetComponent<Image>();
            velocity = vel;
            maxLifetime = duration;
            lifetime = 0f;
        }

        void Update()
        {
            lifetime += Time.unscaledDeltaTime;
            float progress = lifetime / maxLifetime;

            if (progress >= 1.0f)
            {
                Destroy(gameObject);
                return;
            }

            rt.anchoredPosition += velocity * Time.unscaledDeltaTime;
            velocity *= (1f - Time.unscaledDeltaTime * 3f); // Drag

            if (img != null)
            {
                Color c = img.color;
                c.a = (1f - progress) * 0.9f;
                img.color = c;
            }
            transform.localScale = Vector3.one * (1f - progress * 0.5f);
        }
    }
}
