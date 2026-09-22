using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SpearSlashVFX — Premium Hollow-Knight-tier dark-themed spear combo slash VFX.
///
/// Architecture & Visual Design:
///   • 256px HD procedural sprites generated at runtime with zero asset dependencies:
///       - razor crescent (asymmetric beak, razor cutting edge, graceful taper)
///       - crescent bloom (exact curvature match, dual-exponential gaussian aura)
///       - crescent void core (pure abyssal silhouette body)
///       - supersonic thrust needle (Mach cone, shock diamonds, slipstream)
///       - 4-point star gleam (anime/Hollow Knight style impact flare)
///       - cutting streaks (triple-line razor speed cuts)
///       - hollow distortion ring & spark dots
///   • Multi-layered slash compositing per combo step:
///       0. Wide crescent bloom envelope (additive glow halo conforming to the arc)
///       1. Luminous razor edge crescent (vibrant step color outline)
///       2. Abyssal void core blade (pitch-black obsidian cutting silhouette)
///       3. Searing white-hot rim flash (blinding initial edge, 45ms decay)
///       4. Spear-tip star gleam flare (pinpoint impact flash at weapon tip)
///       5. Speed wind cuts (directional slices shooting forward along tangent)
///       6. Directional spark fountain (high-velocity embers spraying with gravity)
///       7. Void debris motes (dark matter floating upward defying gravity)
///       8. Expanding hollow distortion shockwave ring
///   • Triple-layer Catmull-Rom smoothed spline ribbon trail:
///       - Layer 1: Abyssal void shadow under-trail (adds weight and dark fantasy grit)
///       - Layer 2: Luminous bloom trail (rich additive glow)
///       - Layer 3: White-hot blade core edge (razor-sharp cutting line)
///       - Fixed head-to-tail width & alpha curves (100% opaque at spear tip, tapering into mist)
///       - Camera-facing View alignment to prevent 2D Z-tilt clipping
///   • Surgical trajectory alignment:
///       - Crescent arc is anchored at swing center with dynamic radius matching the spear tip reach
///       - Dynamic rotation aligns with the swing arc (downward cleave for Step 1, upward cleave for Step 2)
///       - Tip flare, sparks, and speed lines originate directly from the spear tip
///   • Hollow Knight snappy timing:
///       - 80–110 ms total slash window
///       - Kinetic ease-out-back snap in first 18% of duration
///       - Rapid cubic alpha decay for crisp blade dissolution
///   • Camera shake & impact juice:
///       - Integrated with CameraShakeManager (0.06f light, 0.09f medium, 0.14f finisher)
///       - OnSpearHitEnemy callback for cross-slash flash, impact sparks, and blood motes
///   • Robust fallback material system:
///       - Multi-tier shader resolution (URP 2D, Sprites/Default, Particles Additive, Unlit)
///       - Zero null material exceptions in any build or play mode configuration
/// </summary>
public class SpearSlashVFX : MonoBehaviour
{
    public static SpearSlashVFX Instance { get; private set; }

    [Header("Trail Density & Timing")]
    [Tooltip("Minimum normalized arc delta between trail segment spawns.")]
    public float trailInterval = 0.012f;
    [Tooltip("Minimum normalized arc progress before trail segments appear.")]
    public float trailStartProgress = 0.02f;

    [Header("Sorting Orders")]
    public int slashSortOrder = 82;
    public int trailSortOrder = 78;
    public int glowSortOrder = 74;

    [Header("Camera Shake Profile")]
    public float slashShakeIntensity = 0.06f;
    public float slashShakeDuration = 0.08f;
    public float slash2ShakeIntensity = 0.09f;
    public float slash2ShakeDuration = 0.09f;
    public float thrustShakeIntensity = 0.14f;
    public float thrustShakeDuration = 0.13f;

    // ── Internal State ─────────────────────────────────────────────
    private int activeComboStep;
    private float activeFacing;
    private bool primarySlashSpawned;
    private Vector3 lastTipPos;
    private float lastSpawnProgress;

    // Ribbon Trail (Catmull-Rom smoothed spline)
    private GameObject ribbonRoot;
    private LineRenderer shadowRibbon;
    private LineRenderer glowRibbon;
    private LineRenderer coreRibbon;
    private readonly List<Vector3> rawRibbonPoints = new List<Vector3>(48);
    private readonly List<Vector3> smoothedRibbonPoints = new List<Vector3>(128);
    private bool ribbonFading;
    private Coroutine ribbonFadeCoroutine;

    // Procedural Sprites (static cache, dual-null safe)
    private static Sprite crescentHD;         // 256px razor crescent blade
    private static Sprite crescentBloomHD;    // 256px matching crescent bloom halo
    private static Sprite crescentVoidCoreHD; // 256px solid pitch void silhouette
    private static Sprite thrustNeedleHD;     // 256×96 supersonic Mach cone wedge
    private static Sprite starGleamHD;        // 64px 4-point cross star flare
    private static Sprite cuttingStreaksHD;   // 128×20 triple-line razor speed cuts
    private static Sprite hollowRingHD;       // 128px anti-aliased distortion shockwave
    private static Sprite sparkDotHD;         // 32px point light spark

    // Materials
    private static Material defaultSpriteMat;
    private static Material additiveMat;

    private readonly List<Coroutine> runningCoroutines = new List<Coroutine>(32);

    // ── Dark-Theme Palettes ────────────────────────────────────────
    public struct SlashPalette
    {
        public Color voidCore;      // Deep abyssal obsidian blade interior
        public Color paleEdge;      // Searing luminous razor edge highlight
        public Color bloomGlow;     // Atmospheric volumetric bloom aura
        public Color sparkColor;    // High-speed cutting spark color
        public Color sparkTip;      // Spark core flash color
        public Color starGleam;     // Tip impact star flare color
        public float primaryScale;  // Scale multiplier for the slash arc
        public float trailScale;    // Scale for discrete trail motes
    }

