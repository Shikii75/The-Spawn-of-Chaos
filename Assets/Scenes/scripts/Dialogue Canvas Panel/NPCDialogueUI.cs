using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NPCDialogueUI : MonoBehaviour
{
    public static NPCDialogueUI Instance { get; private set; }

    [Header("UI References (Drag & Drop)")]
    [Tooltip("The speech bubble or dialogue panel GameObject container.")]
    public GameObject dialoguePanel;
    
    [Tooltip("TextMeshPro component for the NPC's name.")]
    public TMP_Text nameText;
    
    [Tooltip("TextMeshPro component for the dialogue body text.")]
    public TMP_Text dialogueText;
    
    [Tooltip("Optional: TextMeshPro component indicating dialogue can be advanced (e.g. '▶').")]
    public TMP_Text advanceIndicator;

    [Header("Settings")]
    [Tooltip("Typewriter character text speed interval in seconds.")]
    public float typeSpeed = 0.02f;

    // ── Dialogue state ──
    private string[] dialogueLines;
    private int currentLineIndex;
    private bool isTyping;
    private Coroutine typingCoroutine;
    private Action onComplete;

    private int openedFrameCount;

    public bool IsDialogueActive => dialoguePanel != null && dialoguePanel.activeSelf;

    private void AutoResolveReferences()
    {
        dialoguePanel = this.gameObject;
        nameText = null;
        dialogueText = null;
        advanceIndicator = null;

        var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
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

        // Fallback for texts if still null (auto-split by screen layout position)
        if (nameText == null || dialogueText == null)
        {
            var list = new System.Collections.Generic.List<TextMeshProUGUI>();
            foreach (var txt in allTexts)
            {
                if (txt != advanceIndicator) list.Add(txt);
            }

            if (list.Count >= 2)
            {
                list.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
                nameText = list[0];
                dialogueText = list[1];
            }
            else if (list.Count == 1)
            {
                dialogueText = list[0];
            }
        }
    }

    [ContextMenu("Apply Dialogue UI Styling")]
    public void ApplyStyling()
    {
        AutoResolveReferences();

        Canvas nearestCanvas = GetComponentInParent<Canvas>();
        Canvas rootCanvas = nearestCanvas != null ? nearestCanvas.rootCanvas : null;
        if (rootCanvas != null)
        {
            CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
            }
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

            CanvasScaler nestedScaler = nearestCanvas.GetComponent<CanvasScaler>();
            if (nestedScaler != null)
            {
                if (Application.isPlaying) Destroy(nestedScaler);
                else DestroyImmediate(nestedScaler);
            }
        }

        if (dialoguePanel != null)
        {
            RectTransform prt = dialoguePanel.GetComponent<RectTransform>();
            if (prt != null) prt.localScale = Vector3.one;
        }
        if (nameText != null) nameText.rectTransform.localScale = Vector3.one;
        if (dialogueText != null) dialogueText.rectTransform.localScale = Vector3.one;
        if (advanceIndicator != null) advanceIndicator.rectTransform.localScale = Vector3.one;
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

        // Advance dialogue on Left Click, Enter, or E key press
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
            {
                // Speed up and finish typing current line instantly
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
        if (lines == null || lines.Length == 0) return;

        dialogueLines = lines;
        currentLineIndex = 0;
        onComplete = onCompleteCallback;
        openedFrameCount = Time.frameCount;

        // Apply scaling and reference resolution checks on show
        ApplyStyling();

        if (nameText != null)
        {
            nameText.text = npcName;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
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
