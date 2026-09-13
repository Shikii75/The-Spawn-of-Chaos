using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// CherryBlossomLorePrologue - Atmospheric opening lore sequence for Chapter 1: The Cherry Blossom Forest (SampleScene).
/// Features:
/// - Living cosmic void & sakura petal ambiance with gentle procedural floating pink bokeh orbs & nebula glow.
/// - Prominent, regal Ancient Title typography with sakura-rose glow.
/// - Smooth cinematic slide Fade-In and Fade-Out.
/// - Floating magical text aura with breathing shimmer.
/// - Interactive advance ([Space], [Enter], [E], Click) and skip ([Esc]).
/// - Live font style cycling ([F1]: Gothic / Papyrus / Serif).
/// - Unveils the mystery of Nyxaris's slaughtered followers and the looming shadow of Chaos.
/// </summary>
public class CherryBlossomLorePrologue : MonoBehaviour
{
    public static CherryBlossomLorePrologue Instance { get; private set; }

    public enum AncientFontStyle
    {
        AncientGothic,   // Old English Text MT / Dark fantasy medieval tome
        AncientPapyrus,  // Papyrus / Weathered mythic scroll
        AncientSerif     // Georgia / Classical mythic inscriptional serif
    }

    [Header("Ancient Font Settings")]
    public AncientFontStyle fontStyle = AncientFontStyle.AncientGothic;
    public Font customFont;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip prologueMusicTrack;
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    [Header("Cinematic Timing Settings")]
    public float slideFadeInDuration = 0.8f;
    public float slideFadeOutDuration = 0.45f;
    public float sceneAwakenDuration = 1.4f;

