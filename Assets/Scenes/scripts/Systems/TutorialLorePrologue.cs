using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// TutorialLorePrologue - Atmospheric opening lore sequence for the Tutorial level.
/// Features:
/// - Living cosmic void backdrop with gentle procedural floating purple orbs & nebula glow.
/// - Prominent, regal Ancient Title typography with amethyst glow.
/// - Smooth cinematic slide Fade-In and Fade-Out (replaces typewriter effect).
/// - Floating magical text aura with breathing shimmer.
/// - Interactive advance ([Space], [Enter], Click) and skip ([Esc]).
/// - Live font style cycling ([F1]: Gothic / Papyrus / Serif).
/// - Asynchronous companion frame pre-warming in background.
/// </summary>
public class TutorialLorePrologue : MonoBehaviour
{
    public static TutorialLorePrologue Instance { get; private set; }
    public static bool IsPrologueActive { get; private set; }

    public enum AncientFontStyle
    {
        AncientGothic,   // Old English Text MT / Dark fantasy medieval tome
        AncientPapyrus,  // Papyrus / Weathered mythic scroll
        AncientSerif     // Georgia / Classical mythic inscriptional serif
    }

    [Header("Ancient Font Settings")]
    public AncientFontStyle fontStyle = AncientFontStyle.AncientGothic;
    public Font customFont;

    [Header("Audio (Assign custom music track when ready)")]
    public AudioSource audioSource;
    public AudioClip prologueMusicTrack;
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    [Header("Voiceover Narration")]
    public AudioSource narrationSource;
    public AudioClip[] narrationClips = new AudioClip[7];
    [Range(0f, 1f)] public float narrationVolume = 1f;
    [Range(0f, 3f)] public float firstNarrationDelay = 0.7f;

    [Header("Cinematic Timing Settings")]
    public float slideFadeInDuration = 0.8f;
    public float slideFadeOutDuration = 0.45f;
    public float sceneAwakenDuration = 1.4f;

    // Narrative Slides (Atmospheric & Captivating)
    private readonly string[] loreSlides = new string[]
    {
        "Before time had a heartbeat, there was only the Void.\n\nAn infinite, formless stillness... commonly known as Chaos.",
        "For uncounted eons, darkness reigned alone.\n\nUntil a spark of radiant defiance ignited within the silence: Aether, the First Light.",
        "They were absolute opposites, yet in their collision, balance was born.\n\nChaos gave substance to the dark; Aether gave warmth and form to reality.",
        "From their union sprang the multiverse, and the children destined to rule it.\n\nForemost among them was Nyxaris, Goddess of Darkness, Heir to Chaos.",
        "For an age, existence held steady between shadow and dawn.\n\nUntil the day the light fractured.",
        "Without warning or whisper... Aether disappeared.\n\nWith her absence, the cosmic equilibrium unraveled.",
        "Bereft of the light that bound them, realm after realm began to fall into the abyss.\n\nNow, from the remnants of a collapsing multiverse, the Void stirs once more...",
        "<size=56><color=#F2B8FF><b>THE SPAWN OF CHAOS</b></color></size>\n\n<size=30><color=#D47BFF><i>✦  Heir to the Void  ✦</i></color></size>"
    };

    // UI References
    private Canvas prologueCanvas;
    private CanvasGroup canvasGroup;
    private CanvasGroup slideCanvasGroup;
    private RectTransform slideContainerRT;
    private Text headerTextComp;
    private Text loreTextComp;
    private Text slideProgressTextComp;
    private Text promptTextComp;
    private Text skipTextComp;
    private Text fontHintTextComp;
    private Shadow loreShadow;
    private Outline loreOutline;
    private Image nebulaGlowImg;

    // Floating Purple Orbs System
    private class FloatingOrb
    {
        public RectTransform rt;
        public Image img;
        public float speed;
        public float xCenter;
        public float swayFreq;
        public float swayAmp;
        public float phase;
        public float baseAlpha;
        public Color baseColor;
    }
    private List<FloatingOrb> orbs = new List<FloatingOrb>();
    private Sprite softOrbSprite;

    // State
    private int currentSlideIndex = 0;
    private bool isTransitioning = false;
    private bool hasFinished = false;
    private Coroutine currentTransitionCoroutine;
    private float baseLoreY = -15f;
    private List<AudioSource> mutedSceneAudioSources = new List<AudioSource>();
    private List<float> mutedSceneAudioVolumes = new List<float>();

