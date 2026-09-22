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
/// DaraNPC - Follower of Nyxaris searching for her missing husband.
/// Polite, funny, modern speaker who gives the player the vital lead to investigate
/// the Cherry Blossom Forest rival clans (Strawhat Clan in Dojo 1 vs Samurai Clan in Dojo 2).
///
/// Mechanics:
/// - Idle animation: Smoothly loops frames from Assets/Scenes/animations/frames/Dara/daraidle-8c309fd7/
/// - Talk animation: Smoothly loops frames from the latest folder Assets/Scenes/animations/frames/Dara/daratalk1-b881739e/
/// - Telepathy Orb requirement:
///     - If the player interacts WITHOUT the Telepathy Orb:
///         Plays a confused mage voice audio clip (mage_confused_01, mage_huh-_02, etc.),
///         plays the talk animation from the latest folder,
///         and displays confused/unintelligible thoughts explaining the player needs the Telepathy Orb.
///     - If the player HAS the Telepathy Orb:
///         Speaks in clear, modern, humorous dialogue directing the player towards the mountain dojos and caves.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Spawn of Chaos/NPC/Dara NPC")]
public class DaraNPC : MonoBehaviour
{
    public static DaraNPC Instance { get; private set; }

    [Header("── Character Identity ──────────────────────────────────────")]
    public string characterName = "Dara";
    public string characterTitle = "✦ Follower of Nyxaris";
    public Color nameColor = new Color(0.86f, 0.62f, 1.00f, 1f);       // Ethereal lilac/violet
    public Color titleColor = new Color(0.68f, 0.55f, 0.90f, 0.85f);    // Soft lavender
    public Color bodyTextColor = new Color(0.96f, 0.94f, 1.00f, 1f);   // Bright soft-white
    public Color bubbleBgColor = new Color(0.07f, 0.04f, 0.14f, 0.95f); // Deep dark purple

    [Header("── Telepathy Dialogue (Has Telepathy Orb) ───────────────")]
    [Tooltip("Delivered in clear, modern, witty speech when player owns the Telepathy Orb.")]
    [TextArea(2, 5)]
    public string[] telepathyDialogueLines = new string[]
    {
        "Oh! Wait—you can actually hear my thoughts?! Okay, huge relief. Hi! I'm Dara. Devoted follower of Lady Nyxaris, though currently… an extremely stressed-out wife.",
        "You must be the warrior Nyxaris sent to investigate the massacre of our people. I felt that dark shockwave rip through the realm. Honestly? It broke my heart...",
        "If you're looking for answers on who butchered our brothers and sisters, you need to head straight into the Cherry Blossom Forest in the mountains.",
        "There are two rival clans up there who despise each other: the Strawhat Clan in Dojo 1 and the Samurai Clan in Dojo 2. Word on the street is the killer is a high-ranking warrior belonging to one of those clans!",
        "Which brings me to my missing hubby… That adorable idiot went up the mountain three days ago with a bamboo stick to investigate the giant spiders swarming around the regional caves, and hasn't returned!",
        "No note, no text, nothing! Classic him, zero self-preservation. If you head up to the dojos or delve into that creepy cave, could you pretty please keep an eye out for him? And kick whoever started this into next week!"
    };

    [Header("── Confused Dialogue (Lacks Telepathy Orb) ─────────────")]
    [Tooltip("Spoken when player interacts without the Telepathy Orb. Accompanied by confused mage voice.")]
    [TextArea(2, 5)]
    public string[] confusedDialogueLines = new string[]
    {
        "…えっ？ 何言ってるのか全然分かんないんだけど…？\n<color=#D47BFF><i>[She tilts her head in utter confusion, mumbling in an unfamiliar tongue...]</i></color>",
        "（困惑したように身振り手振りしている… 言葉が通じないようだ。）\n<color=#D47BFF><i>[Her celestial mindwaves are out of phase! You need the Telepathy Orb to decipher her thoughts.]</i></color>",
        "…あの、Nyxaris様のお告げ…？ ううん、ごめんね、全然聞き取れないよぉ…\n<color=#D47BFF><i>[Search the region for the Telepathy Orb to understand Nyxaris's followers.]</i></color>"
    };

