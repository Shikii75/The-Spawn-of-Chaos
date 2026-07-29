using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerPixelDissolveFX - Satisfying Teleport Jump Visual Engine.
/// Features full-screen optic camera flashes, glowing radial lens flares, camera shake,
/// scale implosion/explosion pops, phantom afterimages, spinning pixel sparks,
/// and responsive squish & stretch landing juice!
/// </summary>
public class PlayerPixelDissolveFX : MonoBehaviour
{
    [Header("Visual Colors & Intensity")]
    public Color shadowColor = new Color(0.12f, 0.02f, 0.25f, 0.95f); // Deep Void Dark
    public Color glowColor = new Color(0.3f, 0.95f, 1.0f, 1.0f);     // Nyxaris Void Cyan
    public Color beamColor = new Color(1.0f, 0.35f, 1.0f, 1.0f);     // Arcane Magenta Flash
    public Color flashColor = new Color(0.7f, 0.95f, 1.0f, 0.45f);   // Optics Flash Color

    [Header("Juice Options")]
    public bool enableScreenFlash = true;
    public bool enableCameraShake = true;
    public bool enableSquishAndStretch = true;

    private SpriteRenderer playerSR;
    private Vector3 originalScale;
    private Coroutine squishCoroutine;

    private static Sprite pixelQuadSprite;
    private static Sprite circleRadialSprite;

    void Awake()
    {
        playerSR = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
        EnsureSprites();
    }

    /// <summary>
    /// Play full dynamic pixelated dissolve at origin, optic screen flash, lens flares,
    /// camera shake, and Mid-Air Rebuild at destination!
    /// </summary>
    public void PlayDissolveTeleport(Vector3 startPos, Vector3 endPos, System.Action onTeleportComplete)
    {
        StartCoroutine(DissolveAndRebuildRoutine(startPos, endPos, onTeleportComplete));
    }

    private IEnumerator DissolveAndRebuildRoutine(Vector3 startPos, Vector3 endPos, System.Action onTeleportComplete)
    {
        EnsureSprites();
        if (originalScale == Vector3.zero) originalScale = transform.localScale;

        // 1. Optic Screen Flash & Micro Camera Shake
        if (enableScreenFlash) SpawnScreenFlashOverlay();
        if (enableCameraShake) TriggerMicroCameraShake(0.08f, 0.15f);

        // 2. Glowing Radial Lens Flares at Origin & Destination
        SpawnGlowingLensFlare(startPos, glowColor, 3.2f);
        SpawnGlowingLensFlare(endPos, beamColor, 3.8f);

        // 3. Dematerialize (Origin Pixel Burst + Implosion Shockwave)
        SpawnPixelBurst(startPos, true, 36);
        SpawnShockwaveRing(startPos, glowColor, 3.0f);

        // 4. Spawn Ghost Phantom Afterimages along path
        SpawnPhantomAfterimages(startPos, endPos);

        // 5. Vertical Void Energy Beam Flash between start and end position
        SpawnEnergyBeam(startPos, endPos);

        // Implode player scale at origin before phase
        if (playerSR != null)
        {
            playerSR.color = new Color(0.3f, 1f, 1f, 0.3f);
        }

        yield return new WaitForSeconds(0.035f);

        // 6. Update Position to Destination
        transform.position = endPos;
        if (playerSR != null)
        {
            playerSR.color = Color.white;
        }

        // 7. Rematerialize (Destination Rebuild Converging Burst + Sparkles)
        SpawnPixelBurst(endPos, false, 42);
        SpawnShockwaveRing(endPos, beamColor, 3.5f);
        SpawnFloatingSparkles(endPos);

        // Arrival micro camera pop
        if (enableCameraShake) TriggerMicroCameraShake(0.06f, 0.12f);

        // 8. Play Squish & Stretch landing juice
        if (enableSquishAndStretch)
        {
            if (squishCoroutine != null) StopCoroutine(squishCoroutine);
            squishCoroutine = StartCoroutine(SquishAndStretchJuice());
        }

        onTeleportComplete?.Invoke();
    }

