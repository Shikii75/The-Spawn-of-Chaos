using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Static utility class for creating consistent, styled UI elements from code.
/// All game UI panels use these helpers to maintain a unified dark gothic-fantasy aesthetic.
/// </summary>
public static class UIFactory
{
    // ── Color Palette ──────────────────────────────────────────────
    public static readonly Color PanelBackground   = new Color(10f/255f, 8f/255f, 20f/255f, 0.92f);
    public static readonly Color PanelBackgroundSolid = new Color(10f/255f, 8f/255f, 20f/255f, 1f);
    public static readonly Color Accent            = new Color(146f/255f, 104f/255f, 255f/255f, 1f);
    public static readonly Color AccentDim         = new Color(146f/255f, 104f/255f, 255f/255f, 0.6f);
    public static readonly Color TextWhite         = Color.white;
    public static readonly Color TextMuted         = new Color(180f/255f, 180f/255f, 200f/255f, 1f);
    public static readonly Color Danger            = new Color(220f/255f, 53f/255f, 69f/255f, 1f);
    public static readonly Color Success           = new Color(61f/255f, 255f/255f, 154f/255f, 1f);

    public static readonly Color ButtonNormal      = new Color(146f/255f, 104f/255f, 255f/255f, 0.15f);
    public static readonly Color ButtonHighlight   = new Color(146f/255f, 104f/255f, 255f/255f, 0.35f);
    public static readonly Color ButtonPressed     = new Color(146f/255f, 104f/255f, 255f/255f, 0.5f);
    public static readonly Color ButtonDisabled    = new Color(60f/255f, 60f/255f, 80f/255f, 0.4f);

    public static readonly Color SliderHealthFill  = new Color(220f/255f, 40f/255f, 40f/255f, 1f);
    public static readonly Color SliderManaFill    = new Color(100f/255f, 80f/255f, 220f/255f, 1f);
    public static readonly Color SliderBossFill    = new Color(200f/255f, 30f/255f, 30f/255f, 1f);
    public static readonly Color SliderTrack       = new Color(30f/255f, 25f/255f, 45f/255f, 1f);

    public static readonly Color BorderColor       = new Color(146f/255f, 104f/255f, 255f/255f, 0.25f);
    public static readonly Color OverlayDark       = new Color(0f, 0f, 0f, 0.75f);