    [Header("── Animation Frame Sets ───────────────────────────────────")]
    [Tooltip("27 frames from Assets/Scenes/animations/frames/Dara/daraidle-8c309fd7")]
    public Sprite[] idleFrames;

    [Tooltip("17 frames from the latest folder Assets/Scenes/animations/frames/Dara/daratalk1-b881739e")]
    public Sprite[] talkFrames;

    [Tooltip("Target animation frame rate.")]
    public float fps = 18f;

    [Header("── Audio Settings ─────────────────────────────────────────")]
    [Tooltip("Confused mage audio clips played when interacting without Telepathy Orb.")]
    public AudioClip[] confusedMageClips;

    [Tooltip("Typewriter character click/beep sound.")]
    public AudioClip textBeepSound;

    [Range(0.5f, 2.0f)]
    public float voicePitch = 1.15f;

    [Header("── Interaction ──────────────────────────────────────────")]
    public float interactRange = 3.5f;
    public KeyCode interactKey = KeyCode.E;
    public float typeSpeed = 0.025f;

    [Header("── Speech Bubble World Offset ─────────────────────────────")]
    public float bubbleHeightAboveNPC = 3.6f;
    public float bubbleWidth = 850f;
    public float bubbleHeight = 280f;

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
    private string[] _activeLines;

    // Speech Bubble UI Objects (Auto-constructed)
    private Canvas _canvas;
    private RectTransform _bubbleRt;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;
    private GameObject _advanceBtn;
    private GameObject _promptGo;

    private enum AnimState { Idle, Talking }
    private AnimState _currentAnimState = AnimState.Idle;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        _sr = GetComponent<SpriteRenderer>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<BoxCollider2D>();

        // Ensure collider is a trigger
        if (_collider != null)
        {
            _collider.isTrigger = true;
            if (_collider.size == Vector2.one)
            {
                _collider.size = new Vector2(2.6f, 4.6f);
            }
        }

        // Configure audio source
        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0.5f;
        }

        // Auto-load audio and frames if empty
        EnsureAudioClipsLoaded();
#if UNITY_EDITOR
        AutoLoadFramesIfEmpty();
