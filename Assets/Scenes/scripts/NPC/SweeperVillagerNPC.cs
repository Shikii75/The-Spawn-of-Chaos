using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SweeperVillagerNPC - Atmospheric villager NPC for Cherry Blossom Forest.
/// Continuously sweeps fallen petals with a broom and shares ambient rumors with the player.
///
/// Features:
/// - Smooth looping animation of broom sweeping frames.
/// - Self-contained world-space speech bubble with character badge, typing effect, and [E] prompt.
/// - Atmospheric dialogue sharing village gossip about rival clans and mountain spiders.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Spawn of Chaos/NPC/Sweeper Villager NPC")]
public class SweeperVillagerNPC : MonoBehaviour
{
    public static SweeperVillagerNPC Instance { get; private set; }

    [Header("── Character Identity ──────────────────────────────────────")]
    public string characterName = "Tessai";
    public string characterTitle = "✦ Village Groundskeeper";
    public Color nameColor = new Color(1.00f, 0.78f, 0.45f, 1f);       // Warm amber
    public Color titleColor = new Color(0.85f, 0.70f, 0.55f, 0.85f);    // Soft earthy bronze
    public Color bodyTextColor = new Color(0.98f, 0.95f, 0.90f, 1f);   // Warm parchment white
    public Color bubbleBgColor = new Color(0.12f, 0.08f, 0.05f, 0.95f); // Deep lacquered wood/charcoal

    [Header("── Ambient Dialogue Lines ─────────────────────────────────")]
    [TextArea(2, 5)]
    public string[] dialogueLines = new string[]
    {
        "Sweep, sweep... Thick spider webs have started covering the mountain trail overnight.",
        "They're sticky, heavy strands... whatever is spinning them up near the caves is far too big to be a normal spider.",
        "Watch your step out there, traveler. If you get tangled in those webs, you won't be coming back."
    };

    [Header("── Animation Settings ──────────────────────────────────────")]
    [Tooltip("Sweeping animation frames from Assets/Scenes/animations/frames/sweepervillager-525173f8")]
    public Sprite[] sweepFrames;

    [Tooltip("Target animation frame rate.")]
    public float fps = 14f;

    [Header("── Interaction ──────────────────────────────────────────")]
    public float interactRange = 3.5f;
    public KeyCode interactKey = KeyCode.E;
    public float typeSpeed = 0.025f;

    [Header("── Speech Bubble World Offset ─────────────────────────────")]
    public float bubbleHeightAboveNPC = 3.4f;
    public float bubbleWidth = 750f;
    public float bubbleHeight = 240f;

    // ── Internal Components & State ─────────────────────────────────────
    private SpriteRenderer _sr;
    private AudioSource _audioSource;
    private BoxCollider2D _collider;
    private Transform _playerTransform;

    private Coroutine _animRoutine;
    private Coroutine _typeRoutine;

    private bool _isPlayerNear;
    private bool _isOpen;
    private bool _isTyping;
    private int _lineIndex;
    private int _openedFrame;

    // Speech Bubble UI Objects (Auto-constructed)
    private Canvas _canvas;
    private RectTransform _bubbleRt;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;
    private GameObject _advanceBtn;
    private GameObject _promptGo;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        _sr = GetComponent<SpriteRenderer>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<BoxCollider2D>();

        if (_collider != null)
        {
            _collider.isTrigger = true;
            _collider.size = new Vector2(2.4f, 4.2f);
        }

        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0.5f;
        }

#if UNITY_EDITOR
        AutoLoadFramesIfEmpty();