    void Awake()
    {
        Instance = this;
        IsPrologueActive = true;

        // Ensure Lumi (the light orb) NEVER exists in TutorialScene
        PurgeLumiInstances();

        // Immediately lock player movement and hide gameplay
        move.ExternalMovementLock = true;

        softOrbSprite = CreateSoftRadialGlowSprite();
        BuildPrologueUI();
    }

    private void PurgeLumiInstances()
    {
        if (LightOrbCompanion.Instance != null)
        {
            Destroy(LightOrbCompanion.Instance.gameObject);
        }
        var allLumi = Object.FindObjectsByType<LightOrbCompanion>(FindObjectsSortMode.None);
        foreach (var l in allLumi)
        {
            if (l != null) Destroy(l.gameObject);
        }
        var allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var rootGO in allRoots)
        {
            if (rootGO != null && (rootGO.name.Contains("Lumi") || rootGO.name.Contains("LightOrbCompanion")))
            {
                Destroy(rootGO);
            }
        }
    }

    void Start()
    {
        PurgeLumiInstances();

        // Pre-warm FoxNyxaris frames asynchronously in background during lore display
        StartCoroutine(FoxNyxarisController.PreloadFramesRoutine());

        // Ensure Nyxaris Seal Breakdown sequence is active in TutorialScene
        NyxarisSealSequence.EnsureInstanceInScene();

        EnsureNarrationSource();
        EnsureNarrationClipsAssigned();
        SilenceSceneAudioDuringLore();

        // Play optional music track if provided
        if (prologueMusicTrack != null)
        {
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = prologueMusicTrack;
            audioSource.volume = musicVolume;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Begin with first slide fading in
        currentTransitionCoroutine = StartCoroutine(ShowSlideRoutine(0));
    }

    private void EnsureNarrationSource()
    {
        if (narrationSource == null)
        {
            narrationSource = GetComponent<AudioSource>();
            if (narrationSource == null)
            {
                narrationSource = gameObject.AddComponent<AudioSource>();
            }
        }

        narrationSource.loop = false;
        narrationSource.playOnAwake = false;
        narrationSource.volume = narrationVolume;

        ApplyNarrationReverb();
    }

    private void ApplyNarrationReverb()
    {
        if (narrationSource == null)
        {
            return;
        }

        try
        {
            var filterType = System.Type.GetType("UnityEngine.AudioReverbFilter, UnityEngine");
            if (filterType == null)
            {
                narrationSource.reverbZoneMix = 1.2f;
                return;
            }

            var filter = narrationSource.GetComponent(filterType);
            if (filter == null)
            {
                filter = narrationSource.gameObject.AddComponent(filterType);
            }

            var presetType = System.Type.GetType("UnityEngine.AudioReverbPreset, UnityEngine");
            if (presetType != null)
            {
                var preset = System.Enum.Parse(presetType, "Cathedral");
                var presetProp = filterType.GetProperty("reverbPreset");
                if (presetProp != null && presetProp.PropertyType == presetType)
                {
                    presetProp.SetValue(filter, preset);
                }
            }

            SetFloatProperty(filter, "dryLevel", 0f);
            SetFloatProperty(filter, "room", -1000f);
            SetFloatProperty(filter, "roomHF", -1000f);
            SetFloatProperty(filter, "decayTime", 4.8f);
            SetFloatProperty(filter, "decayHFRatio", 1.3f);
            SetFloatProperty(filter, "reflections", 80f);
            SetFloatProperty(filter, "reflectionsDelay", 0.03f);
            SetFloatProperty(filter, "reverb", 2000f);
            SetFloatProperty(filter, "reverbDelay", 0.06f);
            SetFloatProperty(filter, "hfReference", 5000f);
            SetFloatProperty(filter, "lfReference", 250f);
            SetFloatProperty(filter, "diffusion", 100f);
            SetFloatProperty(filter, "density", 100f);

            var enabledProp = filterType.GetProperty("enabled");
            if (enabledProp != null && enabledProp.PropertyType == typeof(bool))
            {
                enabledProp.SetValue(filter, true);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TutorialLorePrologue] Reverb setup unavailable in this Unity build: " + ex.Message);
            narrationSource.reverbZoneMix = 1.2f;
        }
    }

    private static void SetFloatProperty(Component target, string propertyName, float value)
    {
        if (target == null)
        {
            return;
        }

        var prop = target.GetType().GetProperty(propertyName);
        if (prop != null && prop.PropertyType == typeof(float))
        {
            prop.SetValue(target, value);
        }
    }

    private void EnsureNarrationClipsAssigned()
    {
        bool hasAssignedClip = narrationClips != null && narrationClips.Length >= 7 && narrationClips[0] != null;
        if (hasAssignedClip)
        {
            return;
        }

        narrationClips = new AudioClip[7];

#if UNITY_EDITOR
        string[] assetPaths = new[]
        {
            "Assets/Audio/Voice/openingsequence/prologue_slide_01.wav",
            "Assets/Audio/Voice/openingsequence/prologue_slide_02.wav",
            "Assets/Audio/Voice/openingsequence/prologue_slide_03.wav",
            "Assets/Audio/Voice/openingsequence/prologue_slide_04.wav",
            "Assets/Audio/Voice/openingsequence/prologue_slide_05.wav",
            "Assets/Audio/Voice/openingsequence/prologue_slide_06.wav",
            "Assets/Audio/Voice/openingsequence/prologue_slide_07.wav"
        };

        for (int i = 0; i < assetPaths.Length; i++)
        {
            narrationClips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPaths[i]);
            if (narrationClips[i] == null)
            {
                Debug.LogWarning($"[TutorialLorePrologue] Missing narration clip at path: {assetPaths[i]}");
            }
        }
#endif

        if (narrationClips[0] == null && narrationClips[1] == null && narrationClips[2] == null && narrationClips[3] == null && narrationClips[4] == null && narrationClips[5] == null && narrationClips[6] == null)
        {
            Debug.LogWarning("[TutorialLorePrologue] No prologue narration clips were assigned. Ensure the seven openingsequence wav files exist under Assets/Audio/Voice/openingsequence.");
        }
    }

    private void SilenceSceneAudioDuringLore()
    {
        if (mutedSceneAudioSources.Count > 0)
        {
            return;
        }

        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < allSources.Length; i++)
        {
            var source = allSources[i];
            if (source == null || source == audioSource || source == narrationSource)
            {
                continue;
            }

            mutedSceneAudioSources.Add(source);
            mutedSceneAudioVolumes.Add(source.volume);
            source.mute = true;
            source.Stop();
        }
    }

    private void RestoreSceneAudioAfterLore()
    {
        for (int i = 0; i < mutedSceneAudioSources.Count; i++)
        {
            var source = mutedSceneAudioSources[i];
            if (source != null)
            {
                source.mute = false;
                source.volume = mutedSceneAudioVolumes[i];
            }
        }

        mutedSceneAudioSources.Clear();
        mutedSceneAudioVolumes.Clear();
    }

    private void PlayNarrationForSlide(int slideIndex)
    {
        if (narrationSource == null)
        {
            EnsureNarrationSource();
        }

        if (slideIndex < 0 || slideIndex >= 7 || narrationClips == null || slideIndex >= narrationClips.Length || narrationClips[slideIndex] == null)
        {
            if (narrationSource != null)
            {
                narrationSource.Stop();
            }
            Debug.LogWarning($"[TutorialLorePrologue] No narration clip available for slide {slideIndex}.");
            return;
        }

        if (narrationSource.clip == narrationClips[slideIndex] && narrationSource.isPlaying)
        {
            return;
        }

        narrationSource.Stop();
        narrationSource.clip = narrationClips[slideIndex];
        narrationSource.volume = narrationVolume;
        narrationSource.Play();
    }

    private IEnumerator DelayNarrationForFirstSlide()
    {
        if (firstNarrationDelay > 0f)
        {
            yield return new WaitForSeconds(firstNarrationDelay);
        }

        PlayNarrationForSlide(0);
    }

    void Update()
    {
        if (hasFinished) return;

        UpdateFloatingOrbs();
        UpdateMagicalTextAtmosphere();

        // Press [F1] to cycle font styles in real-time
        if (Input.GetKeyDown(KeyCode.F1))
        {
            CycleFontStyle();
        }

        // Skip entire prologue with [Escape]
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SkipPrologue();
            return;
        }

        // Advance slide on [Space], [Enter], [E], or Left Click
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
        {
            OnPlayerAdvanceInput();
        }
    }

    private void OnPlayerAdvanceInput()
    {
        if (isTransitioning)
        {
            // Fast-forward transition to fully visible
            if (slideCanvasGroup != null && slideCanvasGroup.alpha < 0.95f)
            {
                if (currentTransitionCoroutine != null) StopCoroutine(currentTransitionCoroutine);
                slideCanvasGroup.alpha = 1f;
                if (slideContainerRT != null) slideContainerRT.anchoredPosition = new Vector2(0f, 0f);
                UpdatePromptState(true);
                isTransitioning = false;
            }
            return;
        }

        currentSlideIndex++;
        if (currentSlideIndex < loreSlides.Length)
        {
            currentTransitionCoroutine = StartCoroutine(TransitionToNextSlideRoutine(currentSlideIndex));
        }
        else
        {
            CompletePrologue();
        }
    }

    private IEnumerator ShowSlideRoutine(int index)
    {
        isTransitioning = true;
        UpdatePromptState(false);

        bool isTitleCard = (index == loreSlides.Length - 1);

        // Populate texts
        if (isTitleCard)
        {
            loreTextComp.text = "<size=62><color=#F2B8FF><b>THE SPAWN OF CHAOS</b></color></size>\n\n<size=34><color=#D47BFF><i>✦  Heir to the Void  ✦</i></color></size>";
            loreTextComp.fontSize = 58;
            headerTextComp.text = "✦   The Multiverse Fractures   ✦";
            slideProgressTextComp.text = "✦   TITLE CARD   ✦";
        }
        else
        {
            loreTextComp.text = loreSlides[index];
            loreTextComp.fontSize = 42;
            headerTextComp.text = "✦   Chronicles of the Void   ✦";
            slideProgressTextComp.text = $"✦  Part {ToRomanNumeral(index + 1)}  ✦";
        }

        if (index == 0)
        {
            if (narrationSource != null)
            {
                narrationSource.Stop();
                narrationSource.clip = null;
            }
            StartCoroutine(DelayNarrationForFirstSlide());
        }
        else if (index < 7)
        {
            PlayNarrationForSlide(index);
        }
        else if (narrationSource != null)
        {
            narrationSource.Stop();
        }

        // Fade In smoothly from alpha 0 to 1 with gentle upward floating rise
        float elapsed = 0f;
        Vector2 startPos = new Vector2(0f, -25f);
        Vector2 endPos = Vector2.zero;

        slideCanvasGroup.alpha = 0f;
        slideContainerRT.anchoredPosition = startPos;

        while (elapsed < slideFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideFadeInDuration);
            slideCanvasGroup.alpha = t;
            slideContainerRT.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        slideCanvasGroup.alpha = 1f;
        slideContainerRT.anchoredPosition = endPos;
        isTransitioning = false;

        // Softly reveal continue prompt
        UpdatePromptState(true);
    }

    private IEnumerator TransitionToNextSlideRoutine(int targetIndex)
    {
        isTransitioning = true;
        UpdatePromptState(false);

        // Fade Out current slide
        float elapsed = 0f;
        float startAlpha = slideCanvasGroup.alpha;
        Vector2 currentPos = slideContainerRT.anchoredPosition;
        Vector2 fadeOutTargetPos = new Vector2(0f, 15f);

        while (elapsed < slideFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideFadeOutDuration;
            slideCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            slideContainerRT.anchoredPosition = Vector2.Lerp(currentPos, fadeOutTargetPos, t);
            yield return null;
        }

        slideCanvasGroup.alpha = 0f;

        // Fade In new slide
        yield return StartCoroutine(ShowSlideRoutine(targetIndex));
    }

    private void UpdateMagicalTextAtmosphere()
    {
        // Gentle undulating float on the lore text
        if (loreTextComp != null)
        {
            RectTransform rt = loreTextComp.GetComponent<RectTransform>();
            if (rt != null)
            {
                float floatOffset = Mathf.Sin(Time.time * 1.6f) * 4.5f;
                rt.anchoredPosition = new Vector2(0f, baseLoreY + floatOffset);
            }
        }

        // Shimmering magical glow aura on outline & shadow
        float pulse = (Mathf.Sin(Time.time * 2.2f) + 1f) * 0.5f;
        if (loreShadow != null)
        {
            loreShadow.effectColor = Color.Lerp(new Color(0.35f, 0.08f, 0.60f, 0.70f), new Color(0.70f, 0.25f, 1.0f, 0.95f), pulse);
        }
        if (loreOutline != null)
        {
            loreOutline.effectColor = Color.Lerp(new Color(0.18f, 0.04f, 0.32f, 0.85f), new Color(0.45f, 0.15f, 0.70f, 0.95f), pulse);
        }

        // Soft breathing on central nebula glow
        if (nebulaGlowImg != null)
        {
            float nebulaPulse = (Mathf.Sin(Time.time * 0.8f) + 1f) * 0.5f;
            nebulaGlowImg.color = new Color(0.55f, 0.15f, 0.85f, Mathf.Lerp(0.12f, 0.24f, nebulaPulse));
        }
    }

    private void UpdateFloatingOrbs()
    {
        float dt = Time.deltaTime;
        float time = Time.time;

        for (int i = 0; i < orbs.Count; i++)
        {
            FloatingOrb orb = orbs[i];
            Vector2 pos = orb.rt.anchoredPosition;

            // Move gently upward
            pos.y += orb.speed * dt;

            // Sinusoidal horizontal sway
            pos.x = orb.xCenter + Mathf.Sin(time * orb.swayFreq + orb.phase) * orb.swayAmp;

            // Wrap around when reaching top of screen
            if (pos.y > 620f)
            {
                pos.y = -620f;
                orb.xCenter = Random.Range(-950f, 950f);
                pos.x = orb.xCenter;
            }

            orb.rt.anchoredPosition = pos;

            // Gentle alpha breathing
            float pulse = 0.7f + 0.3f * Mathf.Sin(time * 1.8f + orb.phase);
            orb.img.color = new Color(orb.baseColor.r, orb.baseColor.g, orb.baseColor.b, orb.baseAlpha * pulse);
        }
    }

    private void UpdatePromptState(bool readyToAdvance)
    {
        if (promptTextComp != null)
        {
            bool isTitleCard = (currentSlideIndex == loreSlides.Length - 1);
            if (readyToAdvance)
            {
                promptTextComp.text = isTitleCard
                    ? "<color=#F0A0FF>► Press [Space] or Click to Enter the Realm</color>"
                    : "<color=#D47BFF>► Press [Space] or Click to Continue</color>";
            }
            else
            {
                promptTextComp.text = "";
            }
        }
    }

    private void SkipPrologue()
    {
        if (hasFinished) return;
        Debug.Log("<color=#D47BFF>[TutorialLorePrologue] Prologue skipped by player.</color>");
        CompletePrologue();
    }

    private void CompletePrologue()
    {
        if (hasFinished) return;
        hasFinished = true;
        IsPrologueActive = false;

        if (currentTransitionCoroutine != null) StopCoroutine(currentTransitionCoroutine);
        StartCoroutine(FadeOutAndAwakenWorld());
    }

    private IEnumerator FadeOutAndAwakenWorld()
    {
        Debug.Log("<color=#55FF88>[TutorialLorePrologue] Opening lore concluded. Transitioning to Tutorial Scene...</color>");

        float elapsed = 0f;
        float startMusicVol = audioSource != null ? audioSource.volume : 0f;

        while (elapsed < sceneAwakenDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sceneAwakenDuration;
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;
            if (audioSource != null) audioSource.volume = Mathf.Lerp(startMusicVol, 0f, t);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (audioSource != null) audioSource.Stop();
        if (narrationSource != null)
        {
            narrationSource.Stop();
            narrationSource.clip = null;
        }
        RestoreSceneAudioAfterLore();
        IsPrologueActive = false;

        // Release player movement lock
        move.ExternalMovementLock = false;

        // Cleanup prologue canvas
        if (prologueCanvas != null)
        {
            Destroy(prologueCanvas.gameObject);
        }

        Destroy(gameObject);
    }

    public void CycleFontStyle()
    {
        int next = ((int)fontStyle + 1) % 3;
        fontStyle = (AncientFontStyle)next;
        Font f = GetActiveFont();
        ApplyFontToAllComponents(f);
        if (fontHintTextComp != null) fontHintTextComp.text = $"[F1] Font: {fontStyle}";
        Debug.Log($"<color=#D47BFF>[TutorialLorePrologue] Switched font style to: {fontStyle} (Loaded: {f?.name})</color>");
    }

    private void ApplyFontToAllComponents(Font f)
    {
        if (f == null) return;
        if (headerTextComp != null) headerTextComp.font = f;
        if (slideProgressTextComp != null) slideProgressTextComp.font = f;
        if (loreTextComp != null) loreTextComp.font = f;
        if (promptTextComp != null) promptTextComp.font = f;
        if (skipTextComp != null) skipTextComp.font = f;
        if (fontHintTextComp != null) fontHintTextComp.font = f;
    }

    public Font GetActiveFont()
    {
        if (customFont != null) return customFont;

        Font f = null;
        switch (fontStyle)
        {
            case AncientFontStyle.AncientGothic:
                f = Resources.Load<Font>("Fonts/AncientGothic");
                if (f == null) f = Font.CreateDynamicFontFromOSFont("Old English Text MT", 42);
                break;
            case AncientFontStyle.AncientPapyrus:
                f = Resources.Load<Font>("Fonts/AncientPapyrus");
                if (f == null) f = Font.CreateDynamicFontFromOSFont("Papyrus", 42);
                break;
            case AncientFontStyle.AncientSerif:
                f = Resources.Load<Font>("Fonts/AncientSerif");
                if (f == null) f = Font.CreateDynamicFontFromOSFont("Georgia", 42);
                break;
        }

        if (f == null) f = Resources.Load<Font>("Fonts/AncientGothic");
        if (f == null) f = Resources.Load<Font>("Fonts/AncientPapyrus");
        if (f == null) f = Resources.Load<Font>("Fonts/AncientSerif");
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return f;
    }

    private string ToRomanNumeral(int number)
    {
        string[] roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        if (number >= 1 && number <= roman.Length) return roman[number - 1];
        return number.ToString();
    }

    #region Procedural UI Canvas Builder & Floating Orbs

    private Sprite CreateSoftRadialGlowSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxRadius = size * 0.49f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                if (dist >= 1.0f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                // Pure out-of-focus optical bokeh Gaussian blur with smooth zero-falloff edge
                float edgeFactor = Mathf.Clamp01(1.0f - dist);
                float smoothEdge = edgeFactor * edgeFactor * (3.0f - 2.0f * edgeFactor);
                // Ultra-diffuse wide Gaussian curve - soft, misty, dreamy ethereal glow
                float alpha = Mathf.Exp(-1.9f * dist * dist) * smoothEdge;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void BuildPrologueUI()
    {
        GameObject canvasGO = new GameObject("[Tutorial_LorePrologueCanvas]");
        prologueCanvas = canvasGO.AddComponent<Canvas>();
        prologueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        prologueCanvas.sortingOrder = 9999;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1.0f;

        // Solid Pitch Black Backdrop
        GameObject bgGO = new GameObject("PitchBlackBackdrop");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = Color.black;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // Central Cosmic Nebula Glow
        GameObject nebulaGO = new GameObject("CosmicNebulaGlow");
        nebulaGO.transform.SetParent(bgGO.transform, false);
        nebulaGlowImg = nebulaGO.AddComponent<Image>();
        nebulaGlowImg.sprite = softOrbSprite;
        nebulaGlowImg.color = new Color(0.55f, 0.15f, 0.85f, 0.18f);
        RectTransform nebulaRT = nebulaGO.GetComponent<RectTransform>();
        nebulaRT.anchorMin = new Vector2(0.5f, 0.5f);
        nebulaRT.anchorMax = new Vector2(0.5f, 0.5f);
        nebulaRT.sizeDelta = new Vector2(1450f, 850f);

        // Floating Purple Orbs Container (Placed behind text)
        GameObject orbsContainer = new GameObject("FloatingOrbsContainer");
        orbsContainer.transform.SetParent(bgGO.transform, false);
        RectTransform orbsContainerRT = orbsContainer.AddComponent<RectTransform>();
        orbsContainerRT.anchorMin = Vector2.zero;
        orbsContainerRT.anchorMax = Vector2.one;
        orbsContainerRT.sizeDelta = Vector2.zero;

        SpawnFloatingOrbs(orbsContainer.transform);

        // Resolve Active Ancient Font
        Font activeFont = GetActiveFont();

        // Main Content Container
        GameObject containerGO = new GameObject("PrologueContainer");
        containerGO.transform.SetParent(bgGO.transform, false);
        RectTransform containerRT = containerGO.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(1350f, 700f);

        // Header Title (Large, Regal, Highly Visible with Amethyst Glow)
        GameObject headerGO = new GameObject("HeaderTitle");
        headerGO.transform.SetParent(containerGO.transform, false);
        headerTextComp = headerGO.AddComponent<Text>();
        headerTextComp.font = activeFont;
        // In gothic scripts, Title Case is significantly more readable and stately than cramped all-caps
        headerTextComp.text = "✦   Chronicles of the Void   ✦";
        headerTextComp.fontSize = 46; // Significantly larger
        headerTextComp.fontStyle = FontStyle.Bold;
        headerTextComp.color = new Color(0.88f, 0.58f, 1f, 0.98f);
        headerTextComp.alignment = TextAnchor.MiddleCenter;
        
        var headerOutline = headerGO.AddComponent<Outline>();
        headerOutline.effectColor = new Color(0.25f, 0.05f, 0.42f, 0.95f);
        headerOutline.effectDistance = new Vector2(2f, -2f);
        
        var headerShadow = headerGO.AddComponent<Shadow>();
        headerShadow.effectColor = new Color(0.65f, 0.18f, 0.95f, 0.75f);
        headerShadow.effectDistance = new Vector2(3.5f, -3.5f);

        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchoredPosition = new Vector2(0f, 210f);
        headerRT.sizeDelta = new Vector2(1250f, 75f);

        // Slide Content Container (Controlled by slideCanvasGroup for smooth fade-in/out)
        GameObject slideContainerGO = new GameObject("SlideContentContainer");
        slideContainerGO.transform.SetParent(containerGO.transform, false);
        slideContainerRT = slideContainerGO.AddComponent<RectTransform>();
        slideContainerRT.anchorMin = new Vector2(0.5f, 0.5f);
        slideContainerRT.anchorMax = new Vector2(0.5f, 0.5f);
        slideContainerRT.sizeDelta = new Vector2(1250f, 400f);
        slideContainerRT.anchoredPosition = Vector2.zero;

        slideCanvasGroup = slideContainerGO.AddComponent<CanvasGroup>();
        slideCanvasGroup.alpha = 0f;

        // Slide Progress Indicator
        GameObject progressGO = new GameObject("SlideProgress");
        progressGO.transform.SetParent(slideContainerGO.transform, false);
        slideProgressTextComp = progressGO.AddComponent<Text>();
        slideProgressTextComp.font = activeFont;
        slideProgressTextComp.text = "✦  Part I  ✦";
        slideProgressTextComp.fontSize = 28;
        slideProgressTextComp.fontStyle = FontStyle.Italic;
        slideProgressTextComp.color = new Color(0.78f, 0.68f, 0.92f, 0.85f);
        slideProgressTextComp.alignment = TextAnchor.MiddleCenter;
        RectTransform progressRT = progressGO.GetComponent<RectTransform>();
        progressRT.anchoredPosition = new Vector2(0f, 135f);
        progressRT.sizeDelta = new Vector2(500f, 45f);

        // Main Lore Text (Large, Ethereal Ivory, Magical Pulsing Outline & Shadow)
        GameObject loreGO = new GameObject("LoreText");
        loreGO.transform.SetParent(slideContainerGO.transform, false);
        loreTextComp = loreGO.AddComponent<Text>();
        loreTextComp.font = activeFont;
        loreTextComp.fontSize = 42;
        loreTextComp.lineSpacing = 1.42f;
        loreTextComp.color = new Color(0.97f, 0.95f, 1f, 1f); // Radiant ivory parchment
        loreTextComp.alignment = TextAnchor.MiddleCenter;
        loreTextComp.horizontalOverflow = HorizontalWrapMode.Wrap;
        loreTextComp.verticalOverflow = VerticalWrapMode.Overflow;

        loreOutline = loreGO.AddComponent<Outline>();
        loreOutline.effectColor = new Color(0.20f, 0.05f, 0.35f, 0.90f);
        loreOutline.effectDistance = new Vector2(2f, -2f);

        loreShadow = loreGO.AddComponent<Shadow>();
        loreShadow.effectColor = new Color(0.55f, 0.15f, 0.85f, 0.80f);
        loreShadow.effectDistance = new Vector2(3f, -3f);

        RectTransform loreRT = loreGO.GetComponent<RectTransform>();
        loreRT.anchoredPosition = new Vector2(0f, baseLoreY);
        loreRT.sizeDelta = new Vector2(1200f, 320f);

        // Bottom Continue Prompt
        GameObject promptGO = new GameObject("PromptText");
        promptGO.transform.SetParent(containerGO.transform, false);
        promptTextComp = promptGO.AddComponent<Text>();
        promptTextComp.font = activeFont;
        promptTextComp.fontSize = 24;
        promptTextComp.fontStyle = FontStyle.Bold;
        promptTextComp.color = new Color(0.90f, 0.52f, 1f, 0.95f);
        promptTextComp.alignment = TextAnchor.MiddleCenter;
        promptTextComp.supportRichText = true;

        var promptShadow = promptGO.AddComponent<Shadow>();
        promptShadow.effectColor = new Color(0.4f, 0.05f, 0.6f, 0.7f);
        promptShadow.effectDistance = new Vector2(2f, -2f);

        RectTransform promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchoredPosition = new Vector2(0f, -230f);
        promptRT.sizeDelta = new Vector2(900f, 50f);

        // Bottom-Right Corner Skip Indicator
        GameObject skipGO = new GameObject("SkipText");
        skipGO.transform.SetParent(bgGO.transform, false);
        skipTextComp = skipGO.AddComponent<Text>();
        skipTextComp.font = activeFont;
        skipTextComp.text = "[Esc] Skip";
        skipTextComp.fontSize = 18;
        skipTextComp.color = new Color(0.55f, 0.55f, 0.65f, 0.65f);
        skipTextComp.alignment = TextAnchor.LowerRight;
        RectTransform skipRT = skipGO.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(1f, 0f);
        skipRT.anchorMax = new Vector2(1f, 0f);
        skipRT.pivot = new Vector2(1f, 0f);
        skipRT.anchoredPosition = new Vector2(-40f, 35f);
        skipRT.sizeDelta = new Vector2(250f, 40f);

        // Bottom-Left Realtime Font Cycling Hint
        GameObject fontHintGO = new GameObject("FontHintText");
        fontHintGO.transform.SetParent(bgGO.transform, false);
        fontHintTextComp = fontHintGO.AddComponent<Text>();
        fontHintTextComp.font = activeFont;
        fontHintTextComp.text = $"[F1] Font: {fontStyle}";
        fontHintTextComp.fontSize = 16;
        fontHintTextComp.color = new Color(0.50f, 0.45f, 0.62f, 0.65f);
        fontHintTextComp.alignment = TextAnchor.LowerLeft;
        RectTransform hintRT = fontHintGO.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(0f, 0f);
        hintRT.pivot = new Vector2(0f, 0f);
        hintRT.anchoredPosition = new Vector2(40f, 35f);
        hintRT.sizeDelta = new Vector2(350f, 40f);
    }

    private void SpawnFloatingOrbs(Transform parent)
    {
        orbs.Clear();
        int orbCount = 38;

        Color[] palette = new Color[]
        {
            new Color(0.65f, 0.20f, 0.95f), // Radiant violet
            new Color(0.85f, 0.35f, 1.00f), // Neon purple
            new Color(0.92f, 0.50f, 0.98f), // Ethereal magenta
            new Color(0.48f, 0.15f, 0.85f), // Deep amethyst
            new Color(0.78f, 0.45f, 0.90f)  // Soft lilac
        };

        for (int i = 0; i < orbCount; i++)
        {
            GameObject orbGO = new GameObject($"PurpleOrb_{i}");
            orbGO.transform.SetParent(parent, false);

            Image img = orbGO.AddComponent<Image>();
            img.sprite = softOrbSprite;

            RectTransform rt = orbGO.GetComponent<RectTransform>();

            // Layered out-of-focus optical bokeh sizing and depth
            float layerRoll = Random.value;
            float size;
            float alpha;
            float speed;
            float swayAmp;

            if (layerRoll < 0.35f)
            {
                // Deep background blur
                size = Random.Range(45f, 80f);
                alpha = Random.Range(0.18f, 0.36f);
                speed = Random.Range(22f, 42f);
                swayAmp = Random.Range(12f, 25f);
            }
            else if (layerRoll < 0.75f)
            {
                // Midground dreamy bokeh
                size = Random.Range(85f, 140f);
                alpha = Random.Range(0.24f, 0.44f);
                speed = Random.Range(35f, 58f);
                swayAmp = Random.Range(20f, 38f);
            }
            else
            {
                // Large foreground out-of-focus bokeh blooms
                size = Random.Range(145f, 225f);
                alpha = Random.Range(0.28f, 0.50f);
                speed = Random.Range(48f, 75f);
                swayAmp = Random.Range(28f, 50f);
            }

            rt.sizeDelta = new Vector2(size, size);

            float initialX = Random.Range(-950f, 950f);
            float initialY = Random.Range(-620f, 620f);
            rt.anchoredPosition = new Vector2(initialX, initialY);

            Color chosenColor = palette[Random.Range(0, palette.Length)];
            img.color = new Color(chosenColor.r, chosenColor.g, chosenColor.b, alpha);

            FloatingOrb orb = new FloatingOrb
            {
                rt = rt,
                img = img,
                speed = speed,
                xCenter = initialX,
                swayFreq = Random.Range(0.5f, 1.2f),
                swayAmp = swayAmp,
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = alpha,
                baseColor = chosenColor
            };

            orbs.Add(orb);
        }
    }

    #endregion

    #region Auto-Initialization Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckAndInstantiateForScene(SceneManager.GetActiveScene().name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndInstantiateForScene(scene.name);
    }

    private static void CheckAndInstantiateForScene(string sceneName)
    {
        if (sceneName == "TutorialScene")
        {
            if (FindFirstObjectByType<TutorialLorePrologue>() == null)
            {
                GameObject go = new GameObject("[TutorialLorePrologue]");
                go.AddComponent<TutorialLorePrologue>();
                Debug.Log("<color=#D47BFF>[TutorialLorePrologue] Initialized opening lore prologue for TutorialScene.</color>");
            }
        }
    }

    #endregion
}
