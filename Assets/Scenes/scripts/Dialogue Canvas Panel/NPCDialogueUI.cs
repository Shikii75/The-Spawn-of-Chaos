using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NPCDialogueUI : MonoBehaviour
{
    public static NPCDialogueUI Instance { get; private set; }

    [Header("UI References (Auto-resolved)")]
    [Tooltip("The speech bubble or dialogue panel GameObject container.")]
    public GameObject dialoguePanel;
    
    [Tooltip("TextMeshPro component for the NPC's name.")]
    public TMP_Text nameText;
    
    [Tooltip("TextMeshPro component for the dialogue body text.")]
    public TMP_Text dialogueText;
    
    [Tooltip("TextMeshPro component indicating dialogue can be advanced (e.g. '▶').")]
    public TMP_Text advanceIndicator;

    [Tooltip("Optional: Image component for character portrait.")]
    public Image portraitImage;

    [Tooltip("Optional: Container for character portrait.")]
    public GameObject portraitContainer;

    [Header("Default Settings")]
    [Tooltip("Typewriter character text speed interval in seconds.")]
    public float typeSpeed = 0.02f;

    // ── Dialogue state ──
    private string[] dialogueLines;
    private int currentLineIndex;
    private bool isTyping;
    private Coroutine typingCoroutine;
    private Action onComplete;

    private int openedFrameCount;

    // ── Custom Styling State ──
    private Color currentNameColor = new Color(0.74f, 0.52f, 1.00f, 1f);  // Lilac purple
    private Color currentTextColor = new Color(0.91f, 0.88f, 0.96f, 1f);  // Near-white
    private AudioClip currentBeepSound;
    private float currentVoicePitch = 1.0f;
    private AudioSource audioSource;

    public bool IsDialogueActive => dialoguePanel != null && dialoguePanel.activeSelf;

    private void AutoResolveReferences()
    {
        dialoguePanel = this.gameObject;
        nameText = null;
        dialogueText = null;
        advanceIndicator = null;

        var allTexts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var txt in allTexts)
        {
            string n = txt.gameObject.name.ToLower();
            if (n.Contains("dialogue") || n.Contains("body") || n.Contains("content") || n == "text" || n.Contains("body text"))
            {
                dialogueText = txt;
            }
            else if (n.Contains("name") || n.Contains("title") || n.Contains("character") || n.Contains("npc name"))
            {
                nameText = txt;
            }
            else if (n.Contains("advance") || n.Contains("indicator") || n.Contains("icon") || n.Contains("arrow"))
            {
                advanceIndicator = txt;
            }
        }
    }

    [ContextMenu("Apply Dialogue UI Styling")]
    public void ApplyStyling()
    {
        AutoResolveReferences();

        // 1. Setup Canvas and Scale mode
        Canvas nearestCanvas = GetComponentInParent<Canvas>();
        Canvas rootCanvas = nearestCanvas != null ? nearestCanvas.rootCanvas : null;
        if (rootCanvas != null)
        {
            CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (nearestCanvas != null && nearestCanvas != rootCanvas)
        {
            RectTransform rt = nearestCanvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin        = Vector2.zero;
                rt.anchorMax        = Vector2.one;
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale       = Vector3.one;
            }
        }

        // Parent panel: Make transparent overlay to block raycasts
        Image parentImg = GetComponent<Image>();
        if (parentImg == null) parentImg = gameObject.AddComponent<Image>();
        parentImg.color = new Color(0f, 0f, 0f, 0f);

        // 2. Setup Dialogue Box container
        Transform boxTrans = transform.Find("DialogueBox");
        GameObject boxGo;
        if (boxTrans == null)
        {
            boxGo = new GameObject("DialogueBox");
            boxGo.transform.SetParent(this.transform, false);
        }
        else
        {
            boxGo = boxTrans.gameObject;
        }
        boxGo.SetActive(true);

        RectTransform boxRt = boxGo.GetComponent<RectTransform>();
        if (boxRt == null) boxRt = boxGo.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0f);
        boxRt.anchorMax = new Vector2(0.5f, 0f);
        boxRt.pivot = new Vector2(0.5f, 0f);
        boxRt.anchoredPosition = new Vector2(0f, 60f);
        boxRt.sizeDelta = new Vector2(1200f, 260f);
        boxRt.localScale = Vector3.one;
        boxRt.localRotation = Quaternion.identity;

        Image boxBg = boxGo.GetComponent<Image>();
        if (boxBg == null) boxBg = boxGo.AddComponent<Image>();
        boxBg.color = new Color(0.06f, 0.04f, 0.12f, 0.93f); // Deep dark purple, matches Nyxaris panel
        boxBg.sprite = null;

        // Modern highlight top bar
        Transform accentTrans = boxGo.transform.Find("HeaderLine");
        GameObject accentGo;
        if (accentTrans == null)
        {
            accentGo = new GameObject("HeaderLine");
            accentGo.transform.SetParent(boxGo.transform, false);
        }
        else
        {
            accentGo = accentTrans.gameObject;
        }
        accentGo.SetActive(true);

        RectTransform accentRt = accentGo.GetComponent<RectTransform>();
        if (accentRt == null) accentRt = accentGo.AddComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.anchoredPosition = Vector2.zero;
        accentRt.sizeDelta = new Vector2(0f, 4f);
        accentRt.localScale = Vector3.one;

        Image accentBg = accentGo.GetComponent<Image>();
        if (accentBg == null) accentBg = accentGo.AddComponent<Image>();
        accentBg.color = new Color(0.55f, 0.18f, 0.95f, 1f); // Rich purple accent #8C2DF2
        accentBg.sprite = null;

        // 3. Setup Portrait Container
        Transform portContainerTrans = boxGo.transform.Find("PortraitContainer");
        GameObject portContainerGo;
        if (portContainerTrans == null)
        {
            portContainerGo = new GameObject("PortraitContainer");
            portContainerGo.transform.SetParent(boxGo.transform, false);
        }
        else
        {
            portContainerGo = portContainerTrans.gameObject;
        }

        RectTransform portContainerRt = portContainerGo.GetComponent<RectTransform>();
        if (portContainerRt == null) portContainerRt = portContainerGo.AddComponent<RectTransform>();
        portContainerRt.anchorMin = new Vector2(0f, 0.5f);
        portContainerRt.anchorMax = new Vector2(0f, 0.5f);
        portContainerRt.pivot = new Vector2(0f, 0.5f);
        portContainerRt.anchoredPosition = new Vector2(25f, 0f);
        portContainerRt.sizeDelta = new Vector2(140f, 140f);
        portContainerRt.localScale = Vector3.one;

        Image portBg = portContainerGo.GetComponent<Image>();
        if (portBg == null) portBg = portContainerGo.AddComponent<Image>();
        portBg.color = new Color(0.16f, 0.16f, 0.22f, 0.6f);
        portBg.sprite = null;

        portraitContainer = portContainerGo;

        Transform portImageTrans = portContainerGo.transform.Find("PortraitImage");
        GameObject portImageGo;
        if (portImageTrans == null)
        {
            portImageGo = new GameObject("PortraitImage");
            portImageGo.transform.SetParent(portContainerGo.transform, false);
        }
        else
        {
            portImageGo = portImageTrans.gameObject;
        }

        RectTransform portImageRt = portImageGo.GetComponent<RectTransform>();
        if (portImageRt == null) portImageRt = portImageGo.AddComponent<RectTransform>();
        portImageRt.anchorMin = Vector2.zero;
        portImageRt.anchorMax = Vector2.one;
        portImageRt.anchoredPosition = Vector2.zero;
        portImageRt.sizeDelta = Vector2.zero;
        portImageRt.localScale = Vector3.one;

        portraitImage = portImageGo.GetComponent<Image>();
        if (portraitImage == null) portraitImage = portImageGo.AddComponent<Image>();
        portraitImage.preserveAspect = true;

        // Hide portrait by default unless actively populated
        bool hasPortrait = portraitImage != null && portraitImage.sprite != null;
        portContainerGo.SetActive(hasPortrait);

        // 4. Setup Name Badge
        Transform badgeTrans = boxGo.transform.Find("NameBadge");
        GameObject badgeGo;
        if (badgeTrans == null)
        {
            badgeGo = new GameObject("NameBadge");
            badgeGo.transform.SetParent(boxGo.transform, false);
        }
        else
        {
            badgeGo = badgeTrans.gameObject;
        }
        badgeGo.SetActive(true);

        RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
        if (badgeRt == null) badgeRt = badgeGo.AddComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 1f);
        badgeRt.anchorMax = new Vector2(0f, 1f);
        badgeRt.pivot = new Vector2(0f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(30f, 0f);
        badgeRt.sizeDelta = new Vector2(220f, 40f);
        badgeRt.localScale = Vector3.one;

        Image badgeBg = badgeGo.GetComponent<Image>();
        if (badgeBg == null) badgeBg = badgeGo.AddComponent<Image>();
        badgeBg.color = new Color(0.15f, 0.15f, 0.22f, 0.95f);
        badgeBg.sprite = null;

        // Reparent and format NPC Name Text
        if (nameText == null)
        {
            Transform t = badgeGo.transform.Find("NPC Name Text");
            if (t != null) nameText = t.GetComponent<TMP_Text>();
            else
            {
                GameObject g = new GameObject("NPC Name Text");
                g.transform.SetParent(badgeGo.transform, false);
                nameText = g.AddComponent<TextMeshProUGUI>();
            }
        }
        else
        {
            nameText.transform.SetParent(badgeGo.transform, false);
        }

        RectTransform nameRt = nameText.rectTransform;
        nameRt.anchorMin = Vector2.zero;
        nameRt.anchorMax = Vector2.one;
        nameRt.anchoredPosition = Vector2.zero;
        nameRt.sizeDelta = Vector2.zero;
        nameRt.localScale = Vector3.one;
        nameRt.localRotation = Quaternion.identity;

        nameText.fontSize = 32;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = currentNameColor;
        nameText.characterSpacing = 0f;
        nameText.characterHorizontalScale = 1.0f;
        nameText.margin = Vector4.zero;
        nameText.enableWordWrapping = false;

        // 5. Reparent and format Dialogue Body Text
        if (dialogueText == null)
        {
            Transform t = boxGo.transform.Find("Dialogue Body Text");
            if (t != null) dialogueText = t.GetComponent<TMP_Text>();
            else
            {
                GameObject g = new GameObject("Dialogue Body Text");
                g.transform.SetParent(boxGo.transform, false);
                dialogueText = g.AddComponent<TextMeshProUGUI>();
            }
        }
        else
        {
            dialogueText.transform.SetParent(boxGo.transform, false);
        }

        RectTransform textRt = dialogueText.rectTransform;
        textRt.localScale = Vector3.one;
        textRt.localRotation = Quaternion.identity;

        // Dynamic padding depending on portrait visibility
        if (hasPortrait)
        {
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(195f, 25f);
            textRt.offsetMax = new Vector2(-45f, -35f);
        }
        else
        {
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(45f, 25f);
            textRt.offsetMax = new Vector2(-45f, -35f);
        }

        dialogueText.fontSize = 32;
        dialogueText.fontStyle = FontStyles.Normal;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.color = currentTextColor;
        dialogueText.enableWordWrapping = true;
        
        dialogueText.characterSpacing = 0f;
        dialogueText.characterHorizontalScale = 1.0f;
        dialogueText.lineSpacing = 0f;
        dialogueText.paragraphSpacing = 0f;
        dialogueText.margin = Vector4.zero;

        // 6. Setup Advance Indicator
        Transform indicatorTrans = boxGo.transform.Find("AdvanceIndicator");
        GameObject indicatorGo;
        if (indicatorTrans == null)
        {
            indicatorGo = new GameObject("AdvanceIndicator");
            indicatorGo.transform.SetParent(boxGo.transform, false);
        }
        else
        {
            indicatorGo = indicatorTrans.gameObject;
        }

        RectTransform indRt = indicatorGo.GetComponent<RectTransform>();
        if (indRt == null) indRt = indicatorGo.AddComponent<RectTransform>();
        indRt.anchorMin = new Vector2(1f, 0f);
        indRt.anchorMax = new Vector2(1f, 0f);
        indRt.pivot = new Vector2(1f, 0f);
        indRt.anchoredPosition = new Vector2(-25f, 20f);
        indRt.sizeDelta = new Vector2(30f, 30f);
        indRt.localScale = Vector3.one;

        advanceIndicator = indicatorGo.GetComponent<TMP_Text>();
        if (advanceIndicator == null) advanceIndicator = indicatorGo.AddComponent<TextMeshProUGUI>();

        advanceIndicator.text = "▼";
        advanceIndicator.fontSize = 28;
        advanceIndicator.fontStyle = FontStyles.Bold;
        advanceIndicator.color = new Color(0.74f, 0.52f, 1.00f, 1f); // Lilac purple indicator
        advanceIndicator.alignment = TextAlignmentOptions.Center;
        advanceIndicator.characterSpacing = 0f;
        advanceIndicator.characterHorizontalScale = 1.0f;
        advanceIndicator.margin = Vector4.zero;

        // 7. Deactivate old legacy background images that are misplaced/stretched
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != boxGo.transform && (child.name == "Image" || child.name == "Image (1)"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Set up audio source for typewriter beeps
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        ApplyStyling();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    void Update()
    {
        if (!IsDialogueActive) return;
        if (Time.frameCount == openedFrameCount) return;

        // Micro-animation: Pulse advance indicator when waiting for input
        if (advanceIndicator != null && advanceIndicator.gameObject.activeSelf && !isTyping)
        {
            float pulse = 1.0f + Mathf.PingPong(Time.time * 2.5f, 0.2f);
            advanceIndicator.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        // Advance dialogue on Left Click, Enter, or E key press
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
            {
                StopTypingAndShowFullLine();
            }
            else
            {
                AdvanceDialogue();
            }
        }

        // Escape closes dialogue
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseDialogue();
        }
    }

    public void ShowDialogue(string npcName, string[] lines, Action onCompleteCallback = null)
    {
        ShowDialogueInternal(
            npcName,
            lines,
            new Color(0.74f, 0.52f, 1.00f, 1f),  // Lilac purple name colour
            new Color(0.91f, 0.88f, 0.96f, 1f),  // Near-white body text
            0.02f,
            null,
            null,
            1.0f,
            onCompleteCallback
        );
    }

    public void ShowDialogue(CharacterDialogueAsset asset, Action onCompleteCallback = null)
    {
        if (asset == null) return;
        ShowDialogueInternal(
            asset.characterName,
            asset.dialogueLines,
            asset.nameColor,
            asset.textColor,
            asset.typingSpeed,
            asset.portrait,
            asset.textBeepSound,
            asset.voicePitch,
            onCompleteCallback
        );
    }

    private void ShowDialogueInternal(
        string npcName, 
        string[] lines, 
        Color nameCol, 
        Color textCol, 
        float speed, 
        Sprite portrait, 
        AudioClip beepSound, 
        float pitch, 
        Action onCompleteCallback)
    {
        if (lines == null || lines.Length == 0) return;

        bool isCompanion = !string.IsNullOrEmpty(npcName) && (npcName.IndexOf("Nyxaris", StringComparison.OrdinalIgnoreCase) >= 0 || npcName.IndexOf("Lumi", StringComparison.OrdinalIgnoreCase) >= 0);
        string[] processedLines = new string[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            processedLines[i] = SpawnOfChaos.Systems.TelepathyOrbSystem.ProcessDialogueLine(lines[i], isCompanion);
        }
        dialogueLines = processedLines;
        currentLineIndex = 0;
        onComplete = onCompleteCallback;
        openedFrameCount = Time.frameCount;

        // Play Mage talk voice reaction
        move playerMove = FindFirstObjectByType<move>();
        if (playerMove != null)
        {
            playerMove.PlayRandomTalkVoice();
        }

        currentNameColor = nameCol;
        currentTextColor = textCol;
        typeSpeed = speed;
        currentBeepSound = beepSound;
        currentVoicePitch = pitch;

        // Apply portrait configuration
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            bool hasPortrait = portrait != null;
            portraitImage.gameObject.SetActive(hasPortrait);
            if (portraitContainer != null)
            {
                portraitContainer.SetActive(hasPortrait);
            }
        }

        // Reapply styling to update layouts with or without portrait layout offsets
        ApplyStyling();

        if (nameText != null)
        {
            nameText.text = npcName;
            nameText.color = currentNameColor;
        }

        if (dialogueText != null)
        {
            dialogueText.color = currentTextColor;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
            HUDManager.Instance?.UpdateVisibility();
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
        }

        // Verify the GameObject is active in the hierarchy
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError($"NPCDialogueUI: Cannot start dialogue because '{gameObject.name}' or one of its parent GameObjects is inactive in the hierarchy!");
            return;
        }

        StartTypingLine(dialogueLines[currentLineIndex]);
    }

    public void CloseDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            HUDManager.Instance?.UpdateVisibility();
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
        }

        isTyping = false;

        // Fire callback
        onComplete?.Invoke();
        onComplete = null;
    }

    private void StartTypingLine(string line)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        typingCoroutine = StartCoroutine(TypeLineRoutine(line));
    }

    private IEnumerator TypeLineRoutine(string line)
    {
        isTyping = true;
        if (dialogueText != null) dialogueText.text = "";
        
        // Hide advance indicator during typing
        if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(false);

        foreach (char c in line)
        {
            if (dialogueText != null) dialogueText.text += c;
            
            // Play writing beep sound for non-whitespace characters
            if (currentBeepSound != null && audioSource != null && !char.IsWhiteSpace(c))
            {
                audioSource.pitch = currentVoicePitch;
                audioSource.PlayOneShot(currentBeepSound);
            }

            yield return new WaitForSeconds(typeSpeed);
        }
        
        if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(true);
        isTyping = false;
    }

    private void StopTypingAndShowFullLine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        if (dialogueText != null) dialogueText.text = dialogueLines[currentLineIndex];
        if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(true);
        isTyping = false;
    }

    private void AdvanceDialogue()
    {
        currentLineIndex++;
        if (currentLineIndex < dialogueLines.Length)
        {
            StartTypingLine(dialogueLines[currentLineIndex]);
        }
        else
        {
            CloseDialogue();
        }
    }
}
