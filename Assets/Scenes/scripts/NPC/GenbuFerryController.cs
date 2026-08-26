using System.Collections;
using UnityEngine;
using TMPro;

namespace SpawnOfChaos.NPC
{
    /// <summary>
    /// GenbuFerryController - Controls Genbu the Giant Turtle NPC ferry.
    /// Ferries the player across chasms/water in TutorialScene with mystical pixelated dissolve FX,
    /// smooth swimming motion along configurable waypoints, and interaction prompts.
    /// </summary>
    public class GenbuFerryController : MonoBehaviour
    {
        public static GenbuFerryController Instance { get; private set; }

        public enum FerryState
        {
            WaitingAtStart,
            Boarding,
            Traveling,
            ArrivedWaitingDisembark,
            Disembarking,
            Completed
        }

        [Header("Ferry State")]
        [SerializeField] private FerryState currentState = FerryState.WaitingAtStart;
        public FerryState CurrentState => currentState;

        [Header("Waypoints & Anchors")]
        [Tooltip("Starting ledge position where Genbu awaits the player.")]
        public Transform startLedgePoint;

        [Tooltip("Destination platform position where Genbu brings the player.")]
        public Transform destinationPlatformPoint;

        [Tooltip("Anchor point on top of Genbu's shell where the player rests during the ride.")]
        public Transform shellMountPoint;

        [Tooltip("Landing location where the player teleports when disembarking at the destination platform.")]
        public Transform disembarkLandingPoint;

        [Header("Movement & Physics")]
        [Tooltip("Swimming travel speed across the water.")]
        public float travelSpeed = 4.2f;

        [Tooltip("Detection distance for player boarding and disembarking interactions.")]
        public float interactionRadius = 4.0f;

        [Tooltip("Water wave vertical bobbing amplitude during swim.")]
        public float bobbingAmplitude = 0.15f;

        [Tooltip("Water wave vertical bobbing frequency.")]
        public float bobbingFrequency = 2.5f;

        [Header("Facing & Orientation")]
        [Tooltip("If true, the raw sprite asset faces Left by default. Facing calculation will invert accordingly.")]
        public bool spriteFacesLeftByDefault = true;

        [Header("Prompt UI")]
        [Tooltip("Custom prompt text displayed when approaching Genbu at start ledge.")]
        public string mountPromptText = "[E] Ride Genbu";

        [Tooltip("Custom prompt text displayed when arriving at destination platform.")]
        public string disembarkPromptText = "[E] Disembark";

        [Tooltip("Y-offset for the floating interaction prompt above Genbu.")]
        public float promptYOffset = 2.8f;

        private Animator anim;
        private SpriteRenderer sr;
        private Transform playerTransform;
        private Rigidbody2D playerRb;
        private PlayerPixelDissolveFX playerDissolveFX;

        private GameObject promptUIGO;
        private TextMeshPro promptTMP;
        private SpriteRenderer promptKeyBg;
        private bool isPlayerInRange = false;

        private Vector3 initialGenbuPos;
        private Vector3 initialAbsScale = Vector3.one;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            anim = GetComponent<Animator>();
            sr = GetComponent<SpriteRenderer>();
            initialGenbuPos = transform.position;

            initialAbsScale = new Vector3(
                Mathf.Abs(transform.localScale.x) > 0.01f ? Mathf.Abs(transform.localScale.x) : 1f,
                Mathf.Abs(transform.localScale.y) > 0.01f ? Mathf.Abs(transform.localScale.y) : 1f,
                Mathf.Abs(transform.localScale.z) > 0.01f ? Mathf.Abs(transform.localScale.z) : 1f
            );

            EnsureShellMountPoint();
            CreateWorldSpacePromptUI();
        }

        private void Start()
        {
            FindPlayer();

            // Snap to start waypoint if defined
            if (startLedgePoint != null)
            {
                transform.position = startLedgePoint.position;
            }

            // Initial facing towards player or forward
            if (playerTransform != null)
            {
                SetFacingDirection(playerTransform.position.x - transform.position.x);
            }
        }

        private void EnsureShellMountPoint()
        {
            if (shellMountPoint == null)
            {
                Transform existing = transform.Find("ShellMountPoint");
                if (existing != null)
                {
                    shellMountPoint = existing;
                }
                else
                {
                    GameObject mountGO = new GameObject("ShellMountPoint");
                    mountGO.transform.SetParent(transform, false);
                    mountGO.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                    shellMountPoint = mountGO.transform;
                }
            }
            else
            {
                // Keep shell mount centered horizontally so flipping doesn't offset rider
                shellMountPoint.localPosition = new Vector3(0f, shellMountPoint.localPosition.y, shellMountPoint.localPosition.z);
            }
        }

