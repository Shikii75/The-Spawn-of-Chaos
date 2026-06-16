using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Applies the Game Dev OS visual theme and hard-coded layout to the Nyxaris Chat Panel.
/// Hierarchy expected:
///   Canvas (MainInterface)
///     UIspace (full-screen panel)
///       Portrait   ← sibling, rendered BEHIND LowerPanel
///       LowerPanel ← the dialogue box at the bottom
///         DialogueText
///         MessageInput
///         SendButton
///           Text (TMP)
///         TopBorderLine (auto-created)
/// </summary>
[ExecuteAlways]
public class NyxarisUIStyler : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The Image component on the LowerPanel GameObject")]
    public Image dialoguePanelImage;
    [Tooltip("The Image component on the Portrait GameObject")]
    public Image portraitImage;
    [Tooltip("The character name TMP Text (inside LowerPanel)")]
    public TMP_Text nameText;
    [Tooltip("The phase/evolution TMP Text (inside LowerPanel)")]
    public TMP_Text phaseText;
    [Tooltip("The dialogue response TMP Text (inside LowerPanel)")]
    public TMP_Text dialogueText;
    [Tooltip("The Image on the MessageInput GameObject")]
    public Image inputFieldImage;
    [Tooltip("The Image on the SendButton GameObject")]
    public Image sendButtonImage;
    [Tooltip("The TMP Text inside the SendButton")]
    public TMP_Text sendButtonText;

    [Header("Panel Configuration")]
    [Tooltip("Height of the dialogue panel at the bottom of the screen in pixels")]
    public float panelHeight = 220f;

    [Header("Portrait Configuration")]
    [Tooltip("Width of the portrait in pixels")]
    public float portraitWidth = 600f;
    [Tooltip("Height of the portrait in pixels")]
    public float portraitHeight = 880f;
    [Tooltip("How far from the right edge the portrait sits (negative = inward)")]
    public float portraitOffsetX = -20f;
    [Tooltip("How far up from the screen bottom the portrait sits")]
    public float portraitOffsetY = 0f;

    [Header("Dialogue Text Padding (inside LowerPanel)")]
    [Tooltip("Left padding of the dialogue text from the panel edge")]
    public float dialogueLeftPad = 30f;
    [Tooltip("Right padding (negative). Make large enough to not overlap portrait area)")]
    public float dialogueRightPad = -630f;
    [Tooltip("Top padding (negative) from the panel top edge")]
    public float dialogueTopPad = -48f;
    [Tooltip("Bottom padding from the panel bottom edge (leave room for input row)")]
    public float dialogueBottomPad = 58f;

    [Header("Name Text Position (inside LowerPanel)")]
    public float nameLeftOffset = 28f;
    public float nameTopOffset = -14f;

    [Header("Input Row (inside LowerPanel)")]
    [Tooltip("Height of the MessageInput field")]
    public float inputHeight = 36f;
    [Tooltip("Height of the Send button")]
    public float buttonHeight = 36f;
    [Tooltip("Width of the Send button")]
    public float buttonWidth = 80f;
    [Tooltip("Bottom margin for the input row inside the panel")]
    public float inputBottomMargin = 10f;
    [Tooltip("Side margin for the input row")]
    public float inputSideMargin = 14f;

    // -----------------------------------------------------------------------

    private void AutoResolveReferences()
    {
        // Try to find the root Canvas or UIspace
        Transform uiSpace = null;
        if (transform.name == "UIspace") uiSpace = transform;
        else if (transform.name == "MainInterface" || GetComponent<Canvas>() != null) uiSpace = transform.Find("UIspace");
        else if (transform.parent != null && transform.parent.name == "UIspace") uiSpace = transform.parent;

        if (uiSpace == null)
        {
            // Fallback: search up the hierarchy for UIspace or Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                uiSpace = canvas.transform.Find("UIspace");
                if (uiSpace == null) uiSpace = canvas.transform;
            }
        }

        if (uiSpace != null)
        {
            // Find LowerPanel
            Transform lp = uiSpace.Find("LowerPanel");
            if (lp == null && transform.name == "LowerPanel") lp = transform;
            if (lp != null)
            {
                if (dialoguePanelImage == null) dialoguePanelImage = lp.GetComponent<Image>();

                // Find dialogue panel children (using our ultra-smart text resolver)
                var allTexts = lp.GetComponentsInChildren<TMP_Text>(true);
                foreach (var txt in allTexts)
                {
                    // Skip button text
                    if (txt.transform.parent != null && txt.transform.parent.name.Contains("Button"))
                        continue;

                    string n = txt.gameObject.name.ToLower();
                    if (n.Contains("dialogue") || n.Contains("body") || n == "text")
                    {
                        if (dialogueText == null) dialogueText = txt;
                    }
                    else if (n.Contains("name") || n.Contains("title") || n.Contains("character"))
                    {
                        if (nameText == null) nameText = txt;
                    }
                    else if (n.Contains("phase") || n.Contains("evolution") || n.Contains("state"))
                    {
                        if (phaseText == null) phaseText = txt;
                    }
                    else
                    {
                        // Fallback assignment based on names/positions
                        if (nameText == null && (n.Contains("tmp") || n == "text (tmp)"))
                            nameText = txt;
                    }
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
                {
                    sendButtonText = sendButtonImage.GetComponentInChildren<TMP_Text>();
                }
            }

            // Find Portrait
            Transform port = uiSpace.Find("Portrait");
            if (port != null && portraitImage == null)
            {
                portraitImage = port.GetComponent<Image>();
            }
        }
    }

    [ContextMenu("Apply Game Dev OS Styling")]
    public void ApplyStyling()
    {
        // ── Auto-resolve references if null ──────────────────────────────────
        AutoResolveReferences();

        // ── Color palette (Game Dev OS) ──────────────────────────────────────
        Color panelBg      = HexToColor("1A1A2E");
        Color cyanAccent   = HexToColor("00D4FF");
        Color pinkAccent   = HexToColor("FFA6FF");
        Color inputBg      = HexToColor("252538");
        Color textColor    = HexToColor("EAF0FB");
        Color btnTextColor = HexToColor("081218");

        // ── 0. Configure CanvasScaler on the actual Root Canvas ───────────────
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

        // If nearestCanvas is a nested Canvas (like MainInterface under Canvas), it must stretch to fill the root Canvas and have scale 1x1x1
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
                rt.localScale       = Vector3.one; // Reset local scale to remove distortion
            }

            // Remove CanvasScaler from nested canvas to avoid Unity warnings and scale conflicts
            CanvasScaler nestedScaler = nearestCanvas.GetComponent<CanvasScaler>();
            if (nestedScaler != null)
            {
                if (Application.isPlaying)
                    Destroy(nestedScaler);
                else
                    DestroyImmediate(nestedScaler);
            }
        }

        // ── 1. UIspace: ensure it is full-screen ─────────────────────────────
        if (dialoguePanelImage != null && dialoguePanelImage.transform.parent != null)
        {
            RectTransform uiSpace = dialoguePanelImage.transform.parent as RectTransform;
            if (uiSpace != null)
            {
                uiSpace.anchorMin        = Vector2.zero;
                uiSpace.anchorMax        = Vector2.one;
                uiSpace.pivot            = new Vector2(0.5f, 0.5f);
                uiSpace.sizeDelta        = Vector2.zero;
                uiSpace.anchoredPosition = Vector2.zero;
                uiSpace.localScale       = Vector3.one; // Enforce correct scale factor
            }
        }

        // ── 2. LowerPanel: bottom-stretch dialogue box ───────────────────────
        if (dialoguePanelImage != null)
        {
            dialoguePanelImage.color = panelBg;

            RectTransform panelRect = dialoguePanelImage.rectTransform;
            panelRect.anchorMin        = new Vector2(0f, 0f);
            panelRect.anchorMax        = new Vector2(1f, 0f);
            panelRect.pivot            = new Vector2(0.5f, 0f);
            panelRect.sizeDelta        = new Vector2(0f, panelHeight);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.localScale       = Vector3.one; // Enforce correct scale factor

            // Cyan top border line (auto-created if missing)
            Transform lineT = dialoguePanelImage.transform.Find("TopBorderLine");
            if (lineT == null)
            {
                var lineGO = new GameObject("TopBorderLine", typeof(Image));
                lineGO.transform.SetParent(dialoguePanelImage.transform, false);
                lineT = lineGO.transform;
            }
            Image lineImg = lineT.GetComponent<Image>();
            lineImg.color = cyanAccent;
            RectTransform lineRect = lineImg.rectTransform;
            lineRect.anchorMin        = new Vector2(0f, 1f);
            lineRect.anchorMax        = new Vector2(1f, 1f);
            lineRect.pivot            = new Vector2(0.5f, 1f);
            lineRect.sizeDelta        = new Vector2(0f, 3f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.localScale       = Vector3.one; // Enforce correct scale factor
        }

        // ── 3. Portrait: bottom-right, explicit size ─────────────────────────
        if (portraitImage != null)
        {
            portraitImage.preserveAspect = true;
            portraitImage.color          = Color.white; // ensure no tint

            RectTransform portRect = portraitImage.rectTransform;
            // Anchor to bottom-right corner of UIspace (= screen)
            portRect.anchorMin        = new Vector2(1f, 0f);
            portRect.anchorMax        = new Vector2(1f, 0f);
            portRect.pivot            = new Vector2(1f, 0f); // pivot bottom-right
            portRect.sizeDelta        = new Vector2(portraitWidth, portraitHeight);
            portRect.anchoredPosition = new Vector2(portraitOffsetX, portraitOffsetY);
            portRect.localScale       = Vector3.one; // Reset local scale to prevent scale multipliers

            // Make portrait render BEHIND the LowerPanel
            if (dialoguePanelImage != null)
            {
                int panelIdx = dialoguePanelImage.transform.GetSiblingIndex();
                if (portRect.GetSiblingIndex() >= panelIdx)
                    portRect.SetSiblingIndex(panelIdx - 1 < 0 ? 0 : panelIdx - 1);
            }
        }

        // ── 4. Dialogue Text ─────────────────────────────────────────────────
        if (dialogueText != null)
        {
            dialogueText.color = textColor;
            RectTransform dr = dialogueText.rectTransform;
            dr.anchorMin        = Vector2.zero;
            dr.anchorMax        = Vector2.one;
            dr.pivot            = new Vector2(0.5f, 0.5f);
            dr.offsetMin        = new Vector2(dialogueLeftPad,  dialogueBottomPad);
            dr.offsetMax        = new Vector2(dialogueRightPad, dialogueTopPad);
            dr.localScale       = Vector3.one; // Enforce correct scale factor
        }

        // ── 5. Name Text ─────────────────────────────────────────────────────
        if (nameText != null)
        {
            nameText.color     = cyanAccent;
            nameText.fontStyle = FontStyles.Bold;
            RectTransform nr   = nameText.rectTransform;
            nr.anchorMin       = new Vector2(0f, 1f);
            nr.anchorMax       = new Vector2(0f, 1f);
            nr.pivot           = new Vector2(0f, 1f);
            nr.sizeDelta       = new Vector2(200f, 28f);
            nr.anchoredPosition = new Vector2(nameLeftOffset, nameTopOffset);
            nr.localScale       = Vector3.one; // Enforce correct scale factor
        }

        // ── 6. Phase Text ─────────────────────────────────────────────────────
        if (phaseText != null)
        {
            phaseText.color     = pinkAccent;
            phaseText.fontStyle = FontStyles.Italic;
            RectTransform pr    = phaseText.rectTransform;
            pr.anchorMin        = new Vector2(1f, 1f);
            pr.anchorMax        = new Vector2(1f, 1f);
            pr.pivot            = new Vector2(1f, 1f);
            pr.sizeDelta        = new Vector2(200f, 28f);
            pr.anchoredPosition = new Vector2(dialogueRightPad - 10f, nameTopOffset);
            pr.localScale       = Vector3.one; // Enforce correct scale factor
        }

        // ── 7. Input Field ───────────────────────────────────────────────────
        if (inputFieldImage != null)
        {
            inputFieldImage.color = inputBg;
            RectTransform ir = inputFieldImage.rectTransform;
            // Stretch across the bottom of the panel, leaving room for the button
            ir.anchorMin        = new Vector2(0f, 0f);
            ir.anchorMax        = new Vector2(1f, 0f);
            ir.pivot            = new Vector2(0f, 0f);
            ir.sizeDelta        = new Vector2(-(buttonWidth + inputSideMargin * 3f), inputHeight);
            ir.anchoredPosition = new Vector2(inputSideMargin, inputBottomMargin);
            ir.localScale       = Vector3.one; // Enforce correct scale factor
        }

        // ── 8. Send Button ────────────────────────────────────────────────────
        if (sendButtonImage != null)
        {
            sendButtonImage.color = cyanAccent;
            RectTransform br = sendButtonImage.rectTransform;
            br.anchorMin        = new Vector2(1f, 0f);
            br.anchorMax        = new Vector2(1f, 0f);
            br.pivot            = new Vector2(1f, 0f);
            br.sizeDelta        = new Vector2(buttonWidth, buttonHeight);
            br.anchoredPosition = new Vector2(-inputSideMargin, inputBottomMargin);
            br.localScale       = Vector3.one; // Enforce correct scale factor
        }

        if (sendButtonText != null)
        {
            sendButtonText.color     = btnTextColor;
            sendButtonText.fontStyle = FontStyles.Bold;
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
