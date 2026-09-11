using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpawnOfChaos.Platforms
{
    /// <summary>
    /// CrumblingRunwayController - Manages the rolling collapse wave across the rock platform sprites
    /// for the Run Mechanic tutorial.
    /// Sprinting (Double-Tap) allows the player to safely outrun the wave, while walking drops them.
    /// </summary>
    public class CrumblingRunwayController : MonoBehaviour
    {
        public static CrumblingRunwayController Instance { get; private set; }

        [Header("Collapse Wave Configuration")]
        [Tooltip("Propagation speed of the crumbling wave in units per second (calibrated between walk speed ~4.0 and run speed ~7.5).")]
        public float waveSpeed = 5.2f;

        [Tooltip("Duration each rock shakes/trembles before it drops with gravity.")]
        public float rockShakeDuration = 0.2f;

        [Tooltip("X coordinate marking successful crossing where the player reaches the safe tunnel platform.")]
        public float completionDestinationX = -2846.0f;

        [Header("State Tracking")]
        [SerializeField] private bool waveTriggered = false;
        [SerializeField] private bool hasCompleted = false;

        [Header("Managed Rocks")]
        [SerializeField] private List<CrumblingRock> rocks = new List<CrumblingRock>();

        private BoxCollider2D triggerZone;
        private Coroutine collapseRoutine;
        private Transform playerTransform;

        public bool HasCompleted => hasCompleted;
        public bool IsWaveActive => waveTriggered && !hasCompleted;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            CollectAndSortRocks();
            EnsureTriggerZone();
        }

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (hasCompleted) return;

            if (playerTransform == null)
            {
                FindPlayer();
            }

            // Check if player has safely crossed the entire runway
            if (playerTransform != null && waveTriggered)
            {
                if (playerTransform.position.x >= completionDestinationX)
                {
                    hasCompleted = true;
                    Debug.Log("<color=#00FFAA>[CrumblingRunwayController] Player successfully cleared the Run Mechanic runway! Rocks remain collapsed.</color>");
                }
            }
        }

        private void FindPlayer()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        /// <summary>
        /// Collects all child CrumblingRock components and sorts them from left to right by world X.
        /// </summary>
        public void CollectAndSortRocks()
        {
            rocks.Clear();
            var found = GetComponentsInChildren<CrumblingRock>(true);
            rocks.AddRange(found);
            rocks.Sort((a, b) => a.WorldX.CompareTo(b.WorldX));
        }

        /// <summary>
        /// Configures a 2D trigger collider at the start of the runway to detect player approach.
        /// </summary>
        public void EnsureTriggerZone()
        {
            triggerZone = GetComponent<BoxCollider2D>();
            if (triggerZone == null)
            {
                triggerZone = gameObject.AddComponent<BoxCollider2D>();
            }

            triggerZone.isTrigger = true;

            if (rocks.Count > 0)
            {
                float minX = rocks[0].WorldX;
                float minY = float.MaxValue;
                float maxY = float.MinValue;

                foreach (var r in rocks)
                {
                    if (r.transform.position.y < minY) minY = r.transform.position.y;
                    if (r.transform.position.y > maxY) maxY = r.transform.position.y;
                }

                // Place trigger ON the rocks (entrance + 1.2f) so it only collapses AFTER player mounts
                Vector3 worldCenter = new Vector3(minX + 1.2f, (minY + maxY) * 0.5f + 1.5f, 0f);
                Vector3 localCenter = transform.InverseTransformPoint(worldCenter);

                triggerZone.offset = new Vector2(localCenter.x, localCenter.y);
                triggerZone.size = new Vector2(2.0f, 5.0f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasCompleted || waveTriggered) return;

            if (other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player")))
            {
                StartCollapseWave();
            }
        }

        /// <summary>
        /// Begins the sequential collapsing wave across the runway.
        /// </summary>
        public void StartCollapseWave()
        {
            if (waveTriggered || hasCompleted) return;
            waveTriggered = true;

            if (collapseRoutine != null) StopCoroutine(collapseRoutine);
            collapseRoutine = StartCoroutine(CollapseWaveRoutine());
        }

        private IEnumerator CollapseWaveRoutine()
        {
            if (rocks.Count == 0) yield break;

            float currentWaveX = rocks[0].WorldX;

            for (int i = 0; i < rocks.Count; i++)
            {
                CrumblingRock rock = rocks[i];
                if (rock == null) continue;

                float distance = Mathf.Max(0f, rock.WorldX - currentWaveX);
                float stepDelay = distance / Mathf.Max(1f, waveSpeed);

                if (stepDelay > 0f)
                {
                    yield return new WaitForSeconds(stepDelay);
                }

                currentWaveX = rock.WorldX;
                rock.TriggerFall(rockShakeDuration, 0f);
            }
        }

        /// <summary>
        /// Resets the entire runway when the player falls and respawns before reaching the destination.
        /// </summary>
        public void ResetRunway()
        {
            if (hasCompleted) return; // If completed, stays collapsed

            if (collapseRoutine != null)
            {
                StopCoroutine(collapseRoutine);
                collapseRoutine = null;
            }

            waveTriggered = false;

            foreach (var r in rocks)
            {
                if (r != null)
                {
                    r.ResetRock();
                }
            }

            Debug.Log("<color=#FFAA00>[CrumblingRunwayController] Runway rocks reset for retry.</color>");
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Setup Run Mechanic Colliders")]
        public void AutoSetupRunMechanicChildren()
        {
            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Setup Run Mechanic Colliders");

            int setupCount = 0;
            var children = GetComponentsInChildren<Transform>(true);
            foreach (var t in children)
            {
                if (t == transform) continue;

                var rock = t.GetComponent<CrumblingRock>();
                if (rock == null)
                {
                    rock = t.gameObject.AddComponent<CrumblingRock>();
                }
                rock.EnsureComponents();
                setupCount++;
            }

            CollectAndSortRocks();
            EnsureTriggerZone();
            UnityEditor.EditorUtility.SetDirty(gameObject);
            Debug.Log($"[CrumblingRunwayController] Successfully configured {setupCount} rock sprites with colliders and crumbling physics!");
        }
#endif
    }
}