    // ── Singleton & Retrieval ──────────────────────────────────────

    public static SpearSlashVFX GetOrCreate(Transform player = null)
    {
        if (Instance != null) return Instance;

        if (player != null)
        {
            var onPlayer = player.GetComponent<SpearSlashVFX>();
            if (onPlayer != null) return onPlayer;
            return player.gameObject.AddComponent<SpearSlashVFX>();
        }

        var existing = FindFirstObjectByType<SpearSlashVFX>();
        if (existing != null) return existing;

        GameObject vfxGO = new GameObject("SpearSlashVFX_System");
        return vfxGO.AddComponent<SpearSlashVFX>();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
        EnsureAssets();
    }

    void OnDisable()
    {
        KillRibbon();
        StopAllManaged();
    }

    void OnDestroy()
    {
        KillRibbon();
        StopAllManaged();
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════════════
    // PUBLIC API — Called by LumiSpearWeapon combo routines
    // ══════════════════════════════════════════════════════════════

    /// <summary>Called at the start of each spear melee swing.</summary>
    public void BeginComboSlash(int comboStep, float facing, Vector3 playerPos)
    {
        KillRibbon();
        activeComboStep = comboStep;
        activeFacing = (facing < 0f) ? -1f : 1f;
        primarySlashSpawned = (comboStep == 3); // Combo 3 thrust spawns via SpawnThrustFinisher
        lastSpawnProgress = -1f;
        lastTipPos = playerPos;

        EnsureAssets();
    }

    /// <summary>Called each frame during arc/thrust coroutines with normalized progress 0–1.</summary>
    public void OnArcProgress(int comboStep, float facing, Vector3 tipWorldPos,
                              Vector3 arcCenter, float normalizedProgress, float spearAngleDeg)
    {
        if (comboStep != activeComboStep) return;

        float dir = (facing < 0f) ? -1f : 1f;
        float t = Mathf.Clamp01(normalizedProgress);

        // ── Kinetic peak moment impact juice (shake + tip flash) ──
        if (!primarySlashSpawned)
        {
            float peak = (comboStep == 1) ? 0.36f : (comboStep == 2 ? 0.40f : 0.50f);
            if (t >= peak)
            {
                primarySlashSpawned = true;
                var pal = GetPalette(comboStep);
                SpawnStarGleam(tipWorldPos, pal.starGleam, 1.0f, 0.06f);

                float shakeMag = (comboStep == 1) ? slashShakeIntensity : slash2ShakeIntensity;
                float shakeDur = (comboStep == 1) ? slashShakeDuration : slash2ShakeDuration;
                TryShake(shakeMag, shakeDur);
            }
        }

        lastTipPos = tipWorldPos;
    }

    /// <summary>Combo 3 Sonic Piercing Finisher — clean focused thrust with spark spray and camera punch.</summary>
    public void SpawnThrustFinisher(Vector3 startPos, float facing, float distance)
    {
        EnsureAssets();
        float dir = (facing < 0f) ? -1f : 1f;
        var pal = GetPalette(3);

        Vector3 endPos = startPos + new Vector3(dir * distance, 0f, 0f);

        // 1. White-Hot Piercing Needle Streak along centerline
        GameObject streak = MakeGO("ThrustCoreStreak");
        streak.transform.position = startPos;
        streak.transform.rotation = Quaternion.Euler(0f, 0f, dir < 0f ? 180f : 0f);
        streak.transform.localScale = new Vector3(0.70f, 0.40f, 1f);
        var streakSr = AddSprite(streak, cuttingStreaksHD, Color.white, slashSortOrder + 5, true);
        StartManaged(AnimateThrustStreak(streak, streakSr, startPos, endPos, 0.09f));

        // 2. Blinding Supernova Star Flare at apex
        SpawnStarGleam(endPos, pal.starGleam, 1.8f, 0.10f);

        // 3. Supersonic Spark Spray Fountain
        SpawnSparkBurst(endPos, dir, pal, 3, 18, 0.14f);

        // 4. Punchy Camera Shake
        TryShake(thrustShakeIntensity, thrustShakeDuration);
    }

    /// <summary>High-impact feedback when the spear strikes an enemy.</summary>
    public void OnSpearHitEnemy(Vector3 hitPos, int comboStep, float dir)
    {
        EnsureAssets();
        var pal = GetPalette(comboStep);

        // 1. Searing Star Gleam Flare at contact point
        SpawnStarGleam(hitPos, pal.starGleam, comboStep == 3 ? 1.8f : 1.25f, 0.08f);

        // 2. Micro Cross-Slash Flash
        SpawnImpactCross(hitPos, pal, comboStep);

        // 3. Impact Spark Burst spraying away from spear
        int sparks = comboStep == 3 ? 16 : (comboStep == 2 ? 10 : 7);
        SpawnSparkBurst(hitPos, dir, pal, comboStep, sparks, 0.11f);

        // 4. Void Blood Motes
        SpawnVoidDebris(hitPos, dir, pal, comboStep == 3 ? 6 : 3, 0.16f);

        // 5. Micro camera shake pulse
        float shakeMag = comboStep == 3 ? 0.10f : (comboStep == 2 ? 0.06f : 0.04f);
        TryShake(shakeMag, 0.06f);
    }

    // ══════════════════════════════════════════════════════════════
    // PRIMARY SLASH COMPOSITING (Peak "Hit" Moment)
    // ══════════════════════════════════════════════════════════════

    void SpawnPrimarySlash(int step, float dir, Vector3 tipPos, float spearAngle, Vector3 arcCenter)
    {
        // Big procedural crescent slash removed per design:
        // Attack visuals are now focused directly on the multiplying black spear silhouette trail.
    }

    // ══════════════════════════════════════════════════════════════
    // TRAIL SEGMENTS — High-speed afterimage crescents along arc
    // ══════════════════════════════════════════════════════════════

    void SpawnTrailSegment(int step, float dir, Vector3 tipPos, float tangentDeg, float progress)
    {
        var pal = GetPalette(step);
        float fadeT = Mathf.Clamp01((progress - trailStartProgress) / (1f - trailStartProgress));
        float alpha = Mathf.Lerp(0.75f, 0.15f, fadeT);

        // Luminous edge trail mote
        {
            var go = MakeGO($"TrailEdge_{step}");
            go.transform.position = tipPos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, tangentDeg);
            go.transform.localScale = new Vector3(dir * pal.trailScale, pal.trailScale * 0.75f, 1f);
            Color c = pal.paleEdge;
            c.a = alpha * 0.70f;
            var sr = AddSprite(go, cuttingStreaksHD, c, trailSortOrder, true);
            StartManaged(AnimateTrailFade(go, sr, 0.07f));
        }

        // Soft bloom envelope mote
        {
            var go = MakeGO($"TrailBloom_{step}");
            go.transform.position = tipPos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, tangentDeg);
            go.transform.localScale = new Vector3(dir * pal.trailScale * 1.4f, pal.trailScale * 1.1f, 1f);
            Color c = pal.bloomGlow;
            c.a = alpha * 0.40f;
            var sr = AddSprite(go, cuttingStreaksHD, c, glowSortOrder - 1, true);
            StartManaged(AnimateTrailFade(go, sr, 0.08f));
        }
    }

    // ══════════════════════════════════════════════════════════════
    // RIBBON TRAIL — Triple-Layer Catmull-Rom Smoothed Blade Ribbon
    // ══════════════════════════════════════════════════════════════

    void CreateRibbon(int step)
    {
        ribbonRoot = new GameObject($"SpearRibbonRoot_{step}");
        rawRibbonPoints.Clear();
        smoothedRibbonPoints.Clear();
        ribbonFading = false;

        var pal = GetPalette(step);
        float baseWidth = (step == 3) ? 1.25f : 1.0f;

        // Layer 1: Abyssal void shadow under-trail (dark fantasy weight)
        shadowRibbon = MakeRibbonLayer("ShadowLayer", trailSortOrder - 2,
            0.36f * baseWidth, pal.voidCore, GetSpriteMaterial(), false);

        // Layer 2: Atmospheric luminous bloom ribbon (rich vibrant glow)
        glowRibbon = MakeRibbonLayer("BloomLayer", glowSortOrder,
            0.24f * baseWidth, pal.bloomGlow, GetAdditiveMaterial(), true);

        // Layer 3: Razor searing core ribbon (white-hot cutting blade path)
        coreRibbon = MakeRibbonLayer("CoreLayer", slashSortOrder - 1,
            0.075f * baseWidth, pal.paleEdge, GetAdditiveMaterial(), true);
    }

    LineRenderer MakeRibbonLayer(string name, int order, float maxWidth, Color color, Material mat, bool isAdditive)
    {
        var layer = new GameObject(name);
        layer.transform.SetParent(ribbonRoot.transform, false);
        var line = layer.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        line.sortingOrder = order;
        if (mat != null) line.material = mat;

        // HEAD-TO-TAIL WIDTH:
        // index 0 (oldest point / tail) -> narrow (0.05)
        // index N-1 (current spear tip / head) -> full width (1.0)
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(new Keyframe(0f, 0.05f));
        widthCurve.AddKey(new Keyframe(0.35f, 0.40f));
        widthCurve.AddKey(new Keyframe(0.75f, 0.88f));
        widthCurve.AddKey(new Keyframe(1f, 1.0f));
        line.widthCurve = widthCurve;
        line.widthMultiplier = maxWidth;

        // HEAD-TO-TAIL ALPHA GRADIENT:
        // index 0 (tail) -> transparent (0f)
        // index N-1 (head) -> 100% opaque (1f)
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(color, 0f),
                new GradientColorKey(Color.Lerp(color, Color.white, isAdditive ? 0.40f : 0f), 0.70f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a * 0.45f, 0.35f),
                new GradientAlphaKey(color.a * 0.88f, 0.70f),
                new GradientAlphaKey(color.a, 1f)
            }
        );
        line.colorGradient = grad;

        return line;
    }

    void AppendRibbonPoint(Vector3 point, float progress)
    {
        if (ribbonRoot == null) return;
        point.z = -0.05f;

        // Threshold to avoid duplicate points
        if (rawRibbonPoints.Count == 0 || Vector3.Distance(rawRibbonPoints[rawRibbonPoints.Count - 1], point) > 0.02f)
        {
            rawRibbonPoints.Add(point);
        }

        if (rawRibbonPoints.Count < 2) return;

        // Catmull-Rom Spline Interpolation for butter-smooth ribbon arcs
        smoothedRibbonPoints.Clear();
        int count = rawRibbonPoints.Count;
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = i > 0 ? rawRibbonPoints[i - 1] : rawRibbonPoints[i];
            Vector3 p1 = rawRibbonPoints[i];
            Vector3 p2 = rawRibbonPoints[i + 1];
            Vector3 p3 = (i + 2 < count) ? rawRibbonPoints[i + 2] : p2;

            int subSteps = 3;
            for (int s = 0; s < subSteps; s++)
            {
                float st = s / (float)subSteps;
                smoothedRibbonPoints.Add(CatmullRom(p0, p1, p2, p3, st));
            }
        }
        smoothedRibbonPoints.Add(rawRibbonPoints[count - 1]);

        var pts = smoothedRibbonPoints.ToArray();
        UpdateRibbonLayer(shadowRibbon, pts, 1.0f);
        UpdateRibbonLayer(glowRibbon, pts, 0.85f);
        UpdateRibbonLayer(coreRibbon, pts, 0.65f);
    }

    void UpdateRibbonLayer(LineRenderer line, Vector3[] pts, float widthScale)
    {
        if (line == null) return;
        line.positionCount = pts.Length;
        line.SetPositions(pts);
    }

    IEnumerator FadeRibbon(float duration)
    {
        float elapsed = 0f;
        float startWidthShadow = shadowRibbon != null ? shadowRibbon.widthMultiplier : 1f;
        float startWidthGlow   = glowRibbon != null ? glowRibbon.widthMultiplier : 1f;
        float startWidthCore   = coreRibbon != null ? coreRibbon.widthMultiplier : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float w = 1f - (t * t * t); // Cubic rapid fade
            if (shadowRibbon != null) shadowRibbon.widthMultiplier = startWidthShadow * w;
            if (glowRibbon != null) glowRibbon.widthMultiplier = startWidthGlow * w;
            if (coreRibbon != null) coreRibbon.widthMultiplier = startWidthCore * w;
            yield return null;
        }
        ribbonFadeCoroutine = null;
        KillRibbon();
    }

    void KillRibbon()
    {
        if (ribbonFadeCoroutine != null)
        {
            StopCoroutine(ribbonFadeCoroutine);
            ribbonFadeCoroutine = null;
        }
        if (ribbonRoot != null) Destroy(ribbonRoot);
        ribbonRoot = null;
        shadowRibbon = null;
        glowRibbon = null;
        coreRibbon = null;
        rawRibbonPoints.Clear();
        smoothedRibbonPoints.Clear();
        ribbonFading = false;
    }

    // ══════════════════════════════════════════════════════════════
    // SUPPORTING VFX SPAWNERS
    // ══════════════════════════════════════════════════════════════

    void SpawnAnticipationFlash(Vector3 playerPos, int step, float dir)
    {
        var pal = GetPalette(step);
        Vector3 gatherPos = playerPos + new Vector3(dir * 0.45f, 0.12f, 0f);

        // Quick energy-gather wisp
        var go = MakeGO("SlashAnticipation");
        go.transform.position = gatherPos;
        go.transform.localScale = Vector3.one * 0.35f;
        var sr = AddSprite(go, sparkDotHD,
            new Color(pal.bloomGlow.r, pal.bloomGlow.g, pal.bloomGlow.b, 0.65f), glowSortOrder - 1, true);
        StartManaged(AnimateSlashSnap(go, sr, 0.05f, 2.0f, pal.paleEdge));

        // Center pinpoint star gleam
        SpawnStarGleam(gatherPos, pal.starGleam, 0.65f, 0.045f);
    }

    void SpawnStarGleam(Vector3 pos, Color color, float scale, float duration)
    {
        if (starGleamHD == null) return;
        var go = MakeGO("StarGleam");
        go.transform.position = pos + new Vector3(0f, 0f, -0.04f);
        go.transform.localScale = Vector3.one * (scale * 0.4f);
        var sr = AddSprite(go, starGleamHD, color, slashSortOrder + 6, true);
        StartManaged(AnimateStarGleam(go, sr, scale, duration));
    }

    void SpawnImpactCross(Vector3 hitPos, SlashPalette pal, int step)
    {
        float scale = (step == 3) ? 1.6f : 1.15f;

        // Diagonal slash cut 1
        var cut1 = MakeGO("ImpactCut_1");
        cut1.transform.position = hitPos;
        cut1.transform.rotation = Quaternion.Euler(0f, 0f, 42f);
        cut1.transform.localScale = new Vector3(scale, 0.12f, 1f);
        var sr1 = AddSprite(cut1, cuttingStreaksHD, Color.white, slashSortOrder + 5, true);
        StartManaged(AnimateSlashSnap(cut1, sr1, 0.065f, 1.25f, pal.paleEdge));

        // Diagonal slash cut 2 (cross)
        var cut2 = MakeGO("ImpactCut_2");
        cut2.transform.position = hitPos;
        cut2.transform.rotation = Quaternion.Euler(0f, 0f, -48f);
        cut2.transform.localScale = new Vector3(scale * 0.85f, 0.10f, 1f);
        var sr2 = AddSprite(cut2, cuttingStreaksHD, pal.paleEdge, slashSortOrder + 4, true);
        StartManaged(AnimateSlashSnap(cut2, sr2, 0.060f, 1.20f, pal.bloomGlow));
    }

    void SpawnSpeedWindLines(Vector3 origin, float dir, float spearAngle, SlashPalette pal, int step)
    {
        int count = (step == 3) ? 5 : (step == 2 ? 4 : 3);
        float baseTangent = spearAngle + (dir < 0f ? 180f : 0f);

        for (int i = 0; i < count; i++)
        {
            float spread = (i - (count - 1) * 0.5f) * 12f;
            var go = MakeGO("WindCutLine");
            go.transform.position = origin + new Vector3(
                dir * Random.Range(-0.10f, 0.15f),
                Random.Range(-0.08f, 0.08f), 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, baseTangent + spread);
            go.transform.localScale = new Vector3(
                dir * Random.Range(0.85f, 1.55f),
                Random.Range(0.06f, 0.12f), 1f);
            var sr = AddSprite(go, cuttingStreaksHD,
                new Color(pal.paleEdge.r, pal.paleEdge.g, pal.paleEdge.b, Random.Range(0.45f, 0.80f)),
                slashSortOrder - 1, true);
            StartManaged(AnimateWindLine(go, sr, 0.06f + i * 0.012f));
        }
    }

    void SpawnShockRing(Vector3 pos, Color color, float diameter, float duration)
    {
        if (hollowRingHD == null) return;
        var go = MakeGO("ShockRing");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.10f;
        var sr = AddSprite(go, hollowRingHD,
            new Color(color.r, color.g, color.b, 0.65f), glowSortOrder - 1, true);
        StartManaged(AnimateShockRing(go, sr, diameter, duration));
    }

    void SpawnHollowRing(Vector3 pos, Color color, float diameter, float duration)
    {
        if (hollowRingHD == null) return;
        var go = MakeGO("HollowDistortionRing");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.12f;
        var sr = AddSprite(go, hollowRingHD, color, glowSortOrder - 2, true);
        StartManaged(AnimateShockRing(go, sr, diameter, duration));
    }

    void SpawnWedge(Vector3 pos, float dir, Color coreColor, Color flashColor,
                    float scaleX, float scaleY, int sortOrder, float duration, float expandMult, bool isAdditive)
    {
        if (thrustNeedleHD == null) return;
        var go = MakeGO("ThrustWedge");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, dir < 0f ? 180f : 0f);
        go.transform.localScale = new Vector3(dir * scaleX, scaleY, 1f);
        var sr = AddSprite(go, thrustNeedleHD, coreColor, sortOrder, isAdditive);
        StartManaged(AnimateSlashSnap(go, sr, duration, expandMult, flashColor));
    }

    void SpawnSparkBurst(Vector3 pos, float dir, SlashPalette pal, int step, int count, float lifetime)
    {
        var go = MakeGO("SlashSparks");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        LowResBlackOrb.ConfigureParticleRenderer(renderer, slashSortOrder + 4);

        var main = ps.main;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = (step == 3) ? 12f : (step == 2 ? 9f : 7f);
        main.startSize = (step == 3) ? 0.15f : (step == 2 ? 0.12f : 0.10f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.1f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.01f, 0.01f, 0.02f, 1f), new Color(0.06f, 0.06f, 0.09f, 0.9f));

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = (step == 3) ? 16f : 32f;
        shape.radius = 0.04f;
        shape.rotation = new Vector3(0f, dir < 0f ? -90f : 90f, 0f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.04f, 0.04f, 0.06f), 0f), new GradientColorKey(new Color(0.01f, 0.01f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        ps.Play();
        Destroy(go, lifetime + 0.2f);
    }

    void SpawnVoidDebris(Vector3 pos, float dir, SlashPalette pal, int count, float lifetime)
    {
        var go = MakeGO("VoidDebris");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        LowResBlackOrb.ConfigureParticleRenderer(renderer, slashSortOrder + 1);

        var main = ps.main;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.35f; // Float upward (void matter defies gravity)
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        Color darkA = new Color(0.01f, 0.01f, 0.02f, 0.95f);
        Color darkB = new Color(0.04f, 0.04f, 0.07f, 0.85f);
        main.startColor = new ParticleSystem.MinMaxGradient(darkA, darkB);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.28f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(darkA, pal.bloomGlow, 0.25f), 0f), new GradientColorKey(darkA, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        ps.Play();
        Destroy(go, lifetime + 0.2f);
    }

    // ══════════════════════════════════════════════════════════════
    // ANIMATION COROUTINES (Snappy Kinetic Polish)
    // ══════════════════════════════════════════════════════════════

    IEnumerator AnimateSlashSnap(GameObject go, SpriteRenderer sr, float duration, float expandMult, Color edgeFlash)
    {
        float elapsed = 0f;
        Vector3 startScale = go.transform.localScale;
        Vector3 peakScale = startScale * expandMult;
        Color startCol = sr.color;
        Color flashCol = Color.Lerp(startCol, edgeFlash, 0.70f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (go == null) yield break;

            // Snap-in: explosive ease-out-back in first 18% of time, then hold peak
            float scaleT = (t < 0.18f) ? EaseOutBack(t / 0.18f) : 1f;
            go.transform.localScale = Vector3.Lerp(startScale * 0.6f, peakScale, scaleT);

            // Searing flash then rapid cubic alpha decay
            Color c = Color.Lerp(flashCol, startCol, Mathf.Clamp01(t * 2.5f));
            c.a = Mathf.Lerp(startCol.a, 0f, t * t * t);
            sr.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator AnimateStarGleam(GameObject go, SpriteRenderer sr, float targetScale, float duration)
    {
        float elapsed = 0f;
        Color startCol = sr.color;
        Vector3 origScale = go.transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (go == null) yield break;

            // Quick pop out, then fade
            float s = (t < 0.25f) ? Mathf.Lerp(origScale.x, targetScale, EaseOutBack(t / 0.25f))
                                  : Mathf.Lerp(targetScale, 0.05f, EaseInCubic((t - 0.25f) / 0.75f));
            go.transform.localScale = Vector3.one * s;

            Color c = startCol;
            c.a = Mathf.Lerp(startCol.a, 0f, t * t * 1.5f);
            sr.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator AnimateTrailFade(GameObject go, SpriteRenderer sr, float duration)
    {
        float elapsed = 0f;
        Color startCol = sr.color;
        Vector3 startScale = go.transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (go == null) yield break;

            Color c = startCol;
            c.a = Mathf.Lerp(startCol.a, 0f, t * t * 1.6f);
            sr.color = c;
            go.transform.localScale = startScale * (1f + t * 0.15f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator AnimateThrustStreak(GameObject go, SpriteRenderer sr, Vector3 start, Vector3 end, float duration)
    {
        float elapsed = 0f;
        Color startCol = sr.color;
        float length = Vector3.Distance(start, end);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (go == null) yield break;

            go.transform.position = Vector3.Lerp(start, end, EaseOutQuad(t));
            go.transform.localScale = new Vector3(
                go.transform.localScale.x + length * t * 0.50f,
                go.transform.localScale.y * (1f - t * 0.60f),
                1f);

            Color c = startCol;
            c.a = Mathf.Lerp(startCol.a, 0f, t * t);
            sr.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator AnimateWindLine(GameObject go, SpriteRenderer sr, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;
        Vector3 drift = go.transform.right * Random.Range(0.40f, 1.05f) * Mathf.Sign(go.transform.localScale.x);
        Color startCol = sr.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (go == null) yield break;

            go.transform.position = startPos + drift * t;
            Color c = startCol;
            c.a = Mathf.Lerp(startCol.a, 0f, t * t * 2.8f);
            sr.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator AnimateShockRing(GameObject go, SpriteRenderer sr, float targetDiameter, float duration)
    {
        float elapsed = 0f;
        Color startCol = sr.color;
        Vector3 startScale = go.transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (go == null) yield break;

            float scale = Mathf.Lerp(startScale.x, targetDiameter, EaseOutQuad(t));
            go.transform.localScale = new Vector3(scale, scale * 0.60f, 1f);
            Color c = startCol;
            c.a = Mathf.Lerp(startCol.a, 0f, t * t * 1.4f);
            sr.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ══════════════════════════════════════════════════════════════
    // PALETTES & ROTATION HELPERS
    // ══════════════════════════════════════════════════════════════

    public static SlashPalette GetPalette(int step)
    {
        switch (step)
        {
            case 1:
                return new SlashPalette
                {
                    // Step 1: Dark void lavender / Abyssal shadow (Hollow Knight pure nail feel)
                    voidCore     = new Color(0.025f, 0.012f, 0.055f, 0.98f),
                    paleEdge     = new Color(0.85f, 0.78f, 1.0f, 0.95f),
                    bloomGlow    = new Color(0.42f, 0.15f, 0.75f, 0.65f),
                    sparkColor   = new Color(0.65f, 0.45f, 1.0f, 1.0f),
                    sparkTip     = new Color(0.92f, 0.88f, 1.0f, 1.0f),
                    starGleam    = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                    primaryScale = 1.25f,
                    trailScale   = 0.60f
                };
            case 2:
                return new SlashPalette
                {
                    // Step 2: Crimson void / Chaos Rose / Hot Magenta (Aggressive ascending uppercut)
                    voidCore     = new Color(0.065f, 0.008f, 0.025f, 0.98f),
                    paleEdge     = new Color(1.0f, 0.28f, 0.60f, 0.95f),
                    bloomGlow    = new Color(0.75f, 0.08f, 0.38f, 0.65f),
                    sparkColor   = new Color(1.0f, 0.42f, 0.72f, 1.0f),
                    sparkTip     = new Color(1.0f, 0.80f, 0.92f, 1.0f),
                    starGleam    = new Color(1.0f, 0.88f, 0.95f, 1.0f),
                    primaryScale = 1.35f,
                    trailScale   = 0.68f
                };
            default:
                return new SlashPalette
                {
                    // Step 3: Supernova Void Finisher (Abyssal singularity black + Supernova diamond white)
                    voidCore     = new Color(0.01f, 0.01f, 0.02f, 0.99f),
                    paleEdge     = new Color(0.96f, 0.97f, 1.0f, 1.0f),
                    bloomGlow    = new Color(0.24f, 0.06f, 0.42f, 0.75f),
                    sparkColor   = new Color(0.55f, 0.22f, 0.85f, 1.0f),
                    sparkTip     = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                    starGleam    = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                    primaryScale = 1.65f,
                    trailScale   = 0.55f
                };
        }
    }

    static float GetSlashCenterRotation(int step, float dir)
    {
        // Step 1: Downward cleave (+65 to -38) -> center around +13 deg forward
        if (step == 1) return (dir < 0f) ? (180f - 14f) : 14f;
        // Step 2: Upward uppercut (-42 to +60) -> center around +9 deg forward
        if (step == 2) return (dir < 0f) ? (180f + 10f) : -10f;
        // Step 3: Horizontal thrust
        return (dir < 0f) ? 180f : 0f;
    }

    // ══════════════════════════════════════════════════════════════
    // MATH & UTILITY
    // ══════════════════════════════════════════════════════════════

    static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    static float EaseInCubic(float t) => t * t * t;
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    void StartManaged(IEnumerator routine)
    {
        runningCoroutines.Add(StartCoroutine(WrapRoutine(routine)));
    }

    IEnumerator WrapRoutine(IEnumerator routine)
    {
        yield return routine;
        runningCoroutines.RemoveAll(c => c == null);
    }

    void StopAllManaged()
    {
        for (int i = 0; i < runningCoroutines.Count; i++)
        {
            if (runningCoroutines[i] != null) StopCoroutine(runningCoroutines[i]);
        }
        runningCoroutines.Clear();
    }

    static GameObject MakeGO(string name)
    {
        return new GameObject(name);
    }

    static SpriteRenderer AddSprite(GameObject go, Sprite sprite, Color color, int sortOrder, bool isAdditive)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortOrder;
        Material mat = isAdditive ? GetAdditiveMaterial() : GetSpriteMaterial();
        if (mat != null) sr.material = mat;
        return sr;
    }

    public static void TryShake(float intensity, float duration)
    {
        try { CameraShakeManager.Shake(duration, intensity); }
        catch (System.Exception) { /* CameraShakeManager may not exist in scene */ }
    }

    // ══════════════════════════════════════════════════════════════
    // MATERIAL & SHADER ENGINE (Multi-Tier Fallback)
    // ══════════════════════════════════════════════════════════════

    public static Material GetSpriteMaterial()
    {
        if (defaultSpriteMat != null) return defaultSpriteMat;

        string[] shaderNames = {
            "Sprites/Default",
            "Universal Render Pipeline/2D/Sprite-Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "Legacy Shaders/Particles/Alpha Blended",
            "Unlit/Transparent",
            "UI/Default"
        };
        foreach (var sName in shaderNames)
        {
            Shader s = Shader.Find(sName);
            if (s != null)
            {
                defaultSpriteMat = new Material(s) { hideFlags = HideFlags.DontSave };
                return defaultSpriteMat;
            }
        }
        return null;
    }

    public static Material GetAdditiveMaterial()
    {
        if (additiveMat != null) return additiveMat;

        string[] shaderNames = {
            "Mobile/Particles/Additive",
            "Legacy Shaders/Particles/Additive",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };
        foreach (var sName in shaderNames)
        {
            Shader s = Shader.Find(sName);
            if (s != null)
            {
                additiveMat = new Material(s) { hideFlags = HideFlags.DontSave };
                return additiveMat;
            }
        }
        return GetSpriteMaterial();
    }

    // ══════════════════════════════════════════════════════════════
    // PROCEDURAL SPRITE GENERATION — HD 256px Dark-Themed Assets
    // ══════════════════════════════════════════════════════════════

    static void EnsureAssets()
    {
        if (crescentHD != null && crescentHD.texture != null &&
            crescentBloomHD != null && crescentBloomHD.texture != null &&
            crescentVoidCoreHD != null && crescentVoidCoreHD.texture != null &&
            thrustNeedleHD != null && thrustNeedleHD.texture != null &&
            starGleamHD != null && starGleamHD.texture != null &&
            cuttingStreaksHD != null && cuttingStreaksHD.texture != null &&
            hollowRingHD != null && hollowRingHD.texture != null &&
            sparkDotHD != null && sparkDotHD.texture != null)
        {
            return;
        }

        crescentHD = CreateCrescentHD(256, 256);
        crescentBloomHD = CreateCrescentBloomHD(256, 256);
        crescentVoidCoreHD = CreateCrescentVoidCoreHD(256, 256);
        thrustNeedleHD = CreateThrustNeedleHD(256, 96);
        starGleamHD = CreateStarGleamHD(64);
        cuttingStreaksHD = CreateCuttingStreaksHD(128, 20);
        hollowRingHD = CreateHollowRingHD(128);
        sparkDotHD = CreateSparkDotHD(32);
    }

    /// <summary>
    /// 256px razor-thin crescent arc. Features an aggressive cutting beak at the leading edge,
    /// high-contrast razor outer rim, graceful inner boundary taper, and sub-pixel anti-aliasing.
    /// Pivot at (0.12f, 0.5f) — the exact curvature center for seamless arc rotation.
    /// </summary>
    static Sprite CreateCrescentHD(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Vector2 center = new Vector2(w * 0.12f, h * 0.5f);

        float outerR = w * 0.76f;         // Outer cutting rim
        float innerBaseR = w * 0.36f;     // Inner belly boundary
        float arcHalfAngle = 66f;         // Sweeping 132-degree arc

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                if (dist >= innerBaseR - 6f && dist <= outerR + 3f &&
                    angle >= -arcHalfAngle && angle <= arcHalfAngle)
                {
                    // Normalized angle from 0 (tail) to 1 (leading beak)
                    float u = (angle + arcHalfAngle) / (2f * arcHalfAngle);

                    // Dynamic thickness profile: belly at u=0.65, sharp beak at u=1.0, fine needle at u=0
                    float belly = Mathf.Sin(u * Mathf.PI);
                    float beak = Mathf.SmoothStep(0f, 1f, (u - 0.5f) * 2f);
                    float innerR = Mathf.Lerp(outerR - 3f, innerBaseR, Mathf.Pow(belly, 0.45f) * 0.85f + beak * 0.15f);
                    float thickness = Mathf.Max(outerR - innerR, 1f);

                    if (dist >= innerR - 2f && dist <= outerR + 2f)
                    {
                        // Razor-sharp outer cutting rim
                        float edgeDist = Mathf.Abs(dist - outerR);
                        float razorEdge = Mathf.Exp(-edgeDist * edgeDist / 6f);

                        // Body radial gradient: smooth luminous ramp
                        float radT = (dist - innerR) / thickness;
                        float bodyAlpha = Mathf.SmoothStep(0f, 1f, radT * 2f) *
                                          Mathf.SmoothStep(1f, 0f, (radT - 0.88f) * 8f);
                        bodyAlpha = Mathf.Clamp01(bodyAlpha + razorEdge * 0.85f);

                        // Angular taper
                        float angAlpha = Mathf.SmoothStep(0f, 1f, u * 4.5f) *
                                         Mathf.SmoothStep(1f, 0f, (u - 0.94f) * 16f);
                        angAlpha = Mathf.Max(angAlpha, beak * Mathf.Exp(-Mathf.Pow((u - 1f) * 8f, 2f)));

                        float alpha = Mathf.Clamp01(bodyAlpha * angAlpha);
                        float brightness = Mathf.Lerp(0.85f, 1.0f, razorEdge);

                        pixels[y * w + x] = new Color(brightness, brightness, brightness, alpha);
                    }
                    else
                    {
                        pixels[y * w + x] = Color.clear;
                    }
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.12f, 0.5f), 96f);
    }

    /// <summary>
    /// 256px volumetric bloom envelope conforming exactly to CreateCrescentHD's curvature.
    /// Dual-exponential gaussian falloff creates a lush glowing aura when rendered additively.
    /// </summary>
    static Sprite CreateCrescentBloomHD(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Vector2 center = new Vector2(w * 0.12f, h * 0.5f);

        float outerR = w * 0.84f;
        float innerBaseR = w * 0.28f;
        float midR = (outerR + innerBaseR) * 0.5f;
        float halfThickness = (outerR - innerBaseR) * 0.5f;
        float arcHalfAngle = 72f;

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                if (dist >= innerBaseR && dist <= outerR &&
                    angle >= -arcHalfAngle && angle <= arcHalfAngle)
                {
                    float u = (angle + arcHalfAngle) / (2f * arcHalfAngle);
                    float d = Mathf.Abs(dist - midR);
                    float bloom = Mathf.Exp(-d * d / (halfThickness * halfThickness * 0.7f));
                    float wideBloom = Mathf.Exp(-d * d / (halfThickness * halfThickness * 2.2f)) * 0.45f;
                    float angFactor = Mathf.Sin(u * Mathf.PI);

                    float alpha = Mathf.Clamp01((bloom + wideBloom) * angFactor);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.12f, 0.5f), 96f);
    }

    /// <summary>
    /// 256px solid pitch void silhouette body. Sits inside the luminous crescent rim to produce
    /// the iconic Hollow Knight pure-black void blade contrast.
    /// </summary>
    static Sprite CreateCrescentVoidCoreHD(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Vector2 center = new Vector2(w * 0.12f, h * 0.5f);

        float outerR = w * 0.73f;
        float innerBaseR = w * 0.40f;
        float arcHalfAngle = 62f;

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                if (dist >= innerBaseR - 2f && dist <= outerR + 2f &&
                    angle >= -arcHalfAngle && angle <= arcHalfAngle)
                {
                    float u = (angle + arcHalfAngle) / (2f * arcHalfAngle);
                    float belly = Mathf.Sin(u * Mathf.PI);
                    float innerR = Mathf.Lerp(outerR - 2f, innerBaseR, Mathf.Pow(belly, 0.45f));

                    if (dist >= innerR && dist <= outerR)
                    {
                        // Solid core body with 1.5px soft boundary
                        float dIn = dist - innerR;
                        float dOut = outerR - dist;
                        float edgeMin = Mathf.Min(dIn, dOut);
                        float alpha = Mathf.SmoothStep(0f, 1f, edgeMin / 2.0f);
                        pixels[y * w + x] = new Color(1f, 1f, 1f, alpha * 0.98f);
                    }
                    else
                    {
                        pixels[y * w + x] = Color.clear;
                    }
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.12f, 0.5f), 96f);
    }

    /// <summary>
    /// 256×96 supersonic Mach cone wedge with internal shock diamond pulses and tapered needle point.
    /// </summary>
    static Sprite CreateThrustNeedleHD(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Vector2 origin = new Vector2(w * 0.05f, h * 0.5f);

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - origin.x) / (w * 0.92f);
                float dy = Mathf.Abs(y - origin.y) / (h * 0.45f);

                if (dx >= 0f && dx <= 1f)
                {
                    // Conical envelope: widens then maintains slipstream
                    float coneHalfH = Mathf.Pow(dx, 0.65f) * 0.95f;
                    if (dy <= coneHalfH)
                    {
                        // White-hot center line
                        float centerLine = Mathf.Exp(-dy * dy * 14f);

                        // Supersonic shock diamond pulses along X
                        float shockPulse = 0.70f + 0.30f * Mathf.Cos(dx * Mathf.PI * 8f);

                        // Aerodynamic edge falloff
                        float envelope = (1f - (dy / coneHalfH)) * (1f - dx * 0.25f);
                        float alpha = Mathf.Clamp01(envelope * shockPulse + centerLine * 0.75f);
                        float brightness = Mathf.Lerp(0.85f, 1.0f, centerLine);

                        pixels[y * w + x] = new Color(brightness, brightness, brightness, alpha);
                    }
                    else
                    {
                        pixels[y * w + x] = Color.clear;
                    }
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.05f, 0.5f), 64f);
    }

    /// <summary>
    /// 64px 4-point cross star flare with diagonal micro-glints for weapon-tip impact sparks.
    /// </summary>
    static Sprite CreateStarGleamHD(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float half = size * 0.5f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - half);
                float dy = Mathf.Abs(y - half);

                // Central hot core
                float core = Mathf.Exp(-(dx * dx + dy * dy) / 14f);

                // Horizontal & vertical diffraction spikes
                float horiz = Mathf.Exp(-dy * dy / 2.2f) * Mathf.Exp(-dx / 10f);
                float vert  = Mathf.Exp(-dx * dx / 2.2f) * Mathf.Exp(-dy / 10f);

                // 45-degree diagonal micro-glints
                float d1 = Mathf.Abs(dx - dy);
                float d2 = Mathf.Abs(dx + dy);
                float diag = (Mathf.Exp(-d1 * d1 / 3.2f) + Mathf.Exp(-d2 * d2 / 3.2f)) *
                             Mathf.Exp(-(dx + dy) / 16f) * 0.35f;

                float alpha = Mathf.Clamp01(core * 1.3f + horiz * 0.90f + vert * 0.90f + diag);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    /// <summary>
    /// 128×20 triple-line razor speed cuts for high-velocity cutting motion read.
    /// </summary>
    static Sprite CreateCuttingStreaksHD(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[w * h];
        float[] streakY = { h * 0.25f, h * 0.50f, h * 0.75f };

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float tx = x / (float)(w - 1);
                float horizAlpha = Mathf.Sin(tx * Mathf.PI) * Mathf.Pow(tx, 0.45f);

                float streakAlpha = 0f;
                for (int s = 0; s < streakY.Length; s++)
                {
                    float dy = Mathf.Abs(y - streakY[s]);
                    streakAlpha += Mathf.Exp(-dy * dy * 3.5f);
                }

                float alpha = Mathf.Clamp01(streakAlpha * horizAlpha);
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 64f);
    }

    /// <summary>
    /// 128px anti-aliased hollow shockwave ring.
    /// </summary>
    static Sprite CreateHollowRingHD(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float half = size * 0.5f;
        float radius = size * 0.40f;
        float thickness = size * 0.05f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                float ringDist = Mathf.Abs(dist - radius);
                float alpha = Mathf.Exp(-ringDist * ringDist / (thickness * thickness * 0.5f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    /// <summary>
    /// 32px point light spark with soft glow aura.
    /// </summary>
    static Sprite CreateSparkDotHD(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float half = size * 0.5f;
        float radius = size * 0.48f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / radius;
                float alpha = dist <= 1f ? (Mathf.Exp(-dist * dist * 4f) * 0.7f + Mathf.Pow(1f - dist, 1.8f) * 0.3f) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }
}
