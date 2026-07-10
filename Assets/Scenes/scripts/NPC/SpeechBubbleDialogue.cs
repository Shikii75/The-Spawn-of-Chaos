using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║          SPEECH BUBBLE DIALOGUE  —  Drag-and-Drop System        ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  HOW TO USE                                                      ║
/// ║  ───────────────────────────────────────────────────────────     ║
/// ║  1. Attach this script to any NPC GameObject.                    ║
/// ║  2. Expand "Dialogue Lines" in the Inspector.                    ║
/// ║     Write each line of dialogue — one entry per bubble page.     ║
/// ║  3. (Optional) Set a Character Name, colours, size, etc.        ║
/// ║  4. Press Play. Walk near the NPC. Press E. Done.                ║
/// ║                                                                  ║
/// ║  REQUIRES NOTHING ELSE                                           ║
/// ║  ───────────────────────────────────────────────────────────     ║
/// ║  • Builds its own Canvas, bubble, text and button via code.      ║
/// ║  • Bubble is World-Space and parented to the NPC, so it         ║
/// ║    follows automatically with zero extra scripting.              ║
/// ║  • Player is located by the "Player" tag automatically.          ║
/// ║  • Purple interaction gizmo shown in Scene view.                 ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// </summary>
[AddComponentMenu("Dialogue/Speech Bubble Dialogue")]
public class SpeechBubbleDialogue : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════
    //  INSPECTOR — fill these in; everything else is automatic
    // ═══════════════════════════════════════════════════════════════════

    [Header("── Dialogue Lines ─────────────────────────────────────────")]
    [Tooltip("Each entry = one page of the speech bubble.")]
    [TextArea(2, 8)]
    public string[] dialogueLines = { "Hello, stranger. What brings you here?" };

    [Header("── Character (optional) ────────────────────────────────────")]
    [Tooltip("Name shown at the top of the bubble. Leave blank to hide.")]
    public string characterName = "";

    [Tooltip("Colour for the name label.")]
    public Color nameColor = new Color(0.74f, 0.52f, 1f, 1f);

    [Header("── Interaction ──────────────────────────────────────────────")]
    [Tooltip("Distance the player must be within before the prompt appears.")]
    public float interactRange = 3f;

    [Tooltip("Key that starts dialogue or advances to the next line.")]
    public KeyCode interactKey = KeyCode.E;

    [Header("── Typewriter ───────────────────────────────────────────────")]
    [Tooltip("Seconds between each letter. Lower = faster.")]
    [Range(0.01f, 0.1f)]
    public float typeSpeed = 0.03f;

    [Header("── Bubble Appearance ─────────────────────────────────────────")]
    [Tooltip("Background colour of the speech bubble.")]
    public Color bubbleColor = new Color(0.11f, 0.12f, 0.16f, 0.97f);

    [Tooltip("Main dialogue body text colour.")]
    public Color textColor = Color.white;

    [Tooltip("Colour of the circular advance button.")]
    public Color buttonColor = new Color(0.24f, 0.40f, 0.20f, 1f);

    [Tooltip("Width of the bubble in pixels (reference resolution 1920×1080).")]
    public float bubbleWidth = 680f;

    [Tooltip("Height of the bubble in pixels.")]
    public float bubbleHeight = 240f;

    [Tooltip("How far above the NPC's pivot the bubble hovers (world units).")]
    public float heightAboveNPC = 2.8f;

    [Tooltip("Body text font size (pixels).")]
    public float fontSize = 28f;

    // ═══════════════════════════════════════════════════════════════════
    //  PRIVATE RUNTIME STATE — never touch these manually
    // ═══════════════════════════════════════════════════════════════════

    private Canvas        _canvas;
    private RectTransform _bubbleRt;
    private TMP_Text      _bodyText;
    private GameObject    _advanceBtn;
    private GameObject    _promptGo;

    private bool      _playerNear;
    private bool      _isOpen;
    private bool      _typing;
    private int       _lineIndex;
    private int       _openedFrame;
    private Coroutine _typeRoutine;
    private Transform _playerTf;

    // ═══════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        // Auto-locate the player by tag — no manual assignment needed
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null) _playerTf = playerGo.transform;

        // Assign world-space camera now that the scene is loaded
        if (_canvas != null && Camera.main != null)
            _canvas.worldCamera = Camera.main;

        SetBubbleActive(false);
        SetPromptActive(false);
    }

    private void Update()
    {
        UpdateProximity();
        HandleInput();
        PulseAdvanceButton();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PROXIMITY CHECK
    // ═══════════════════════════════════════════════════════════════════

    private void UpdateProximity()
    {
        if (_playerTf == null) return;

        float dist = Vector2.Distance(transform.position, _playerTf.position);
        bool near = dist <= interactRange;
        if (near == _playerNear) return;

        _playerNear = near;

        if (!near && _isOpen)
            CloseDialogue();

        if (!_isOpen)
            SetPromptActive(near);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  INPUT
    // ═══════════════════════════════════════════════════════════════════

    private void HandleInput()
    {
        // Open
        if (_playerNear && !_isOpen && Input.GetKeyDown(interactKey))
        {
            OpenDialogue();
            return;
        }

        if (!_isOpen) return;
        if (Time.frameCount == _openedFrame) return;   // ignore the frame we opened on

        // Advance / skip
        if (Input.GetKeyDown(interactKey) || Input.GetMouseButtonDown(0))
        {
            if (_typing) SkipTyping();
            else         AdvanceLine();
        }

        // Close
        if (Input.GetKeyDown(KeyCode.Escape))
            CloseDialogue();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DIALOGUE FLOW
    // ═══════════════════════════════════════════════════════════════════

    private void OpenDialogue()
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;

        _lineIndex   = 0;
        _openedFrame = Time.frameCount;
        _isOpen      = true;

        SetPromptActive(false);
        SetBubbleActive(true);
        TypeLine(dialogueLines[_lineIndex]);
    }

    private void AdvanceLine()
    {
        _lineIndex++;
        if (_lineIndex < dialogueLines.Length)
            TypeLine(dialogueLines[_lineIndex]);
        else
            CloseDialogue();
    }

    private void CloseDialogue()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _isOpen = false;
        _typing = false;
        SetBubbleActive(false);
        if (_playerNear) SetPromptActive(true);
    }

    private void TypeLine(string line)
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeRoutine(line));
    }

    private IEnumerator TypeRoutine(string line)
    {
        _typing = true;
        _bodyText.text = "";
        SetAdvanceActive(false);

        foreach (char c in line)
        {
            _bodyText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        _typing = false;
        SetAdvanceActive(true);
    }

    private void SkipTyping()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _bodyText.text = dialogueLines[_lineIndex];
        _typing = false;
        SetAdvanceActive(true);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BUTTON ANIMATION
    // ═══════════════════════════════════════════════════════════════════

    private void PulseAdvanceButton()
    {
        if (_advanceBtn == null || !_advanceBtn.activeSelf) return;
        float s = 1f + Mathf.Sin(Time.time * 5.5f) * 0.07f;
        _advanceBtn.transform.localScale = new Vector3(s, s, 1f);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VISIBILITY HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private void SetBubbleActive(bool on)
    {
        if (_bubbleRt != null) _bubbleRt.gameObject.SetActive(on);
        if (!on) SetAdvanceActive(false);
    }

    private void SetAdvanceActive(bool on)
    {
        if (_advanceBtn != null) _advanceBtn.SetActive(on);
    }

    private void SetPromptActive(bool on)
    {
        if (_promptGo != null) _promptGo.SetActive(on);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION — everything built here from zero
    // ═══════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── World-Space Canvas ────────────────────────────────────────────────
        //    Parented to this NPC → follows it for free.
        var canvasGo = new GameObject("[SpeechBubble]");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.WorldSpace;
        _canvas.sortingOrder = 50;           // always on top of 2-D sprites

        canvasGo.AddComponent<GraphicRaycaster>();

        // 100 px = 1 Unity world unit; canvas size in pixels
        const float PPU = 100f;
        var canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta    = new Vector2(900f, 600f);
        canvasRt.localScale   = Vector3.one / PPU;
        canvasRt.localPosition = new Vector3(0f, heightAboveNPC, -0.1f);

        // ── Bubble panel ──────────────────────────────────────────────────────
        var panelGo = new GameObject("BubblePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);

        _bubbleRt = panelGo.AddComponent<RectTransform>();
        _bubbleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        _bubbleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        _bubbleRt.pivot            = new Vector2(0.5f, 0.5f);
        _bubbleRt.sizeDelta        = new Vector2(bubbleWidth, bubbleHeight);
        _bubbleRt.anchoredPosition = Vector2.zero;

        var panelImg   = panelGo.AddComponent<Image>();
        panelImg.color = bubbleColor;
        panelImg.sprite = GetRoundedSprite();
        panelImg.type   = Image.Type.Sliced;

        // ── Bubble tail (rotated square – bottom-left area) ───────────────────
        //    Overlapping the panel edge creates the classic speech-bubble pointer.
        var tailGo = MakeChild("Tail", panelGo.transform);
        var tailRt = tailGo.AddComponent<RectTransform>();
        tailRt.anchorMin        = new Vector2(0.18f, 0f);
        tailRt.anchorMax        = new Vector2(0.18f, 0f);
        tailRt.pivot            = new Vector2(0.5f, 0.5f);
        tailRt.sizeDelta        = new Vector2(36f, 36f);
        tailRt.anchoredPosition = new Vector2(0f, -15f);       // half-overlap below panel
        tailRt.localRotation    = Quaternion.Euler(0f, 0f, 45f);
        tailGo.AddComponent<Image>().color = bubbleColor;

        // ── Character name label (top-left inside bubble) ─────────────────────
        float bodyTopOffset = -22f;  // how much body text is pushed from top
        if (!string.IsNullOrEmpty(characterName))
        {
            var nameLGo = MakeChild("NameLabel", panelGo.transform);
            var nrt = nameLGo.AddComponent<RectTransform>();
            nrt.anchorMin        = new Vector2(0f, 1f);
            nrt.anchorMax        = new Vector2(1f, 1f);
            nrt.pivot            = new Vector2(0f, 1f);
            nrt.sizeDelta        = new Vector2(0f, 38f);
            nrt.anchoredPosition = new Vector2(28f, -10f);

            var nlTxt = nameLGo.AddComponent<TextMeshProUGUI>();
            ApplyTMPDefaults(nlTxt);
            nlTxt.text      = characterName;
            nlTxt.color     = nameColor;
            nlTxt.fontSize  = 22f;
            nlTxt.fontStyle = FontStyles.Bold;
            nlTxt.enableWordWrapping = false;

            bodyTopOffset = -54f;   // body text clears the name row
        }

        // ── Body text ─────────────────────────────────────────────────────────
        var bodyGo = MakeChild("BodyText", panelGo.transform);
        var brt = bodyGo.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(28f, 32f);          // left, bottom padding
        brt.offsetMax = new Vector2(-76f, bodyTopOffset); // right (clears button), top

        _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(_bodyText);
        _bodyText.text              = "";
        _bodyText.color             = textColor;
        _bodyText.fontSize          = fontSize;
        _bodyText.enableWordWrapping = true;
        _bodyText.overflowMode      = TextOverflowModes.Truncate;

        // ── Advance button (circle, bottom-right of panel) ────────────────────
        _advanceBtn = MakeChild("AdvanceBtn", panelGo.transform);
        var art = _advanceBtn.AddComponent<RectTransform>();
        art.anchorMin        = new Vector2(1f, 0f);
        art.anchorMax        = new Vector2(1f, 0f);
        art.pivot            = new Vector2(1f, 0f);
        art.sizeDelta        = new Vector2(56f, 56f);
        art.anchoredPosition = new Vector2(-14f, 14f);

        var btnImg   = _advanceBtn.AddComponent<Image>();
        btnImg.color = buttonColor;
        btnImg.sprite = MakeCircleSprite(64);

        // Arrow label inside button
        var arrowGo = MakeChild("Arrow", _advanceBtn.transform);
        var arrRt   = arrowGo.AddComponent<RectTransform>();
        arrRt.anchorMin        = Vector2.zero;
        arrRt.anchorMax        = Vector2.one;
        arrRt.sizeDelta        = Vector2.zero;
        arrRt.anchoredPosition = new Vector2(3f, 0f);   // optical centering

        var arrTxt = arrowGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(arrTxt);
        arrTxt.text      = "▶";
        arrTxt.color     = new Color(0.92f, 1f, 0.88f, 1f);
        arrTxt.fontSize  = 26f;
        arrTxt.alignment = TextAlignmentOptions.Center;

        _advanceBtn.SetActive(false);

        // ── "Press E to talk" prompt ──────────────────────────────────────────
        //    Appears below centre of the canvas when player is in range.
        _promptGo = MakeChild("Prompt", canvasGo.transform);
        var prt = _promptGo.AddComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.5f, 0f);
        prt.anchorMax        = new Vector2(0.5f, 0f);
        prt.pivot            = new Vector2(0.5f, 0.5f);
        prt.sizeDelta        = new Vector2(220f, 46f);
        prt.anchoredPosition = new Vector2(0f, 35f);   // sits just below the bubble canvas

        var promptImg   = _promptGo.AddComponent<Image>();
        promptImg.color = new Color(0.06f, 0.04f, 0.14f, 0.88f);
        promptImg.sprite = GetRoundedSprite();
        promptImg.type   = Image.Type.Sliced;

        var ptxtGo = MakeChild("PromptText", _promptGo.transform);
        var ptrt   = ptxtGo.AddComponent<RectTransform>();
        ptrt.anchorMin = Vector2.zero;
        ptrt.anchorMax = Vector2.one;
        ptrt.sizeDelta = Vector2.zero;
        ptrt.anchoredPosition = Vector2.zero;

        var ptxt = ptxtGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(ptxt);
        ptxt.text      = $"[{interactKey}]  Talk";
        ptxt.color     = new Color(0.74f, 0.52f, 1f, 1f);   // lilac purple
        ptxt.fontSize  = 20f;
        ptxt.alignment = TextAlignmentOptions.Center;
        ptxt.fontStyle = FontStyles.Bold;

        _promptGo.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SMALL UTILITIES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Resets TMP properties that Unity sometimes initialises badly.</summary>
    private static void ApplyTMPDefaults(TMP_Text t)
    {
        t.characterSpacing        = 0f;
        t.characterHorizontalScale = 1f;
        t.lineSpacing             = 0f;
        t.paragraphSpacing        = 0f;
        t.margin                  = Vector4.zero;
    }

    /// <summary>Creates a named, empty child GameObject.</summary>
    private static GameObject MakeChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Returns dynamic 9-slice rounded-rect sprite from UIFactory.</summary>
    private static Sprite GetRoundedSprite()
        => UIFactory.GetRoundedSprite();

    /// <summary>Generates a crisp circle sprite from pixels at runtime.</summary>
    private static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                  { filterMode = FilterMode.Bilinear };
        float c = size * 0.5f, r = c - 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - c, dy = y - c;
            tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EDITOR GIZMO
    // ═══════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Interaction range ring
        Gizmos.color = new Color(0.60f, 0.30f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactRange);

        // Solid fill so it's obvious in Scene view
        Gizmos.color = new Color(0.60f, 0.30f, 1f, 0.08f);
        Gizmos.DrawSphere(transform.position, interactRange);

        // Label
        UnityEditor.Handles.color = new Color(0.74f, 0.52f, 1f, 1f);
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (interactRange + 0.25f),
            $"  ◉ interact range  {interactRange:F1} u");
    }
#endif
}