#endif

        BuildSpeechBubbleUI();
    }

    private void Start()
    {
        LocatePlayer();

        if (_canvas != null && Camera.main != null)
        {
            _canvas.worldCamera = Camera.main;
        }

        SetBubbleActive(false);
        SetPromptActive(false);

        PlaySweepingAnimation();
    }

    private void Update()
    {
        UpdateProximity();
        HandleFacing();
        HandleInput();
        PulseAdvanceButton();
    }

    private void LocatePlayer()
    {
        if (_playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
            else
            {
                move m = UnityEngine.Object.FindFirstObjectByType<move>();
                if (m != null) _playerTransform = m.transform;
            }
        }
    }

    private void HandleFacing()
    {
        if (_playerTransform == null || _sr == null) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist <= interactRange + 2.0f)
        {
            bool playerIsLeft = _playerTransform.position.x < transform.position.x;
            _sr.flipX = playerIsLeft;
        }
    }

    private void UpdateProximity()
    {
        if (NyxarisManager.IsTyping || NyxarisManager.IsChatActive)
        {
            SetPromptActive(false);
            return;
        }

        LocatePlayer();
        if (_playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        bool near = dist <= interactRange;

        if (!near && _isOpen)
        {
            CloseDialogue();
        }

        if (near != _isPlayerNear)
        {
            _isPlayerNear = near;
            if (_isPlayerNear && !_isOpen)
            {
                SetPromptActive(true);
            }
            else if (!_isPlayerNear)
            {
                SetPromptActive(false);
            }
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            if (_isOpen)
            {
                if (_isTyping) SkipTyping();
                else AdvanceLine();
            }
            else if (_isPlayerNear)
            {
                OpenDialogue();
            }
        }
        else if (Input.GetKeyDown(KeyCode.Space) && _isOpen)
        {
            if (Time.frameCount == _openedFrame) return;

            if (_isTyping) SkipTyping();
            else AdvanceLine();
        }
    }

    public void OpenDialogue()
    {
        if (_isOpen) return;

        LocatePlayer();
        _isOpen = true;
        _lineIndex = 0;
        _openedFrame = Time.frameCount;

        SetPromptActive(false);
        SetBubbleActive(true);

        TypeCurrentLine();
    }

    private void TypeCurrentLine()
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeLineRoutine(dialogueLines[_lineIndex]));
    }

    private IEnumerator TypeLineRoutine(string fullText)
    {
        _isTyping = true;
        if (_advanceBtn != null) _advanceBtn.SetActive(false);
        if (_bodyText != null) _bodyText.text = "";

        for (int i = 0; i <= fullText.Length; i++)
        {
            if (_bodyText != null)
            {
                _bodyText.text = fullText.Substring(0, i);
            }
            yield return new WaitForSeconds(typeSpeed);
        }

        _isTyping = false;
        if (_advanceBtn != null) _advanceBtn.SetActive(true);
    }

    private void SkipTyping()
    {
        if (!_isTyping || dialogueLines == null || _lineIndex >= dialogueLines.Length) return;

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _isTyping = false;

        if (_bodyText != null)
        {
            _bodyText.text = dialogueLines[_lineIndex];
        }

        if (_advanceBtn != null) _advanceBtn.SetActive(true);
    }

    public void AdvanceLine()
    {
        _lineIndex++;
        if (dialogueLines != null && _lineIndex < dialogueLines.Length)
        {
            TypeCurrentLine();
        }
        else
        {
            CloseDialogue();
        }
    }

    public void CloseDialogue()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _isOpen = false;
        _isTyping = false;

        SetBubbleActive(false);
        if (_isPlayerNear)
        {
            SetPromptActive(true);
        }
    }

    public void PlaySweepingAnimation()
    {
        if (sweepFrames != null && sweepFrames.Length > 0)
        {
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(LoopFrameRoutine(sweepFrames));
        }
    }

    private IEnumerator LoopFrameRoutine(Sprite[] frames)
    {
        int index = 0;
        float frameDelay = 1f / Mathf.Max(1f, fps);

        while (true)
        {
            if (frames[index] != null && _sr != null)
            {
                _sr.sprite = frames[index];
            }

            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(frameDelay);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Load Sweep Frames")]
    public void AutoLoadFramesIfEmpty()
    {
        string folder = "Assets/Scenes/animations/frames/sweepervillager-525173f8";

        if (sweepFrames == null || sweepFrames.Length == 0)
        {
            sweepFrames = LoadSpritesSafely(folder);
        }

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && sweepFrames != null && sweepFrames.Length > 0)
        {
            _sr.sprite = sweepFrames[0];
        }

        EditorUtility.SetDirty(this);
    }

    private static Sprite[] LoadSpritesSafely(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (!Directory.Exists(folderPath))
        {
            return new Sprite[0];
        }

        var pngFiles = Directory.GetFiles(folderPath, "*.png")
            .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        var result = new List<Sprite>();

        foreach (var file in pngFiles)
        {
            FileInfo fi = new FileInfo(file);
            if (fi.Length < 5000) continue;

            string unityPath = file.Replace("\\", "/");
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(unityPath);
            var sprites = allAssets.OfType<Sprite>().ToList();

            if (sprites.Count > 0)
            {
                Sprite mainSprite = sprites.OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
                if (mainSprite != null)
                {
                    result.Add(mainSprite);
                }
            }
        }

        return result.ToArray();
    }
#endif

    private void BuildSpeechBubbleUI()
    {
        var canvasGo = new GameObject("[SweeperSpeechBubble]");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 70;

        canvasGo.AddComponent<GraphicRaycaster>();

        const float PPU = 55f;
        var canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(1000f, 700f);
        canvasRt.localScale = Vector3.one / PPU;
        canvasRt.localPosition = new Vector3(0f, bubbleHeightAboveNPC, -0.1f);

        // Bubble Panel
        var panelGo = new GameObject("BubblePanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);

        _bubbleRt = panelGo.GetComponent<RectTransform>();
        _bubbleRt.anchorMin = new Vector2(0.5f, 0.5f);
        _bubbleRt.anchorMax = new Vector2(0.5f, 0.5f);
        _bubbleRt.pivot = new Vector2(0.5f, 0.5f);
        _bubbleRt.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);
        _bubbleRt.anchoredPosition = Vector2.zero;

        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = bubbleBgColor;
        panelImg.sprite = UIFactory.GetRoundedSprite();
        panelImg.type = Image.Type.Sliced;

        var outline = panelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.65f, 0.35f, 0.75f); // Amber outline
        outline.effectDistance = new Vector2(2f, 2f);

        // Accent Line
        var accentGo = new GameObject("AccentLine", typeof(RectTransform));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(-16f, 3f);
        accentRt.anchoredPosition = new Vector2(0f, -4f);
        var accentImg = accentGo.AddComponent<Image>();
        accentImg.color = new Color(1.0f, 0.78f, 0.40f, 0.95f);

        // Tail
        var tailGo = new GameObject("Tail", typeof(RectTransform));
        tailGo.transform.SetParent(panelGo.transform, false);
        var tailRt = tailGo.GetComponent<RectTransform>();
        tailRt.anchorMin = new Vector2(0.18f, 0f);
        tailRt.anchorMax = new Vector2(0.18f, 0f);
        tailRt.pivot = new Vector2(0.5f, 0.5f);
        tailRt.sizeDelta = new Vector2(28f, 28f);
        tailRt.anchoredPosition = new Vector2(0f, -12f);
        tailRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var tailImg = tailGo.AddComponent<Image>();
        tailImg.color = bubbleBgColor;

        // Name Badge
        var badgeGo = new GameObject("NameBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(panelGo.transform, false);
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 1f);
        badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(0f, 1f);
        badgeRt.sizeDelta = new Vector2(0f, 44f);
        badgeRt.anchoredPosition = new Vector2(24f, -10f);

        _titleText = badgeGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(_titleText);
        _titleText.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(nameColor)}>{characterName}</color>  <size=75%><color=#{ColorUtility.ToHtmlStringRGBA(titleColor)}>{characterTitle}</color></size>";
        _titleText.fontSize = 30f;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.alignment = TextAlignmentOptions.Left;

        // Body Text
        var bodyGo = new GameObject("BodyText", typeof(RectTransform));
        bodyGo.transform.SetParent(panelGo.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(24f, 20f);
        bodyRt.offsetMax = new Vector2(-70f, -48f);

        _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(_bodyText);
        _bodyText.text = "";
        _bodyText.color = bodyTextColor;
        _bodyText.fontSize = 28f;
        _bodyText.enableWordWrapping = true;
        _bodyText.overflowMode = TextOverflowModes.Truncate;

        // Advance Button
        _advanceBtn = new GameObject("AdvanceBtn", typeof(RectTransform));
        _advanceBtn.transform.SetParent(panelGo.transform, false);
        var advRt = _advanceBtn.GetComponent<RectTransform>();
        advRt.anchorMin = new Vector2(1f, 0f);
        advRt.anchorMax = new Vector2(1f, 0f);
        advRt.pivot = new Vector2(1f, 0f);
        advRt.sizeDelta = new Vector2(50f, 50f);
        advRt.anchoredPosition = new Vector2(-14f, 14f);

        var advImg = _advanceBtn.AddComponent<Image>();
        advImg.color = new Color(0.45f, 0.30f, 0.15f, 0.95f);
        advImg.sprite = UIFactory.GetRoundedSprite();
        advImg.type = Image.Type.Sliced;

        var advBtnComp = _advanceBtn.AddComponent<Button>();
        advBtnComp.onClick.AddListener(() =>
        {
            if (_isTyping) SkipTyping();
            else AdvanceLine();
        });

        var arrGo = new GameObject("Arrow", typeof(RectTransform));
        arrGo.transform.SetParent(_advanceBtn.transform, false);
        var arrRt = arrGo.GetComponent<RectTransform>();
        arrRt.anchorMin = Vector2.zero;
        arrRt.anchorMax = Vector2.one;
        arrRt.sizeDelta = Vector2.zero;
        arrRt.anchoredPosition = Vector2.zero;

        var arrTxt = arrGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(arrTxt);
        arrTxt.text = "▶";
        arrTxt.color = new Color(1.0f, 0.90f, 0.70f, 1f);
        arrTxt.fontSize = 24f;
        arrTxt.alignment = TextAlignmentOptions.Center;

        _advanceBtn.SetActive(false);

        // Proximity Prompt ("[E] Talk")
        _promptGo = new GameObject("Prompt", typeof(RectTransform));
        _promptGo.transform.SetParent(canvasGo.transform, false);
        var prt = _promptGo.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(240f, 50f);
        prt.anchoredPosition = new Vector2(0f, 28f);

        var promptImg = _promptGo.AddComponent<Image>();
        promptImg.color = new Color(0.14f, 0.09f, 0.05f, 0.92f);
        promptImg.sprite = UIFactory.GetRoundedSprite();
        promptImg.type = Image.Type.Sliced;

        var promptOutline = _promptGo.AddComponent<Outline>();
        promptOutline.effectColor = new Color(0.90f, 0.70f, 0.35f, 0.70f);
        promptOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var ptxtGo = new GameObject("PromptText", typeof(RectTransform));
        ptxtGo.transform.SetParent(_promptGo.transform, false);
        var ptrt = ptxtGo.GetComponent<RectTransform>();
        ptrt.anchorMin = Vector2.zero;
        ptrt.anchorMax = Vector2.one;
        ptrt.sizeDelta = Vector2.zero;
        ptrt.anchoredPosition = Vector2.zero;

        var ptxt = ptxtGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(ptxt);
        ptxt.text = $"[{interactKey}]  Talk";
        ptxt.color = new Color(1.0f, 0.85f, 0.60f, 1f);
        ptxt.fontSize = 24f;
        ptxt.alignment = TextAlignmentOptions.Center;
        ptxt.fontStyle = FontStyles.Bold;
    }

    private static void ApplyTMPDefaults(TMP_Text text)
    {
        if (text == null) return;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.richText = true;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) text.font = font;
    }

    private void PulseAdvanceButton()
    {
        if (_advanceBtn == null || !_advanceBtn.activeSelf) return;

        float s = 1f + Mathf.PingPong(Time.time * 1.5f, 0.12f);
        _advanceBtn.transform.localScale = new Vector3(s, s, 1f);
    }

    private void SetBubbleActive(bool active)
    {
        if (_bubbleRt != null) _bubbleRt.gameObject.SetActive(active);
    }

    private void SetPromptActive(bool active)
    {
        if (_promptGo != null) _promptGo.SetActive(active);
    }
}
