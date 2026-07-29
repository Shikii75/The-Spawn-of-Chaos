using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Faithful port of the Game Dev OS nyxaris.html web design into Unity UI.
/// Layout: full-width dialogue bar at bottom with 3px cyan top border,
/// portrait on the right side above the panel, white input + cyan send button.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasGroup))]
public class NyxarisUIStyler : MonoBehaviour
{
    [Header("UI References — Core")]
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

    [Header("UI References — Extended (Game Dev OS)")]
    [Tooltip("Text showing sprite_key value below controls")]
    public TMP_Text spriteKeyText;
    [Tooltip("Loading text shown during API calls")]
    public TMP_Text loadingText;
    [Tooltip("Optional mode dropdown (idle/combat/story)")]
    public TMP_Dropdown modeDropdown;

    // ───────────────────────────────────────────────────────────
    //  Layout — matches nyxaris.html proportions at 1920×1080
    // ───────────────────────────────────────────────────────────
    [Header("Panel Configuration")]
    [Tooltip("Height of the bottom dialogue bar")]
    public float panelHeight = 220f;

    [Header("Portrait (right-side, red box area)")]
    public float portraitWidth = 480f;
    public float portraitHeight = 500f;
    [Tooltip("Horizontal offset from right edge (negative = inward)")]
    public float portraitOffsetX = -80f;
    [Tooltip("Vertical offset above panel (28 = sitting just above cyan line)")]
    public float portraitOffsetY = 28f;

    [Header("Dialogue Text Padding")]
    public float dialogueLeftPad = 24f;
    public float dialogueRightPad = -500f;
    public float dialogueTopPad = -70f;
    public float dialogueBottomPad = 90f;

