using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// NyxarisSealSequence - Orchestrates the tutorial finale:
/// 1. Tracks all covering WEBs around the seal (checks IsBroken or destruction).
/// 2. Fox Nyxaris stops and turns into the celestial orb right where she stands (no jumping or dragging).
/// 3. The orb glides gracefully into the center of the seal with an ethereal star trail.
/// 4. The seal violently overloads with energy sparks, high-frequency pulsing plasma, and screen rumble.
/// 5. Fullscreen Whiteout Screen Flash.
/// 6. Intact seal visuals swapped with the Broken Seal remnant in-place,
///    properly scaled with respect to the seal sprite size (representing the broken 35% remainder),
///    and Chibi Nyxaris emerges.
/// 7. Transitions to the Story Dialogue with animated Nyxaris portraits.
/// 8. Unlocks Tutorial_ExitPortal to journey into the Cherry Blossom Forest.
/// </summary>
public class NyxarisSealSequence : MonoBehaviour
{
    public static NyxarisSealSequence Instance { get; private set; }

    [Header("Seal GameObjects")]
    public GameObject intactSealGO;
    public GameObject brokenSealGO;

    [Header("Web Coverage")]
    public List<BreakableObject> coveringWebs = new List<BreakableObject>();

    [Header("Chibi Emergence")]
    public Vector3 chibiSpawnOffset = new Vector3(1.2f, -0.2f, 0f);

    [Header("Exit Portal")]
    public GameObject exitPortalGO;

    [Header("FX Settings")]
    public float overloadDuration = 2.4f;
    public float maxCameraShake = 0.42f;
    public Color flashColor = Color.white;