    private void SpawnScreenFlashOverlay()
    {
        GameObject canvasGO = new GameObject("TeleportFlashCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        GameObject imgGO = new GameObject("FlashImage");
        imgGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rt = imgGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = imgGO.AddComponent<Image>();
        img.color = flashColor;
        img.raycastTarget = false;

        imgGO.AddComponent<FlashCanvasAnim>().Initialize(0.06f);
    }

    private void TriggerMicroCameraShake(float duration, float intensity)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.gameObject.AddComponent<MicroCameraShakeAnim>().Initialize(duration, intensity);
        }
    }

    private void SpawnGlowingLensFlare(Vector3 pos, Color color, float targetScale)
    {
        GameObject flare = new GameObject("TeleportLensFlare");
        flare.transform.position = pos;

        SpriteRenderer sr = flare.AddComponent<SpriteRenderer>();
        sr.sprite = circleRadialSprite;
        sr.color = color;
        sr.sortingOrder = playerSR != null ? playerSR.sortingOrder + 3 : 18;

        flare.AddComponent<FlarePulseAnim>().Initialize(color, targetScale);
    }

    private void SpawnPhantomAfterimages(Vector3 start, Vector3 end)
    {
        if (playerSR == null || playerSR.sprite == null) return;

        int ghostCount = 4;
        for (int i = 1; i <= ghostCount; i++)
        {
            float t = (float)i / (ghostCount + 1);
            Vector3 pos = Vector3.Lerp(start, end, t);

            GameObject ghost = new GameObject($"TeleportGhost_{i}");
            ghost.transform.position = pos;
            ghost.transform.localScale = transform.localScale;
            ghost.transform.rotation = transform.rotation;

            SpriteRenderer gsr = ghost.AddComponent<SpriteRenderer>();
            gsr.sprite = playerSR.sprite;
            gsr.sortingOrder = playerSR.sortingOrder - 1;

            Color gColor = Color.Lerp(glowColor, beamColor, t);
            gColor.a = 0.7f - (t * 0.15f);
            gsr.color = gColor;

            ghost.AddComponent<GhostFadeAnim>().Initialize(0.20f);
        }
    }

    private void SpawnEnergyBeam(Vector3 start, Vector3 end)
    {
        GameObject beam = new GameObject("TeleportEnergyBeam");
        Vector3 mid = (start + end) * 0.5f;
        beam.transform.position = mid;

        float dist = Vector3.Distance(start, end);
        beam.transform.localScale = new Vector3(0.45f, dist, 1f);

        SpriteRenderer sr = beam.AddComponent<SpriteRenderer>();
        sr.sprite = pixelQuadSprite;
        sr.color = beamColor;
        sr.sortingOrder = playerSR != null ? playerSR.sortingOrder + 1 : 12;

        beam.AddComponent<BeamFlashAnim>().Initialize(0.09f);
    }

    private void SpawnPixelBurst(Vector3 centerPos, bool explodeOutward, int particleCount)
    {
        GameObject container = new GameObject("PixelDissolveBurst");
        container.transform.position = centerPos;

        for (int i = 0; i < particleCount; i++)
        {
            GameObject p = new GameObject("PixelParticle");
            p.transform.SetParent(container.transform, false);

            SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = pixelQuadSprite;
            sr.color = Random.value > 0.35f ? glowColor : (Random.value > 0.5f ? beamColor : shadowColor);
            sr.sortingOrder = playerSR != null ? playerSR.sortingOrder + 2 : 15;

            float scale = Random.Range(0.14f, 0.32f);
            p.transform.localScale = new Vector3(scale, scale, 1f);

            Vector2 spawnOffset = Random.insideUnitCircle * 0.55f;
            p.transform.localPosition = spawnOffset;

            Vector2 vel = explodeOutward ? (spawnOffset.normalized * Random.Range(7f, 16f)) : (-spawnOffset.normalized * Random.Range(8f, 17f));
            p.AddComponent<DynamicPixelParticleAnim>().Initialize(vel, explodeOutward);
        }

        Destroy(container, 0.5f);
    }

    private void SpawnFloatingSparkles(Vector3 centerPos)
    {
        GameObject container = new GameObject("SparkleBurst");
        container.transform.position = centerPos;

        for (int i = 0; i < 18; i++)
        {
            GameObject sp = new GameObject("Sparkle");
            sp.transform.SetParent(container.transform, false);
            sp.transform.localPosition = Random.insideUnitCircle * 0.6f;

            SpriteRenderer sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = circleRadialSprite;
            sr.color = Color.Lerp(glowColor, Color.white, Random.value);
            sr.sortingOrder = playerSR != null ? playerSR.sortingOrder + 3 : 16;

            float sz = Random.Range(0.15f, 0.35f);
            sp.transform.localScale = new Vector3(sz, sz, 1f);

            Vector2 vel = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, -3.5f));
            sp.AddComponent<SparkleAnim>().Initialize(vel, Random.Range(0.25f, 0.45f));
        }

        Destroy(container, 0.5f);
    }

    private void SpawnShockwaveRing(Vector3 pos, Color color, float maxScale)
    {
        GameObject ring = new GameObject("TeleportRing");
        ring.transform.position = pos;

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = circleRadialSprite;
        sr.color = color;
        sr.sortingOrder = playerSR != null ? playerSR.sortingOrder + 1 : 14;

        ring.AddComponent<DynamicRingExpandAnim>().Initialize(color, maxScale);
    }

    private IEnumerator SquishAndStretchJuice()
    {
        float signX = Mathf.Sign(transform.localScale.x);
        if (signX == 0) signX = 1f;

        float absX = 1.145f;
        float absY = 1.1842f;
        float absZ = 1.1042f;

        // Phase 1: Dynamic vertical stretch upon air arrival
        Vector3 stretch = new Vector3(signX * absX * 0.68f, absY * 1.42f, absZ);
        transform.localScale = stretch;

        yield return new WaitForSeconds(0.035f);

        // Phase 2: Horizontal squish rebound
        Vector3 squish = new Vector3(signX * absX * 1.28f, absY * 0.78f, absZ);
        transform.localScale = squish;

        yield return new WaitForSeconds(0.04f);

        // Phase 3: Settle back smoothly
        transform.localScale = new Vector3(signX * absX, absY, absZ);
    }

    private static void EnsureSprites()
    {
        if (pixelQuadSprite == null)
        {
            Texture2D tex = new Texture2D(16, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++) tex.SetPixel(x, y, Color.white);
            }
            tex.Apply();
            pixelQuadSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        }

        if (circleRadialSprite == null)
        {
            Texture2D tex2 = new Texture2D(64, 64);
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
                        tex2.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex2.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex2.Apply();
            circleRadialSprite = Sprite.Create(tex2, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64);
        }
    }
}