    // ───────────────────────────────────────────────────────────
    //  Colors — exact hex values from nyxaris.html CSS
    // ───────────────────────────────────────────────────────────
    [Header("Colors (Game Dev OS — nyxaris.html)")]
    [Tooltip("#0f0f1a — body background")]
    public Color bodyBgColor = new Color(0.059f, 0.059f, 0.102f, 0.45f);
    [Tooltip("#1a1a2e — dialogue panel background")]
    public Color panelBgColor = new Color(0.102f, 0.102f, 0.180f, 1f);
    [Tooltip("#00d4ff — cyan accent (name, divider, button)")]
    public Color accentCyan = new Color(0f, 0.831f, 1f, 1f);
    [Tooltip("#ffa6ff — magenta (phase text)")]
    public Color accentMagenta = new Color(1f, 0.651f, 1f, 1f);
    [Tooltip("#ffffff — input field white background")]
    public Color inputBgColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("Dark text on white input")]
    public Color inputTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [Tooltip("#081218 — dark text on cyan button")]
    public Color buttonTextColor = new Color(0.031f, 0.071f, 0.094f, 1f);
    [Tooltip("White — normal dialogue text")]
    public Color textNormalColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("Muted grey for subtitles")]
    public Color textMutedColor = new Color(0.55f, 0.55f, 0.63f, 0.74f);
    [Tooltip("Hover tint for send button")]
    public Color buttonHoverColor = new Color(0.2f, 0.9f, 1f, 1f);

    // ───────────────────────────────────────────────────────────
    //  Internals
    // ───────────────────────────────────────────────────────────
    private static Sprite _roundedBoxSprite;
    private static Sprite _circleSprite;

    private CanvasGroup canvasGroup;
    private Coroutine openCloseCoroutine;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Start()
    {
        ApplyStyling();
    }

    // ═══════════════════════════════════════════════════════════
    //  MASTER APPLY
    // ═══════════════════════════════════════════════════════════
    [ContextMenu("Apply Game Dev OS Chat UI")]
    public void ApplyStyling()
    {
        AutoResolveReferences();
        GenerateDynamicSprites();

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        // Canvas scaler — 1920×1080 reference
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

        // UIspace — full-screen with subtle dark overlay (matches body bg #0f0f1a)
        if (dialoguePanelImage != null && dialoguePanelImage.transform.parent != null)
        {
            RectTransform uiSpace = dialoguePanelImage.transform.parent as RectTransform;
            if (uiSpace != null && uiSpace.name == "UIspace")
            {
                uiSpace.anchorMin = Vector2.zero;
                uiSpace.anchorMax = Vector2.one;
                uiSpace.offsetMin = Vector2.zero;
                uiSpace.offsetMax = Vector2.zero;

                Image bgImg = uiSpace.GetComponent<Image>();
                if (bgImg == null) bgImg = uiSpace.gameObject.AddComponent<Image>();
                bgImg.color = bodyBgColor;
            }
        }

        StyleLowerPanel();
        // Portrait is now managed by NyxarisManager as a standalone overlay on the root Canvas
        StyleTextElements();
        StyleInputField();
        StyleSendButton();
        StyleExtendedElements();

        AttachPolishComponents();
    }

    // ═══════════════════════════════════════════════════════════
    //  DIALOGUE BAR — bottom of screen, full-width, #1a1a2e
    // ═══════════════════════════════════════════════════════════
    private void StyleLowerPanel()
    {
        if (dialoguePanelImage == null) return;

        dialoguePanelImage.sprite = _roundedBoxSprite;
        dialoguePanelImage.type = Image.Type.Sliced;
        dialoguePanelImage.color = panelBgColor;

        RectTransform rt = dialoguePanelImage.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, panelHeight);
        rt.anchoredPosition = Vector2.zero;

        // Hide any old border
        Transform oldLine = dialoguePanelImage.transform.Find("TopBorderLine");
        if (oldLine != null) oldLine.gameObject.SetActive(false);

        // ── 3px cyan divider at top (border-top: 3px solid #00d4ff) ──
        Transform dividerLine = dialoguePanelImage.transform.Find("DividerLine");
        if (dividerLine == null)
        {
            var go = new GameObject("DividerLine", typeof(Image));
            go.transform.SetParent(dialoguePanelImage.transform, false);
            dividerLine = go.transform;
            dividerLine.SetAsFirstSibling();
        }

        Image dividerImg = dividerLine.GetComponent<Image>();
        dividerImg.color = accentCyan;

        RectTransform drt = dividerImg.rectTransform;
        drt.anchorMin = new Vector2(0f, 1f);
        drt.anchorMax = new Vector2(1f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(0f, 3f);
        drt.anchoredPosition = Vector2.zero;
    }

    // ═══════════════════════════════════════════════════════════
    //  PORTRAIT — right side, red box area
    // ═══════════════════════════════════════════════════════════
    private void StylePortrait()
    {
        if (portraitImage == null) AutoResolveReferences();
        if (portraitImage == null) return;

        // Ensure the portrait image is enabled and active
        portraitImage.enabled = true;
        portraitImage.gameObject.SetActive(true);

        // Resolve target UIspace parent
        Transform uiSpace = null;
        if (dialoguePanelImage != null && dialoguePanelImage.transform.parent != null)
        {
            uiSpace = dialoguePanelImage.transform.parent;
        }
        if (uiSpace == null || uiSpace.name != "UIspace")
        {
            uiSpace = transform.Find("UIspace") ?? transform;
        }

        // Reparent portrait directly to UIspace so it is NOT trapped or clipped by LowerPanel
        if (portraitImage.transform.parent != uiSpace)
        {
            portraitImage.transform.SetParent(uiSpace, false);
        }

        // Render in front of dark background tint, but behind LowerPanel dialogue box
        if (dialoguePanelImage != null)
        {
            int panelIndex = dialoguePanelImage.transform.GetSiblingIndex();
            portraitImage.transform.SetSiblingIndex(Mathf.Max(1, panelIndex));
        }
        else
        {
            portraitImage.transform.SetAsLastSibling();
        }

        // Anchor to bottom-right of UIspace, resting right on top of the cyan bar line
        // Pivot (1, 0) means (right, bottom) of the image
        RectTransform portRt = portraitImage.rectTransform;
        portRt.anchorMin = new Vector2(1f, 0f);
        portRt.anchorMax = new Vector2(1f, 0f);
        portRt.pivot = new Vector2(1f, 0f);
        portRt.sizeDelta = new Vector2(portraitWidth, portraitHeight);
        portRt.anchoredPosition = new Vector2(portraitOffsetX, panelHeight + portraitOffsetY);

        // Display properties
        portraitImage.color = Color.white;
        portraitImage.preserveAspect = true;

        // Ensure default sprite if currently missing
        if (portraitImage.sprite == null && NyxarisManager.Instance != null)
        {
            NyxarisManager.Instance.EnsureDefaultPortrait();
        }

        // Ultimate fallback: if sprite is still null, load directly from Resources
        if (portraitImage.sprite == null)
        {
            Sprite s = Resources.Load<Sprite>("NyxarisExpressions/neutral");
            if (s == null)
            {
                Sprite[] all = Resources.LoadAll<Sprite>("NyxarisExpressions");
                if (all != null && all.Length > 0)
                {
                    foreach (var a in all)
                    {
                        if (a != null && a.name.ToLower().Contains("neutral")) { s = a; break; }
                    }
                    if (s == null && all.Length > 0) s = all[0];
                }
            }
            if (s != null) portraitImage.sprite = s;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  TEXT ELEMENTS — name, phase, dialogue
    // ═══════════════════════════════════════════════════════════
    private void StyleTextElements()
    {
        // ── Dialogue / reply text ──
        if (dialogueText != null)
        {
            dialogueText.color = textNormalColor;
            dialogueText.fontSize = 24f;
            dialogueText.lineSpacing = 3f;

            // Hide speech bubble background/outline if present
            Transform container = dialogueText.transform.Find("SpeechBubbleBg");
            if (container != null)
            {
                container.gameObject.SetActive(false);
            }

            // Position: fills the panel area, with padding to avoid the portrait on right
            RectTransform txtRt = dialogueText.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(dialogueLeftPad, dialogueBottomPad);
            txtRt.offsetMax = new Vector2(dialogueRightPad, dialogueTopPad);
        }

        // ── Name text: "Nyxaris" — bold, cyan, top-left of panel ──
        if (nameText != null)
        {
            nameText.color = accentCyan;
            nameText.fontSize = 28f;
            nameText.fontStyle = FontStyles.Bold;

            RectTransform nr = nameText.rectTransform;
            nr.anchorMin = new Vector2(0f, 1f);
            nr.anchorMax = new Vector2(0f, 1f);
            nr.pivot = new Vector2(0f, 1f);
            nr.anchoredPosition = new Vector2(20f, -12f);
            nr.sizeDelta = new Vector2(250f, 36f);
        }

        // ── Phase text: magenta, small, to the right of name ──
        if (phaseText != null)
        {
            phaseText.color = accentMagenta;
            phaseText.fontSize = 17f;
            phaseText.fontStyle = FontStyles.Normal;
            phaseText.alignment = TextAlignmentOptions.Left;

            RectTransform phr = phaseText.rectTransform;
            phr.anchorMin = new Vector2(0f, 1f);
            phr.anchorMax = new Vector2(0f, 1f);
            phr.pivot = new Vector2(0f, 1f);
            phr.anchoredPosition = new Vector2(270f, -18f);
            phr.sizeDelta = new Vector2(300f, 26f);

            // No badge background on phase text
            Image badgeBg = phaseText.GetComponent<Image>();
            if (badgeBg != null) badgeBg.enabled = false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INPUT FIELD — white bg, rounded, dark text (web: default input)
    // ═══════════════════════════════════════════════════════════
    private void StyleInputField()
    {
        if (inputFieldImage == null) return;

        inputFieldImage.sprite = _roundedBoxSprite;
        inputFieldImage.type = Image.Type.Sliced;
        inputFieldImage.color = inputBgColor;

        // Position: bottom row of the panel, takes ~65% width, leaving room for button
        RectTransform ir = inputFieldImage.rectTransform;
        ir.anchorMin = new Vector2(0f, 0f);
        ir.anchorMax = new Vector2(0.65f, 0f);
        ir.pivot = new Vector2(0f, 0f);
        ir.sizeDelta = new Vector2(0f, 48f);
        ir.anchoredPosition = new Vector2(20f, 16f);

        // Hide any glow border
        Transform glowBorder = inputFieldImage.transform.Find("InputGlowBorder");
        if (glowBorder != null) glowBorder.gameObject.SetActive(false);

        TMP_InputField inputField = inputFieldImage.GetComponent<TMP_InputField>();
        if (inputField != null)
        {
            if (inputField.placeholder != null)
            {
                var ph = inputField.placeholder as TMP_Text;
                if (ph != null)
                {
                    ph.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                    ph.fontSize = 20f;
                    ph.text = "Talk to Nyxaris…";
                }
            }
            if (inputField.textComponent != null)
            {
                inputField.textComponent.color = inputTextColor;
                inputField.textComponent.fontSize = 20f;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  SEND BUTTON — cyan bg #00d4ff, dark bold text, rounded
    // ═══════════════════════════════════════════════════════════
    private void StyleSendButton()
    {
        if (sendButtonImage == null) return;

        sendButtonImage.sprite = _roundedBoxSprite;
        sendButtonImage.type = Image.Type.Sliced;
        sendButtonImage.color = accentCyan;

        // Position: right of the input field, same row
        RectTransform br = sendButtonImage.rectTransform;
        br.anchorMin = new Vector2(0.65f, 0f);
        br.anchorMax = new Vector2(0.65f, 0f);
        br.pivot = new Vector2(0f, 0f);
        br.sizeDelta = new Vector2(130f, 48f);
        br.anchoredPosition = new Vector2(12f, 16f);

        if (sendButtonText != null)
        {
            sendButtonText.color = buttonTextColor;
            sendButtonText.fontSize = 20f;
            sendButtonText.fontStyle = FontStyles.Bold;
            sendButtonText.text = "send";
            sendButtonText.alignment = TextAlignmentOptions.Center;
        }

        // Hide button glow
        Transform buttonGlow = sendButtonImage.transform.Find("ButtonGlow");
        if (buttonGlow != null) buttonGlow.gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════
    //  EXTENDED ELEMENTS — sprite-key display, loading text, mode dropdown
    // ═══════════════════════════════════════════════════════════
    private void StyleExtendedElements()
    {
        // ── Sprite key display: "sprite_key: calm_neutral_neutral" ──
        if (spriteKeyText != null)
        {
            spriteKeyText.color = accentCyan;
            spriteKeyText.fontSize = 13f;
            spriteKeyText.fontStyle = FontStyles.Normal;
            spriteKeyText.text = "sprite_key: calm_neutral_neutral";

            RectTransform skRt = spriteKeyText.rectTransform;
            skRt.anchorMin = new Vector2(0f, 0f);
            skRt.anchorMax = new Vector2(0.65f, 0f);
            skRt.pivot = new Vector2(0f, 0f);
            skRt.anchoredPosition = new Vector2(20f, 62f);
            skRt.sizeDelta = new Vector2(0f, 20f);
        }

        // ── Loading text: "conjuring glyphs…" ──
        if (loadingText != null)
        {
            loadingText.color = textMutedColor;
            loadingText.fontSize = 13f;
            loadingText.fontStyle = FontStyles.Italic;
            loadingText.text = "conjuring glyphs…";
            loadingText.gameObject.SetActive(false); // hidden by default
        }

        // ── Mode dropdown styling ──
        if (modeDropdown != null)
        {
            Image dropdownImg = modeDropdown.GetComponent<Image>();
            if (dropdownImg != null)
            {
                dropdownImg.sprite = _roundedBoxSprite;
                dropdownImg.type = Image.Type.Sliced;
                dropdownImg.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            }

            // Position: to the right of the send button
            RectTransform dr = modeDropdown.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0.65f, 0f);
            dr.anchorMax = new Vector2(0.65f, 0f);
            dr.pivot = new Vector2(0f, 0f);
            dr.sizeDelta = new Vector2(110f, 42f);
            dr.anchoredPosition = new Vector2(140f, 16f);

            // Ensure options exist
            if (modeDropdown.options.Count == 0)
            {
                modeDropdown.options.Add(new TMP_Dropdown.OptionData("idle"));
                modeDropdown.options.Add(new TMP_Dropdown.OptionData("combat"));
                modeDropdown.options.Add(new TMP_Dropdown.OptionData("story"));
                modeDropdown.RefreshShownValue();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  POLISH — hover/bounce effects
    // ═══════════════════════════════════════════════════════════
    private void AttachPolishComponents()
    {
        if (!Application.isPlaying) return;

        // Hover effect on send button
        if (sendButtonImage != null && sendButtonImage.GetComponent<NyxarisBounceEffect>() == null)
        {
            var bounce = sendButtonImage.gameObject.AddComponent<NyxarisBounceEffect>();
            bounce.normalColor = accentCyan;
            bounce.hoverColor = buttonHoverColor;
            bounce.targetImage = sendButtonImage;
            bounce.bounceScale = 1.03f;
        }

        // Subtle hover effect on input field
        if (inputFieldImage != null && inputFieldImage.GetComponent<NyxarisBounceEffect>() == null)
        {
            var bounce = inputFieldImage.gameObject.AddComponent<NyxarisBounceEffect>();
            bounce.normalColor = inputBgColor;
            bounce.hoverColor = new Color(0.95f, 0.95f, 0.98f, 1f);
            bounce.targetImage = inputFieldImage;
            bounce.bounceScale = 1.01f;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API — for NyxarisManager to call
    // ═══════════════════════════════════════════════════════════

    /// <summary>Show the loading indicator.</summary>
    public void ShowLoading()
    {
        if (loadingText != null) loadingText.gameObject.SetActive(true);
    }

    /// <summary>Hide the loading indicator.</summary>
    public void HideLoading()
    {
        if (loadingText != null) loadingText.gameObject.SetActive(false);
    }

    /// <summary>Update the sprite_key display text.</summary>
    public void SetSpriteKeyDisplay(string spriteKey)
    {
        if (spriteKeyText != null)
            spriteKeyText.text = $"sprite_key: {spriteKey}";
    }

    /// <summary>Get the currently selected mode from the dropdown.</summary>
    public string GetSelectedMode()
    {
        if (modeDropdown == null || modeDropdown.options.Count == 0) return "idle";
        return modeDropdown.options[modeDropdown.value].text;
    }

    // ═══════════════════════════════════════════════════════════
    //  OPEN / CLOSE ANIMATIONS
    // ═══════════════════════════════════════════════════════════

    public void AnimateOpen()
    {
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy) return;

        if (openCloseCoroutine != null) StopCoroutine(openCloseCoroutine);
        openCloseCoroutine = StartCoroutine(DoOpenAnimation());
    }

    public void AnimateClose()
    {
        if (!gameObject.activeInHierarchy) return;

        if (openCloseCoroutine != null) StopCoroutine(openCloseCoroutine);
        openCloseCoroutine = StartCoroutine(DoCloseAnimation());
    }

    private IEnumerator DoOpenAnimation()
    {
        canvasGroup.alpha = 0f;
        transform.localScale = new Vector3(0.99f, 0.99f, 1f);

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            transform.localScale = Vector3.Lerp(new Vector3(0.99f, 0.99f, 1f), Vector3.one, eased);
            canvasGroup.alpha = eased;
            yield return null;
        }

        transform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
    }

    private IEnumerator DoCloseAnimation()
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(startScale, new Vector3(0.98f, 0.98f, 1f), eased);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, eased);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════
    //  PORTRAIT BOUNCE
    // ═══════════════════════════════════════════════════════════

    public void BouncePortrait()
    {
        if (portraitImage == null || !gameObject.activeInHierarchy) return;
        
        Transform target = portraitImage.transform;
        StartCoroutine(DoPortraitBounce(target));
    }

    private IEnumerator DoPortraitBounce(Transform target)
    {
        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 baseScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float bounceAmount = Mathf.Sin(t * 8f) * Mathf.Pow(1f - t, 2f) * 0.02f;
            target.localScale = baseScale + new Vector3(bounceAmount, -bounceAmount * 0.3f, 0f);
            yield return null;
        }

        target.localScale = baseScale;
    }

    // ═══════════════════════════════════════════════════════════
    //  DYNAMIC SPRITE GENERATION
    // ═══════════════════════════════════════════════════════════

    private void GenerateDynamicSprites()
    {
        if (_roundedBoxSprite != null && _circleSprite != null) return;

        int size = 128;
        int cornerRadius = 20; // border-radius: 10px at 2x density

        // ── Rounded rectangle ──
        Texture2D roundTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        roundTex.filterMode = FilterMode.Bilinear;
        roundTex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = x < cornerRadius ? cornerRadius : (x >= size - cornerRadius ? size - cornerRadius - 1 : x);
                int cy = y < cornerRadius ? cornerRadius : (y >= size - cornerRadius ? size - cornerRadius - 1 : y);

                float dx = x - cx;
                float dy = y - cy;
                float distSq = dx * dx + dy * dy;

                if (distSq > cornerRadius * cornerRadius)
                {
                    float dist = Mathf.Sqrt(distSq);
                    float diff = dist - cornerRadius;
                    float alpha = 1f - Mathf.Clamp01(diff);
                    roundTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    roundTex.SetPixel(x, y, Color.white);
                }
            }
        }
        roundTex.Apply();

        Vector4 border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
        _roundedBoxSprite = Sprite.Create(roundTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);

        // ── Circle ──
        Texture2D circTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        circTex.filterMode = FilterMode.Bilinear;
        circTex.wrapMode = TextureWrapMode.Clamp;
        float center = size / 2.0f;
        float radius = size / 2.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ddx = x - center;
                float ddy = y - center;
                float dist = Mathf.Sqrt(ddx * ddx + ddy * ddy);

                if (dist > radius)
                {
                    circTex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float diff = radius - dist;
                    float alpha = Mathf.Clamp01(diff);
                    circTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        circTex.Apply();
        _circleSprite = Sprite.Create(circTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ═══════════════════════════════════════════════════════════
    //  AUTO-RESOLVE REFERENCES & GUARANTEED PORTRAIT CREATION
    // ═══════════════════════════════════════════════════════════

    private void AutoResolveReferences()
    {
        Transform uiSpace = null;
        if (transform.name == "UIspace")
            uiSpace = transform;
        else if (transform.name == "MainInterface" || GetComponent<Canvas>() != null)
            uiSpace = transform.Find("UIspace");

        if (uiSpace == null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                uiSpace = c.transform.Find("UIspace") ?? c.transform;
            }
        }

        if (uiSpace == null) uiSpace = transform;

        Transform lp = uiSpace.Find("LowerPanel");
        if (lp != null)
        {
            if (dialoguePanelImage == null) dialoguePanelImage = lp.GetComponent<Image>();

            foreach (var txt in lp.GetComponentsInChildren<TMP_Text>(true))
            {
                if (txt.transform.parent != null && txt.transform.parent.name.Contains("Button"))
                    continue;

                string n = txt.gameObject.name.ToLower();
                if ((n.Contains("dialogue") || n.Contains("body") || n == "text") && dialogueText == null)
                    dialogueText = txt;
                else if ((n.Contains("name") || n.Contains("title") || n.Contains("character") || n.Contains("tmp") || n == "text (tmp)") && nameText == null)
                    nameText = txt;
                else if ((n.Contains("phase") || n.Contains("state") || n.Contains("evolution")) && phaseText == null)
                    phaseText = txt;
                else if ((n.Contains("sprite") || n.Contains("key")) && spriteKeyText == null)
                    spriteKeyText = txt;
                else if ((n.Contains("loading") || n.Contains("conjuring")) && loadingText == null)
                    loadingText = txt;
            }

            if (inputFieldImage == null)
            {
                Transform t = lp.Find("MessageInput") ?? lp.Find("InputField");
                if (t != null) inputFieldImage = t.GetComponent<Image>();
            }
            if (sendButtonImage == null)
            {
                Transform t = lp.Find("SendButton") ?? lp.Find("Send");
                if (t != null) sendButtonImage = t.GetComponent<Image>();
            }
            if (sendButtonImage != null && sendButtonText == null)
                sendButtonText = sendButtonImage.GetComponentInChildren<TMP_Text>();

            // Try to find mode dropdown
            if (modeDropdown == null)
            {
                Transform t = lp.Find("ModeDropdown") ?? lp.Find("Mode");
                if (t != null) modeDropdown = t.GetComponent<TMP_Dropdown>();
            }
        }

        // Search for existing Portrait object
        if (portraitImage == null)
        {
            Transform port = uiSpace.Find("Portrait");
            if (port == null && lp != null) port = lp.Find("Portrait");
            if (port != null) portraitImage = port.GetComponent<Image>();
        }

        // GUARANTEED CREATION: If portraitImage is STILL null/missing, create it automatically!
        if (portraitImage == null)
        {
            GameObject portGO = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portGO.transform.SetParent(uiSpace, false);
            portraitImage = portGO.GetComponent<Image>();
            Debug.Log("[NyxarisUIStyler] Created missing Portrait GameObject under UIspace.");
        }

        // Keep NyxarisManager synced with the exact same Image reference
        if (NyxarisManager.Instance != null && portraitImage != null)
        {
            NyxarisManager.Instance.portrait = portraitImage;
        }
    }

    public void SetActiveDot(int activeIndex, int totalDots = 3)
    {
    }
}

// ═══════════════════════════════════════════════════════════
//  HELPER COMPONENTS
// ═══════════════════════════════════════════════════════════

public class NyxarisGlowPulse : MonoBehaviour
{
    public float pulseSpeed = 1.2f;
    public float minScale = 0.985f;
    public float maxScale = 1.01f;

    private Vector3 baseScale;

    void Start()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        float sine = Mathf.Sin(Time.time * pulseSpeed);
        float multiplier = Mathf.Lerp(minScale, maxScale, (sine + 1f) * 0.5f);
        transform.localScale = baseScale * multiplier;
    }
}

public class NyxarisBounceEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Color normalColor;
    public Color hoverColor;
    public Image targetImage;
    public float bounceScale = 1.03f;

    private Vector3 baseScale;
    private Coroutine animCoroutine;

    void Start()
    {
        baseScale = transform.localScale;
        if (targetImage != null) targetImage.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetImage != null) targetImage.color = hoverColor;
        TriggerScaleAnimation(baseScale * bounceScale, 0.16f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetImage != null) targetImage.color = normalColor;
        TriggerScaleAnimation(baseScale, 0.16f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TriggerScaleAnimation(baseScale * 0.97f, 0.08f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        TriggerScaleAnimation(baseScale * bounceScale, 0.12f);
    }

    private void TriggerScaleAnimation(Vector3 targetScale, float duration)
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        if (gameObject.activeInHierarchy)
        {
            animCoroutine = StartCoroutine(DoScale(targetScale, duration));
        }
    }

    private IEnumerator DoScale(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float tSmooth = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, tSmooth);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}