    // ── Canvas ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a full Screen Space Overlay canvas with CanvasScaler (1920x1080) and GraphicRaycaster.
    /// </summary>
    public static Canvas CreateCanvas(string name, int sortOrder = 0)
    {
        GameObject go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ── Panels ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a panel with an Image component and specified anchoring.
    /// </summary>
    public static RectTransform CreatePanel(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = color;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        return rt;
    }

    /// <summary>
    /// Creates a full-screen overlay panel (covers entire canvas).
    /// </summary>
    public static RectTransform CreateFullScreenPanel(Transform parent, string name, Color color)
    {
        return CreatePanel(parent, name, color,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    // ── Text ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a TextMeshProUGUI element with specified styling.
    /// </summary>
    public static TextMeshProUGUI CreateText(Transform parent, string name, string content,
        float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;

        return text;
    }

    /// <summary>
    /// Sets RectTransform anchoring and sizing on any existing RectTransform.
    /// </summary>
    public static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    /// <summary>
    /// Sets RectTransform using anchored position and size delta (for fixed-size elements).
    /// </summary>
    public static void SetRectFixed(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
    }

    // ── Buttons ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a styled button with a TMP label and an onClick callback.
    /// Returns the Button component. The button's RectTransform can be further configured.
    /// </summary>
    public static Button CreateButton(Transform parent, string name, string label,
        float fontSize, UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image bg = go.AddComponent<Image>();
        bg.color = ButtonNormal;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = ButtonNormal;
        cb.highlightedColor = ButtonHighlight;
        cb.pressedColor = ButtonPressed;
        cb.disabledColor = ButtonDisabled;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        // Add text child
        TextMeshProUGUI text = CreateText(go.transform, "Label", label,
            fontSize, TextWhite, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return btn;
    }

    /// <summary>
    /// Creates a small close "✕" button in the top-right corner of the parent.
    /// </summary>
    public static Button CreateCloseButton(Transform parent, UnityAction onClick)
    {
        Button btn = CreateButton(parent, "CloseButton", "✕", 22f, onClick);
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-25f, -25f);
        rt.sizeDelta = new Vector2(40f, 40f);

        // Danger red tint
        Image bg = btn.GetComponent<Image>();
        bg.color = new Color(220f/255f, 53f/255f, 69f/255f, 0.25f);

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(220f/255f, 53f/255f, 69f/255f, 0.25f);
        cb.highlightedColor = new Color(220f/255f, 53f/255f, 69f/255f, 0.5f);
        cb.pressedColor = new Color(220f/255f, 53f/255f, 69f/255f, 0.7f);
        btn.colors = cb;

        return btn;
    }

    // ── Sliders ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a horizontal Slider with a background track and colored fill.
    /// Returns the Slider component.
    /// </summary>
    public static Slider CreateSlider(Transform parent, string name, Color fillColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Slider slider = go.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.value = 100;
        slider.wholeNumbers = true;

        // Background track
        RectTransform bgRT = CreatePanel(go.transform, "Background", SliderTrack,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
        SetRect(fillAreaRT, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, 0));

        // Fill
        RectTransform fillRT = CreatePanel(fillArea.transform, "Fill", fillColor,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        slider.fillRect = fillRT;

        // We don't need a handle for stat bars
        slider.handleRect = null;
        slider.interactable = false;

        return slider;
    }

    // ── Dividers ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a thin horizontal divider line.
    /// </summary>
    public static RectTransform CreateDivider(Transform parent, string name = "Divider")
    {
        return CreatePanel(parent, name, BorderColor,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f),
            new Vector2(10, -1), new Vector2(-10, 1));
    }

    // ── Layout Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Adds a VerticalLayoutGroup to a transform for automatic child stacking.
    /// </summary>
    public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing = 8f,
        RectOffset padding = null, TextAnchor childAlignment = TextAnchor.UpperCenter)
    {
        VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.padding = padding ?? new RectOffset(20, 20, 20, 20);
        vlg.childAlignment = childAlignment;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        return vlg;
    }

    /// <summary>
    /// Adds a HorizontalLayoutGroup to a transform for automatic child stacking.
    /// </summary>
    public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing = 8f,
        RectOffset padding = null, TextAnchor childAlignment = TextAnchor.MiddleCenter)
    {
        HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.padding = padding ?? new RectOffset(10, 10, 5, 5);
        hlg.childAlignment = childAlignment;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return hlg;
    }

    /// <summary>
    /// Adds a LayoutElement to control sizing within layout groups.
    /// </summary>
    public static LayoutElement AddLayoutElement(GameObject go,
        float preferredHeight = -1, float preferredWidth = -1,
        float minHeight = -1, float minWidth = -1,
        bool flexibleWidth = false)
    {
        LayoutElement le = go.AddComponent<LayoutElement>();
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
        if (minHeight >= 0) le.minHeight = minHeight;
        if (minWidth >= 0) le.minWidth = minWidth;
        if (flexibleWidth) le.flexibleWidth = 1;
        return le;
    }

    /// <summary>
    /// Adds a ContentSizeFitter to auto-size based on content.
    /// </summary>
    public static ContentSizeFitter AddContentFitter(GameObject go,
        ContentSizeFitter.FitMode horizontalFit = ContentSizeFitter.FitMode.Unconstrained,
        ContentSizeFitter.FitMode verticalFit = ContentSizeFitter.FitMode.PreferredSize)
    {
        ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = horizontalFit;
        csf.verticalFit = verticalFit;
        return csf;
    }
}