    private bool sequenceTriggered = false;
    private bool isMonitoringWebs = true;
    private bool hasRegisteredInitialWebs = false;
    private int initialWebCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterGlobalSceneHook()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name.ToLower().Contains("tutorial"))
            {
                EnsureInstanceInScene();
            }
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitInTutorialScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName.ToLower().Contains("tutorial"))
        {
            EnsureInstanceInScene();
        }
    }

    public static NyxarisSealSequence EnsureInstanceInScene()
    {
        if (Instance != null) return Instance;

        GameObject seqHost = GameObject.Find("Nyxaris_SealSequenceManager");
        if (seqHost == null)
        {
            seqHost = new GameObject("Nyxaris_SealSequenceManager");
        }

        NyxarisSealSequence managerSeq = seqHost.GetComponent<NyxarisSealSequence>();
        if (managerSeq == null) managerSeq = seqHost.AddComponent<NyxarisSealSequence>();
        Instance = managerSeq;

        // Find seal reference to assign
        GameObject sealObj = GameObject.Find("seal") ?? GameObject.Find("Seal") ??
                             GameObject.Find("NyxarisShrineCage") ?? GameObject.Find("ShrineCage") ??
                             GameObject.Find("shrine") ?? GameObject.Find("Shrine");
        if (sealObj != null)
        {
            managerSeq.intactSealGO = sealObj;
            // Clean up any duplicate component mistakenly attached directly to seal to prevent accidental self-deactivation
            NyxarisSealSequence duplicateOnSeal = sealObj.GetComponent<NyxarisSealSequence>();
            if (duplicateOnSeal != null && duplicateOnSeal != managerSeq)
            {
                Destroy(duplicateOnSeal);
            }
        }

        Debug.Log("<color=#D47BFF>[NyxarisSealSequence] Dedicated host 'Nyxaris_SealSequenceManager' initialized in TutorialScene.</color>");
        return managerSeq;
    }

    void Awake()
    {
        Instance = this;
        move.ExternalMovementLock = false;

        LocateSceneReferences();
        if (brokenSealGO != null && intactSealGO != null)
        {
            ApplyProperBrokenSealSizingAndAlignment();
            brokenSealGO.SetActive(false);
        }
    }

    void OnEnable()
    {
        BreakableObject.OnAnyObjectBroken += OnWebObjectBroken;
    }

    void OnDisable()
    {
        BreakableObject.OnAnyObjectBroken -= OnWebObjectBroken;
        move.ExternalMovementLock = false;
    }

    void OnDestroy()
    {
        BreakableObject.OnAnyObjectBroken -= OnWebObjectBroken;
        move.ExternalMovementLock = false;
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        LocateSceneReferences();

        // Ensure Broken seal is sized proportionally and starts hidden
        if (brokenSealGO != null && intactSealGO != null)
        {
            ApplyProperBrokenSealSizingAndAlignment();
            brokenSealGO.SetActive(false);
        }

        // Lock exit portal until seal breakdown sequence concludes
        if (exitPortalGO != null)
        {
            Collider2D portalCol = exitPortalGO.GetComponent<Collider2D>();
            if (portalCol != null) portalCol.enabled = false;
        }

        AutoDetectCoveringWebs();
        NyxarisManager.EnsureInstanceInScene();
    }

    private void LocateSceneReferences()
    {
        if (intactSealGO == null)
        {
            intactSealGO = GameObject.Find("seal") ?? GameObject.Find("Seal") ??
                           GameObject.Find("NyxarisShrineCage") ?? GameObject.Find("ShrineCage") ??
                           GameObject.Find("shrine") ?? GameObject.Find("Shrine");
        }

        if (brokenSealGO == null)
        {
            GameObject[] allGOs = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                string n = go.name.ToLower();
                if (n.Contains("broken") && (n.Contains("seal") || n.Contains("shrine") || n.Contains("cage")))
                {
                    brokenSealGO = go;
                    break;
                }
            }
        }

        if (exitPortalGO == null)
        {
            exitPortalGO = GameObject.Find("Tutorial_ExitPortal") ?? GameObject.FindGameObjectWithTag("Finish");
        }
    }

    /// <summary>
    /// Scales and positions the Broken Seal remnant with respect to the intact seal sprite dimensions.
    /// The broken seal sprite is the bottom broken fragment representing ~35% of the total crystal spire.
    /// Sizing with respect to the seal sprite ensures pixel-perfect width matching and natural 35% height proportion.
    /// </summary>
    public void ApplyProperBrokenSealSizingAndAlignment()
    {
        if (brokenSealGO == null || intactSealGO == null) return;

        SpriteRenderer intactSr = intactSealGO.GetComponentInChildren<SpriteRenderer>();
        SpriteRenderer brokenSr = brokenSealGO.GetComponentInChildren<SpriteRenderer>();

        if (intactSr != null && intactSr.sprite != null && brokenSr != null && brokenSr.sprite != null)
        {
            // Sizing with respect to the seal sprite:
            // Width of intact sprite in pixels vs broken sprite in pixels
            float intactPxWidth = intactSr.sprite.rect.width;
            float brokenPxWidth = brokenSr.sprite.rect.width;
            float ppuRatio = brokenSr.sprite.pixelsPerUnit / intactSr.sprite.pixelsPerUnit;
            float relativeScale = (intactPxWidth / brokenPxWidth) * ppuRatio;

            Vector3 baseScale = intactSealGO.transform.localScale;
            brokenSealGO.transform.localScale = new Vector3(
                baseScale.x * relativeScale,
                baseScale.y * relativeScale,
                baseScale.z
            );
            brokenSealGO.transform.rotation = intactSealGO.transform.rotation;

            // Ground the broken piece so its base bounds align with intact seal bottom baseline
            float intactBottomY = intactSr.bounds.min.y;
            float intactCenterX = intactSr.bounds.center.x;

            brokenSealGO.transform.position = intactSealGO.transform.position;
            float brokenBottomY = brokenSr.bounds.min.y;
            float brokenCenterX = brokenSr.bounds.center.x;

            brokenSealGO.transform.position += new Vector3(intactCenterX - brokenCenterX, intactBottomY - brokenBottomY, 0f);
        }
        else
        {
            brokenSealGO.transform.position = intactSealGO.transform.position;
            brokenSealGO.transform.rotation = intactSealGO.transform.rotation;
            // 0.493f relative scale accounts for 505px vs 1024px sprite difference
            brokenSealGO.transform.localScale = intactSealGO.transform.localScale * 0.493f;
        }
    }

    private void AutoDetectCoveringWebs()
    {
        if (coveringWebs == null) coveringWebs = new List<BreakableObject>();
        coveringWebs.Clear();

        Vector3 sealPos = intactSealGO != null ? intactSealGO.transform.position : transform.position;

        var allBreakables = FindObjectsByType<BreakableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var b in allBreakables)
        {
            if (b == null) continue;
            string n = b.gameObject.name.ToUpper();
            float dist = Vector2.Distance(sealPos, b.transform.position);

            if (n.Contains("WEB") || dist < 22.0f)
            {
                if (!coveringWebs.Contains(b))
                {
                    coveringWebs.Add(b);
                }
            }
        }

        if (coveringWebs.Count > 0)
        {
            hasRegisteredInitialWebs = true;
            initialWebCount = coveringWebs.Count;
        }

        Debug.Log($"<color=#D47BFF>[NyxarisSealSequence] Registered {coveringWebs.Count} covering webs near seal.</color>");
    }

    private void OnWebObjectBroken(BreakableObject broken)
    {
        if (isMonitoringWebs && !sequenceTriggered)
        {
            CheckWebStatus();
        }
    }

    void Update()
    {
        if (sequenceTriggered || !isMonitoringWebs) return;

        CheckWebStatus();
        CheckProximityFallback();
    }

    private void CheckWebStatus()
    {
        if (!hasRegisteredInitialWebs || coveringWebs.Count == 0)
        {
            AutoDetectCoveringWebs();
            return;
        }

        // Remove broken, disabled, or destroyed webs
        coveringWebs.RemoveAll(w => w == null || w.IsBroken || !w.gameObject.activeInHierarchy);

        if (coveringWebs.Count == 0 && !sequenceTriggered)
        {
            TriggerSequence();
        }
    }

    private void CheckProximityFallback()
    {
        if (sequenceTriggered || intactSealGO == null) return;

        // Proximity check: If player is at the seal and all webs around the seal are gone
        GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player") ?? GameObject.Find("BasePlayer");
        if (player != null)
        {
            float distToSeal = Vector2.Distance(player.transform.position, intactSealGO.transform.position);
            if (distToSeal < 8.0f)
            {
                // Count any remaining active webs near the seal
                int activeNearWebs = 0;
                var allBreakables = FindObjectsByType<BreakableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var b in allBreakables)
                {
                    if (b != null && !b.IsBroken && b.gameObject.activeInHierarchy)
                    {
                        if (Vector2.Distance(intactSealGO.transform.position, b.transform.position) < 22.0f)
                        {
                            activeNearWebs++;
                        }
                    }
                }

                if (activeNearWebs == 0)
                {
                    TriggerSequence();
                }
            }
        }
    }

    private void TriggerSequence()
    {
        if (sequenceTriggered) return;
        sequenceTriggered = true;
        isMonitoringWebs = false;
        StartCoroutine(SealOverloadAndBreakRoutine());
    }

    private IEnumerator SealOverloadAndBreakRoutine()
    {
        Debug.Log("<color=#D47BFF>[NyxarisSealSequence] All webs cleared! Initiating Fox Nyxaris morph -> Orb -> Seal Breakdown sequence...</color>");

        move.ExternalMovementLock = true;
        Vector3 sealCenter = intactSealGO != null ? intactSealGO.transform.position : transform.position;

        // 1. Fox turns into the orb, then the orb enters the seal
        if (FoxNyxarisController.Instance != null && FoxNyxarisController.Instance.gameObject.activeInHierarchy)
        {
            yield return StartCoroutine(FoxTransformAndEnterSealRoutine(sealCenter));
        }
        else if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.ActivateAirborneOrb(sealCenter + Vector3.up * 2.5f);
            yield return StartCoroutine(GlideOrbIntoSeal(sealCenter));
            NyxarisOrbGuide.Instance.DeactivateAirborneOrb();
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        // 2. Violent Seal Overload: Pulsing Glow + Screen Shake
        // The seal violently reacts to absorbing the goddess's core essence
        Camera mainCam = Camera.main;
        Vector3 origCamPos = mainCam != null ? mainCam.transform.position : Vector3.zero;

        SpriteRenderer sealSr = intactSealGO != null ? intactSealGO.GetComponentInChildren<SpriteRenderer>() : null;
        Color baseSealColor = sealSr != null ? sealSr.color : Color.white;

        float elapsed = 0f;
        while (elapsed < overloadDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / overloadDuration;

            // Violent plasma glow: frequency escalates from 10Hz to 36Hz
            if (sealSr != null)
            {
                float pulseFreq = Mathf.Lerp(10f, 36f, progress);
                float ping = Mathf.PingPong(elapsed * pulseFreq, 1f);
                Color violentGlow = Color.Lerp(new Color(0.92f, 0.15f, 1f, 1f), new Color(2.4f, 1.8f, 2.5f, 1f), ping);
                sealSr.color = violentGlow;
            }

            // Violent camera rumble shake
            if (mainCam != null)
            {
                float shakeMag = Mathf.Lerp(0.05f, maxCameraShake, progress);
                Vector2 randOffset = Random.insideUnitCircle * shakeMag;
                mainCam.transform.position = new Vector3(origCamPos.x + randOffset.x, origCamPos.y + randOffset.y, origCamPos.z);
            }

            // Periodic star spark bursts from seal core as energy overloads
            if (NyxarisOrbGuide.Instance != null && Random.value < 0.18f)
            {
                NyxarisOrbGuide.Instance.BurstSpawnStarRing(sealCenter + (Vector3)Random.insideUnitCircle * 0.8f, 6);
            }

            yield return null;
        }

        if (mainCam != null) mainCam.transform.position = origCamPos;
        if (sealSr != null) sealSr.color = baseSealColor;

        // 3. Fullscreen Whiteout Screen Flash & Seal Shatter
        yield return StartCoroutine(WhiteoutFlashSequence(sealCenter));

        // 4. Trigger Cinematic Chatbot Dialogue
        yield return new WaitForSeconds(0.3f);
        TriggerChatbotStoryDialogue();
    }

    /// <summary>
    /// Reworked finale sequence:
    /// Fox Nyxaris does NOT jump or get dragged into the seal.
    /// Instead, she stops, delivers her announcement, morphs into the celestial orb right where she stands,
    /// and then the orb glides smoothly into the center of the seal with an ethereal star trail.
    /// </summary>
    private IEnumerator FoxTransformAndEnterSealRoutine(Vector3 sealCenter)
    {
        FoxNyxarisController fox = FoxNyxarisController.Instance;
        if (fox == null || !fox.gameObject.activeInHierarchy) yield break;

        // 1. Shouts dialogue announcement
        fox.ShowDialogue("The webs are broken! Stand back, mortal... I will return to my true essence and shatter the seal!", "");
        yield return new WaitForSeconds(1.8f);
        fox.HideSpeechBubble();

        // 2. Freeze fox completely in place (no movement, no jumping, no dragging)
        fox.enabled = false;
        Rigidbody2D foxRb = fox.GetComponent<Rigidbody2D>();
        if (foxRb != null)
        {
            foxRb.linearVelocity = Vector2.zero;
            foxRb.simulated = false;
        }

        Transform foxT = fox.transform;
        SpriteRenderer foxSr = fox.GetComponent<SpriteRenderer>();
        Vector3 foxPos = foxT.position + new Vector3(0f, 0.6f, 0f);

        // 3. Fox turns into the celestial orb right where she is!
        // Swell of violet/white light, graceful shrink into the celestial orb core
        float morphDuration = 0.75f;
        float elapsed = 0f;
        Vector3 origScale = foxT.localScale;
        Color origColor = foxSr != null ? foxSr.color : Color.white;

        while (elapsed < morphDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / morphDuration);

            if (foxSr != null)
            {
                // Glow up to brilliant white-magenta plasma
                foxSr.color = Color.Lerp(origColor, new Color(2.0f, 1.4f, 2.4f, 1f), t);
            }
            if (foxT != null)
            {
                // Smooth ease-in scale into compact mote
                float scaleT = Mathf.Lerp(1.1f, 0.04f, t * t);
                foxT.localScale = origScale * scaleT;
            }
            yield return null;
        }

        // Burst of purple star motes at the transformation epicenter
        if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.BurstSpawnStarRing(foxPos, 18);
        }

        // Hide fox GameObject
        fox.gameObject.SetActive(false);

        // 4. Activate celestial orb at fox's exact transformation position
        Transform orbT = null;
        if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.ActivateAirborneOrb(foxPos);
            orbT = NyxarisOrbGuide.Instance.orbTransform;
        }

        // Brief dramatic beat hovering as the radiant orb
        yield return new WaitForSeconds(0.25f);

        // 5. The Orb glides smoothly into the center of the seal
        if (orbT != null)
        {
            Vector3 startP = orbT.position;
            float glideDuration = 1.1f;
            elapsed = 0f;

            while (elapsed < glideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / glideDuration);
                // Smooth ease-in-out curve
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                // Subtle celestial hover arc
                float arcY = Mathf.Sin(t * Mathf.PI) * 0.9f;
                orbT.position = Vector3.Lerp(startP, sealCenter, smoothT) + new Vector3(0f, arcY, 0f);
                yield return null;
            }

            orbT.position = sealCenter;
        }
        else
        {
            yield return StartCoroutine(GlideOrbIntoSeal(sealCenter));
        }

        // 6. Enter seal core with explosive star rays
        if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.BurstSpawnStarRing(sealCenter, 26);
            NyxarisOrbGuide.Instance.DeactivateAirborneOrb();
        }
    }

    private IEnumerator GlideOrbIntoSeal(Vector3 targetPos)
    {
        if (NyxarisOrbGuide.Instance == null || NyxarisOrbGuide.Instance.orbTransform == null) yield break;

        Transform orbT = NyxarisOrbGuide.Instance.orbTransform;
        Vector3 startP = orbT.position;
        float duration = 0.95f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            if (orbT != null) orbT.position = Vector3.Lerp(startP, targetPos, t);
            yield return null;
        }

        NyxarisOrbGuide.Instance.BurstSpawnStarRing(targetPos, 22);
    }

    private IEnumerator WhiteoutFlashSequence(Vector3 sealPos)
    {
        GameObject flashGO = new GameObject("WhiteoutFlashCanvas");
        Canvas c = flashGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 999;

        CanvasGroup cg = flashGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        GameObject imgGO = new GameObject("WhiteImg");
        imgGO.transform.SetParent(flashGO.transform, false);
        Image img = imgGO.AddComponent<Image>();
        img.color = flashColor;
        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        try
        {
            // Flash In (rapid)
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / 0.15f);
                yield return null;
            }
            cg.alpha = 1.0f;

            // At peak whiteout: Hide intact seal renderers & colliders
            if (intactSealGO != null)
            {
                var renderers = intactSealGO.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) if (r != null) r.enabled = false;
                var colliders = intactSealGO.GetComponentsInChildren<Collider2D>();
                foreach (var col in colliders) if (col != null) col.enabled = false;

                // CRITICAL FIX: NEVER call SetActive(false) on the GameObject that is hosting NyxarisSealSequence,
                // or if brokenSealGO is a child of intactSealGO! Deactivating this GameObject kills all running
                // coroutines instantly, permanently trapping the game on the whiteout flash screen.
                if (intactSealGO != gameObject && (brokenSealGO == null || !brokenSealGO.transform.IsChildOf(intactSealGO.transform)))
                {
                    intactSealGO.SetActive(false);
                }
            }

            // Reveal Broken Seal in-place, properly scaled with respect to the seal sprite (representing broken 35%)
            if (brokenSealGO != null)
            {
                ApplyProperBrokenSealSizingAndAlignment();
                brokenSealGO.SetActive(true);
                var brokenRenderers = brokenSealGO.GetComponentsInChildren<Renderer>(true);
                foreach (var r in brokenRenderers) if (r != null) r.enabled = true;
            }

            // Spawn Chibi Nyxaris
            if (ChibiNyxarisController.Instance == null)
            {
                Vector3 chibiPos = sealPos + chibiSpawnOffset;
                GameObject chibiGO = new GameObject("ChibiNyxaris_Companion");
                chibiGO.transform.position = chibiPos;
                chibiGO.AddComponent<ChibiNyxarisController>();
            }

            yield return new WaitForSecondsRealtime(0.2f);

            // Flash Out (smooth reveal of the broken 35% remainder)
            t = 0f;
            float fadeOutDuration = 1.0f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1.0f - (t / fadeOutDuration);
                yield return null;
            }
        }
        finally
        {
            if (flashGO != null)
            {
                Destroy(flashGO);
            }
        }
    }

    private void TriggerChatbotStoryDialogue()
    {
        Debug.Log("<color=#D47BFF>[NyxarisSealSequence] Starting Nyxaris Cinematic Story Dialogue...</color>");
        NyxarisManager mgr = NyxarisManager.EnsureInstanceInScene();
        if (mgr == null)
        {
            mgr = FindAnyObjectByType<NyxarisManager>(FindObjectsInactive.Include);
        }

        if (mgr == null)
        {
            Debug.LogWarning("[NyxarisSealSequence] NyxarisManager could not be found or instantiated. Proceeding with fallback conclusion.");
            OnDialogueConcluded();
            return;
        }

        mgr.gameObject.SetActive(true);
        if (mgr.mainInterfacePanel != null) mgr.mainInterfacePanel.SetActive(true);

        NyxarisManager.CinematicLine[] storyLines = new NyxarisManager.CinematicLine[]
        {
            new NyxarisManager.CinematicLine("confidently", "Finally! Free from that wretched binding."),
            new NyxarisManager.CinematicLine("explaining", "My own father thought he could lock me away in this forgotten realm forever... but he clearly underestimated you, mortal."),
            new NyxarisManager.CinematicLine("happy", "You wield that shadow steel with surprising grace. With me at your side, you might actually survive what lies ahead."),
            new NyxarisManager.CinematicLine("excited", "The portal to the Cherry Blossom Forest is open. Let us make haste!")
        };

        mgr.StartCinematicStoryDialogue(storyLines, OnDialogueConcluded);
    }

    private void OnDialogueConcluded()
    {
        Debug.Log("<color=#55FF88>[NyxarisSealSequence] Dialogue concluded! Unlocking Exit Portal & Presenting Tutorial Card...</color>");

        // 1. Unlock Exit Portal to Next Chapter
        if (exitPortalGO != null)
        {
            exitPortalGO.SetActive(true);
            Collider2D portalCol = exitPortalGO.GetComponent<Collider2D>();
            if (portalCol != null) portalCol.enabled = true;
            Debug.Log("<color=#55FF88>[NyxarisSealSequence] Tutorial_ExitPortal is now ACTIVE and UNLOCKED!</color>");
        }

        // 2. Show Tutorial Card
        StartCoroutine(ShowNyxarisTutorialCardRoutine());
    }

    private IEnumerator ShowNyxarisTutorialCardRoutine()
    {
        UIFactory.EnsureEventSystem();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Canvas cardCanvas = UIFactory.CreateCanvas("Nyxaris_TutorialCardCanvas", 980);
        cardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        RectTransform overlayRT = UIFactory.CreateFullScreenPanel(cardCanvas.transform, "Backdrop", new Color(0.04f, 0.01f, 0.08f, 0.90f));

        RectTransform cardRT = UIFactory.CreatePanel(
            overlayRT, "CardFrame", new Color(0.08f, 0.04f, 0.14f, 0.96f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        cardRT.sizeDelta = new Vector2(660f, 440f);

        var cardImg = cardRT.GetComponent<UnityEngine.UI.Image>();
        cardImg.sprite = UIFactory.GetRoundedSprite();
        cardImg.type = UnityEngine.UI.Image.Type.Sliced;

        var outline = cardRT.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.82f, 0.2f, 1.0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Header
        TextMeshProUGUI headerTxt = UIFactory.CreateText(
            cardRT, "Header", "NYXARIS UNLOCKED", 26f,
            new Color(0.95f, 0.65f, 1f, 1f),
            new Vector2(0.5f, 0.87f), new Vector2(580f, 44f),
            TextAlignmentOptions.Center);
        headerTxt.fontStyle = FontStyles.Bold;
        headerTxt.raycastTarget = false;

        // Subtitle
        TextMeshProUGUI subTxt = UIFactory.CreateText(
            cardRT, "Subtitle", "Goddess of Darkness • Heir to Chaos", 14f,
            new Color(0.70f, 0.40f, 0.95f, 0.85f),
            new Vector2(0.5f, 0.78f), new Vector2(580f, 28f),
            TextAlignmentOptions.Center);
        subTxt.raycastTarget = false;

        // Decorative horizontal separator
        RectTransform divRT = UIFactory.CreatePanel(
            cardRT, "Divider", new Color(0.75f, 0.25f, 1.0f, 0.45f),
            new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f));
        divRT.sizeDelta = new Vector2(540f, 2f);
        var divImg = divRT.GetComponent<UnityEngine.UI.Image>();
        if (divImg != null) divImg.raycastTarget = false;

        // Body Text
        string bodyInstructions =
            "Nyxaris is now free from the ancient binding and will travel with you.\n\n" +
            "• Press [T] or [Chat Button] anytime to commune with Nyxaris.\n" +
            "• She offers tactical guidance, realm lore, and arcane advice.\n" +
            "• Step through the Exit Portal to enter Chapter 1: The Cherry Blossom Forest.";

        TextMeshProUGUI bodyTxt = UIFactory.CreateText(
            cardRT, "BodyText", bodyInstructions, 15f,
            new Color(0.92f, 0.90f, 0.96f, 0.95f),
            new Vector2(0.5f, 0.49f), new Vector2(560f, 145f),
            TextAlignmentOptions.TopLeft);
        bodyTxt.lineSpacing = 10f;
        bodyTxt.raycastTarget = false;

        // "PROCEED TO REALM" Button
        Button proceedBtn = UIFactory.CreateCyberGothicButton(
            cardRT, "ProceedBtn", "ENTER CHERRY BLOSSOM FOREST",
            new Vector2(0.5f, 0.17f), new Vector2(400f, 54f),
            null);

        // Prompt Hint below button
        TextMeshProUGUI hintTxt = UIFactory.CreateText(
            cardRT, "HintText", "Press [SPACE], [ENTER], [E] or Click to Continue", 12f,
            new Color(0.85f, 0.65f, 1.0f, 0.85f),
            new Vector2(0.5f, 0.05f), new Vector2(500f, 24f),
            TextAlignmentOptions.Center);
        hintTxt.raycastTarget = false;

        bool hasProceeded = false;
        void DoProceed()
        {
            if (hasProceeded) return;
            hasProceeded = true;
            Debug.Log("<color=#55FF88>[NyxarisSealSequence] Transitioning to Chapter 1 (SampleScene)...</color>");
            move.ExternalMovementLock = false;
            if (cardCanvas != null) Destroy(cardCanvas.gameObject);
            SaveGameProgress();
            SpawnOfChaos.Systems.ArcaneLoadingScreen.LoadScene("SampleScene");
        }

        proceedBtn.onClick.AddListener(DoProceed);

        // Active listener loop: guarantees instant progression via click OR keypress
        while (!hasProceeded)
        {
            if (hintTxt != null)
            {
                float a = 0.50f + 0.50f * Mathf.Sin(Time.unscaledTime * 4.5f);
                hintTxt.color = new Color(0.85f, 0.65f, 1.0f, a);
            }

            // Keyboard/Gamepad inputs
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.E))
            {
                DoProceed();
                yield break;
            }

            // Direct screen-point test on left click
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Input.mousePosition;
                RectTransform btnRT = proceedBtn.GetComponent<RectTransform>();
                if (btnRT != null && RectTransformUtility.RectangleContainsScreenPoint(btnRT, mousePos))
                {
                    DoProceed();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void SaveGameProgress()
    {
        PlayerPrefs.SetInt("NyxarisFreed", 1);
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        PlayerPrefs.SetString("CurrentLevel", "SampleScene");
        PlayerPrefs.Save();
        Debug.Log("<color=#55FF88>[NyxarisSealSequence] Progress saved successfully! Checkpoint set to SampleScene.</color>");
    }
}
