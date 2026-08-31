using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// TouchControlsManager - Complete Cyber-Gothic On-Screen Mobile Touch Controller:
/// 1. Virtual Analog Joystick (Left Thumb): Dynamic/Anchored directional thumbstick.
/// 2. Action Cluster (Right Thumb): Glassmorphic buttons for Attack, Jump, Dash, Blob, Magic, Spear.
/// 3. Utility Buttons: Pause (top-right) & Contextual Interact (floating prompt).
/// 4. Tactile Micro-Animations: Scale-punch (0.92x) and glow-flare on touch down.
/// 5. Smart Platform Detection: Automatically active on Mobile and testable in Editor with mouse.
/// </summary>
public class TouchControlsManager : MonoBehaviour
{
    public static TouchControlsManager Instance { get; private set; }

    [Header("Visibility Settings")]
    [Tooltip("Enables touch controls inside the Unity Editor for instant mouse playtesting.")]
    public bool forceEnableInEditor = true;

    [Header("Joystick Settings")]
    public float joystickRadius = 85f;
    public float deadzone = 0.12f;

    // Runtime UI elements
    private Canvas touchCanvas;
    private GameObject joystickBaseObj;
    private RectTransform joystickBaseRT;
    private RectTransform joystickHandleRT;

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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
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
            touchCanvas.gameObject.SetActive(scene.name != "MainMenu");
        }
    }

    void Update()
    {
        // Keep visibility synced with current platform/settings
        bool shouldBeVisible = (Application.isMobilePlatform || forceEnableInEditor) && 
                               SceneManager.GetActiveScene().name != "MainMenu";

        // Hide during active pause menu to keep screen clear
        if (PauseMenu.Instance != null && PauseMenu.Instance.isPaused)
        {
            shouldBeVisible = false;
        }

        if (touchCanvas != null && touchCanvas.gameObject.activeSelf != shouldBeVisible)
        {
            touchCanvas.gameObject.SetActive(shouldBeVisible);
        }
    }

    void BuildTouchUI()
    {
        EnsureEventSystem();
        GenerateProceduralSprites();

        // 1. Create Canvas
        GameObject canvasGO = new GameObject("TouchControlsCanvas");
        canvasGO.transform.SetParent(transform);
        touchCanvas = canvasGO.AddComponent<Canvas>();
        touchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        touchCanvas.sortingOrder = 95; // Just below PauseMenu (100) and above gameplay HUD

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 2. Build Virtual Joystick (Bottom Left)
        CreateVirtualJoystick(canvasGO.transform);

        // 3. Build Action Button Cluster (Bottom Right)
        CreateActionCluster(canvasGO.transform);

        // 4. Build Utility Buttons (Top Right & Interact)
        CreateUtilityButtons(canvasGO.transform);
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

    void CreateVirtualJoystick(Transform parent)
    {
        joystickBaseObj = new GameObject("VirtualJoystickBase");
        joystickBaseObj.transform.SetParent(parent, false);
        joystickBaseRT = joystickBaseObj.AddComponent<RectTransform>();
        joystickBaseRT.anchorMin = new Vector2(0, 0);
        joystickBaseRT.anchorMax = new Vector2(0, 0);
        joystickBaseRT.pivot = new Vector2(0.5f, 0.5f);
        joystickBaseRT.anchoredPosition = new Vector2(250, 240);
        joystickBaseRT.sizeDelta = new Vector2(210, 210);

        Image baseImg = joystickBaseObj.AddComponent<Image>();
        baseImg.sprite = ringSprite;
        baseImg.color = new Color(0.04f, 0.85f, 1.0f, 0.35f);

        GameObject handleObj = new GameObject("JoystickHandle");
        handleObj.transform.SetParent(joystickBaseObj.transform, false);
        joystickHandleRT = handleObj.AddComponent<RectTransform>();
        joystickHandleRT.anchorMin = new Vector2(0.5f, 0.5f);
        joystickHandleRT.anchorMax = new Vector2(0.5f, 0.5f);
        joystickHandleRT.pivot = new Vector2(0.5f, 0.5f);
        joystickHandleRT.anchoredPosition = Vector2.zero;
        joystickHandleRT.sizeDelta = new Vector2(90, 90);

        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.sprite = circleSprite;
        handleImg.color = new Color(0.1f, 0.95f, 1.0f, 0.75f);

        TouchJoystickHandler handler = joystickBaseObj.AddComponent<TouchJoystickHandler>();
        handler.Init(this, joystickHandleRT, joystickRadius, deadzone);
    }

    public void OnJoystickDragged(Vector2 normalizedInput)
    {
        if (move.Instance != null)
        {
            move.Instance.virtualHorizontalInput = normalizedInput.x;

            if (normalizedInput.y < -0.65f)
            {
                move.Instance.virtualBlobPressed = true;
            }
            else
            {
                move.Instance.virtualBlobPressed = false;
            }
        }
    }

    public void OnJoystickReleased()
    {
        if (move.Instance != null)
        {
            move.Instance.virtualHorizontalInput = 0f;
            move.Instance.virtualBlobPressed = false;
        }
    }

    void CreateActionCluster(Transform parent)
    {
        // 1. ATTACK (J)
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

        // 2. JUMP (Space)
        CreateTouchButton(parent, "TouchBtn_Jump", 
            new Vector2(-95, 130), new Vector2(120, 120), 
            new Color(0.0f, 0.92f, 1.0f, 0.85f), "▲\nJUMP", 22,
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

        // 3. DASH (Shift)
        CreateTouchButton(parent, "TouchBtn_Dash", 
            new Vector2(-350, 130), new Vector2(95, 95), 
            new Color(0.1f, 0.82f, 1.0f, 0.80f), "💨\nDASH", 18,
            onDown: () => {
                if (move.Instance != null)
                {
                    move.Instance.virtualDashPressed = true;
                }
            }
        );

        // 4. BLOB (B)
        CreateTouchButton(parent, "TouchBtn_Blob", 
            new Vector2(-220, 395), new Vector2(90, 90), 
            new Color(0.72f, 0.25f, 1.0f, 0.80f), "💧\nBLOB", 18,
            onDown: () => {
                if (move.Instance != null)
                {
                    move.Instance.virtualBlobPressed = !move.Instance.virtualBlobPressed;
                }
            }
        );

        // 5. MAGIC (K)
        CreateTouchButton(parent, "TouchBtn_Magic", 
            new Vector2(-355, 265), new Vector2(90, 90), 
            new Color(1.0f, 0.68f, 0.1f, 0.80f), "✨\nMAGIC", 18,
            onDown: () => {
                if (MageCombat.Instance != null)
                {
                    MageCombat.Instance.TriggerRangedAttackFromTouch();
                }
            }
        );

        // 6. SPEAR (Q / X)
        CreateTouchButton(parent, "TouchBtn_Spear", 
            new Vector2(-95, 285), new Vector2(90, 90), 
            new Color(0.2f, 0.98f, 0.95f, 0.80f), "🔱\nSPEAR", 18,
            onDown: () => {
                if (LumiSpearWeapon.Instance != null)
                {
                    LumiSpearWeapon.Instance.TriggerSpearActionFromTouch();
                }
            }
        );
    }

    void CreateUtilityButtons(Transform parent)
    {
        // 1. PAUSE
        CreateTouchButton(parent, "TouchBtn_Pause", 
            new Vector2(-70, -70), new Vector2(70, 70), 
            new Color(0.25f, 0.85f, 1.0f, 0.75f), "⏸", 28,
            anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1),
            onDown: () => {
                if (PauseMenu.Instance != null)
                {
                    PauseMenu.Instance.TogglePause();
                }
            }
        );

        // 2. INTERACT
        CreatePillButton(parent, "TouchBtn_Interact",
            new Vector2(-220, 520), new Vector2(170, 55),
            new Color(1.0f, 0.82f, 0.2f, 0.85f), "💬 INTERACT (E)", 16,
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

        GameObject ringObj = new GameObject("AccentRing");
        ringObj.transform.SetParent(btnObj.transform, false);
        RectTransform ringRT = ringObj.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero;
        ringRT.anchorMax = Vector2.one;
        ringRT.sizeDelta = Vector2.zero;
        Image ringImg = ringObj.AddComponent<Image>();
        ringImg.sprite = ringSprite;
        ringImg.color = accentColor;

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

public class TouchJoystickHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private TouchControlsManager manager;
    private RectTransform handleRT;
    private RectTransform baseRT;
    private float radius;
    private float deadzone;

    public void Init(TouchControlsManager mgr, RectTransform handle, float maxRadius, float deadzoneVal)
    {
        manager = mgr;
        handleRT = handle;
        baseRT = GetComponent<RectTransform>();
        radius = maxRadius;
        deadzone = deadzoneVal;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateHandle(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateHandle(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (handleRT != null) handleRT.anchoredPosition = Vector2.zero;
        if (manager != null) manager.OnJoystickReleased();
    }

    private void UpdateHandle(PointerEventData eventData)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRT, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            Vector2 offset = localPoint;
            float mag = offset.magnitude;
            if (mag > radius)
            {
                offset = offset.normalized * radius;
            }

            if (handleRT != null) handleRT.anchoredPosition = offset;

            Vector2 norm = offset / radius;
            if (norm.magnitude < deadzone)
            {
                norm = Vector2.zero;
            }

            if (manager != null) manager.OnJoystickDragged(norm);
        }
    }
}

public class TouchButtonTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
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
        scaleCoroutine = StartCoroutine(AnimateScale(0.90f, 0.06f));

        onDownAction?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(1.0f, 0.12f));

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
