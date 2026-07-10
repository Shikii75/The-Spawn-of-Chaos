using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Applies the Nyxaris chat panel layout to match the web-app design:
///
///  ┌──────────────────────────────────────────────┐
///  │  [Portrait – left aligned, tall, no tint]    │
///  │   fills the top portion of the screen        │
///  ├──────────────────────────────────────────────┤  ← purple top border
///  │ NYXARIS  (name, purple bold)                 │
///  │ dialogue text …                              │
///  │ ┌──────────────────────────────┐ [  send  ]  │
///  │ │ Talk to Nyxaris…             │             │
///  │ └──────────────────────────────┘             │
///  └──────────────────────────────────────────────┘
///
/// Hierarchy expected:
///   Canvas  (root – Screen Space Overlay)
///     MainInterface  (nested Canvas + this script)
///       UIspace  (RectTransform, full-screen)
///         Portrait       ← Image, left side
///         LowerPanel     ← Image, bottom bar
///           NameText / Text (TMP)
///           DialogueText
///           MessageInput
///           SendButton
///             Text (TMP)
///           TopBorderLine  (auto-created)
/// </summary>
[ExecuteAlways]
public class NyxarisUIStyler : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Image on LowerPanel")]
    public Image dialoguePanelImage;
    [Tooltip("Image on Portrait")]
    public Image portraitImage;
    [Tooltip("Character name text (inside LowerPanel)")]
    public TMP_Text nameText;
    [Tooltip("Phase / status text (inside LowerPanel)")]
    public TMP_Text phaseText;
    [Tooltip("Main dialogue response text (inside LowerPanel)")]
    public TMP_Text dialogueText;
    [Tooltip("Image on MessageInput")]
    public Image inputFieldImage;
    [Tooltip("Image on SendButton")]
    public Image sendButtonImage;
    [Tooltip("TMP Text inside SendButton")]
    public TMP_Text sendButtonText;

    [Header("Panel Configuration")]
    [Tooltip("Height of the bottom dialogue panel in pixels")]
    public float panelHeight = 260f;

    [Header("Portrait – RIGHT side")]
    [Tooltip("Portrait width in pixels (reference 1920×1080)")]
    public float portraitWidth  = 520f;
    [Tooltip("Portrait height in pixels")]
    public float portraitHeight = 820f;
    [Tooltip("X offset from the LEFT screen edge (positive = inward)")]
    public float portraitOffsetX = 0f;
    [Tooltip("Y offset from the BOTTOM screen edge (positive = upward)")]
    public float portraitOffsetY = 0f;

    [Header("Dialogue Text Padding")]
    [Tooltip("Left indent of dialogue text (should clear the portrait)")]
    public float dialogueLeftPad   = 30f;
    [Tooltip("Right indent of dialogue text (negative shrinks)")]
    public float dialogueRightPad  = -20f;
    [Tooltip("Top indent from panel top (negative = downward)")]
    public float dialogueTopPad    = -52f;
    [Tooltip("Bottom indent leaving room for the input row")]
    public float dialogueBottomPad = 60f;

    [Header("Name Text")]
    public float nameLeftOffset = 26f;
    public float nameTopOffset  = -14f;
    public float nameWidth      = 300f;
    public float nameHeight     = 42f;

    [Header("Input Row")]
    public float inputHeight      = 48f;
    public float buttonHeight     = 48f;
    public float buttonWidth      = 110f;
    public float inputBottomMargin = 14f;
    public float inputSideMargin   = 18f;

    // ──────────────────────────────────────────────────────────────────────────

    private void AutoResolveReferences()
    {
        // Locate UIspace
        Transform uiSpace = null;
        if (transform.name == "UIspace")
            uiSpace = transform;
        else if (transform.name == "MainInterface" || GetComponent<Canvas>() != null)
            uiSpace = transform.Find("UIspace");
        else if (transform.parent != null && transform.parent.name == "UIspace")
            uiSpace = transform.parent;

        if (uiSpace == null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                uiSpace = c.transform.Find("UIspace") ?? c.transform;
            }
        }

        if (uiSpace == null) return;

        // ── LowerPanel ──
        Transform lp = uiSpace.Find("LowerPanel");
        if (lp == null && transform.name == "LowerPanel") lp = transform;

        if (lp != null)
        {
            if (dialoguePanelImage == null)
                dialoguePanelImage = lp.GetComponent<Image>();

            foreach (var txt in lp.GetComponentsInChildren<TMP_Text>(true))
            {
                if (txt.transform.parent != null &&
                    txt.transform.parent.name.IndexOf("Button", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string n = txt.gameObject.name.ToLower();
                if      ((n.Contains("dialogue") || n.Contains("body") || n == "text") && dialogueText == null)
                    dialogueText = txt;
                else if ((n.Contains("name") || n.Contains("title") || n.Contains("character")) && nameText == null)
                    nameText = txt;
                else if ((n.Contains("phase") || n.Contains("evolution") || n.Contains("state")) && phaseText == null)
                    phaseText = txt;
                else if (nameText == null && (n.Contains("tmp") || n == "text (tmp)"))
                    nameText = txt;
            }

            if (inputFieldImage == null)
            {
                Transform t = lp.Find("MessageInput") ?? lp.Find("InputField") ?? lp.Find("Input");
                if (t != null) inputFieldImage = t.GetComponent<Image>();
            }
            if (sendButtonImage == null)
            {
                Transform t = lp.Find("SendButton") ?? lp.Find("Send") ?? lp.Find("Button");
                if (t != null) sendButtonImage = t.GetComponent<Image>();
            }
            if (sendButtonImage != null && sendButtonText == null)
                sendButtonText = sendButtonImage.GetComponentInChildren<TMP_Text>();
        }

        // ── Portrait ──
        if (portraitImage == null)
        {
            Transform port = uiSpace.Find("Portrait");
            if (port != null) portraitImage = port.GetComponent<Image>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    [ContextMenu("Apply Nyxaris UI Styling")]
    public void ApplyStyling()
    {
        AutoResolveReferences();

        // ── Colour palette  (web-app inspired, purple theme) ─────────────────
        // Panel background: very dark navy/purple, semi-transparent
        Color panelBg       = new Color(0.06f, 0.04f, 0.12f, 0.93f);   // #0F0A1E ~93%
        // Accent: rich purple
        Color purpleAccent  = new Color(0.55f, 0.18f, 0.95f, 1f);      // #8C2DF2
        // Soft lilac for name
        Color nameColor     = new Color(0.74f, 0.52f, 1.00f, 1f);      // #BD84FF
        // Input background
        Color inputBg       = new Color(0.10f, 0.07f, 0.18f, 0.90f);   // #1A1230 ~90%
        // Send button: purple
        Color sendBtnBg     = new Color(0.48f, 0.14f, 0.82f, 1f);      // #7A24D1
        // Text: near-white
        Color textColor     = new Color(0.91f, 0.88f, 0.96f, 1f);      // #E8E0F5
        // Phase text: muted lilac
        Color phaseColor    = new Color(0.65f, 0.55f, 0.85f, 1f);      // #A68CD9
        // Button text: white
        Color btnTextColor  = Color.white;

        // ── 0. Root Canvas scaler ─────────────────────────────────────────────
        Canvas nearestCanvas = GetComponentInParent<Canvas>();
        Canvas rootCanvas    = nearestCanvas != null ? nearestCanvas.rootCanvas : null;

        if (rootCanvas != null)
        {
            CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode   = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Nested canvas must stretch full-screen and have no extra scaler
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
            CanvasScaler ns = nearestCanvas.GetComponent<CanvasScaler>();
            if (ns != null)
            {
                if (Application.isPlaying) Destroy(ns);
                else                       DestroyImmediate(ns);
            }
        }

        // ── 1. UIspace – full-screen stretch ─────────────────────────────────
        if (dialoguePanelImage != null && dialoguePanelImage.transform.parent != null)
        {
            RectTransform uiSpaceRt = dialoguePanelImage.transform.parent as RectTransform;
            if (uiSpaceRt != null)
            {
                uiSpaceRt.anchorMin        = Vector2.zero;
                uiSpaceRt.anchorMax        = Vector2.one;
                uiSpaceRt.pivot            = new Vector2(0.5f, 0.5f);
                uiSpaceRt.sizeDelta        = Vector2.zero;
                uiSpaceRt.anchoredPosition = Vector2.zero;
                uiSpaceRt.localScale       = Vector3.one;

                // Dark semi-transparent full-screen backdrop (matches web app)
                Image bgImg = uiSpaceRt.GetComponent<Image>();
                if (bgImg == null) bgImg = uiSpaceRt.gameObject.AddComponent<Image>();
                bgImg.color = new Color(0.02f, 0.01f, 0.06f, 0.82f);   // deep dark purple ~82%
            }
        }

        // ── 2. LowerPanel – bottom bar, full width ────────────────────────────
        if (dialoguePanelImage != null)
        {
            dialoguePanelImage.color = panelBg;

            RectTransform pr = dialoguePanelImage.rectTransform;
            pr.anchorMin        = new Vector2(0f, 0f);
            pr.anchorMax        = new Vector2(1f, 0f);
            pr.pivot            = new Vector2(0.5f, 0f);
            pr.sizeDelta        = new Vector2(0f, panelHeight);
            pr.anchoredPosition = Vector2.zero;
            pr.localScale       = Vector3.one;

            // Purple top border line
            Transform lineT = dialoguePanelImage.transform.Find("TopBorderLine");
            if (lineT == null)
            {
                var lineGO = new GameObject("TopBorderLine", typeof(Image));
                lineGO.transform.SetParent(dialoguePanelImage.transform, false);
                lineT = lineGO.transform;
            }
            Image lineImg = lineT.GetComponent<Image>();
            lineImg.color = purpleAccent;
            RectTransform lr = lineImg.rectTransform;
            lr.anchorMin        = new Vector2(0f, 1f);
            lr.anchorMax        = new Vector2(1f, 1f);
            lr.pivot            = new Vector2(0.5f, 1f);
            lr.sizeDelta        = new Vector2(0f, 3f);
            lr.anchoredPosition = Vector2.zero;
            lr.localScale       = Vector3.one;
        }

        // ── 3. Portrait – LEFT side, tall, bottom-anchored ───────────────────
        if (portraitImage != null)
        {
            portraitImage.preserveAspect = true;
            portraitImage.color          = Color.white;   // no tint

            RectTransform portRt = portraitImage.rectTransform;

            // Anchor bottom-left corner → portrait sits on left, above panel
            portRt.anchorMin        = new Vector2(0f, 0f);
            portRt.anchorMax        = new Vector2(0f, 0f);
            portRt.pivot            = new Vector2(0f, 0f);   // bottom-left pivot
            portRt.sizeDelta        = new Vector2(portraitWidth, portraitHeight);
            portRt.anchoredPosition = new Vector2(portraitOffsetX, portraitOffsetY);
            portRt.localScale       = Vector3.one;

            // Ensure portrait renders BEHIND LowerPanel (lower sibling index)
            if (dialoguePanelImage != null)
            {
                int panelIdx = dialoguePanelImage.transform.GetSiblingIndex();
                if (portRt.GetSiblingIndex() >= panelIdx)
                    portRt.SetSiblingIndex(Mathf.Max(0, panelIdx - 1));
            }
        }

        // ── 4. Dialogue text ──────────────────────────────────────────────────
        if (dialogueText != null)
        {
            dialogueText.color          = textColor;
            dialogueText.fontSize       = 28f;
            dialogueText.enableWordWrapping = true;
            dialogueText.characterSpacing       = 0f;
            dialogueText.characterHorizontalScale = 1f;

            RectTransform dr = dialogueText.rectTransform;
            dr.anchorMin        = Vector2.zero;
            dr.anchorMax        = Vector2.one;
            dr.pivot            = new Vector2(0.5f, 0.5f);
            dr.offsetMin        = new Vector2(dialogueLeftPad,  dialogueBottomPad);
            dr.offsetMax        = new Vector2(dialogueRightPad, dialogueTopPad);
            dr.localScale       = Vector3.one;
        }

        // ── 5. Name text ──────────────────────────────────────────────────────
        if (nameText != null)
        {
            nameText.color     = nameColor;
            nameText.fontStyle = FontStyles.Bold;
            nameText.fontSize  = 32f;
            nameText.characterSpacing       = 0f;
            nameText.characterHorizontalScale = 1f;

            RectTransform nr = nameText.rectTransform;
            nr.anchorMin        = new Vector2(0f, 1f);
            nr.anchorMax        = new Vector2(0f, 1f);
            nr.pivot            = new Vector2(0f, 1f);
            nr.sizeDelta        = new Vector2(nameWidth, nameHeight);
            nr.anchoredPosition = new Vector2(nameLeftOffset, nameTopOffset);
            nr.localScale       = Vector3.one;
        }

        // ── 6. Phase text ─────────────────────────────────────────────────────
        if (phaseText != null)
        {
            phaseText.color     = phaseColor;
            phaseText.fontStyle = FontStyles.Italic;
            phaseText.fontSize  = 20f;
            phaseText.characterSpacing       = 0f;
            phaseText.characterHorizontalScale = 1f;

            RectTransform phr = phaseText.rectTransform;
            phr.anchorMin        = new Vector2(1f, 1f);
            phr.anchorMax        = new Vector2(1f, 1f);
            phr.pivot            = new Vector2(1f, 1f);
            phr.sizeDelta        = new Vector2(220f, 28f);
            phr.anchoredPosition = new Vector2(-20f, nameTopOffset);
            phr.localScale       = Vector3.one;
        }

        // ── 7. Input field ────────────────────────────────────────────────────
        if (inputFieldImage != null)
        {
            inputFieldImage.color = inputBg;

            RectTransform ir = inputFieldImage.rectTransform;
            ir.anchorMin        = new Vector2(0f, 0f);
            ir.anchorMax        = new Vector2(1f, 0f);
            ir.pivot            = new Vector2(0f, 0f);
            ir.sizeDelta        = new Vector2(-(buttonWidth + inputSideMargin * 3f), inputHeight);
            ir.anchoredPosition = new Vector2(inputSideMargin, inputBottomMargin);
            ir.localScale       = Vector3.one;
        }

        // ── 8. Send button ────────────────────────────────────────────────────
        if (sendButtonImage != null)
        {
            sendButtonImage.color = sendBtnBg;

            RectTransform br = sendButtonImage.rectTransform;
            br.anchorMin        = new Vector2(1f, 0f);
            br.anchorMax        = new Vector2(1f, 0f);
            br.pivot            = new Vector2(1f, 0f);
            br.sizeDelta        = new Vector2(buttonWidth, buttonHeight);
            br.anchoredPosition = new Vector2(-inputSideMargin, inputBottomMargin);
            br.localScale       = Vector3.one;
        }

        if (sendButtonText != null)
        {
            sendButtonText.color     = btnTextColor;
            sendButtonText.fontStyle = FontStyles.Bold;
            sendButtonText.fontSize  = 22f;
        }

        Debug.Log("[NyxarisUIStyler] Layout applied successfully.");
    }

    private void Start()
    {
        ApplyStyling();
    }

    private Color HexToColor(string hex)
    {
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length == 6)
        {
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }
        return Color.white;
    }
}
