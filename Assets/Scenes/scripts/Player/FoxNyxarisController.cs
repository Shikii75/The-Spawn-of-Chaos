using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FoxNyxarisController - Ground Companion Controller for Nyxaris in her Mystical Fox Form.
/// Features 6 frame-accurate procedural animation states (Idle, StartWalk, Walk, StepForwardAFK, StartLayDown, Resting),
/// ground navigation with 2D physics, personal space bubble, shadow teleportation across gaps/cliffs,
/// and integrated speech bubble guidance.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class FoxNyxarisController : MonoBehaviour
{
    public static FoxNyxarisController Instance { get; private set; }

    public enum FoxAnimState
    {
        Idle,
        StartWalk,
        Walk,
        StepForwardAFK,
        StartLayDown,
        Resting,
        Teleporting
    }

    [Header("State")]
    public FoxAnimState currentAnimState = FoxAnimState.Idle;

    [Header("Size & Scale")]
    [Tooltip("Visual scale multiplier for Fox Nyxaris (default 1.45 for clear visibility).")]
    [Range(0.8f, 2.5f)]
    public float foxScale = 1.45f;

    [Header("Follow & Personal Space")]
    public float followDistance = 2.4f;
    public float minPersonalSpace = 1.6f;
    public float walkSpeed = 3.8f;
    public float runSpeed = 6.2f;
    public float teleportDistance = 7.5f;

    [Header("Idle Timers")]
    public float idleStepForwardThreshold = 4.0f;
    public float idleRestThreshold = 8.0f;

    [Header("Visual Effects")]
    public Color shadowAuraColor = new Color(0.85f, 0.2f, 1.0f, 0.85f);
    public string sortingLayerName = "Default";
    public int baseSortingOrder = 50;

    // Sprite Animation Arrays (Loaded from Resources)
    private Sprite[] idleFrames;
    private Sprite[] startWalkFrames;
    private Sprite[] walkFrames;
    private Sprite[] stepForwardFrames;
    private Sprite[] startLayDownFrames;
    private Sprite[] restingFrames;

    // Components & References
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private move playerMove;

    // Speech UI
    private GameObject speechCanvasGO;
    private CanvasGroup speechCanvasGroup;
    private TextMeshProUGUI dialogueTextComp;
    private TextMeshProUGUI promptBadgeComp;
    private AudioSource audioSource;
    private AudioClip chirpClip;

    // Animation Playback Variables
    private Sprite[] currentClipFrames;
    private int currentFrameIndex = 0;
    private float frameTimer = 0f;
    private float fps = 24f;
    private bool isLooping = true;
    private System.Action onClipFinished;

    // Timers & State
    private float playerIdleTimer = 0f;
    private bool isFacingRight = true;
    private bool isTeleporting = false;
    private bool isSpeaking = false;
    private Coroutine typewriterCoroutine;

    void Awake()
    {
        Instance = this;

        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CapsuleCollider2D>();

        rb.gravityScale = 2.5f;
        rb.freezeRotation = true;
        col.size = new Vector2(1.2f, 1.0f);
        col.offset = new Vector2(0f, 0.5f);

        LoadAnimationSprites();
        SetupWorldSpaceSpeechBubble();
        SynthesizeAudio();
    }

    void Start()
    {
        FindPlayer();
        PlayAnimation(FoxAnimState.Idle);
    }

    private void LoadAnimationSprites()
    {
        idleFrames = Resources.LoadAll<Sprite>("Sprites/FoxNyxaris/Idle");
        startWalkFrames = Resources.LoadAll<Sprite>("Sprites/FoxNyxaris/StartWalk");
        walkFrames = Resources.LoadAll<Sprite>("Sprites/FoxNyxaris/Walk");
        stepForwardFrames = Resources.LoadAll<Sprite>("Sprites/FoxNyxaris/StepForward");
        startLayDownFrames = Resources.LoadAll<Sprite>("Sprites/FoxNyxaris/StartLayDown");
        restingFrames = Resources.LoadAll<Sprite>("Sprites/FoxNyxaris/Resting");

        Debug.Log($"<color=#D47BFF>[FoxNyxaris] Loaded frames - Idle:{idleFrames?.Length}, Walk:{walkFrames?.Length}, Step:{stepForwardFrames?.Length}, Lay:{startLayDownFrames?.Length}, Rest:{restingFrames?.Length}</color>");
    }

    void FindPlayer()
    {
        if (move.Instance != null)
        {
            playerTransform = move.Instance.transform;
            playerRb = move.Instance.GetComponent<Rigidbody2D>();
            playerMove = move.Instance;
        }
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerRb = p.GetComponent<Rigidbody2D>();
                playerMove = p.GetComponent<move>();
            }
        }

        IgnorePlayerCollisions();
    }

    public void IgnorePlayerCollisions()
    {
        if (col == null && gameObject != null) col = GetComponent<CapsuleCollider2D>();
        if (col == null || playerTransform == null) return;

        Collider2D[] playerCols = playerTransform.GetComponentsInChildren<Collider2D>(true);
        foreach (var pCol in playerCols)
        {
            if (pCol != null && pCol != col)
            {
                Physics2D.IgnoreCollision(col, pCol, true);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (playerTransform != null && (collision.gameObject == playerTransform.gameObject || collision.transform.IsChildOf(playerTransform) || collision.gameObject.CompareTag("Player")))
        {
            if (col != null && collision.collider != null)
            {
                Physics2D.IgnoreCollision(col, collision.collider, true);
            }
        }
    }

    void Update()
    {
        UpdateSpritePlayback();

        // Dismiss dialogue on any player button press so it never blocks gameplay
        if (speechCanvasGroup != null && speechCanvasGroup.alpha > 0.05f)
        {
            if (Input.anyKeyDown)
            {
                HideSpeechBubble();
            }
        }

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        SyncSortingLayers();
        UpdateIdleDetection();
        if (CheckAerialStateAndMorphToOrb()) return;
        UpdateMovementAndNavigation();
    }

    #region Animation Playback Engine

    public void PlayAnimation(FoxAnimState state, System.Action onFinish = null)
    {
        if (currentAnimState == state && isLooping) return;

        currentAnimState = state;
        currentFrameIndex = 0;
        frameTimer = 0f;
        onClipFinished = onFinish;

        switch (state)
        {
            case FoxAnimState.Idle:
                currentClipFrames = idleFrames;
                fps = 18f;
                isLooping = true;
                break;

            case FoxAnimState.StartWalk:
                currentClipFrames = startWalkFrames;
                fps = 24f;
                isLooping = false;
                onClipFinished = () => PlayAnimation(FoxAnimState.Walk);
                break;

            case FoxAnimState.Walk:
                currentClipFrames = walkFrames;
                fps = 28f;
                isLooping = true;
                break;

            case FoxAnimState.StepForwardAFK:
                currentClipFrames = stepForwardFrames;
                fps = 24f;
                isLooping = false;
                onClipFinished = () => PlayAnimation(FoxAnimState.Idle);
                break;

            case FoxAnimState.StartLayDown:
                currentClipFrames = startLayDownFrames;
                fps = 26f;
                isLooping = false;
                onClipFinished = () => PlayAnimation(FoxAnimState.Resting);
                break;

            case FoxAnimState.Resting:
                currentClipFrames = restingFrames;
                fps = 14f;
                isLooping = false; // Pause and hold on the final frame while resting to avoid looping glitch
                break;
        }

        if (currentClipFrames != null && currentClipFrames.Length > 0)
        {
            sr.sprite = currentClipFrames[0];
        }
    }

    private void UpdateSpritePlayback()
    {
        if (currentClipFrames == null || currentClipFrames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float frameInterval = 1f / Mathf.Max(fps, 1f);

        if (frameTimer >= frameInterval)
        {
            frameTimer -= frameInterval;
            currentFrameIndex++;

            if (currentFrameIndex >= currentClipFrames.Length)
            {
                if (isLooping)
                {
                    currentFrameIndex = 0;
                }
                else
                {
                    currentFrameIndex = currentClipFrames.Length - 1;
                    var finishCb = onClipFinished;
                    onClipFinished = null;
                    finishCb?.Invoke();
                }
            }

            if (currentFrameIndex < currentClipFrames.Length)
            {
                sr.sprite = currentClipFrames[currentFrameIndex];
            }
        }
    }

    #endregion

    #region Movement, Following & Shadow Teleport

    private void UpdateIdleDetection()
    {
        float hInput = Mathf.Abs(Input.GetAxisRaw("Horizontal"));
        float hVel = playerRb != null ? Mathf.Abs(playerRb.linearVelocity.x) : 0f;
        bool playerMoving = hInput > 0.1f || hVel > 0.3f;

        if (playerMoving)
        {
            playerIdleTimer = 0f;
            if (currentAnimState == FoxAnimState.Resting || currentAnimState == FoxAnimState.StartLayDown)
            {
                PlayAnimation(FoxAnimState.Idle);
            }
        }
        else
        {
            playerIdleTimer += Time.deltaTime;

            if (playerIdleTimer >= idleRestThreshold && currentAnimState != FoxAnimState.Resting && currentAnimState != FoxAnimState.StartLayDown)
            {
                PlayAnimation(FoxAnimState.StartLayDown);
            }
            else if (playerIdleTimer >= idleStepForwardThreshold && currentAnimState == FoxAnimState.Idle)
            {
                PlayAnimation(FoxAnimState.StepForwardAFK);
            }
        }
    }

    private void UpdateMovementAndNavigation()
    {
        if (isTeleporting) return;

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        float xDiff = playerTransform.position.x - transform.position.x;

        // Check if falling too far behind -> Trigger Shadow Teleport
        if (distToPlayer > teleportDistance)
        {
            StartCoroutine(ShadowTeleportRoutine(playerTransform.position + new Vector3(-Mathf.Sign(playerTransform.localScale.x) * followDistance, 0.2f, 0f)));
            return;
        }

        // Player's facing sign: +1 for facing right, -1 for facing left
        float playerFacing = 1f;
        if (playerMove != null && Mathf.Abs(playerMove.LastFacingSign) > 0.1f)
        {
            playerFacing = Mathf.Sign(playerMove.LastFacingSign);
        }
        else if (playerTransform != null)
        {
            playerFacing = Mathf.Sign(playerTransform.localScale.x);
        }

        // Target position is ALWAYS behind the player
        float targetX = playerTransform.position.x - (playerFacing * followDistance);
        float xDistanceToTarget = targetX - transform.position.x;

        // Follow navigation
        if (Mathf.Abs(xDistanceToTarget) > 0.35f)
        {
            float moveDir = Mathf.Sign(xDistanceToTarget);
            float currentSpeed = (Mathf.Abs(xDistanceToTarget) > 3.2f || distToPlayer > 4.5f) ? runSpeed : walkSpeed;

            rb.linearVelocity = new Vector2(moveDir * currentSpeed, rb.linearVelocity.y);
            SetFacing(moveDir > 0f);

            if (currentAnimState != FoxAnimState.Walk && currentAnimState != FoxAnimState.StartWalk)
            {
                PlayAnimation(FoxAnimState.StartWalk);
            }
        }
        else
        {
            // Reached position behind player: slow to a stop
            rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, 12f * Time.deltaTime), rb.linearVelocity.y);

            // Face in the same direction the player is looking when settled
            SetFacing(playerFacing > 0f);

            if (currentAnimState == FoxAnimState.Walk || currentAnimState == FoxAnimState.StartWalk)
            {
                PlayAnimation(FoxAnimState.Idle);
            }
        }

        if (speechCanvasGO != null)
        {
            speechCanvasGO.transform.position = transform.position + new Vector3(0f, 1.25f + foxScale * 0.75f, 0f);
            // Counter-flip canvas localScale.x so text is NEVER mirrored, scaled up for readability
            float parentSign = Mathf.Sign(transform.localScale.x);
            speechCanvasGO.transform.localScale = new Vector3(parentSign * 0.022f, 0.022f, 1f);
        }
    }

    private void SetFacing(bool right)
    {
        isFacingRight = right;
        // Inverted: raw sprite faces LEFT by default, scaled by foxScale
        transform.localScale = new Vector3(right ? -foxScale : foxScale, foxScale, 1f);
    }

    private IEnumerator ShadowTeleportRoutine(Vector3 targetPos)
    {
        isTeleporting = true;
        rb.linearVelocity = Vector2.zero;

        // 1. Spawn Outbound Shadow Burst FX
        SpawnTeleportBurstFX(transform.position);

        // Fade Out
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            Color c = sr.color;
            c.a = 1f - (t / 0.18f);
            sr.color = c;
            yield return null;
        }

        // Move position
        transform.position = targetPos;
        yield return new WaitForSeconds(0.05f);

        // 2. Spawn Inbound Shadow Burst FX
        SpawnTeleportBurstFX(transform.position);

        // Fade In
        t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            Color c = sr.color;
            c.a = t / 0.18f;
            sr.color = c;
            yield return null;
        }

        sr.color = Color.white;
        isTeleporting = false;
        PlayAnimation(FoxAnimState.Idle);
    }

    private void SpawnTeleportBurstFX(Vector3 pos)
    {
        GameObject burstGO = new GameObject("ShadowPuffBurst");
        burstGO.transform.position = pos;
        SpriteRenderer puffSr = burstGO.AddComponent<SpriteRenderer>();
        puffSr.sprite = Resources.Load<Sprite>("Sprites/Nyxaris/Nyxaris_Aura");
        puffSr.color = shadowAuraColor;
        puffSr.sortingOrder = baseSortingOrder + 2;

        Destroy(burstGO, 0.35f);
    }

    #endregion

    #region Speech Bubble & Guidance

    public void ShowDialogue(string text, string promptBadge)
    {
        if (speechCanvasGroup == null) return;

        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterRoutine(text, promptBadge));
    }

    public void HideSpeechBubble()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        isSpeaking = false;
        StartCoroutine(FadeCanvasGroup(speechCanvasGroup, 0f, 0.25f));
    }

    private IEnumerator TypewriterRoutine(string text, string promptBadge)
    {
        isSpeaking = true;
        speechCanvasGroup.alpha = 1.0f;
        speechCanvasGO.SetActive(true);

        dialogueTextComp.text = "";
        if (promptBadgeComp != null)
        {
            promptBadgeComp.text = !string.IsNullOrEmpty(promptBadge) ? $"<color=#D47BFF>► {promptBadge}</color>" : "";
            promptBadgeComp.gameObject.SetActive(!string.IsNullOrEmpty(promptBadge));
        }

        for (int i = 0; i < text.Length; i++)
        {
            dialogueTextComp.text += text[i];

            if (char.IsLetterOrDigit(text[i]) && audioSource != null && chirpClip != null && (i % 2 == 0))
            {
                audioSource.pitch = Random.Range(1.4f, 1.8f);
                audioSource.PlayOneShot(chirpClip, 0.45f);
            }

            yield return new WaitForSeconds(0.028f);
        }

        isSpeaking = false;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        if (cg == null) yield break;
        float startAlpha = cg.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        cg.alpha = targetAlpha;
    }

    private void SyncSortingLayers()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (playerTransform != null)
        {
            var pSr = playerTransform.GetComponentInChildren<SpriteRenderer>();
            if (pSr != null)
            {
                sr.sortingLayerName = pSr.sortingLayerName;
                sr.sortingOrder = pSr.sortingOrder + 1; // Layered just above the player
            }
        }
    }

    private void SetupWorldSpaceSpeechBubble()
    {
        speechCanvasGO = new GameObject("FoxNyxaris_SpeechCanvas");
        speechCanvasGO.transform.SetParent(transform, false);

        Canvas canvas = speechCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 250;

        speechCanvasGO.AddComponent<CanvasScaler>();
        speechCanvasGroup = speechCanvasGO.AddComponent<CanvasGroup>();
        speechCanvasGroup.alpha = 0f;

        RectTransform canvasRT = speechCanvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(6.5f, 3.5f);
        canvasRT.localScale = Vector3.one * 0.022f;

        GameObject bgGO = new GameObject("BubbleBackground");
        bgGO.transform.SetParent(speechCanvasGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.015f, 0.12f, 0.68f); // Semi-transparent glassmorphic obsidian
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(390f, 150f);

        Outline outline = bgGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.25f, 1.0f, 0.9f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        GameObject textGO = new GameObject("DialogueText");
        textGO.transform.SetParent(bgGO.transform, false);
        dialogueTextComp = textGO.AddComponent<TextMeshProUGUI>();
        dialogueTextComp.fontSize = 24f;
        dialogueTextComp.color = Color.white;
        dialogueTextComp.alignment = TextAlignmentOptions.TopLeft;
        dialogueTextComp.textWrappingMode = TextWrappingModes.Normal;
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.sizeDelta = new Vector2(360f, 85f);
        textRT.anchoredPosition = new Vector2(0f, 18f);

        GameObject promptGO = new GameObject("PromptBadge");
        promptGO.transform.SetParent(bgGO.transform, false);
        promptBadgeComp = promptGO.AddComponent<TextMeshProUGUI>();
        promptBadgeComp.fontSize = 21f;
        promptBadgeComp.fontStyle = FontStyles.Bold;
        promptBadgeComp.color = new Color(0.92f, 0.65f, 1f, 1f);
        promptBadgeComp.alignment = TextAlignmentOptions.BottomLeft;
        RectTransform promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.sizeDelta = new Vector2(360f, 34f);
        promptRT.anchoredPosition = new Vector2(0f, -46f);
    }

    private void SynthesizeAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        int rate = 44100;
        int chirpSamples = Mathf.FloorToInt(rate * 0.045f);
        float[] cSamples = new float[chirpSamples];
        for (int i = 0; i < chirpSamples; i++)
        {
            float t = (float)i / chirpSamples;
            float freq = Mathf.Lerp(620f, 440f, t);
            float env = Mathf.Sin(t * Mathf.PI);
            cSamples[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)rate)) * env * 0.4f;
        }
        chirpClip = AudioClip.Create("FoxNyxaris_Chirp", chirpSamples, 1, rate, false);
        chirpClip.SetData(cSamples, 0);
    }

    #endregion

    private bool isAirborne = false;

    private bool CheckAerialStateAndMorphToOrb()
    {
        if (playerTransform == null) return false;

        bool playerInAir = false;
        if (playerMove != null)
        {
            playerInAir = !playerMove.IsGrounded || (playerRb != null && Mathf.Abs(playerRb.linearVelocity.y) > 1.2f);
        }
        else if (playerRb != null)
        {
            playerInAir = Mathf.Abs(playerRb.linearVelocity.y) > 1.2f;
        }

        if (playerInAir && !isAirborne)
        {
            // Morph into Orb Form for jumping/falling
            isAirborne = true;
            StartCoroutine(MorphToOrbAirRoutine());
            return true;
        }

        return false;
    }

    private IEnumerator MorphToOrbAirRoutine()
    {
        SpawnTeleportBurstFX(transform.position);

        // Hide Fox visuals & disable physics while airborne
        if (sr != null) sr.enabled = false;
        if (col != null) col.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Activate Orb Guide
        if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.ActivateAirborneOrb(transform.position);
        }

        // Wait until player lands back on the ground
        yield return new WaitUntil(() => playerMove != null && playerMove.IsGrounded && Mathf.Abs(playerRb.linearVelocity.y) < 0.4f);

        // Land and morph back into Fox Form
        Vector3 landPos = playerTransform.position + new Vector3(-Mathf.Sign(playerTransform.localScale.x) * followDistance, 0f, 0f);
        transform.position = landPos;

        if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.DeactivateAirborneOrb();
        }

        SpawnTeleportBurstFX(transform.position);

        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;

        isAirborne = false;
        PlayAnimation(FoxAnimState.Idle);
    }

}
