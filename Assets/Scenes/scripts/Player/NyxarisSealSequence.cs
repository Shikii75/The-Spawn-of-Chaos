using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// NyxarisSealSequence - Orchestrates the tutorial finale:
/// 1. Tracks all covering WEBs (checks IsBroken or destruction).
/// 2. Fox Nyxaris morphs into Dark Orb and enters the seal.
/// 3. Seal violently overloads with energy sparks and camera shake.
/// 4. Fullscreen Whiteout Flash.
/// 5. Intact seal visuals swapped with Broken Seal, and Chibi Nyxaris emerges.
/// 6. Transitions to the Chatbot UI with animated portraits and [E] prompt.
/// 7. Unlocks Tutorial_ExitPortal upon conclusion.
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
    public float overloadDuration = 2.2f;
    public float maxCameraShake = 0.35f;
    public Color flashColor = Color.white;

    private bool sequenceTriggered = false;
    private bool isMonitoringWebs = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitInTutorialScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        // Strictly only auto-initialize in Tutorial scenes — never in SampleScene or main gameplay levels
        if (sceneName.ToLower().Contains("tutorial"))
        {
            if (Instance == null)
            {
                GameObject seqHost = new GameObject("Nyxaris_SealSequenceManager");
                seqHost.AddComponent<NyxarisSealSequence>();
            }
        }
    }

    void Awake()
    {
        Instance = this;
        if (intactSealGO == null)
        {
            intactSealGO = GameObject.Find("seal") ?? GameObject.Find("Seal") ?? 
                           GameObject.Find("NyxarisShrineCage") ?? GameObject.Find("ShrineCage") ??
                           GameObject.Find("shrine") ?? GameObject.Find("Shrine");
        }

        AutoDetectCoveringWebs();
        AutoDetectBrokenSealAndExitPortal();
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
    }

    private void OnWebObjectBroken(BreakableObject broken)
    {
        if (isMonitoringWebs && !sequenceTriggered)
        {
            CheckWebStatus();
        }
    }

    void Start()
    {
        if (brokenSealGO != null && brokenSealGO != intactSealGO)
        {
            brokenSealGO.SetActive(false);
        }

        AutoDetectCoveringWebs();
    }

    private void AutoDetectCoveringWebs()
    {
        if (coveringWebs == null) coveringWebs = new List<BreakableObject>();
        if (coveringWebs.Count == 0)
        {
            var allBreakables = FindObjectsByType<BreakableObject>(FindObjectsSortMode.None);
            foreach (var b in allBreakables)
            {
                if (b == null) continue;
                string n = b.gameObject.name.ToUpper();
                if (n.Contains("WEB") || (intactSealGO != null && Vector2.Distance(intactSealGO.transform.position, b.transform.position) < 12.0f))
                {
                    if (!coveringWebs.Contains(b)) coveringWebs.Add(b);
                }
            }
        }
        Debug.Log($"<color=#D47BFF>[NyxarisSealSequence] Registered {coveringWebs.Count} covering webs.</color>");
    }

    private void AutoDetectBrokenSealAndExitPortal()
    {
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

    void Update()
    {
        if (!sequenceTriggered && isMonitoringWebs)
        {
            CheckWebStatus();
        }
    }

    private void CheckWebStatus()
    {
        if (coveringWebs.Count == 0)
        {
            AutoDetectCoveringWebs();
        }

        coveringWebs.RemoveAll(w => w == null || w.IsBroken || !w.gameObject.activeInHierarchy);

        if (coveringWebs.Count == 0 && !sequenceTriggered)
        {
            sequenceTriggered = true;
            isMonitoringWebs = false;
            StartCoroutine(SealOverloadAndBreakRoutine());
        }
    }

    private IEnumerator SealOverloadAndBreakRoutine()
    {
        Debug.Log("<color=#D47BFF>[NyxarisSealSequence] All webs cleared! Initiating Nyxaris Seal Breakdown sequence...</color>");

        move.ExternalMovementLock = true;
        Vector3 sealCenter = intactSealGO != null ? intactSealGO.transform.position : transform.position;

        // 1. Fox Nyxaris morphs into Dark Orb and flies into the Seal
        if (FoxNyxarisController.Instance != null)
        {
            FoxNyxarisController.Instance.gameObject.SetActive(false);
        }

        if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.ActivateAirborneOrb(sealCenter + Vector3.up * 2.5f);
            yield return StartCoroutine(GlideOrbIntoSeal(sealCenter));
            NyxarisOrbGuide.Instance.DeactivateAirborneOrb();
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        // 2. Violent Seal Overload: Pulsing Glow + Camera Shake
        Camera mainCam = Camera.main;
        Vector3 origCamPos = mainCam != null ? mainCam.transform.position : Vector3.zero;

        SpriteRenderer sealSr = intactSealGO != null ? intactSealGO.GetComponentInChildren<SpriteRenderer>() : null;

        float elapsed = 0f;
        while (elapsed < overloadDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / overloadDuration;

            if (sealSr != null)
            {
                float pulseFreq = Mathf.Lerp(8f, 28f, progress);
                Color targetGlow = Color.Lerp(new Color(0.85f, 0.2f, 1f, 1f), Color.white, Mathf.PingPong(elapsed * pulseFreq, 1f));
                sealSr.color = targetGlow;
            }

            if (mainCam != null)
            {
                float shakeMag = Mathf.Lerp(0.04f, maxCameraShake, progress);
                Vector2 randOffset = Random.insideUnitCircle * shakeMag;
                mainCam.transform.position = new Vector3(origCamPos.x + randOffset.x, origCamPos.y + randOffset.y, origCamPos.z);
            }

            yield return null;
        }

        if (mainCam != null) mainCam.transform.position = origCamPos;

        // 3. Whiteout Screen Flash
        yield return StartCoroutine(WhiteoutFlashSequence(sealCenter));

        // 4. Trigger Cinematic Chatbot Dialogue
        yield return new WaitForSeconds(0.3f);
        TriggerChatbotStoryDialogue();
    }

    private IEnumerator GlideOrbIntoSeal(Vector3 targetPos)
    {
        if (NyxarisOrbGuide.Instance == null || NyxarisOrbGuide.Instance.orbTransform == null) yield break;

        Transform orbT = NyxarisOrbGuide.Instance.orbTransform;
        Vector3 startP = orbT.position;
        float duration = 0.85f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            if (orbT != null) orbT.position = Vector3.Lerp(startP, targetPos, t);
            yield return null;
        }

        NyxarisOrbGuide.Instance.BurstSpawnStarRing(targetPos, 20);
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

        // Flash In (rapid)
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
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
        }

        if (brokenSealGO != null)
        {
            brokenSealGO.SetActive(true);
        }

        // Spawn Chibi Nyxaris
        if (ChibiNyxarisController.Instance == null)
        {
            Vector3 chibiPos = sealPos + chibiSpawnOffset;
            GameObject chibiGO = new GameObject("ChibiNyxaris_Companion");
            chibiGO.transform.position = chibiPos;
            chibiGO.AddComponent<ChibiNyxarisController>();
        }

        yield return new WaitForSeconds(0.2f);

        // Flash Out (smooth reveal)
        t = 0f;
        float fadeOutDuration = 1.0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            cg.alpha = 1.0f - (t / fadeOutDuration);
            yield return null;
        }

        Destroy(flashGO);
    }

    private void TriggerChatbotStoryDialogue()
    {
        NyxarisManager mgr = NyxarisManager.EnsureInstanceInScene();
        if (mgr == null)
        {
            OnDialogueConcluded();
            return;
        }

        NyxarisManager.CinematicLine[] storyLines = new NyxarisManager.CinematicLine[]
        {
            new NyxarisManager.CinematicLine("confidently", "Finally! Free from that wretched binding."),
            new NyxarisManager.CinematicLine("explaining", "My own father thought he could lock me away in this forgotten realm forever... but he clearly underestimated you, mortal."),
            new NyxarisManager.CinematicLine("talking", "You've proven you have the strength to wield shadow and blade. The mountain path ahead is dangerous, but with me at your side, you might actually survive."),
            new NyxarisManager.CinematicLine("happy", "Let us proceed to the forest. The portal is open!")
        };

        mgr.StartCinematicStoryDialogue(storyLines, OnDialogueConcluded);
    }

    private void OnDialogueConcluded()
    {
        Debug.Log("<color=#55FF88>[NyxarisSealSequence] Dialogue concluded! Opening Tutorial_ExitPortal.</color>");

        if (exitPortalGO != null)
        {
            exitPortalGO.SetActive(true);
            var portalSr = exitPortalGO.GetComponentInChildren<SpriteRenderer>();
            if (portalSr != null)
            {
                portalSr.color = new Color(0.85f, 0.4f, 1f, 1f);
            }
        }

        move.ExternalMovementLock = false;
    }
}
