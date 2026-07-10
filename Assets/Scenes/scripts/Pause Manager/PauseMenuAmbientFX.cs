using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ambient visual effects for the dark fantasy pause overlay. Spawns many soft
/// glowing orbs that drift upward and pulse gently. Uses unscaled time so
/// animations continue while Time.timeScale is 0.
/// </summary>
public class PauseMenuAmbientFX : MonoBehaviour
{
    private Image outerGlow;
    private RectTransform orbContainer;

    private Color glowBaseColor;
    private float glowPulseSpeed = 1.0f;

    private struct Orb
    {
        public RectTransform rt;
        public Image img;
        public Vector2 velocity;
        public float lifetime;
        public float maxLifetime;
        public float baseSize;
        public float pulsePhase;
        public float pulseSpeed;
        public int colorType; // 0 = bright purple, 1 = violet, 2 = faint white, 3 = deep magenta
    }

    private Orb[] orbs;
    private int orbCount = 45; // many more orbs

    public void Initialize(Image glow, RectTransform orbsRoot, Color glowColor)
    {
        outerGlow = glow;
        orbContainer = orbsRoot;
        glowBaseColor = glowColor;

        orbs = new Orb[orbCount];
        for (int i = 0; i < orbCount; i++)
        {
            SpawnOrb(i, Random.Range(0f, 1f));
        }
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy)
            return;

        float dt = Time.unscaledDeltaTime;

        PulseGlow(dt);
        UpdateOrbs(dt);
    }

    private void PulseGlow(float dt)
    {
        if (outerGlow == null)
            return;

        // Slow, breathing pulse — mystical feel
        float pulse = 0.60f + Mathf.Sin(Time.unscaledTime * glowPulseSpeed) * 0.18f
                            + Mathf.Sin(Time.unscaledTime * glowPulseSpeed * 0.6f) * 0.08f;
        Color c = glowBaseColor;
        c.a = glowBaseColor.a * pulse;
        outerGlow.color = c;
    }

    private void UpdateOrbs(float dt)
    {
        if (orbContainer == null || orbs == null)
            return;

        for (int i = 0; i < orbs.Length; i++)
        {
            ref Orb o = ref orbs[i];
            if (o.rt == null)
                continue;

            o.lifetime += dt;

            // Gentle drifting with slight sine wobble (organic, not linear)
            float wobble = Mathf.Sin(Time.unscaledTime * 1.5f + o.pulsePhase) * 6f;
            Vector2 drift = new Vector2(o.velocity.x + wobble * dt, o.velocity.y);
            o.rt.anchoredPosition += drift * dt;

            // Lifecycle alpha: fade in, sustain, fade out
            float t = o.lifetime / o.maxLifetime;
            float fadeIn = Mathf.Clamp01(t * 4f);  // quick fade in over first 25%
            float fadeOut = Mathf.Clamp01((1f - t) * 3f); // fade out over last 33%
            float baseAlpha = fadeIn * fadeOut;

            // Individual orb pulsing
            float pulse = 0.7f + Mathf.Sin(Time.unscaledTime * o.pulseSpeed + o.pulsePhase) * 0.3f;
            float finalAlpha = baseAlpha * pulse;

            // Size alpha multiplier varies by color type for depth
            float alphaMax;
            switch (o.colorType)
            {
                case 0: alphaMax = 0.55f; break; // bright purple — most visible
                case 1: alphaMax = 0.40f; break; // violet
                case 2: alphaMax = 0.18f; break; // faint white — background depth
                case 3: alphaMax = 0.35f; break; // deep magenta
                default: alphaMax = 0.35f; break;
            }

            Color c = o.img.color;
            c.a = finalAlpha * alphaMax;
            o.img.color = c;

            // Gentle size pulsing
            float sizePulse = o.baseSize * (0.9f + Mathf.Sin(Time.unscaledTime * o.pulseSpeed * 0.5f + o.pulsePhase) * 0.15f);
            o.rt.sizeDelta = new Vector2(sizePulse, sizePulse);

            // Respawn if lifetime exceeded or drifted too far
            if (o.lifetime >= o.maxLifetime ||
                Mathf.Abs(o.rt.anchoredPosition.y) > orbContainer.rect.height * 0.55f ||
                Mathf.Abs(o.rt.anchoredPosition.x) > orbContainer.rect.width * 0.55f)
            {
                SpawnOrb(i, 0f);
            }
        }
    }

    private void SpawnOrb(int index, float lifetimeOffset)
    {
        if (orbContainer == null)
            return;

        RectTransform rt;
        Image img;

        if (orbs[index].rt != null)
        {
            rt = orbs[index].rt;
            img = orbs[index].img;
        }
        else
        {
            GameObject go = new GameObject("Orb");
            go.transform.SetParent(orbContainer, false);
            rt = go.AddComponent<RectTransform>();
            img = go.AddComponent<Image>();
            img.raycastTarget = false;

            // Use a soft circle sprite
            img.sprite = CreateSoftOrbSprite(32);
        }

        float w = orbContainer.rect.width;
        float h = orbContainer.rect.height;

        // Spawn across the full screen area
        rt.anchoredPosition = new Vector2(
            Random.Range(-w * 0.48f, w * 0.48f),
            Random.Range(-h * 0.48f, h * 0.48f));

        // Varied sizes: some tiny background particles, some larger focal orbs
        float roll = Random.value;
        float baseSize;
        int colorType;

        if (roll < 0.30f)
        {
            // Large focal orbs (bright purple)
            baseSize = Random.Range(8f, 16f);
            colorType = 0;
            img.color = new Color(0.78f, 0.18f, 1f, 0f);
        }
        else if (roll < 0.55f)
        {
            // Medium violet orbs
            baseSize = Random.Range(5f, 10f);
            colorType = 1;
            img.color = new Color(0.50f, 0.30f, 0.90f, 0f);
        }
        else if (roll < 0.80f)
        {
            // Small faint white orbs (depth particles)
            baseSize = Random.Range(2f, 5f);
            colorType = 2;
            img.color = new Color(0.85f, 0.75f, 1f, 0f);
        }
        else
        {
            // Deep magenta orbs
            baseSize = Random.Range(6f, 12f);
            colorType = 3;
            img.color = new Color(0.90f, 0.10f, 0.65f, 0f);
        }

        rt.sizeDelta = new Vector2(baseSize, baseSize);

        orbs[index] = new Orb
        {
            rt = rt,
            img = img,
            velocity = new Vector2(Random.Range(-5f, 5f), Random.Range(6f, 18f)),
            lifetime = lifetimeOffset,
            maxLifetime = Random.Range(4f, 10f),
            baseSize = baseSize,
            pulsePhase = Random.Range(0f, Mathf.PI * 2f),
            pulseSpeed = Random.Range(1.2f, 3.0f),
            colorType = colorType
        };
    }

    /// <summary>Creates a soft glowing circle texture for orbs.</summary>
    private static Sprite CreateSoftOrbSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                // Smooth gaussian-like falloff
                float alpha;
                if (dist <= 0.3f)
                    alpha = 1f;
                else if (dist <= 1f)
                    alpha = Mathf.Pow(1f - (dist - 0.3f) / 0.7f, 2f);
                else
                    alpha = 0f;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
