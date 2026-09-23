using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Strawhat Leader NPC Controller — Dojo 1 Dialogue Leader & Cutscene Handler.
///
/// Features:
/// 1. Intro Cutscene: Freezes player upon entry, walks towards player, stops at distance.
/// 2. Background Audio: Plays "concrete-syntax" during entrance & dialogue.
/// 3. Dialogue System: 7-layer speech-box dialogue with dynamic talking animation rotation.
/// 4. 3-Second Post-Dialogue Fight Countdown: Plays EndOfConversation, waits 3s, locks gates,
///    switches music to "temple-thunder", unfrees player, and starts wave battle.
/// 5. Battle Stance: Plays StartSitting -> SitIdle / SitLookDown loop watching combat.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class StrawhatLeaderNPC : MonoBehaviour
{
    public static StrawhatLeaderNPC Instance { get; private set; }

    [Header("── Cutscene Settings ──────────────────────────────────────────")]
    [Tooltip("Target distance to maintain from the player when stopping after the entrance walk.")]
    public float stopDistance = 3.0f;

    [Tooltip("Walking speed during intro sequence.")]
    public float walkSpeed = 2.5f;

    [Tooltip("Delay in seconds between dialogue completion and challenge fight start.")]
    public float fightStartDelay = 3.0f;

    [Header("── Audio Settings ─────────────────────────────────────────────")]
    [Tooltip("Background music track during dialogue (e.g. concrete-syntax).")]
    public AudioClip dialogueBGM;

    [Header("── Dialogue Lines (7 Layers) ──────────────────────────────────")]
    [TextArea(2, 5)]
    public string[] leaderDialogueLines = new string[]
    {
        "Halt, stranger. You tread upon sacred  Clan grounds.",
        "Word reaches us of a shadowy invader investigating the recent slaughter in our region.",
        "They say you serve the goddess Nyxaris... seeking blood and answers.",
        "If you accuse my clan of this dishonorable massacre, you are sorely mistaken.",
        "We warriors fight with honor. But the Samurai Clan across the ridge? They hide secrets.",
        "Before I allow you further into our mountains, you must prove your strength against my disciples.",
        "Draw your weapon, traveler. Let us see if your faith can withstand our blade!"
    };

    [Header("── Animation Frame Sets ───────────────────────────────────────")]
    public Sprite[] idleFrames;
    public Sprite[] beginWalkFrames;
    public Sprite[] keepWalkingFrames;
    public Sprite[] stopWalkingFrames;
    public Sprite[] casualTalkFrames;
    public Sprite[] explainingFrames;
    public Sprite[] explainingLosingInterestFrames;
    public Sprite[] endOfConversationFrames;
    public Sprite[] startSittingFrames;
    public Sprite[] sitIdleFrames;
    public Sprite[] sitLookDownFrames;

    [Header("── Animation Speed ───────────────────────────────────────────")]
    public float fps = 20f;

    // ── Internal State ──────────────────────────────────────────────────
    private SpriteRenderer _sr;
    private SpeechBubbleDialogue _dialogue;
    private Transform _playerTf;
    private bool _introComplete = false;
    private Coroutine _animRoutine;
    private Vector3 _startPosition;

    private enum State { IntroWalk, Talking, PostDialogueWait, SittingInBattle }
    private State _currentState = State.IntroWalk;

    void Awake()
    {
        Instance = this;
        _sr = GetComponent<SpriteRenderer>();
        _startPosition = transform.position;

#if UNITY_EDITOR
        AutoLoadSpritesIfEmpty();
#endif
    }

#if UNITY_EDITOR
    private void AutoLoadSpritesIfEmpty()
    {
        string basePath = "Assets/Scenes/animations/frames/strawleader";
        if (idleFrames == null || idleFrames.Length == 0) idleFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleaderidle-6bd176a7"));
        if (beginWalkFrames == null || beginWalkFrames.Length == 0) beginWalkFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleaderbeginwalk-b8c36e54"));
        if (keepWalkingFrames == null || keepWalkingFrames.Length == 0) keepWalkingFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleaderkeepwalking-d9827282"));
        if (stopWalkingFrames == null || stopWalkingFrames.Length == 0) stopWalkingFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleaderstopwalking-32778928"));
        
        string talkPath = System.IO.Path.Combine(basePath, "talkinganimationframes");
        if (casualTalkFrames == null || casualTalkFrames.Length == 0) casualTalkFrames = LoadSpritesFromFolder(System.IO.Path.Combine(talkPath, "strawleadercasualtalk-b27731fd"));
        if (explainingFrames == null || explainingFrames.Length == 0) explainingFrames = LoadSpritesFromFolder(System.IO.Path.Combine(talkPath, "strawleaderexplaining-cc9fc453"));
        if (explainingLosingInterestFrames == null || explainingLosingInterestFrames.Length == 0) explainingLosingInterestFrames = LoadSpritesFromFolder(System.IO.Path.Combine(talkPath, "strawleaderexplainingandlosinginterest-123a9311"));

        if (endOfConversationFrames == null || endOfConversationFrames.Length == 0) endOfConversationFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleaderendofconversation-0bc2dda8"));
        if (startSittingFrames == null || startSittingFrames.Length == 0) startSittingFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleaderstartsitting-b50e7fe7"));
        if (sitIdleFrames == null || sitIdleFrames.Length == 0) sitIdleFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleadersitidle-7ce88497"));
        if (sitLookDownFrames == null || sitLookDownFrames.Length == 0) sitLookDownFrames = LoadSpritesFromFolder(System.IO.Path.Combine(basePath, "strawleaderoccasionallookdownwhilesitting-1b36fe4c"));
    }

    private Sprite[] LoadSpritesFromFolder(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (!System.IO.Directory.Exists(folderPath)) return new Sprite[0];
        string[] fileGuids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        return fileGuids
            .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path))
            .Where(sprite => sprite != null)
            .OrderBy(sprite => sprite.name)
            .ToArray();
    }
