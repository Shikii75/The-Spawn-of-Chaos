using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NyxarisManager : MonoBehaviour
{
    public static NyxarisManager Instance { get; private set; }

    [Header("UI Structure")]
    public GameObject mainInterfacePanel; 

    [Header("UI Elements")]
    public TMP_Text dialogueText;
    public TMP_InputField messageInput;
    public Image portrait;

    [Header("Direct Expression Sprites (Game Dev OS)")]
    public Sprite neutralSprite;
    public Sprite explainingSprite;
    public Sprite cuteSprite;

    [Header("Sprite Lists")]
    public Sprite[] neutralSprites;
    public Sprite[] explainingSprites;
    public Sprite[] thinkingSprites;
    public Sprite[] angrySprites;
    public Sprite[] cuteSprites;
    public Sprite[] motherlySprites;
    public Sprite[] kindSprites;

    [Header("Current Game State Connection")]
    [Tooltip("Options: idle, combat, story")]
    public string currentMode = "idle";
    [Range(0f, 1f)]
    public float currentTrust = 0.5f;

    private Coroutine resetEmotionCoroutine;
    private GameObject portraitRoot; // standalone portrait container on root canvas

    [System.Serializable]
    public class NyxarisRequest { public string message; public string mode; public float trust; public string level; }
    [System.Serializable]
    public class NyxarisResponse { public string response; public string emotion; public string sprite_key; }

    public static bool IsChatActive => Instance != null && Instance.mainInterfacePanel != null && Instance.mainInterfacePanel.activeSelf;

    public static bool IsTyping
    {
        get
        {
            if (IsChatActive) return true;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            {
                var go = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                if (go.GetComponent<TMPro.TMP_InputField>() != null || go.GetComponent<UnityEngine.UI.InputField>() != null)
                    return true;
            }
            return false;
        }
    }

    void Awake()
    {
        Instance = this;
        AutoLoadExpressionSprites();
        if (mainInterfacePanel != null)
        {
            mainInterfacePanel.SetActive(false);
        }
    }

    void Start()
    {
        AutoLoadExpressionSprites();
        CreateStandalonePortrait();

        NyxarisUIStyler styler = GetStyler();
        if (styler != null)
        {
            styler.ApplyStyling();
        }

        if (messageInput != null)
        {
            messageInput.onSubmit.AddListener((text) => SendInputMessage());
        }

        HideInterface();
    }

    // ═══════════════════════════════════════════════════════════
    //  STANDALONE PORTRAIT — lives on the root Canvas directly
    //  Nothing can clip, mask, or hide this.
    // ═══════════════════════════════════════════════════════════
    private void CreateStandalonePortrait()
    {
        // Find the root canvas in the scene
        Canvas rootCanvas = null;

        // First check if MainInterface has a canvas
        if (mainInterfacePanel != null)
        {
            Canvas panelCanvas = mainInterfacePanel.GetComponentInParent<Canvas>();
            if (panelCanvas != null) rootCanvas = panelCanvas.rootCanvas;
        }

        // Fallback: find any canvas
        if (rootCanvas == null)
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas c in allCanvases)
            {
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    rootCanvas = c;
                    break;
                }
            }
            if (rootCanvas == null && allCanvases.Length > 0)
                rootCanvas = allCanvases[0].rootCanvas;
        }

        if (rootCanvas == null)
        {
            Debug.LogError("[NyxarisManager] No Canvas found in scene! Cannot create portrait.");
            return;
        }

        // Create (or find) the standalone portrait container
        Transform existing = rootCanvas.transform.Find("NyxarisPortraitOverlay");
        if (existing != null)
        {
            portraitRoot = existing.gameObject;
            portrait = portraitRoot.GetComponentInChildren<Image>();
        }
        else
        {
            // Create the container
            portraitRoot = new GameObject("NyxarisPortraitOverlay");
            portraitRoot.transform.SetParent(rootCanvas.transform, false);

            // Create the portrait image
            GameObject imgGO = new GameObject("PortraitImage");
            imgGO.transform.SetParent(portraitRoot.transform, false);

            portrait = imgGO.AddComponent<Image>();
            portrait.raycastTarget = false; // don't block clicks
        }

        // Layer IN FRONT of the dark background tint, but BEHIND the dialogue chat panel
        if (mainInterfacePanel != null)
        {
            Transform panelParent = mainInterfacePanel.transform.parent;
            if (panelParent != null)
            {
                portraitRoot.transform.SetParent(panelParent, false);
                int panelIndex = mainInterfacePanel.transform.GetSiblingIndex();
                // Place right before mainInterfacePanel so it renders in front of dark tint but behind panel
                portraitRoot.transform.SetSiblingIndex(Mathf.Max(1, panelIndex));
            }
        }
        else
        {
            portraitRoot.transform.SetAsFirstSibling();
        }

        // Position the portrait: right side, bottom of PNG touching the 220px blue top line of dialogue panel
        RectTransform containerRt = portraitRoot.GetComponent<RectTransform>();
        if (containerRt == null) containerRt = portraitRoot.AddComponent<RectTransform>();
        containerRt.anchorMin = Vector2.zero;
        containerRt.anchorMax = Vector2.one;
        containerRt.offsetMin = Vector2.zero;
        containerRt.offsetMax = Vector2.zero;

        RectTransform portRt = portrait.rectTransform;
        portRt.anchorMin = new Vector2(1f, 0f);
        portRt.anchorMax = new Vector2(1f, 0f);
        portRt.pivot = new Vector2(1f, 0f);
        portRt.sizeDelta = new Vector2(480f, 540f);
        portRt.anchoredPosition = new Vector2(-80f, 248f); // Bottom sits just above the 220px cyan top line

        // Set display properties
        portrait.color = Color.white;
        portrait.preserveAspect = true;
        portrait.enabled = true;

        // Assign default sprite
        Sprite def = neutralSprite ??
                    (neutralSprites != null && neutralSprites.Length > 0 ? neutralSprites[0] : null) ??
                    explainingSprite ?? cuteSprite;
        if (def != null)
        {
            portrait.sprite = def;
        }

        // Also sync the styler's reference
        NyxarisUIStyler styler = GetStyler();
        if (styler != null)
        {
            styler.portraitImage = portrait;
        }

        // Start hidden (HideInterface will be called after)
        portraitRoot.SetActive(false);

        Debug.Log($"[NyxarisManager] Portrait created on root Canvas '{rootCanvas.name}'. " +
                  $"Sprite: {(portrait.sprite != null ? portrait.sprite.name : "NONE")}");
    }

    // ═══════════════════════════════════════════════════════════
    //  SPRITE LOADING
    // ═══════════════════════════════════════════════════════════

    [ContextMenu("Auto Load Expression Sprites")]
    public void AutoLoadExpressionSprites()
    {
        if (neutralSprite == null) neutralSprite = LoadSpriteDirectly("neutral");
        if (explainingSprite == null) explainingSprite = LoadSpriteDirectly("explaining");
        if (cuteSprite == null) cuteSprite = LoadSpriteDirectly("cutely-annoyed");

        if (neutralSprites == null || neutralSprites.Length == 0)
        {
            List<Sprite> list = LoadAllSubSprites("neutral");
            if (neutralSprite != null && !list.Contains(neutralSprite)) list.Insert(0, neutralSprite);
            neutralSprites = list.ToArray();
        }
        if (explainingSprites == null || explainingSprites.Length == 0)
        {
            List<Sprite> list = LoadAllSubSprites("explaining");
            if (explainingSprite != null && !list.Contains(explainingSprite)) list.Insert(0, explainingSprite);
            explainingSprites = list.ToArray();
        }
        if (cuteSprites == null || cuteSprites.Length == 0)
        {
            List<Sprite> list = LoadAllSubSprites("cutely-annoyed");
            if (cuteSprite != null && !list.Contains(cuteSprite)) list.Insert(0, cuteSprite);
            cuteSprites = list.ToArray();
        }
    }

    public Sprite LoadSpriteDirectly(string fileNameNoExt)
    {
        // Single sprite load
        Sprite s = Resources.Load<Sprite>("NyxarisExpressions/" + fileNameNoExt);
        if (s != null) return s;

        // LoadAll for Multiple sprite mode textures
        Sprite[] all = Resources.LoadAll<Sprite>("NyxarisExpressions");
        if (all != null)
        {
            foreach (Sprite spr in all)
            {
                if (spr != null && spr.name.ToLower().Contains(fileNameNoExt.ToLower()))
                    return spr;
            }
        }

#if UNITY_EDITOR
        // Editor fallback: try AssetDatabase
        string[] paths = new[] {
            "Assets/Scenes/art/Nyxaris Expressions art/" + fileNameNoExt + ".png",
            "Assets/Resources/NyxarisExpressions/" + fileNameNoExt + ".png"
        };
        foreach (string path in paths)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets != null)
            {
                foreach (var a in assets)
                {
                    if (a is Sprite spr && spr != null) return spr;
                }
            }
        }