public class FlarePulseAnim : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color startColor;
    private float maxScale;
    private float timer = 0f;
    private float duration = 0.12f;

    public void Initialize(Color color, float scale)
    {
        sr = GetComponent<SpriteRenderer>();
        startColor = color;
        maxScale = scale;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (progress >= 1.0f)
        {
            Destroy(gameObject);
            return;
        }

        float s = Mathf.Lerp(0f, maxScale, Mathf.Sin(progress * Mathf.PI * 0.5f));
        transform.localScale = new Vector3(s, s, 1f);

        if (sr != null)
        {
            Color c = startColor;
            c.a = (1f - progress);
            sr.color = c;
        }
    }
}

public class FlashCanvasAnim : MonoBehaviour
{
    private Image img;
    private float duration;
    private float timer = 0f;

    public void Initialize(float dur)
    {
        img = GetComponent<Image>();
        duration = dur;
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        float progress = timer / duration;

        if (progress >= 1.0f)
        {
            Destroy(transform.root.gameObject);
            return;
        }

        if (img != null)
        {
            Color c = img.color;
            c.a *= (1f - Time.unscaledDeltaTime * 20f);
            img.color = c;
        }
    }
}

public class MicroCameraShakeAnim : MonoBehaviour
{
    private Vector3 originalPos;
    private float duration;
    private float intensity;
    private float timer = 0f;

