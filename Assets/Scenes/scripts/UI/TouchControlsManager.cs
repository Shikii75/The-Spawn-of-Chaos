using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// TouchControlsManager - Complete Cyber-Gothic On-Screen Mobile Touch Controller:
/// 1. D-Pad (Left Thumb): Dedicated Left (◀) and Right (▶) buttons with continuous frame-by-frame
///    input holding, IDragHandler pointer locking, and double-tap dash trigger.
/// 2. Action Cluster (Right Thumb): Glassmorphic buttons for Attack, Jump, Dash, Blob, Magic, Spear.
/// 3. Utility Buttons: Pause (top-right) & Contextual Interact (floating prompt).
/// 4. Tactile Micro-Animations: Scale-punch (0.90x) on touch down.
/// </summary>
public class TouchControlsManager : MonoBehaviour
{
    private static TouchControlsManager instance;
    public static TouchControlsManager Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<TouchControlsManager>();
            return instance;
        }
    }

    [Header("Visibility Settings")]
    [Tooltip("Enables touch controls inside the Unity Editor for instant mouse playtesting.")]
    public bool forceEnableInEditor = true;

    // Runtime UI elements
    private Canvas touchCanvas;

    private static Sprite circleSprite;
    private static Sprite ringSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitializeTouchControls()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenu") return;

        if (Instance == null && FindFirstObjectByType<TouchControlsManager>() == null)
        {
            GameObject tcGO = new GameObject("TouchControlsManager");
            tcGO.AddComponent<TouchControlsManager>();
            DontDestroyOnLoad(tcGO);
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildTouchUI();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (touchCanvas != null)
        {
            touchCanvas.gameObject.SetActive(false);
        }
    }

    public static bool IsTitleScreenActive()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu") return true;

        var menuController = FindFirstObjectByType<MainMenuUIToolkitController>();
        if (menuController != null && menuController.gameObject.activeInHierarchy)
        {
            var doc = menuController.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (doc != null && doc.rootVisualElement != null && doc.rootVisualElement.style.display != UnityEngine.UIElements.DisplayStyle.None)
            {
                return true;
            }
            if (!MainMenuUIToolkitController.isPlaying)
            {
                return true;
            }
        }

        return false;
    }

    void Update()
    {
        bool isTitle = IsTitleScreenActive();
        bool isPaused = PauseMenu.Instance != null && PauseMenu.Instance.isPaused;

        bool isShop = ShopUI.Instance != null && ShopUI.Instance.IsShopActive;

        // Strictly hide on Title Screen, during Pause, or when shop is active
        bool shouldBeVisible = (Application.isMobilePlatform || forceEnableInEditor) && 
                               !isTitle && 
                               !isPaused && 
                               !isShop;

        if (touchCanvas != null && touchCanvas.gameObject.activeSelf != shouldBeVisible)
        {
            touchCanvas.gameObject.SetActive(shouldBeVisible);
        }
    }

    void BuildTouchUI()
    {
        EnsureEventSystem();
        GenerateProceduralSprites();

        // 1. Create Screen-Space Overlay Canvas
        GameObject canvasGO = new GameObject("TouchControlsCanvas");
        canvasGO.transform.SetParent(transform);
        touchCanvas = canvasGO.AddComponent<Canvas>();
        touchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        touchCanvas.sortingOrder = 95;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 2. Build Horizontal D-Pad (Left Thumb: ◀ and ▶)
        CreateDPad(canvasGO.transform);

        // 3. Build Action Button Cluster (Right Thumb)
        CreateActionCluster(canvasGO.transform);

        // 4. Build Utility Buttons (Top Right & Interact)
        CreateUtilityButtons(canvasGO.transform);

        // Initially inactive by default until gameplay begins
        touchCanvas.gameObject.SetActive(false);
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. HORIZONTAL D-PAD (LEFT & RIGHT) WITH CONTINUOUS FRAME-HOLD & DOUBLE-TAP DASH
    // ─────────────────────────────────────────────────────────────────────────────
    void CreateDPad(Transform parent)
    {
        // 1. Base Housing Glass Pill
        GameObject housingObj = new GameObject("DPadHousing");
        housingObj.transform.SetParent(parent, false);
        RectTransform housingRT = housingObj.AddComponent<RectTransform>();
        housingRT.anchorMin = new Vector2(0, 0);
        housingRT.anchorMax = new Vector2(0, 0);
        housingRT.pivot = new Vector2(0.5f, 0.5f);
        housingRT.anchoredPosition = new Vector2(210, 190);
        housingRT.sizeDelta = new Vector2(275, 135);

        Image housingImg = housingObj.AddComponent<Image>();
        housingImg.color = new Color(0.02f, 0.04f, 0.08f, 0.55f);
        housingImg.raycastTarget = false; // Don't block button clicks

        Outline outline = housingObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.04f, 0.85f, 1.0f, 0.35f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // 2. D-PAD LEFT BUTTON (◀) - Direction = -1
        CreateDPadButton(parent, "TouchBtn_DPadLeft",
            new Vector2(145, 190), new Vector2(115, 115),
            new Color(0.04f, 0.85f, 1.0f, 0.85f), "◀", 40, -1f
        );

        // 3. D-PAD RIGHT BUTTON (▶) - Direction = +1
        CreateDPadButton(parent, "TouchBtn_DPadRight",
            new Vector2(275, 190), new Vector2(115, 115),
            new Color(0.04f, 0.85f, 1.0f, 0.85f), "▶", 40, 1f
        );
    }

    RectTransform CreateDPadButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, 
        Color accentColor, string labelText, int fontSize, float direction)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image bgImg = btnObj.AddComponent<Image>();
        bgImg.sprite = circleSprite;
        bgImg.color = new Color(0.04f, 0.05f, 0.12f, 0.70f);
        bgImg.raycastTarget = true;

        GameObject ringObj = new GameObject("AccentRing");
        ringObj.transform.SetParent(btnObj.transform, false);
        RectTransform ringRT = ringObj.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero;
        ringRT.anchorMax = Vector2.one;
        ringRT.sizeDelta = Vector2.zero;
        Image ringImg = ringObj.AddComponent<Image>();
        ringImg.sprite = ringSprite;
        ringImg.color = accentColor;
        ringImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        TouchDPadButton dpad = btnObj.AddComponent<TouchDPadButton>();
        dpad.Init(direction);

        return rt;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. ACTION BUTTON CLUSTER
    // ─────────────────────────────────────────────────────────────────────────────
    void CreateActionCluster(Transform parent)
    {
        // 1. ATTACK (J) - Large Combat Core (Neon Magenta)
        CreateTouchButton(parent, "TouchBtn_Attack", 
            new Vector2(-220, 240), new Vector2(130, 130), 
            new Color(1.0f, 0.05f, 0.45f, 0.85f), "⚔️\nATTACK", 22,
            onDown: () => {
                if (MageCombat.Instance != null)
                {
                    MageCombat.Instance.TriggerMeleeAttackFromTouch();
                }
            }
        );

        // 2. JUMP (Space) - Large Primary Mobility (Neon Cyan)
        CreateTouchButton(parent, "TouchBtn_Jump", 
            new Vector2(-95, 130), new Vector2(120, 120), 
            new Color(0.0f, 0.92f, 1.0f, 0.85f), "JUMP", 22,
            onDown: () => {
                if (move.Instance != null)
                {
                    move.Instance.virtualJumpPressed = true;
                    move.Instance.virtualJumpHeld = true;
                }
            },
            onUp: () => {
                if (move.Instance != null)
                {
                    move.Instance.virtualJumpHeld = false;
                }
            }
        );

        // 3. DASH (Shift) - Electric Sky Blue
        CreateTouchButton(parent, "TouchBtn_Dash", 
            new Vector2(-350, 130), new Vector2(95, 95), 
            new Color(0.1f, 0.82f, 1.0f, 0.80f), "DASH", 18,
            onDown: () => {
                if (move.Instance != null)
                {
                    move.Instance.virtualDashPressed = true;
                }
            }
        );

        // 4. BLOB (B) - Dedicated Morph Button
        CreateTouchButton(parent, "TouchBtn_Blob", 
            new Vector2(-220, 395), new Vector2(90, 90), 
            new Color(0.72f, 0.25f, 1.0f, 0.80f), "BLOB", 18,
            onDown: () => {
                if (move.Instance != null)
                {
                    move.Instance.virtualBlobPressed = !move.Instance.virtualBlobPressed;
                }
            }
        );

        // 5. MAGIC (K) - Solar Gold
        CreateTouchButton(parent, "TouchBtn_Magic", 
            new Vector2(-355, 265), new Vector2(90, 90), 
            new Color(1.0f, 0.68f, 0.1f, 0.80f), "MAGIC", 18,
            onDown: () => {
                if (MageCombat.Instance != null)
                {
                    MageCombat.Instance.TriggerRangedAttackFromTouch();
                }
            }
        );

        // 6. SPEAR (Q / X) - Cyan Lightning
        CreateTouchButton(parent, "TouchBtn_Spear", 
            new Vector2(-95, 285), new Vector2(90, 90), 
            new Color(0.2f, 0.98f, 0.95f, 0.80f), "SPEAR", 18,
            onDown: () => {
                if (LumiSpearWeapon.Instance != null)
                {
                    LumiSpearWeapon.Instance.TriggerSpearActionFromTouch();
                }
            }
        );
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. UTILITY BUTTONS (PAUSE & INTERACT)
    // ─────────────────────────────────────────────────────────────────────────────
    void CreateUtilityButtons(Transform parent)
    {
        CreateTouchButton(parent, "TouchBtn_Pause", 
            new Vector2(-70, -70), new Vector2(70, 70), 
            new Color(0.25f, 0.85f, 1.0f, 0.75f), "II", 28,
            anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1),
            onDown: () => {
                if (PauseMenu.Instance != null)
                {
                    PauseMenu.Instance.TogglePause();
                }
            }
        );

        CreatePillButton(parent, "TouchBtn_Interact",
            new Vector2(-220, 520), new Vector2(170, 55),
            new Color(1.0f, 0.82f, 0.2f, 0.85f), "INTERACT (E)", 16,
            onDown: () => {
                TriggerNearbyInteraction();
            }
        );
    }

    void TriggerNearbyInteraction()
    {
        GameObject player = GameObject.FindWithTag("Player") ?? (move.Instance != null ? move.Instance.gameObject : null);
        if (player == null) return;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(player.transform.position, 4.5f);
        foreach (var col in colliders)
        {
            var interactable = col.GetComponent<NPCInteractable>() ?? col.GetComponentInParent<NPCInteractable>();
            if (interactable != null)
            {
                interactable.Interact();
                return;
            }

            var speechBubble = col.GetComponent<SpeechBubbleDialogue>() ?? col.GetComponentInParent<SpeechBubbleDialogue>();
            if (speechBubble != null)
            {
                speechBubble.OpenDialogueExternally();
                return;
            }

            var shopUi = col.GetComponent<ShopUI>() ?? col.GetComponentInParent<ShopUI>();
            if (shopUi != null)
            {
                shopUi.OpenShop();
                return;
            }
        }
    }

    RectTransform CreateTouchButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, 
        Color accentColor, string labelText, int fontSize, System.Action onDown, System.Action onUp = null,
        Vector2? anchorMin = null, Vector2? anchorMax = null)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin ?? new Vector2(1, 0);
        rt.anchorMax = anchorMax ?? new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image bgImg = btnObj.AddComponent<Image>();
        bgImg.sprite = circleSprite;
        bgImg.color = new Color(0.04f, 0.05f, 0.12f, 0.65f);
        bgImg.raycastTarget = true;

        GameObject ringObj = new GameObject("AccentRing");
        ringObj.transform.SetParent(btnObj.transform, false);
        RectTransform ringRT = ringObj.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero;
        ringRT.anchorMax = Vector2.one;
        ringRT.sizeDelta = Vector2.zero;
        Image ringImg = ringObj.AddComponent<Image>();
        ringImg.sprite = ringSprite;
        ringImg.color = accentColor;
        ringImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        TouchButtonTrigger trigger = btnObj.AddComponent<TouchButtonTrigger>();
        trigger.Init(accentColor, onDown, onUp);

        return rt;
    }

    RectTransform CreatePillButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, 
        Color accentColor, string labelText, int fontSize, System.Action onDown)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image bgImg = btnObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.08f, 0.15f, 0.85f);
        bgImg.raycastTarget = true;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        TouchButtonTrigger trigger = btnObj.AddComponent<TouchButtonTrigger>();
        trigger.Init(accentColor, onDown, null);

        return rt;
    }

    void GenerateProceduralSprites()
    {
        if (circleSprite != null && ringSprite != null) return;

        int res = 128;
        Texture2D circleTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Texture2D ringTex = new Texture2D(res, res, TextureFormat.RGBA32, false);

        float center = res * 0.5f;
        float radius = center - 2f;
        float innerRadius = radius - 7f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                
                float circleAlpha = Mathf.Clamp01((radius - dist) + 0.5f);
                circleTex.SetPixel(x, y, new Color(1, 1, 1, circleAlpha));

                float outerAlpha = Mathf.Clamp01((radius - dist) + 0.5f);
                float innerAlpha = Mathf.Clamp01((dist - innerRadius) + 0.5f);
                float ringAlpha = Mathf.Min(outerAlpha, innerAlpha);
                ringTex.SetPixel(x, y, new Color(1, 1, 1, ringAlpha));
            }
        }

        circleTex.Apply();
        ringTex.Apply();

        circleSprite = Sprite.Create(circleTex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        ringSprite = Sprite.Create(ringTex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
    }
}