        /// <summary>
        /// Flips Genbu's sprite scale to face the given horizontal direction.
        /// </summary>
        public void SetFacingDirection(float dirX)
        {
            if (Mathf.Abs(dirX) < 0.01f) return;

            // Genbu raw sprite faces LEFT by default.
            // When dirX < 0 (facing Left): scale.x is positive.
            // When dirX > 0 (facing Right): scale.x is negative (mirrored).
            float sign = (dirX < 0f) ? 1f : -1f;
            if (!spriteFacesLeftByDefault)
            {
                sign = (dirX > 0f) ? 1f : -1f;
            }

            float absX = Mathf.Max(0.1f, initialAbsScale.x);
            float absY = Mathf.Max(0.1f, initialAbsScale.y);
            float absZ = Mathf.Max(0.1f, initialAbsScale.z);

            transform.localScale = new Vector3(sign * absX, absY, absZ);

            // Counteract parent horizontal flip so prompt UI and text are NEVER mirrored
            if (promptUIGO != null)
            {
                promptUIGO.transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f);
            }
        }

        private void CreateWorldSpacePromptUI()
        {
            promptUIGO = new GameObject("Genbu_PromptUI");
            promptUIGO.transform.SetParent(transform, false);
            promptUIGO.transform.localPosition = new Vector3(0f, promptYOffset, 0f);
            promptUIGO.transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f);

            // Glowing Dark-Arcane Capsule Background
            GameObject bgGO = new GameObject("PromptBg");
            bgGO.transform.SetParent(promptUIGO.transform, false);
            promptKeyBg = bgGO.AddComponent<SpriteRenderer>();
            promptKeyBg.sortingOrder = 50;

            Texture2D tex = new Texture2D(128, 48);
            Color bgCol = new Color(0.06f, 0.02f, 0.14f, 0.92f);
            Color borderCol = new Color(0.35f, 0.95f, 1.0f, 0.95f);
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    bool isBorder = (x < 3 || x >= 125 || y < 3 || y >= 45);
                    tex.SetPixel(x, y, isBorder ? borderCol : bgCol);
                }
            }
            tex.Apply();
            promptKeyBg.sprite = Sprite.Create(tex, new Rect(0, 0, 128, 48), new Vector2(0.5f, 0.5f), 50f);

            // Text
            GameObject textGO = new GameObject("PromptText");
            textGO.transform.SetParent(promptUIGO.transform, false);
            textGO.transform.localPosition = new Vector3(0f, 0f, 0f);
            promptTMP = textGO.AddComponent<TextMeshPro>();
            promptTMP.alignment = TextAlignmentOptions.Center;
            promptTMP.fontSize = 4.2f;
            promptTMP.color = Color.white;
            promptTMP.sortingOrder = 51;
            promptTMP.text = mountPromptText;

            promptUIGO.SetActive(false);
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            // Floating prompt bobbing animation and unmirrored scale maintenance
            if (promptUIGO != null && promptUIGO.activeSelf)
            {
                float floatY = promptYOffset + Mathf.Sin(Time.time * 3f) * 0.08f;
                promptUIGO.transform.localPosition = new Vector3(0f, floatY, 0f);
                promptUIGO.transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f);
            }

            switch (currentState)
            {
                case FerryState.WaitingAtStart:
                    CheckPlayerProximity(mountPromptText);
                    if (isPlayerInRange)
                    {
                        // Turn to face the approaching player
                        SetFacingDirection(playerTransform.position.x - transform.position.x);

                        if (Input.GetKeyDown(KeyCode.E))
                        {
                            StartCoroutine(BoardPlayerRoutine());
                        }
                    }
                    break;

                case FerryState.ArrivedWaitingDisembark:
                    UpdatePrompt(disembarkPromptText, true);
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        StartCoroutine(DisembarkPlayerRoutine());
                    }
                    break;

                default:
                    if (promptUIGO != null && promptUIGO.activeSelf)
                    {
                        promptUIGO.SetActive(false);
                    }
                    break;
            }
        }

        private void LateUpdate()
        {
            // Keep player locked firmly to shell mount point during transit
            if (currentState == FerryState.Traveling || currentState == FerryState.Boarding || currentState == FerryState.ArrivedWaitingDisembark)
            {
                if (playerTransform != null && shellMountPoint != null)
                {
                    playerTransform.position = shellMountPoint.position;
                    if (playerRb != null)
                    {
                        playerRb.linearVelocity = Vector2.zero;
                    }
                }
            }
        }

        private void CheckPlayerProximity(string promptText)
        {
            if (playerTransform == null) return;

            float dist = Vector2.Distance(transform.position, playerTransform.position);
            isPlayerInRange = (dist <= interactionRadius);

            UpdatePrompt(promptText, isPlayerInRange);
        }

        private void UpdatePrompt(string text, bool visible)
        {
            if (promptUIGO == null) return;

            if (visible)
            {
                if (!promptUIGO.activeSelf) promptUIGO.SetActive(true);
                if (promptTMP != null && promptTMP.text != text) promptTMP.text = text;
            }
            else
            {
                if (promptUIGO.activeSelf) promptUIGO.SetActive(false);
            }
        }

        /// <summary>
        /// Boards the player onto Genbu's shell with pixelated dissolve teleport FX and starts swimming.
        /// </summary>
        private IEnumerator BoardPlayerRoutine()
        {
            currentState = FerryState.Boarding;
            UpdatePrompt("", false);

            move.ExternalMovementLock = true;

            Vector3 startPos = playerTransform.position;
            Vector3 mountPos = shellMountPoint.position;

            if (playerDissolveFX != null)
            {
                bool dissolveDone = false;
                playerDissolveFX.PlayDissolveTeleport(startPos, mountPos, () => {
                    dissolveDone = true;
                });
                yield return new WaitUntil(() => dissolveDone);
            }
            else
            {
                playerTransform.position = mountPos;
                yield return new WaitForSeconds(0.1f);
            }

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }

            // Turn Genbu to face the destination platform swim direction before departing
            Vector3 targetPlatformPos = destinationPlatformPoint != null ? destinationPlatformPoint.position : (transform.position + new Vector3(32f, 0f, 0f));
            SetFacingDirection(targetPlatformPos.x - transform.position.x);

            // Start Swimming to Destination
            StartCoroutine(TravelRoutine());
        }

        /// <summary>
        /// Smoothly swims Genbu across waypoints towards the combat platform.
        /// </summary>
        private IEnumerator TravelRoutine()
        {
            currentState = FerryState.Traveling;

            if (anim != null)
            {
                anim.SetBool("isSwimming", true);
            }

            Vector3 startPos = transform.position;
            Vector3 targetPos = destinationPlatformPoint != null ? destinationPlatformPoint.position : (startPos + new Vector3(32f, 0f, 0f));

            // Ensure facing travel direction throughout the swim
            SetFacingDirection(targetPos.x - startPos.x);

            float totalDistance = Vector3.Distance(startPos, targetPos);
            float duration = totalDistance / Mathf.Max(0.5f, travelSpeed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth ease in-out
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 currentTarget = Vector3.Lerp(startPos, targetPos, smoothT);

                // Wave oscillation
                float waveY = Mathf.Sin(elapsed * bobbingFrequency) * bobbingAmplitude;
                transform.position = new Vector3(currentTarget.x, currentTarget.y + waveY, currentTarget.z);

                yield return null;
            }

            transform.position = targetPos;

            // Arrived at destination
            if (anim != null)
            {
                anim.SetBool("isSwimming", false);
            }

            // Keep facing towards the disembark landing point / forward
            if (disembarkLandingPoint != null)
            {
                SetFacingDirection(disembarkLandingPoint.position.x - transform.position.x);
            }
            else
            {
                SetFacingDirection(targetPos.x - startPos.x);
            }

            currentState = FerryState.ArrivedWaitingDisembark;
        }

        /// <summary>
        /// Disembarks the player onto the combat platform with dissolve teleport FX and restores movement controls.
        /// </summary>
        private IEnumerator DisembarkPlayerRoutine()
        {
            currentState = FerryState.Disembarking;
            UpdatePrompt("", false);

            Vector3 currentMountPos = shellMountPoint != null ? shellMountPoint.position : transform.position;
            Vector3 landingPos = disembarkLandingPoint != null ? disembarkLandingPoint.position : (transform.position + new Vector3(3.5f, 1.2f, 0f));

            if (playerDissolveFX != null)
            {
                bool dissolveDone = false;
                playerDissolveFX.PlayDissolveTeleport(currentMountPos, landingPos, () => {
                    dissolveDone = true;
                });
                yield return new WaitUntil(() => dissolveDone);
            }
            else
            {
                playerTransform.position = landingPos;
                yield return new WaitForSeconds(0.1f);
            }

            // Restore full player controls
            move.ExternalMovementLock = false;

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }

            currentState = FerryState.Completed;
            Debug.Log("<color=#55FF88>[GenbuFerryController] Player successfully ferried and disembarked at combat arena!</color>");
        }

        /// <summary>
        /// Resets Genbu if player died / fell during transit.
        /// </summary>
        public void ResetFerry()
        {
            if (currentState == FerryState.Completed) return;

            StopAllCoroutines();

            if (startLedgePoint != null)
            {
                transform.position = startLedgePoint.position;
            }
            else
            {
                transform.position = initialGenbuPos;
            }

            if (anim != null)
            {
                anim.SetBool("isSwimming", false);
            }

            if (playerTransform != null)
            {
                SetFacingDirection(playerTransform.position.x - transform.position.x);
            }

            move.ExternalMovementLock = false;
            currentState = FerryState.WaitingAtStart;
            UpdatePrompt("", false);
        }

        private void FindPlayer()
        {
            if (move.Instance != null)
            {
                playerTransform = move.Instance.transform;
                playerRb = move.Instance.GetComponent<Rigidbody2D>();
                playerDissolveFX = move.Instance.GetComponent<PlayerPixelDissolveFX>();
                return;
            }

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerRb = p.GetComponent<Rigidbody2D>();
                playerDissolveFX = p.GetComponent<PlayerPixelDissolveFX>();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);

            if (startLedgePoint != null && destinationPlatformPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(startLedgePoint.position, destinationPlatformPoint.position);
                Gizmos.DrawWireCube(startLedgePoint.position, Vector3.one * 0.8f);
                Gizmos.DrawWireCube(destinationPlatformPoint.position, Vector3.one * 0.8f);
            }

            if (disembarkLandingPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(disembarkLandingPoint.position, 0.6f);
            }
        }
    }
}