#endif

        // Build self-contained world-space speech bubble UI
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

        // Start idle animation
        PlayIdleAnimation();
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
            // Dara's sprite faces right by default. FlipX when player is to the left.
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

        _isPlayerNear = near;
        SetPromptActive(near && !_isOpen);
    }

    private void HandleInput()
    {
        if (NyxarisManager.IsTyping || NyxarisManager.IsChatActive) return;

        // Open dialogue
        if (_isPlayerNear && !_isOpen && Input.GetKeyDown(interactKey))
        {
            OpenDialogue();
            return;
        }

        if (!_isOpen) return;
        if (Time.frameCount == _openedFrame) return;

        // Advance line or skip typewriter
        if (Input.GetKeyDown(interactKey) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (_isTyping)
            {
                SkipTyping();
            }
            else
            {
                AdvanceLine();
            }
        }

        // Close on Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseDialogue();
        }
    }

    // ── Dialogue Execution ──────────────────────────────────────────────

    public void OpenDialogue()
    {
        _isOpen = true;
        _openedFrame = Time.frameCount;
        _lineIndex = 0;

        SetPromptActive(false);
        SetBubbleActive(true);

        // Check Telepathy Orb requirement
        bool hasOrb = SpawnOfChaos.Systems.TelepathyOrbSystem.HasTelepathyOrb;

        if (hasOrb)
        {
            // Player understands Dara
            _activeLines = telepathyDialogueLines;
        }
        else
        {
            // Player lacks Telepathy Orb: play confused audio and show confused dialogue
            _activeLines = confusedDialogueLines;
            PlayConfusedMageVoice();
            TriggerPlayerConfusedAnimation();
        }

        if (_activeLines == null || _activeLines.Length == 0)
        {
            _activeLines = new string[] { "..." };
        }

        // Start talk animation using the latest frame folder
        PlayTalkingAnimation();

        // Begin typewriter on line 0
        TypeCurrentLine();
    }

    private void PlayConfusedMageVoice()
    {
        if (_audioSource == null) return;

        if (confusedMageClips == null || confusedMageClips.Length == 0)
        {
            EnsureAudioClipsLoaded();
        }

        if (confusedMageClips != null && confusedMageClips.Length > 0)
        {
            AudioClip clip = confusedMageClips[UnityEngine.Random.Range(0, confusedMageClips.Length)];
            if (clip != null)
            {
                _audioSource.pitch = voicePitch;
                _audioSource.PlayOneShot(clip, 1.0f);
            }
        }
    }

    private void TriggerPlayerConfusedAnimation()
    {
        if (_playerTransform == null)
        {
            LocatePlayer();
        }

        if (_playerTransform == null) return;

        GameObject playerGO = _playerTransform.gameObject;
        var voiceCtrl = playerGO.GetComponent<SpawnOfChaos.Entities.PlayerMageVoiceController>();
        if (voiceCtrl == null)
        {
            voiceCtrl = SpawnOfChaos.Entities.PlayerMageVoiceController.EnsureAttached(playerGO);
        }

        if (voiceCtrl != null)
        {
            voiceCtrl.PlayConfusedVoice();
        }
    }

    private void TypeCurrentLine()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        if (_lineIndex < 0 || _lineIndex >= _activeLines.Length) return;

        string line = _activeLines[_lineIndex];
        _typeRoutine = StartCoroutine(TypeRoutine(line));
    }

    private IEnumerator TypeRoutine(string line)
    {
        _isTyping = true;
        _bodyText.text = "";
        SetAdvanceActive(false);

        // Keep talking animation going during speech
        PlayTalkingAnimation();

        bool insideTag = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            // Handle rich text tags instantaneously without delay
            if (c == '<') insideTag = true;
            _bodyText.text += c;
            if (c == '>') insideTag = false;

            if (insideTag) continue;

            // Character beep
            if (textBeepSound != null && _audioSource != null && !char.IsWhiteSpace(c))
            {
                _audioSource.pitch = voicePitch + UnityEngine.Random.Range(-0.05f, 0.05f);
                _audioSource.PlayOneShot(textBeepSound, 0.35f);
            }

            yield return new WaitForSeconds(typeSpeed);
        }

        _isTyping = false;
        SetAdvanceActive(true);
    }

    private void SkipTyping()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        if (_lineIndex < _activeLines.Length)
        {
            _bodyText.text = _activeLines[_lineIndex];
        }
        _isTyping = false;
        SetAdvanceActive(true);
    }

    private void AdvanceLine()
    {
        _lineIndex++;
        if (_lineIndex < _activeLines.Length)
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

        // Return to idle animation loop
        PlayIdleAnimation();
    }

    // ── Animation Sequencer ─────────────────────────────────────────────

    public void PlayIdleAnimation()
    {
        _currentAnimState = AnimState.Idle;
        if (idleFrames != null && idleFrames.Length > 0)
        {
            PlayLoopingSequence(idleFrames);
        }
    }

    public void PlayTalkingAnimation()
    {
        if (_currentAnimState == AnimState.Talking && _animRoutine != null) return;
        _currentAnimState = AnimState.Talking;

        if (talkFrames != null && talkFrames.Length > 0)
        {
            PlayLoopingSequence(talkFrames);
        }
    }

    private void PlayLoopingSequence(Sprite[] frames)
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        if (frames == null || frames.Length == 0) return;
        _animRoutine = StartCoroutine(LoopFrameRoutine(frames));
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

    // ── Frame & Audio Auto-Loading ──────────────────────────────────────

    public void EnsureAudioClipsLoaded()
    {
        if (confusedMageClips == null || confusedMageClips.Length == 0 || confusedMageClips.Any(c => c == null))
        {
            var loaded = Resources.LoadAll<AudioClip>("Voice/mage/confused");
            if (loaded != null && loaded.Length > 0)
            {
                confusedMageClips = loaded;
            }
#if UNITY_EDITOR
            if (confusedMageClips == null || confusedMageClips.Length == 0)
            {
                string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Voice/mage/confused", "Assets/Resources/Voice/mage/confused" });
                confusedMageClips = audioGuids
                    .Select(g => AssetDatabase.GUIDToAssetPath(g))
                    .Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p))
                    .Where(c => c != null)
                    .Distinct()
                    .ToArray();
            }