#endif

    [Header("── Spawn Point ──────────────────────────────────────────────")]
    [Tooltip("Transform/GameObject to snap position to at start. If null, searches scene for 'Strawleaderspawnpoint'.")]
    public Transform customSpawnPoint;

    private void SnapToSpawnPoint()
    {
        if (customSpawnPoint != null)
        {
            transform.position = customSpawnPoint.position;
            return;
        }

        GameObject spawnGo = GameObject.Find("Strawleaderspawnpoint");
        if (spawnGo == null) spawnGo = GameObject.Find("StrawLeaderSpawnPoint");
        if (spawnGo == null) spawnGo = GameObject.Find("strawleaderspawnpoint");

        if (spawnGo != null)
        {
            transform.position = spawnGo.transform.position;
            Debug.Log($"[StrawhatLeaderNPC] Snapped initial position to '{spawnGo.name}' at {transform.position}");
        }
    }

    void Start()
    {
        SnapToSpawnPoint();

        // Find player
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _playerTf = p.transform;

        // Auto-get or add SpeechBubbleDialogue component
        _dialogue = GetComponent<SpeechBubbleDialogue>();
        if (_dialogue == null)
        {
            _dialogue = gameObject.AddComponent<SpeechBubbleDialogue>();
        }

        // Ensure leader gives the player plenty of space
        if (stopDistance < 5.0f)
        {
            stopDistance = 5.5f;
        }

        // Configure speech bubble
        _dialogue.characterName = "Strawhat Leader";
        _dialogue.dialogueLines = leaderDialogueLines;
        _dialogue.bubbleWidth = 440f;
        _dialogue.bubbleHeight = 155f;
        _dialogue.heightAboveNPC = 2.5f;
        _dialogue.fontSize = 18f;

        // Load background music if not set
        if (dialogueBGM == null)
        {
#if UNITY_EDITOR
            dialogueBGM = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/concrete-syntax.mp3");
#endif
            if (dialogueBGM == null) dialogueBGM = Resources.Load<AudioClip>("Audio/concrete-syntax");
            if (dialogueBGM == null) dialogueBGM = Resources.Load<AudioClip>("concrete-syntax");
        }

        // Start background music
        if (dialogueBGM != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(dialogueBGM, fade: true);
            Debug.Log($"[StrawhatLeaderNPC] Playing intro/dialogue BGM: {dialogueBGM.name}");
        }

        // Initial face towards player
        FacePlayer();

        // Hook up dialogue events
        _dialogue.OnDialogueStart += OnDialogueStarted;
        _dialogue.OnPageChanged += OnDialoguePageChanged;
        _dialogue.OnDialogueComplete += OnDialogueFinished;

        // Begin intro sequence
        StartCoroutine(RunIntroSequence());
    }

    private void FacePlayer()
    {
        if (_playerTf == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTf = p.transform;
        }
        if (_playerTf != null && _sr != null)
        {
            // Native frames face LEFT. If player is to the right (x > transform.x), flipX = true.
            _sr.flipX = _playerTf.position.x > transform.position.x;
        }
    }

    private IEnumerator RunIntroSequence()
    {
        // Lock player movement during approach and dialogue
        move.ExternalMovementLock = true;

        if (_playerTf != null)
        {
            // Face player correctly (native frames face left)
            FacePlayer();

            // Play Begin Walk
            if (beginWalkFrames != null && beginWalkFrames.Length > 0)
            {
                yield return StartCoroutine(PlayFrameSequence(beginWalkFrames, false));
            }

            // Walk towards player until stopDistance (checking horizontal distance so Y delta doesn't trap the loop)
            PlayLoopingFrames(keepWalkingFrames);
            float walkTimeout = 6.0f;
            float walkTimer = 0f;
            while (_playerTf != null && Mathf.Abs(transform.position.x - _playerTf.position.x) > stopDistance && walkTimer < walkTimeout)
            {
                walkTimer += Time.deltaTime;
                move.ExternalMovementLock = true;
                FacePlayer();
                float dir = _playerTf.position.x - transform.position.x;
                float targetX = _playerTf.position.x + (dir > 0 ? -stopDistance : stopDistance);
                Vector3 target = new Vector3(targetX, transform.position.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
                yield return null;
            }

            // Make player face the Strawhat Leader while frozen
            move playerMove = _playerTf != null ? _playerTf.GetComponent<move>() : null;
            if (playerMove != null) playerMove.FaceTarget(transform.position);

            // Play Stop Walk animation sequence cleanly
            FacePlayer();
            if (stopWalkingFrames != null && stopWalkingFrames.Length > 0)
            {
                yield return StartCoroutine(PlayFrameSequence(stopWalkingFrames, false));
            }

            if (playerMove != null) playerMove.FaceTarget(transform.position);
        }

        // Stand in Idle (standing animation)
        FacePlayer();
        PlayLoopingFrames(idleFrames);
        yield return new WaitForSeconds(0.4f);

        // Open speech bubble dialogue
        _dialogue.OpenDialogueExternally();
    }

    private void OnDialogueStarted()
    {
        _currentState = State.Talking;
        move.ExternalMovementLock = true;
        FacePlayer();
        PlayTalkingAnimation(0);
    }

    private void OnDialoguePageChanged(int lineIndex)
    {
        move.ExternalMovementLock = true;
        FacePlayer();
        PlayTalkingAnimation(lineIndex);
    }

    private void PlayTalkingAnimation(int lineIndex)
    {
        // Rotate/randomize talk animations across lines
        int style = lineIndex % 3;
        switch (style)
        {
            case 0:
                PlayLoopingFrames(casualTalkFrames);
                break;
            case 1:
                PlayLoopingFrames(explainingFrames);
                break;
            case 2:
                PlayLoopingFrames(explainingLosingInterestFrames);
                break;
        }
    }

    private void OnDialogueFinished()
    {
        // Immediately free the player to move as soon as dialogue ends!
        move.ExternalMovementLock = false;

        StartCoroutine(RunPostDialogueSequence());
    }

    private IEnumerator RunPostDialogueSequence()
    {
        _currentState = State.PostDialogueWait;

        // Player is free to move right after dialogue
        move.ExternalMovementLock = false;

        // Disable interact prompt / dialogue re-trigger during fight
        if (_dialogue != null) _dialogue.interactionDisabled = true;

        // Play End Of Conversation animation gesture
        FacePlayer();
        if (endOfConversationFrames != null && endOfConversationFrames.Length > 0)
        {
            yield return StartCoroutine(PlayFrameSequence(endOfConversationFrames, false));
        }
        FacePlayer();
        PlayLoopingFrames(idleFrames);

        // 3-second fight countdown — player can move and reposition freely!
        yield return new WaitForSeconds(fightStartDelay);

        // Lock gates & start challenge via DojoWaveManager
        DojoWaveManager waveMgr = DojoWaveManager.Instance != null ? DojoWaveManager.Instance : Object.FindFirstObjectByType<DojoWaveManager>();
        if (waveMgr != null)
        {
            waveMgr.StartChallenge();
        }

        // Guarantee player is unlocked
        move.ExternalMovementLock = false;

        // Start sitting sequence back on altar mat or current position
        _currentState = State.SittingInBattle;
        FacePlayer();
        if (startSittingFrames != null && startSittingFrames.Length > 0)
        {
            yield return StartCoroutine(PlayFrameSequence(startSittingFrames, false));
        }

        // Sitting loop during battle
        StartCoroutine(RunBattleSittingLoop());
    }

    private IEnumerator RunBattleSittingLoop()
    {
        while (_currentState == State.SittingInBattle)
        {
            FacePlayer();
            // Play Sit Idle loop for a random duration (3-6 seconds)
            PlayLoopingFrames(sitIdleFrames);
            yield return new WaitForSeconds(Random.Range(3.0f, 6.0f));

            if (_currentState != State.SittingInBattle) yield break;

            // Play Sit Look Down animation once
            FacePlayer();
            yield return StartCoroutine(PlayFrameSequence(sitLookDownFrames, false));
        }
    }

    // ── Frame Animation Engine ──────────────────────────────────────────

    private void PlayLoopingFrames(Sprite[] frames)
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        if (frames == null || frames.Length == 0) return;
        _animRoutine = StartCoroutine(LoopFrameSequence(frames));
    }

    private IEnumerator LoopFrameSequence(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) yield break;
        int frameIndex = 0;
        float frameTime = 1f / fps;

        while (true)
        {
            if (frames[frameIndex] != null)
            {
                _sr.sprite = frames[frameIndex];
            }
            frameIndex = (frameIndex + 1) % frames.Length;
            yield return new WaitForSeconds(frameTime);
        }
    }

    private IEnumerator PlayFrameSequence(Sprite[] frames, bool loop = false)
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        if (frames == null || frames.Length == 0) yield break;

        float frameTime = 1f / fps;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                _sr.sprite = frames[i];
            }
            yield return new WaitForSeconds(frameTime);
        }
    }

    void OnDestroy()
    {
        // Ensure player is unlocked if scene changes or NPC destroyed
        move.ExternalMovementLock = false;
        if (_dialogue != null)
        {
            _dialogue.OnDialogueStart -= OnDialogueStarted;
            _dialogue.OnPageChanged -= OnDialoguePageChanged;
            _dialogue.OnDialogueComplete -= OnDialogueFinished;
        }
    }
}
