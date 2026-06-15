using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NPCDialogueUI : MonoBehaviour
{
    public static NPCDialogueUI Instance { get; private set; }

    [Header("Settings")]
    public float typeSpeed = 0.02f;

    // ── Runtime-built UI references ──
    private Canvas canvas;
    private GameObject dialoguePanel;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI advanceIndicator;

    // ── Dialogue state ──
    private string[] dialogueLines;
    private int currentLineIndex;
    private bool isTyping;
    private Coroutine typingCoroutine;
    private Action onComplete;

    public bool IsDialogueActive => dialoguePanel != null && dialoguePanel.activeSelf;

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

        BuildUI();
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION — entire hierarchy built from code
    // ══════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── Canvas (sort order 5) ──
        canvas = UIFactory.CreateCanvas("NPCDialogueCanvas", 5);
        canvas.transform.SetParent(transform, false);

        // ── Bottom-of-screen dialogue bar ──
        // Full width, 180px tall, anchored to bottom
        RectTransform panelRT = UIFactory.CreatePanel(
            canvas.transform, "DialoguePanel", UIFactory.PanelBackground,
            new Vector2(0f, 0f), new Vector2(1f, 0f),          // anchor bottom-stretch
            new Vector2(0f, 0f), new Vector2(0f, 180f)          // 180px tall from bottom
        );
        dialoguePanel = panelRT.gameObject;

        // ── NPC Name text — top-left of panel, accent color, 22pt ──
        nameText = UIFactory.CreateText(
            panelRT, "NameText", "",
            22f, UIFactory.Accent, TextAlignmentOptions.TopLeft
        );
        UIFactory.SetRect(nameText.rectTransform,
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(20f, -40f), new Vector2(0f, -10f)
        );
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableWordWrapping = false;

        // ── Dialogue body text — main area, white, 18pt ──
        dialogueText = UIFactory.CreateText(
            panelRT, "DialogueText", "",
            18f, UIFactory.TextWhite, TextAlignmentOptions.TopLeft
        );
        UIFactory.SetRect(dialogueText.rectTransform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(20f, 15f), new Vector2(-20f, -45f)
        );

        // ── Advance indicator '▶' — bottom-right, muted, 16pt ──
        advanceIndicator = UIFactory.CreateText(
            panelRT, "AdvanceIndicator", "▶",
            16f, UIFactory.TextMuted, TextAlignmentOptions.BottomRight
        );
        UIFactory.SetRect(advanceIndicator.rectTransform,
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-60f, 8f), new Vector2(-15f, 30f)
        );

        // Start hidden
        dialoguePanel.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════
    //  INPUT HANDLING
    // ══════════════════════════════════════════════════════════════════

    void Update()
    {
        if (!IsDialogueActive) return;

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

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════════

    public void ShowDialogue(string npcName, string[] lines, Action onCompleteCallback = null)
    {
        if (lines == null || lines.Length == 0) return;

        dialogueLines = lines;
        currentLineIndex = 0;
        onComplete = onCompleteCallback;

        if (nameText != null)
        {
            nameText.text = npcName;
        }

        // Ensure this Manager GameObject itself is active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        // Verify the GameObject is active in the hierarchy (not blocked by an inactive parent)
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError($"NPCDialogueUI: Cannot start dialogue because '{gameObject.name}' or one of its parent GameObjects is inactive in the hierarchy! Please make sure they are active.");
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

    // ══════════════════════════════════════════════════════════════════
    //  TYPEWRITER EFFECT
    // ══════════════════════════════════════════════════════════════════

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
        dialogueText.text = "";
        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }
        isTyping = false;
    }

    private void StopTypingAndShowFullLine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        dialogueText.text = dialogueLines[currentLineIndex];
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