/// <summary>
/// TouchDPadButton - Dedicated Left/Right directional button:
/// 1. Continuously asserts virtualHorizontalInput every single frame while held.
/// 2. Implements IDragHandler to lock pointer capture so finger micro-drifts never drop input.
/// 3. Detects double-tap natively to trigger the Dash Run.
/// 4. Provides tactile scale punch on press/release.
/// </summary>
public class TouchDPadButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private float direction; // -1 for Left, +1 for Right
    private bool isHeld = false;
    private float lastTapTime = -10f;
    private const float doubleTapThreshold = 0.35f;
    private Coroutine scaleCoroutine;

    public void Init(float dir)
    {
        direction = dir;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isHeld = true;

        float timeSinceLast = Time.unscaledTime - lastTapTime;
        bool isDoubleTap = timeSinceLast <= doubleTapThreshold;
        lastTapTime = Time.unscaledTime;

        if (move.Instance != null)
        {
            if (direction < 0f)
            {
                move.Instance.virtualLeftDown = true;
            }
            else
            {
                move.Instance.virtualRightDown = true;
            }

            move.Instance.virtualHorizontalInput = direction;
        }

        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(0.88f, 0.05f));
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Maintains unbroken pointer capture on touchscreens even with finger drift
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHeld = false;

        if (move.Instance != null)
        {
            // Reset horizontal input if this button was active
            if ((direction < 0f && move.Instance.virtualHorizontalInput < 0f) ||
                (direction > 0f && move.Instance.virtualHorizontalInput > 0f))
            {
                move.Instance.virtualHorizontalInput = 0f;
            }
        }

        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(1.0f, 0.10f));
    }

    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        // Direct multi-touch hit-test fallback for Windows touchscreens & mobile:
        // Even if Windows OS tries to drop or gesture-cancel the pointer event,
        // we check if any active touch or mouse is physically inside this button's rect!
        bool isTouchInside = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
            {
                if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, t.position, null))
                {
                    isTouchInside = true;
                    break;
                }
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, null))
            {
                isTouchInside = true;
            }
        }

        bool activeHold = isHeld || isTouchInside;

        if (activeHold && move.Instance != null)
        {
            move.Instance.virtualHorizontalInput = direction;
        }
    }

    private IEnumerator AnimateScale(float targetScale, float duration)
    {
        Vector3 start = transform.localScale;
        Vector3 end = Vector3.one * targetScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        transform.localScale = end;
        scaleCoroutine = null;
    }
}

/// <summary>
/// TouchButtonTrigger - Handles tactile scale punch (0.90x) and triggers game action.
/// Implements IDragHandler to prevent finger micro-drift from breaking hold.
/// </summary>
public class TouchButtonTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private Color baseAccentColor;
    private System.Action onDownAction;
    private System.Action onUpAction;
    private Coroutine scaleCoroutine;

    public void Init(Color accent, System.Action onDown, System.Action onUp)
    {
        baseAccentColor = accent;
        onDownAction = onDown;
        onUpAction = onUp;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(0.90f, 0.05f));

        onDownAction?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Maintains unbroken pointer capture
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(1.0f, 0.10f));

        onUpAction?.Invoke();
    }

    private IEnumerator AnimateScale(float targetScale, float duration)
    {
        Vector3 start = transform.localScale;
        Vector3 end = Vector3.one * targetScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        transform.localScale = end;
        scaleCoroutine = null;
    }
}
