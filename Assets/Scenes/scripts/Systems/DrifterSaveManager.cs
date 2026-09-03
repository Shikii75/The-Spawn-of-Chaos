using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// Manages the Drifter's unique voluntary death checkpoint and time-rewind system.
    /// As an immortal Drifter, the player can only save by dying voluntarily (severing her head to anchor her soul in time).
    /// </summary>
    public class DrifterSaveManager : MonoBehaviour
    {
        public static DrifterSaveManager Instance { get; private set; }

        public const string PREF_HAS_CHECKPOINT = "Drifter_HasCheckpoint";
        public const string PREF_CHECKPOINT_X = "Drifter_CheckpointX";
        public const string PREF_CHECKPOINT_Y = "Drifter_CheckpointY";
        public const string PREF_CHECKPOINT_Z = "Drifter_CheckpointZ";
        public const string PREF_CHECKPOINT_SCENE = "Drifter_CheckpointScene";

        [Header("Severed Head Asset")]
        public Sprite drifterHeadSprite;

        private bool isRitualInProgress = false;
        public bool IsRitualInProgress => isRitualInProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadHeadSpriteIfNeeded();
        }

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("DrifterSaveManager");
                Instance = go.AddComponent<DrifterSaveManager>();
            }
        }

        private void LoadHeadSpriteIfNeeded()
        {
            if (drifterHeadSprite == null)
            {
                drifterHeadSprite = Resources.Load<Sprite>("DrifterHead");
#if UNITY_EDITOR
                if (drifterHeadSprite == null)
                {
                    drifterHeadSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Drifter/DrifterHead.png");
                }
#endif
            }
        }

        public bool HasCheckpoint()
        {
            return PlayerPrefs.GetInt(PREF_HAS_CHECKPOINT, 0) == 1;
        }

        public Vector3 GetCheckpointPosition()
        {
            float x = PlayerPrefs.GetFloat(PREF_CHECKPOINT_X, 0f);
            float y = PlayerPrefs.GetFloat(PREF_CHECKPOINT_Y, 0f);
            float z = PlayerPrefs.GetFloat(PREF_CHECKPOINT_Z, 0f);
            return new Vector3(x, y, z);
        }

        public string GetCheckpointScene()
        {
            return PlayerPrefs.GetString(PREF_CHECKPOINT_SCENE, "SampleScene");
        }

        public void SaveCheckpoint(Vector3 altarAnchorPosition, string sceneName)
        {
            PlayerPrefs.SetInt(PREF_HAS_CHECKPOINT, 1);
            PlayerPrefs.SetFloat(PREF_CHECKPOINT_X, altarAnchorPosition.x);
            PlayerPrefs.SetFloat(PREF_CHECKPOINT_Y, altarAnchorPosition.y);
            PlayerPrefs.SetFloat(PREF_CHECKPOINT_Z, altarAnchorPosition.z);
            PlayerPrefs.SetString(PREF_CHECKPOINT_SCENE, sceneName);
            PlayerPrefs.Save();
            Debug.Log($"[DrifterSaveManager] Voluntary death checkpoint anchored at {altarAnchorPosition} in scene '{sceneName}'.");
        }

        /// <summary>
        /// Executes the full voluntary death saving ritual at the specified altar.
        /// </summary>
        public void ExecuteVoluntaryDeathRitual(Entities.DrifterAltar altar, Action onComplete = null)
        {
            if (isRitualInProgress) return;
            isRitualInProgress = true;

            GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (player == null)
            {
                isRitualInProgress = false;
                return;
            }

            LoadHeadSpriteIfNeeded();
            DrifterFlashOverlay.EnsureExists();

            move playerMove = player.GetComponent<move>();
            MageCombat playerCombat = player.GetComponent<MageCombat>();
            Health playerHealth = player.GetComponent<Health>();
            Animator playerAnim = player.GetComponent<Animator>();
            SpriteRenderer playerSR = player.GetComponent<SpriteRenderer>();
            Rigidbody2D playerRB = player.GetComponent<Rigidbody2D>();

            // Freeze player before the ritual begins
            if (playerMove != null) playerMove.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;
            if (playerRB != null)
            {
                playerRB.linearVelocity = Vector2.zero;
                playerRB.simulated = false;
            }

            Vector3 anchorPos = altar != null ? altar.AnchorPoint.position : player.transform.position;
            string sceneName = SceneManager.GetActiveScene().name;

            DrifterFlashOverlay.Instance.PlaySaveSequence(
                onSeverBody: () =>
                {
                    // Save checkpoint data permanently
                    SaveCheckpoint(anchorPos, sceneName);

                    // Position player on the altar and display only the severed head
                    player.transform.position = anchorPos;
                    if (playerAnim != null) playerAnim.enabled = false;
                    if (playerSR != null && drifterHeadSprite != null)
                    {
                        playerSR.sprite = drifterHeadSprite;
                    }
                },
                onRestoreBody: () =>
                {
                    // Reconstruct the full body
                    if (playerAnim != null)
                    {
                        playerAnim.enabled = true;
                        try { playerAnim.Play("idle", 0, 0f); } catch (Exception) { }
                    }

                    // Restore full health and mana
                    if (playerHealth != null)
                    {
                        playerHealth.Heal(playerHealth.MaxHealth);
                    }
                    if (playerCombat != null)
                    {
                        playerCombat.currentMana = playerCombat.maxMana;
                    }

                    // Update HUD readouts immediately
                    if (Minigames.HUDOrbPanel.Instance != null)
                    {
                        Minigames.HUDOrbPanel.Instance.UpdateVisibility();
                    }
                },
                onComplete: () =>
                {
                    // Restore movement and physics
                    if (playerMove != null) playerMove.enabled = true;
                    if (playerCombat != null) playerCombat.enabled = true;
                    if (playerRB != null) playerRB.simulated = true;

                    isRitualInProgress = false;
                    onComplete?.Invoke();
                    Debug.Log("[DrifterSaveManager] Voluntary death ritual complete. Body reconstructed.");
                }
            );
        }

        /// <summary>
        /// Rewinds time back to the active death checkpoint upon defeat.
        /// </summary>
        public void RespawnAtDeathAnchor(Action onComplete = null)
        {
            if (!HasCheckpoint())
            {
                // Fallback to default scene reload if no checkpoint exists
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (player == null)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            LoadHeadSpriteIfNeeded();
            DrifterFlashOverlay.EnsureExists();

            move playerMove = player.GetComponent<move>();
            MageCombat playerCombat = player.GetComponent<MageCombat>();
            Health playerHealth = player.GetComponent<Health>();
            Animator playerAnim = player.GetComponent<Animator>();
            SpriteRenderer playerSR = player.GetComponent<SpriteRenderer>();
            Rigidbody2D playerRB = player.GetComponent<Rigidbody2D>();

            // Freeze player
            if (playerMove != null) playerMove.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;
            if (playerRB != null)
            {
                playerRB.linearVelocity = Vector2.zero;
                playerRB.simulated = false;
            }

            Vector3 anchorPos = GetCheckpointPosition();
            player.transform.position = anchorPos;

            // Start as severed head on the altar
            if (playerAnim != null) playerAnim.enabled = false;
            if (playerSR != null && drifterHeadSprite != null)
            {
                playerSR.sprite = drifterHeadSprite;
            }

            DrifterFlashOverlay.Instance.PlayRespawnAwakenSequence(
                onRestoreBody: () =>
                {
                    // Reconstruct full body
                    if (playerAnim != null)
                    {
                        playerAnim.enabled = true;
                        try { playerAnim.Play("idle", 0, 0f); } catch (Exception) { }
                    }

                    // Restore health & mana
                    if (playerHealth != null)
                    {
                        playerHealth.Resurrect();
                    }
                    if (playerCombat != null)
                    {
                        playerCombat.currentMana = playerCombat.maxMana;
                    }
                },
                onComplete: () =>
                {
                    // Restore controls & physics
                    if (playerMove != null) playerMove.enabled = true;
                    if (playerCombat != null) playerCombat.enabled = true;
                    if (playerRB != null) playerRB.simulated = true;

                    onComplete?.Invoke();
                    Debug.Log("[DrifterSaveManager] Respawn at death anchor complete. The Drifter has returned.");
                }
            );
        }
    }
}