#endif
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Load Frames and Audio")]
    public void AutoLoadFramesIfEmpty()
    {
        EnsureAudioClipsLoaded();

        string idleFolder = "Assets/Scenes/animations/frames/Dara/daraidle-8c309fd7";
        string talkFolder = "Assets/Scenes/animations/frames/Dara/daratalk1-b881739e";

        if (idleFrames == null || idleFrames.Length == 0)
        {
            idleFrames = LoadSpritesSafely(idleFolder);
        }

        if (talkFrames == null || talkFrames.Length == 0)
        {
            talkFrames = LoadSpritesSafely(talkFolder);
        }

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && idleFrames != null && idleFrames.Length > 0)
        {
            _sr.sprite = idleFrames[0];
        }

        EditorUtility.SetDirty(this);
    }

    private static Sprite[] LoadSpritesSafely(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[DaraNPC] Folder not found: {folderPath}");
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
            // Skip tiny empty/placeholder frames (< 5 KB like frame_001.png)
            if (fi.Length < 5000) continue;

            string unityPath = file.Replace("\\", "/");
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(unityPath);
            var sprites = allAssets.OfType<Sprite>().ToList();

            if (sprites.Count > 0)
            {
                // Select the character sprite with the largest rect area to ignore stray sliced pixels
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

    // ── Speech Bubble UI Construction ───────────────────────────────────

    private void BuildSpeechBubbleUI()
    {
        // 1. World-Space Canvas parented to Dara
        var canvasGo = new GameObject("[DaraSpeechBubble]");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 70; // Always render above sprites

        canvasGo.AddComponent<GraphicRaycaster>();

        // 55 px = 1 Unity world unit for high readability
        const float PPU = 55f;
        var canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(1000f, 700f);
        canvasRt.localScale = Vector3.one / PPU;
        canvasRt.localPosition = new Vector3(0f, bubbleHeightAboveNPC, -0.1f);

        // 2. Bubble Container Panel
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

        // Glowing Border Outline
        var outline = panelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.45f, 1.0f, 0.75f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Top Accent Neon Glow Line
        var accentGo = new GameObject("AccentLine", typeof(RectTransform));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(-16f, 3f);
        accentRt.anchoredPosition = new Vector2(0f, -4f);
        var accentImg = accentGo.AddComponent<Image>();
        accentImg.color = new Color(0.85f, 0.45f, 1.0f, 0.95f);

        // Speech Bubble Pointer Tail
        var tailGo = new GameObject("Tail", typeof(RectTransform));
        tailGo.transform.SetParent(panelGo.transform, false);
        var tailRt = tailGo.GetComponent<RectTransform>();
        tailRt.anchorMin = new Vector2(0.18f, 0f);
        tailRt.anchorMax = new Vector2(0.18f, 0f);
        tailRt.pivot = new Vector2(0.5f, 0.5f);
        tailRt.sizeDelta = new Vector2(32f, 32f);
        tailRt.anchoredPosition = new Vector2(0f, -14f);
        tailRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var tailImg = tailGo.AddComponent<Image>();
        tailImg.color = bubbleBgColor;

        // 3. Name & Title Badge
        var badgeGo = new GameObject("NameBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(panelGo.transform, false);
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 1f);
        badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(0f, 1f);
        badgeRt.sizeDelta = new Vector2(0f, 48f);
        badgeRt.anchoredPosition = new Vector2(26f, -12f);

        _titleText = badgeGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(_titleText);
        _titleText.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(nameColor)}>{characterName}</color>  <size=75%><color=#{ColorUtility.ToHtmlStringRGBA(titleColor)}>{characterTitle}</color></size>";
        _titleText.fontSize = 32f;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.alignment = TextAlignmentOptions.Left;

        // 4. Dialogue Body Text
        var bodyGo = new GameObject("BodyText", typeof(RectTransform));
        bodyGo.transform.SetParent(panelGo.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(26f, 24f);
        bodyRt.offsetMax = new Vector2(-75f, -54f);

        _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        ApplyTMPDefaults(_bodyText);
        _bodyText.text = "";
        _bodyText.color = bodyTextColor;
        _bodyText.fontSize = 30f;
        _bodyText.enableWordWrapping = true;
        _bodyText.overflowMode = TextOverflowModes.Truncate;

        // 5. Advance Button (Right-aligned circle)
        _advanceBtn = new GameObject("AdvanceBtn", typeof(RectTransform));
        _advanceBtn.transform.SetParent(panelGo.transform, false);
        var advRt = _advanceBtn.GetComponent<RectTransform>();
        advRt.anchorMin = new Vector2(1f, 0f);
        advRt.anchorMax = new Vector2(1f, 0f);
        advRt.pivot = new Vector2(1f, 0f);
        advRt.sizeDelta = new Vector2(56f, 56f);
        advRt.anchoredPosition = new Vector2(-16f, 16f);

        var advImg = _advanceBtn.AddComponent<Image>();
        advImg.color = new Color(0.35f, 0.18f, 0.55f, 0.95f);
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
        arrTxt.color = new Color(0.95f, 0.85f, 1f, 1f);
        arrTxt.fontSize = 26f;
        arrTxt.alignment = TextAlignmentOptions.Center;

        _advanceBtn.SetActive(false);

        // 6. Proximity Prompt ("[E] Talk")
        _promptGo = new GameObject("Prompt", typeof(RectTransform));
        _promptGo.transform.SetParent(canvasGo.transform, false);
        var prt = _promptGo.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(260f, 54f);
        prt.anchoredPosition = new Vector2(0f, 32f);

        var promptImg = _promptGo.AddComponent<Image>();
        promptImg.color = new Color(0.08f, 0.05f, 0.16f, 0.92f);
        promptImg.sprite = UIFactory.GetRoundedSprite();
        promptImg.type = Image.Type.Sliced;

        var promptOutline = _promptGo.AddComponent<Outline>();
        promptOutline.effectColor = new Color(0.75f, 0.50f, 1.0f, 0.70f);
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
        ptxt.color = new Color(0.88f, 0.68f, 1f, 1f);
        ptxt.fontSize = 26f;
        ptxt.alignment = TextAlignmentOptions.Center;
        ptxt.fontStyle = FontStyles.Bold;

        _promptGo.SetActive(false);
    }

    private static void ApplyTMPDefaults(TMP_Text t)
    {
        t.characterSpacing = 0f;
        t.characterHorizontalScale = 1f;
        t.lineSpacing = 0f;
        t.paragraphSpacing = 0f;
        t.margin = Vector4.zero;
    }

    private void PulseAdvanceButton()
    {
        if (_advanceBtn == null || !_advanceBtn.activeSelf) return;
        float s = 1f + Mathf.Sin(Time.time * 6f) * 0.08f;
        _advanceBtn.transform.localScale = new Vector3(s, s, 1f);
    }

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.85f, 0.45f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
        Gizmos.color = new Color(0.85f, 0.45f, 1f, 0.08f);
        Gizmos.DrawSphere(transform.position, interactRange);

        UnityEditor.Handles.color = new Color(0.85f, 0.55f, 1f, 1f);
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (interactRange + 0.3f),
            $"  ✦ Dara Proximity [{interactRange:F1} u]");
    }
#endif
}
