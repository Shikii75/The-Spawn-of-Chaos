using System.Collections;
using UnityEngine;
using SpawnOfChaos.Platforms;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// TutorialFallRespawnManager - Monitors player falling in TutorialScene.
    /// If the player falls for 3 seconds continuously or drops below the pit threshold,
    /// teleports them to the last safe checkpoint, triggers a 1.5s visual blink effect,
    /// and resets incomplete tutorial mechanics (like the crumbling runway).
    /// </summary>
    public class TutorialFallRespawnManager : MonoBehaviour
    {
        public static TutorialFallRespawnManager Instance { get; private set; }

        [Header("Checkpoint Settings")]
        [Tooltip("Active respawn location when the player falls.")]
        public Vector3 currentCheckpoint = new Vector3(-2887.0f, 498.5f, 0f);

        [Header("Fall Detection Settings")]
        [Tooltip("Number of continuous seconds falling downward before triggering respawn.")]
        public float continuousFallTimeLimit = 3.0f;

        [Tooltip("Absolute Y world position below which the player is instantly respawned.")]
        public float bottomPitYThreshold = 475.0f;

        [Header("Respawn Feedback")]
        [Tooltip("Duration of player sprite blinking after respawning.")]
        public float blinkDuration = 1.5f;

        [Tooltip("Blink flash speed in seconds per flicker.")]
        public float blinkInterval = 0.1f;

        [Header("Debug State")]
        [SerializeField] private float currentFallTimer = 0f;
        [SerializeField] private bool isRespawning = false;

        private Transform playerTransform;
        private Rigidbody2D playerRb;
        private SpriteRenderer playerSr;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindPlayerComponents();
        }

        private void Update()
        {
            if (isRespawning) return;

            if (playerTransform == null || playerRb == null)
            {
                FindPlayerComponents();
                if (playerTransform == null) return;
            }

            // Check if player has fallen below absolute pit bottom
            if (playerTransform.position.y < bottomPitYThreshold)
            {
                StartCoroutine(RespawnPlayerRoutine());
                return;
            }

            // Check continuous downward fall velocity (linearVelocity.y < -1f)
            float vertVel = playerRb.linearVelocity.y;
            if (vertVel < -1.5f)
            {
                currentFallTimer += Time.deltaTime;
                if (currentFallTimer >= continuousFallTimeLimit)
                {
                    StartCoroutine(RespawnPlayerRoutine());
                }
            }
            else
            {
                currentFallTimer = 0f;
            }
        }

        private void FindPlayerComponents()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerRb = p.GetComponent<Rigidbody2D>();
                playerSr = p.GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// Updates the safe tutorial checkpoint position when advancing through tutorial sections.
        /// </summary>
        public void SetCheckpoint(Vector3 newCheckpoint)
        {
            currentCheckpoint = newCheckpoint;
            Debug.Log($"<color=#55FF88>[TutorialFallRespawnManager] Updated tutorial checkpoint to: {currentCheckpoint}</color>");
        }

        /// <summary>
        /// Executes player checkpoint respawn, resets physics, blinks sprite, and resets crumbling obstacles.
        /// </summary>
        public IEnumerator RespawnPlayerRoutine()
        {
            isRespawning = true;
            currentFallTimer = 0f;

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
            }

            if (playerTransform != null)
            {
                playerTransform.position = currentCheckpoint;
            }

            // Reset crumbling runway if not cleared
            if (CrumblingRunwayController.Instance != null && !CrumblingRunwayController.Instance.HasCompleted)
            {
                CrumblingRunwayController.Instance.ResetRunway();
            }

            // Reset Genbu ferry if ride was in progress
            if (SpawnOfChaos.NPC.GenbuFerryController.Instance != null)
            {
                SpawnOfChaos.NPC.GenbuFerryController.Instance.ResetFerry();
            }

            // Visual blink effect
            if (playerSr != null)
            {
                float elapsed = 0f;
                bool visible = true;

                while (elapsed < blinkDuration)
                {
                    elapsed += blinkInterval;
                    visible = !visible;
                    playerSr.color = new Color(playerSr.color.r, playerSr.color.g, playerSr.color.b, visible ? 1f : 0.2f);
                    yield return new WaitForSeconds(blinkInterval);
                }

                // Restore full opacity
                playerSr.color = new Color(playerSr.color.r, playerSr.color.g, playerSr.color.b, 1f);
            }
            else
            {
                yield return new WaitForSeconds(blinkDuration);
            }

            isRespawning = false;
        }
    }
}