    // Narrative Slides (Cherry Blossom Forest / Chapter 1 Arrival)
    private readonly string[] loreSlides = new string[]
    {
        "Beyond the shattered seal, the dimensional rift hurled you across the fabric of spacetime.\n\nThe celestial currents of the void subsided, depositing you on the outskirts of a forgotten realm: The Cherry Blossom Forest.",
        "Here, petals drift endlessly upon ancient stone—a fragile veil of mortal serenity.\n\nYet beneath the crimson blooms, an icy stillness lingers in the air.",
        "You were not placed in this timeline by chance.\n\nAcross this realm, the devotees of Nyxaris—keepers of ancient multi-versal wisdom—have been systematically eradicated in cold blood.",
        "No witness remains. No sanctuary was spared.\n\nOnly whispers echoing in the void, pointing toward a power far greater and more terrifying than any mortal hand...",
        "Nyxaris's own father... Chaos, the Primordial Void.\n\nCould the cosmic sovereign himself have orchestrated the massacre to purge his daughter's legacy from existence?",
        "With Nyxaris now free and at your side, your true mission begins.\n\nInvestigate the slaughter, uncover what happened to her followers, and confront whatever darkness lies waiting beneath the blossoms.",
        "<size=52><color=#FFAAE0><b>CHAPTER I: THE CHERRY BLOSSOM FOREST</b></color></size>\n\n<size=28><color=#FF88B8><i>✦  Echoes of the Slaughtered  ✦</i></color></size>"
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

    // Floating Pink Orbs System
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

    void Awake()
    {
        // Safety: Never run over the Main Menu / Title Screen
        if (!MainMenuUIToolkitController.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        if (PlayerPrefs.GetInt("CherryBlossom_Prologue_Played", 0) == 1)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Immediately lock player movement during lore sequence
        move.ExternalMovementLock = true;

        softOrbSprite = CreateSoftRadialGlowSprite();
        BuildPrologueUI();
    }

    void Start()
    {
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

        // Populate texts
        loreTextComp.text = loreSlides[index];
        bool isTitleCard = (index == loreSlides.Length - 1);
        if (isTitleCard)
        {
            headerTextComp.text = "✦   The Mortal Realm Awaits   ✦";
            slideProgressTextComp.text = "✦   CHAPTER TITLE   ✦";
        }
        else
        {
            headerTextComp.text = "✦   The Cherry Blossom Mystery   ✦";
            slideProgressTextComp.text = $"✦  Part {ToRomanNumeral(index + 1)}  ✦";
        }

        // Fade In smoothly from alpha 0 to 1 with gentle upward floating rise
        float elapsed = 0f;
        Vector2 startPos = new Vector2(0f, -25f);
        Vector2 endPos = Vector2.zero;

        slideCanvasGroup.alpha = 0f;
        slideContainerRT.anchoredPosition = startPos;

        while (elapsed < slideFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
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

        // Fade Out current slide smoothly
        float elapsed = 0f;
        Vector2 startPos = slideContainerRT.anchoredPosition;
        Vector2 endPos = new Vector2(0f, 15f);

        while (elapsed < slideFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / slideFadeOutDuration;
            slideCanvasGroup.alpha = 1f - t;
            slideContainerRT.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        slideCanvasGroup.alpha = 0f;
        yield return new WaitForSeconds(0.08f);

        // Fade In next slide
        yield return StartCoroutine(ShowSlideRoutine(targetIndex));
    }

    private void UpdatePromptState(bool show)
    {
        if (promptTextComp != null)
        {
            promptTextComp.enabled = show;
        }
    }

    private void SkipPrologue()
    {
        if (hasFinished) return;
        Debug.Log("<color=#FF88B8>[CherryBlossomLorePrologue] Prologue skipped by player.</color>");
        CompletePrologue();
    }

    private void CompletePrologue()
    {
        if (hasFinished) return;
        hasFinished = true;
        PlayerPrefs.SetInt("CherryBlossom_Prologue_Played", 1);
        PlayerPrefs.Save();

        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
        }

        StartCoroutine(AwakenSceneRoutine());
    }

    private IEnumerator AwakenSceneRoutine()
    {
        Debug.Log("<color=#55FF88>[CherryBlossomLorePrologue] Opening lore concluded. Awakening Chapter 1: The Cherry Blossom Forest...</color>");

        float elapsed = 0f;
        float startMusicVol = audioSource != null ? audioSource.volume : 0f;

        while (elapsed < sceneAwakenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / sceneAwakenDuration;
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;
            if (audioSource != null) audioSource.volume = Mathf.Lerp(startMusicVol, 0f, t);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (audioSource != null) audioSource.Stop();

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
        Debug.Log($"<color=#FF88B8>[CherryBlossomLorePrologue] Switched font style to: {fontStyle} (Loaded: {f?.name})</color>");
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

        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f;
    }

    private void BuildPrologueUI()
    {
        // 1. Root Canvas
        GameObject canvasGO = new GameObject("CherryBlossom_PrologueCanvas");
        canvasGO.transform.SetParent(transform, false);
        prologueCanvas = canvasGO.AddComponent<Canvas>();
        prologueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        prologueCanvas.sortingOrder = 950;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        // 2. Fullscreen Deep Cosmic Void Backdrop
        GameObject bgGO = new GameObject("VoidBackdrop");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.025f, 0.008f, 0.045f, 1f); // Deep cherry obsidian void
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // 3. Ambient Sakura Nebula Glow (Centerpiece soft radial bloom)
        GameObject glowGO = new GameObject("SakuraNebulaGlow");
        glowGO.transform.SetParent(canvasGO.transform, false);
        nebulaGlowImg = glowGO.AddComponent<Image>();
        nebulaGlowImg.sprite = softOrbSprite;
        nebulaGlowImg.color = new Color(0.95f, 0.40f, 0.70f, 0.12f);
        RectTransform glowRT = glowGO.GetComponent<RectTransform>();
        glowRT.anchorMin = new Vector2(0.5f, 0.5f);
        glowRT.anchorMax = new Vector2(0.5f, 0.5f);
        glowRT.sizeDelta = new Vector2(1400f, 850f);

        // 4. Floating Pink Bokeh Orbs Container
        GameObject orbsContainer = new GameObject("PinkOrbsContainer");
        orbsContainer.transform.SetParent(canvasGO.transform, false);
        RectTransform orbsRT = orbsContainer.AddComponent<RectTransform>();
        orbsRT.anchorMin = Vector2.zero;
        orbsRT.anchorMax = Vector2.one;
        orbsRT.sizeDelta = Vector2.zero;
        SpawnFloatingPinkOrbs(orbsContainer.transform);

        // 5. Slide Container
        GameObject slideContainerGO = new GameObject("SlideContainer");
        slideContainerGO.transform.SetParent(canvasGO.transform, false);
        slideContainerRT = slideContainerGO.AddComponent<RectTransform>();
        slideContainerRT.anchorMin = new Vector2(0.5f, 0.5f);
        slideContainerRT.anchorMax = new Vector2(0.5f, 0.5f);
        slideContainerRT.sizeDelta = new Vector2(1300f, 650f);
        slideContainerRT.anchoredPosition = Vector2.zero;
        slideCanvasGroup = slideContainerGO.AddComponent<CanvasGroup>();

        Font activeFont = GetActiveFont();

        // 6. Header
        GameObject headerGO = new GameObject("HeaderText");
        headerGO.transform.SetParent(slideContainerGO.transform, false);
        headerTextComp = headerGO.AddComponent<Text>();
        headerTextComp.text = "✦   The Cherry Blossom Mystery   ✦";
        headerTextComp.font = activeFont;
        headerTextComp.fontSize = 24;
        headerTextComp.alignment = TextAnchor.MiddleCenter;
        headerTextComp.color = new Color(1.0f, 0.65f, 0.85f, 0.85f);
        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0.5f, 0.88f);
        headerRT.anchorMax = new Vector2(0.5f, 0.88f);
        headerRT.sizeDelta = new Vector2(1000f, 40f);

        // 7. Slide Progress / Chapter Part Indicator
        GameObject partGO = new GameObject("PartIndicator");
        partGO.transform.SetParent(slideContainerGO.transform, false);
        slideProgressTextComp = partGO.AddComponent<Text>();
        slideProgressTextComp.text = "✦  Part I  ✦";
        slideProgressTextComp.font = activeFont;
        slideProgressTextComp.fontSize = 17;
        slideProgressTextComp.alignment = TextAnchor.MiddleCenter;
        slideProgressTextComp.color = new Color(0.95f, 0.50f, 0.75f, 0.65f);
        RectTransform partRT = partGO.GetComponent<RectTransform>();
        partRT.anchorMin = new Vector2(0.5f, 0.81f);
        partRT.anchorMax = new Vector2(0.5f, 0.81f);
        partRT.sizeDelta = new Vector2(600f, 30f);

        // 8. Main Lore Body Text
        GameObject loreGO = new GameObject("LoreText");
        loreGO.transform.SetParent(slideContainerGO.transform, false);
        loreTextComp = loreGO.AddComponent<Text>();
        loreTextComp.text = "";
        loreTextComp.font = activeFont;
        loreTextComp.fontSize = 32;
        loreTextComp.lineSpacing = 1.35f;
        loreTextComp.alignment = TextAnchor.MiddleCenter;
        loreTextComp.color = new Color(0.98f, 0.94f, 0.98f, 0.96f);

        loreOutline = loreGO.AddComponent<Outline>();
        loreOutline.effectColor = new Color(0.92f, 0.25f, 0.65f, 0.45f);
        loreOutline.effectDistance = new Vector2(1.5f, -1.5f);

        loreShadow = loreGO.AddComponent<Shadow>();
        loreShadow.effectColor = new Color(0.12f, 0.02f, 0.18f, 0.85f);
        loreShadow.effectDistance = new Vector2(2f, -2f);

        RectTransform loreRT = loreGO.GetComponent<RectTransform>();
        loreRT.anchorMin = new Vector2(0.5f, 0.45f);
        loreRT.anchorMax = new Vector2(0.5f, 0.45f);
        loreRT.sizeDelta = new Vector2(1150f, 320f);
        loreRT.anchoredPosition = new Vector2(0f, baseLoreY);

        // 9. Interactive Continue Prompt (Bottom Center)
        GameObject promptGO = new GameObject("ContinuePrompt");
        promptGO.transform.SetParent(canvasGO.transform, false);
        promptTextComp = promptGO.AddComponent<Text>();
        promptTextComp.text = "✦  Press [Space] or Click to Continue  ✦";
        promptTextComp.font = activeFont;
        promptTextComp.fontSize = 18;
        promptTextComp.alignment = TextAnchor.MiddleCenter;
        promptTextComp.color = new Color(1.0f, 0.65f, 0.85f, 0.70f);
        RectTransform promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.5f, 0.08f);
        promptRT.anchorMax = new Vector2(0.5f, 0.08f);
        promptRT.sizeDelta = new Vector2(600f, 35f);

        // 10. Skip Hint (Bottom Right)
        GameObject skipGO = new GameObject("SkipHint");
        skipGO.transform.SetParent(canvasGO.transform, false);
        skipTextComp = skipGO.AddComponent<Text>();
        skipTextComp.text = "[Esc] Skip Prologue";
        skipTextComp.font = activeFont;
        skipTextComp.fontSize = 14;
        skipTextComp.alignment = TextAnchor.MiddleRight;
        skipTextComp.color = new Color(0.75f, 0.60f, 0.78f, 0.45f);
        RectTransform skipRT = skipGO.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(0.96f, 0.06f);
        skipRT.anchorMax = new Vector2(0.96f, 0.06f);
        skipRT.pivot = new Vector2(1f, 0.5f);
        skipRT.sizeDelta = new Vector2(200f, 30f);

        // 11. Font Hint (Bottom Left)
        GameObject fontHintGO = new GameObject("FontHint");
        fontHintGO.transform.SetParent(canvasGO.transform, false);
        fontHintTextComp = fontHintGO.AddComponent<Text>();
        fontHintTextComp.text = $"[F1] Font: {fontStyle}";
        fontHintTextComp.font = activeFont;
        fontHintTextComp.fontSize = 14;
        fontHintTextComp.alignment = TextAnchor.MiddleLeft;
        fontHintTextComp.color = new Color(0.75f, 0.60f, 0.78f, 0.45f);
        RectTransform fontHintRT = fontHintGO.GetComponent<RectTransform>();
        fontHintRT.anchorMin = new Vector2(0.04f, 0.06f);
        fontHintRT.anchorMax = new Vector2(0.04f, 0.06f);
        fontHintRT.pivot = new Vector2(0f, 0.5f);
        fontHintRT.sizeDelta = new Vector2(260f, 30f);
    }

    private void SpawnFloatingPinkOrbs(Transform parent)
    {
        orbs.Clear();
        int orbCount = 28;

        // Sakura & Rose Pink Color Palette
        Color[] pinkPalette = new Color[]
        {
            new Color(1.00f, 0.45f, 0.75f), // Radiant cherry blossom pink
            new Color(1.00f, 0.62f, 0.85f), // Soft sakura pink
            new Color(0.95f, 0.35f, 0.65f), // Vivid rose magenta
            new Color(1.00f, 0.74f, 0.88f), // Pale blush petal
            new Color(0.92f, 0.50f, 0.80f)  // Arcane orchid rose
        };

        for (int i = 0; i < orbCount; i++)
        {
            GameObject orbGO = new GameObject($"PinkOrb_{i}");
            orbGO.transform.SetParent(parent, false);

            Image img = orbGO.AddComponent<Image>();
            img.sprite = softOrbSprite;
            img.raycastTarget = false;

            RectTransform rt = orbGO.GetComponent<RectTransform>();

            float size = Random.Range(35f, 160f);
            rt.sizeDelta = new Vector2(size, size);

            float x = Random.Range(-950f, 950f);
            float y = Random.Range(-550f, 550f);
            rt.anchoredPosition = new Vector2(x, y);

            Color col = pinkPalette[Random.Range(0, pinkPalette.Length)];
            float alpha = Random.Range(0.12f, 0.38f);
            img.color = new Color(col.r, col.g, col.b, alpha);

            orbs.Add(new FloatingOrb
            {
                rt = rt,
                img = img,
                speed = Random.Range(18f, 52f),
                xCenter = x,
                swayFreq = Random.Range(0.5f, 1.6f),
                swayAmp = Random.Range(14f, 40f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = alpha,
                baseColor = col
            });
        }
    }

    private void UpdateFloatingOrbs()
    {
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        for (int i = 0; i < orbs.Count; i++)
        {
            FloatingOrb orb = orbs[i];
            Vector2 pos = orb.rt.anchoredPosition;

            pos.y += orb.speed * dt;
            pos.x = orb.xCenter + Mathf.Sin(time * orb.swayFreq + orb.phase) * orb.swayAmp;

            if (pos.y > 600f)
            {
                pos.y = -600f;
                orb.xCenter = Random.Range(-950f, 950f);
                pos.x = orb.xCenter;
            }

            orb.rt.anchoredPosition = pos;

            float pulse = 0.80f + 0.20f * Mathf.Sin(time * 2.2f + orb.phase);
            orb.img.color = new Color(orb.baseColor.r, orb.baseColor.g, orb.baseColor.b, orb.baseAlpha * pulse);
        }
    }

    private void UpdateMagicalTextAtmosphere()
    {
        float t = Time.unscaledTime;

        // Gentle text float bobbing
        if (loreTextComp != null)
        {
            float bob = Mathf.Sin(t * 1.5f) * 4f;
            loreTextComp.rectTransform.anchoredPosition = new Vector2(0f, baseLoreY + bob);
        }

        // Shimmering sakura outline
        if (loreOutline != null)
        {
            float shimmer = 0.30f + 0.22f * Mathf.Sin(t * 2.8f);
            loreOutline.effectColor = new Color(0.95f, 0.40f, 0.70f, shimmer);
        }

        // Nebula glow breathing
        if (nebulaGlowImg != null)
        {
            float glow = 0.10f + 0.04f * Mathf.Sin(t * 1.2f);
            nebulaGlowImg.color = new Color(0.95f, 0.40f, 0.70f, glow);
        }

        // Prompt pulse
        if (promptTextComp != null && promptTextComp.enabled)
        {
            float pAlpha = 0.45f + 0.35f * Mathf.Sin(t * 3.2f);
            promptTextComp.color = new Color(1.0f, 0.65f, 0.85f, pAlpha);
        }
    }

    private Sprite CreateSoftRadialGlowSprite()
    {
        int size = 128;
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
                }
                else
                {
                    float falloff = Mathf.Cos(dist * Mathf.PI * 0.5f);
                    falloff = Mathf.Pow(falloff, 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, falloff));
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private string ToRomanNumeral(int number)
    {
        switch (number)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            default: return number.ToString();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitInCherryBlossomScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase))
        {
            // CRITICAL: Never auto-init during Main Menu / Title Screen!
            // Doing so overlays the menu at sortingOrder 950 and freezes while timeScale is 0.
            if (!MainMenuUIToolkitController.isPlaying)
            {
                return;
            }

            if (PlayerPrefs.GetInt("CherryBlossom_Prologue_Played", 0) == 1)
            {
                return;
            }

            if (FindFirstObjectByType<CherryBlossomLorePrologue>() == null)
            {
                GameObject go = new GameObject("[CherryBlossomLorePrologue]");
                go.AddComponent<CherryBlossomLorePrologue>();
                Debug.Log("<color=#FF88B8>[CherryBlossomLorePrologue] Initialized Chapter 1 opening lore prologue for SampleScene.</color>");
            }
        }
    }
}