    public void Initialize(float dur, float inten)
    {
        originalPos = transform.localPosition;
        duration = dur;
        intensity = inten;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            transform.localPosition = originalPos;
            Destroy(this);
            return;
        }

        Vector3 offset = Random.insideUnitSphere * intensity;
        offset.z = 0f;
        transform.localPosition = originalPos + offset;
    }
}

public class SparkleAnim : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 velocity;
    private float duration;
    private float timer = 0f;

    public void Initialize(Vector2 vel, float dur)
    {
        sr = GetComponent<SpriteRenderer>();
        velocity = vel;
        duration = dur;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (progress >= 1.0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)velocity * Time.deltaTime;

        if (sr != null)
        {
            Color c = sr.color;
            c.a = (1f - progress);
            sr.color = c;
        }
        transform.localScale *= (1f - Time.deltaTime * 2.5f);
    }
}

public class DynamicPixelParticleAnim : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 velocity;
    private bool isExplode;
    private float timer = 0f;
    private float duration = 0.3f;
    private float spinSpeed;

    public void Initialize(Vector2 vel, bool explode)
    {
        sr = GetComponent<SpriteRenderer>();
        velocity = vel;
        isExplode = explode;
        spinSpeed = Random.Range(-720f, 720f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (progress >= 1.0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)velocity * Time.deltaTime;
        transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
        velocity *= (1f - Time.deltaTime * 5f); // Drag

        if (sr != null)
        {
            Color c = sr.color;
            c.a = 1f - progress;
            sr.color = c;
        }
        transform.localScale *= (1f - Time.deltaTime * 3.5f);
    }
}

public class DynamicRingExpandAnim : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color startColor;
    private float maxScale;
    private float timer = 0f;
    private float duration = 0.22f;

    public void Initialize(Color col, float targetScale)
    {
        sr = GetComponent<SpriteRenderer>();
        startColor = col;
        maxScale = targetScale;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (progress >= 1.0f)
        {
            Destroy(gameObject);
            return;
        }

        float scale = Mathf.Lerp(0f, maxScale, Mathf.Sin(progress * Mathf.PI * 0.5f));
        transform.localScale = new Vector3(scale, scale * 0.45f, 1f);

        if (sr != null)
        {
            Color c = startColor;
            c.a = 1f - progress;
            sr.color = c;
        }
    }
}

public class GhostFadeAnim : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration;
    private float timer = 0f;

    public void Initialize(float dur)
    {
        sr = GetComponent<SpriteRenderer>();
        duration = dur;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (progress >= 1.0f)
        {
            Destroy(gameObject);
            return;
        }

        if (sr != null)
        {
            Color c = sr.color;
            c.a *= (1f - Time.deltaTime * 12f);
            sr.color = c;
        }
    }
}

public class BeamFlashAnim : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration;
    private float timer = 0f;

    public void Initialize(float dur)
    {
        sr = GetComponent<SpriteRenderer>();
        duration = dur;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;

        if (progress >= 1.0f)
        {
            Destroy(gameObject);
            return;
        }

        if (sr != null)
        {
            Color c = sr.color;
            c.a = (1f - progress);
            sr.color = c;
        }
        transform.localScale = new Vector3(transform.localScale.x * (1f - Time.deltaTime * 10f), transform.localScale.y, 1f);
    }
}