#endif
        return null;
    }

    private List<Sprite> LoadAllSubSprites(string fileNameNoExt)
    {
        List<Sprite> result = new List<Sprite>();
        Sprite[] all = Resources.LoadAll<Sprite>("NyxarisExpressions");
        if (all != null)
        {
            foreach (var s in all)
            {
                if (s != null && s.name.ToLower().Contains(fileNameNoExt.ToLower()) && !result.Contains(s))
                    result.Add(s);
            }
        }
#if UNITY_EDITOR
        if (result.Count == 0)
        {
            string path = "Assets/Scenes/art/Nyxaris Expressions art/" + fileNameNoExt + ".png";
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets != null)
            {
                foreach (var a in assets)
                {
                    if (a is Sprite s && s != null && !result.Contains(s)) result.Add(s);
                }
            }
        }
#endif
        return result;
    }

    public void EnsureDefaultPortrait()
    {
        if (portrait == null) CreateStandalonePortrait();
        if (portrait == null) return;

        portrait.enabled = true;
        portrait.gameObject.SetActive(true);
        portrait.color = Color.white;
        portrait.preserveAspect = true;

        if (portrait.sprite == null)
        {
            AutoLoadExpressionSprites();
            Sprite def = neutralSprite ??
                        (neutralSprites != null && neutralSprites.Length > 0 ? neutralSprites[0] : null) ??
                        explainingSprite ?? cuteSprite;
            if (def != null) portrait.sprite = def;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INTERFACE SHOW / HIDE
    // ═══════════════════════════════════════════════════════════

    private NyxarisUIStyler GetStyler()
    {
        if (mainInterfacePanel == null) return null;
        return mainInterfacePanel.GetComponent<NyxarisUIStyler>() ?? 
               mainInterfacePanel.GetComponentInChildren<NyxarisUIStyler>(true);
    }

    public void ShowInterface()
    {
        if (HUDManager.IsInMainMenu())
        {
            HideInterface();
            return;
        }

        EnsureDefaultPortrait();

        // Show portrait overlay
        if (portraitRoot != null) portraitRoot.SetActive(true);

        if (mainInterfacePanel != null)
        {
            NyxarisUIStyler styler = GetStyler();
            if (styler != null)
            {
                mainInterfacePanel.SetActive(true);
                styler.ApplyStyling();
                styler.AnimateOpen();
            }
            else
            {
                mainInterfacePanel.SetActive(true);
            }
            if (messageInput != null)
            {
                messageInput.text = "";
                messageInput.ActivateInputField();
            }
            HUDManager.Instance?.UpdateVisibility();
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
        }
    }

    public void HideInterface()
    {
        // Hide portrait overlay
        if (portraitRoot != null) portraitRoot.SetActive(false);

        if (mainInterfacePanel != null)
        {
            NyxarisUIStyler styler = GetStyler();
            if (styler != null)
            {
                styler.AnimateClose();
            }
            else
            {
                mainInterfacePanel.SetActive(false);
            }
        }
        HUDManager.Instance?.UpdateVisibility();
        SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
    }

    void Update()
    {
        if (HUDManager.IsInMainMenu())
        {
            if ((mainInterfacePanel != null && mainInterfacePanel.activeSelf) || (portraitRoot != null && portraitRoot.activeSelf))
            {
                HideInterface();
            }
            return;
        }

        if (mainInterfacePanel != null && mainInterfacePanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideInterface();
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (messageInput != null && !string.IsNullOrWhiteSpace(messageInput.text))
                {
                    SendInputMessage();
                }
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (mainInterfacePanel != null)
            {
                CanvasGroup cg = mainInterfacePanel.GetComponent<CanvasGroup>();
                bool isVisible = mainInterfacePanel.activeSelf && (cg == null || cg.alpha > 0.01f);
                if (isVisible)
                    HideInterface();
                else
                    ShowInterface();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  MESSAGING
    // ═══════════════════════════════════════════════════════════

    public void SendInputMessage()
    {
        string msg = messageInput.text;
        if (string.IsNullOrWhiteSpace(msg)) return;

        StartCoroutine(SendRequest(msg));
        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    IEnumerator SendRequest(string msg)
    {
        NyxarisUIStyler styler = GetStyler();
        string mode = currentMode;
        if (styler != null)
        {
            string dropdownMode = styler.GetSelectedMode();
            if (!string.IsNullOrEmpty(dropdownMode)) mode = dropdownMode;
        }

        NyxarisRequest requestData = new NyxarisRequest { 
            message = msg, mode = mode, trust = currentTrust,
            level = SceneManager.GetActiveScene().name
        };
        string json = JsonUtility.ToJson(requestData);

        if (styler != null) styler.ShowLoading();

        bool success = false;
        NyxarisResponse response = null;

        using (UnityWebRequest request = new UnityWebRequest("http://127.0.0.1:5001/nyxaris", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 2; // 2-second strict timeout for instant feedback

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(request.downloadHandler.text))
            {
                try
                {
                    response = JsonUtility.FromJson<NyxarisResponse>(request.downloadHandler.text);
                    if (response != null && !string.IsNullOrEmpty(response.response))
                    {
                        success = true;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[NyxarisManager] Server response parse error, falling back to local AI engine: " + ex.Message);
                }
            }
        }

        if (styler != null) styler.HideLoading();

        if (!success)
        {
            // Fast Instant Local Engine Fallback
            response = GetLocalFastResponse(msg);
        }

        Debug.Log("Nyxaris Expression: " + response.sprite_key);
        if (styler != null && !string.IsNullOrEmpty(response.sprite_key))
            styler.SetSpriteKeyDisplay(response.sprite_key);

        SetEmotion(response.emotion);
        StartCoroutine(TypeText(response.response));
    }

    private NyxarisResponse GetLocalFastResponse(string userMsg)
    {
        string textLower = userMsg.ToLower();
        string activeScene = SceneManager.GetActiveScene().name;
        
        string respText = "";
        string emotion = "explaining";

        if (textLower.Contains("who") || textLower.Contains("killer") || textLower.Contains("massacre") || textLower.Contains("follower"))
        {
            respText = "My followers were slaughtered in cold blood across this realm. Clues point to a warrior hidden among the mountain clans—or something far darker impersonating them.";
            emotion = "explaining";
        }
        else if (textLower.Contains("strawhat") || textLower.Contains("dojo 1") || textLower.Contains("dojo1"))
        {
            respText = "The Strawhat Clan claims innocence, insisting the killer is a shape-shifter in the Samurai Clan. Do not lower your guard in their dōjō.";
            emotion = "thinking";
        }
        else if (textLower.Contains("samurai") || textLower.Contains("dojo 2") || textLower.Contains("dojo2") || textLower.Contains("master"))
        {
            respText = "Rumors say the Samurai Clan's Master was sighted alive, despite dying two years ago... Be vigilant; things are not as they appear.";
            emotion = "angry";
        }
        else if (textLower.Contains("cave") || textLower.Contains("spider") || textLower.Contains("tsuchigumo"))
        {
            respText = "The giant spiders multiplying in the regional cave are no mere beasts. They serve Tsuchigumo—the true culprit behind the massacre!";
            emotion = "explaining";
        }
        else if (textLower.Contains("hello") || textLower.Contains("hi") || textLower.Contains("hey") || textLower.Contains("nyxaris"))
        {
            respText = "I am with you, mortal. Speak your mind or ask for guidance on our investigation.";
            emotion = "kind";
        }
        else
        {
            // Scene-based contextual responses
            if (activeScene.Contains("Dojo1"))
            {
                respText = "Confront the Strawhat warriors at the altar. Search for any sign of who orchestrated the massacre.";
                emotion = "explaining";
            }
            else if (activeScene.Contains("Dojo2"))
            {
                respText = "This dōjō reeks of deception. The Master who stands before you is a false illusion!";
                emotion = "thinking";
            }
            else if (activeScene.Contains("Cave"))
            {
                respText = "We stand at the threshold of truth. Tsuchigumo awaits below in the webs. Prepare yourself for battle!";
                emotion = "angry";
            }
            else
            {
                respText = "Keep moving through the Cherry Blossom Forest. The answers we seek lie in the mountain dōjōs and the depths of the cave.";
                emotion = "explaining";
            }
        }

        return new NyxarisResponse
        {
            response = respText,
            emotion = emotion,
            sprite_key = emotion
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  EMOTIONS
    // ═══════════════════════════════════════════════════════════

    void SetEmotion(string emotion)
    {
        if (resetEmotionCoroutine != null) StopCoroutine(resetEmotionCoroutine);
        
        Sprite selectedSprite = null;
        switch (emotion.ToLower().Trim())
        {
            case "explaining":
                selectedSprite = explainingSprite ?? GetRandomSprite(explainingSprites);
                break;
            case "thinking":
                selectedSprite = GetRandomSprite(thinkingSprites) ?? explainingSprite;
                break;
            case "angry":
                selectedSprite = GetRandomSprite(angrySprites) ?? cuteSprite;
                break;
            case "cute":
            case "cutely-annoyed":
                selectedSprite = cuteSprite ?? GetRandomSprite(cuteSprites);
                break;
            case "motherly":
                selectedSprite = GetRandomSprite(motherlySprites) ?? neutralSprite;
                break;
            case "kind":
                selectedSprite = GetRandomSprite(kindSprites) ?? neutralSprite;
                break;
            default:
                selectedSprite = neutralSprite ?? GetRandomSprite(neutralSprites);
                break;
        }

        if (selectedSprite != null && portrait != null)
        {
            portrait.sprite = selectedSprite;
            portrait.enabled = true;
            portrait.color = Color.white;
            if (emotion != "neutral")
                resetEmotionCoroutine = StartCoroutine(ResetEmotionAfterDelay());
        }
    }

    Sprite GetRandomSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0) return null;
        return sprites[Random.Range(0, sprites.Length)];
    }

    IEnumerator ResetEmotionAfterDelay()
    {
        yield return new WaitForSeconds(3.0f);
        if (portrait != null)
        {
            Sprite def = neutralSprite ?? (neutralSprites != null && neutralSprites.Length > 0 ? neutralSprites[0] : null);
            if (def != null)
            {
                portrait.sprite = def;
                portrait.enabled = true;
                portrait.color = Color.white;
            }
        }
    }

    IEnumerator TypeText(string text)
    {
        NyxarisUIStyler styler = GetStyler();
        if (styler != null) styler.BouncePortrait();

        dialogueText.text = "";
        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(0.008f); // Super crisp & instant typing animation
        }
    }
}