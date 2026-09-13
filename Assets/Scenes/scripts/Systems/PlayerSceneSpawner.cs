using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Handles player spawning/transport on scene transitions.
/// Finds the persistent DontDestroyOnLoad player, moves them to the target spawn point,
/// re-enables controls, and binds the camera.
/// </summary>
public class PlayerSceneSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public string defaultSpawnPointName = "DefaultSpawnPoint";

    private void Start()
    {
        // Keep game frozen if title menu is active, otherwise unpause
        if (HUDManager.IsInMainMenu())
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[PlayerSceneSpawner] ===== START in scene '{sceneName}' =====");

        // Ensure Drifter Voluntary Death Checkpoint & Overlay exist
        SpawnOfChaos.Systems.DrifterSaveManager.EnsureExists();
        SpawnOfChaos.Systems.DrifterFlashOverlay.EnsureExists();

        string targetName = PlayerSpawnPointManager.targetSpawnPointName;
        bool isRespawning = PlayerSpawnPointManager.isRespawning;

        Debug.Log($"[PlayerSceneSpawner] targetSpawnPointName='{targetName}', isRespawning={isRespawning}");

        // Clear immediately so it does not persist across future play tests/restarts
        PlayerSpawnPointManager.targetSpawnPointName = "";
        PlayerSpawnPointManager.isRespawning = false;

        bool isTutorialScene = sceneName.Equals("TutorialScene", System.StringComparison.OrdinalIgnoreCase);
        bool isSampleScene = sceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase);
        bool toriiIntroPending = isSampleScene && PlayerPrefs.GetInt(SampleSceneToriiIntroSequence.PREF_INTRO_COMPLETED, 0) == 0;

        // --- Step 1: Find the persistent player ---
        GameObject player = FindPersistentPlayer();
        Debug.Log($"[PlayerSceneSpawner] FindPersistentPlayer returned: {(player != null ? player.name : "NULL")}");

        // --- Step 2: Clean up duplicates ---
        CleanupDuplicatePlayers(ref player);

        // In TutorialScene, enforce BasePlayer. If existing player is the Mage, remove it so BasePlayer spawns fresh!
        if ((isTutorialScene || toriiIntroPending) && player != null && !player.name.Contains("BasePlayer"))
        {
            Debug.Log($"[PlayerSceneSpawner] Clearing non-BasePlayer '{player.name}' for BasePlayer sequence.");
            Destroy(player);
            player = null;
        }

        // --- Step 3: Resolve spawn point name & position ---
        if (string.IsNullOrEmpty(targetName))
        {
            targetName = isTutorialScene ? "DefaultSpawnPoint" : defaultSpawnPointName;
            Debug.Log($"[PlayerSceneSpawner] Using default spawn point: '{targetName}'");
        }

        Vector3 spawnPosition = Vector3.zero;
        bool foundSpawnPoint = false;
        if (!string.IsNullOrEmpty(targetName))
        {
            GameObject spawnPoint = GameObject.Find(targetName);
            if (spawnPoint != null)
            {
                spawnPosition = spawnPoint.transform.position;
                foundSpawnPoint = true;
                Debug.Log($"[PlayerSceneSpawner] Found spawn point '{targetName}' at position {spawnPosition}");
            }
        }

        // Fallback search for DefaultSpawnPoint or StartScene if initial target not found
        if (!foundSpawnPoint)
        {
            GameObject startSceneObj = GameObject.Find("StartScene");
            if (isTutorialScene && startSceneObj != null)
            {
                spawnPosition = startSceneObj.transform.position + Vector3.up * 2f;
                foundSpawnPoint = true;
                Debug.Log($"[PlayerSceneSpawner] Snapped directly to StartScene at {spawnPosition}");
            }
            else
            {
                GameObject defSp = GameObject.Find("DefaultSpawnPoint");
                if (defSp != null)
                {
                    spawnPosition = defSp.transform.position;
                    foundSpawnPoint = true;
                    Debug.Log($"[PlayerSceneSpawner] Snapped to DefaultSpawnPoint at {spawnPosition}");
                }
                else if (toriiIntroPending)
                {
                    spawnPosition = new Vector3(1070f, 167.5f, 0f);
                    foundSpawnPoint = true;
                    Debug.Log($"[PlayerSceneSpawner] Snapped to Torii Gate entrance at {spawnPosition}");
                }
                else if (isTutorialScene)
                {
                    spawnPosition = new Vector3(-3378.2f, 518.5f, 0f);
                    foundSpawnPoint = true;
                }
                else
                {
                    spawnPosition = transform.position;
                    foundSpawnPoint = true;
                    Debug.Log($"[PlayerSceneSpawner] Defaulted to spawner position at {spawnPosition}");
                }
            }
        }

        // --- Step 4: Instantiate or transport ---
        if (player == null)
        {
            GameObject prefabToUse = playerPrefab;
            if (prefabToUse == null || ((isTutorialScene || toriiIntroPending) && !prefabToUse.name.Contains("BasePlayer")))
            {
                prefabToUse = Resources.Load<GameObject>("Prefabs/BasePlayer");
#if UNITY_EDITOR
                if (prefabToUse == null)
                {
                    prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BasePlayer.prefab");
                }
#endif
            }

            if (prefabToUse != null)
            {
                player = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);
                Debug.Log($"[PlayerSceneSpawner] INSTANTIATED player '{prefabToUse.name}' at pos={spawnPosition}");
            }
            else
            {
                Debug.LogError("[PlayerSceneSpawner] CRITICAL: No playerPrefab found!");
                return;
            }
        }
        else
        {
            // Transport player to the spawn point
            if (foundSpawnPoint)
            {
                player.transform.position = spawnPosition;
                Debug.Log($"[PlayerSceneSpawner] TRANSPORTED existing player '{player.name}' to '{targetName}' pos={spawnPosition}");
            }
        }

        // Reset health if respawning (player died and is being resurrected)
        if (isRespawning)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.Resurrect();
                Debug.Log("[PlayerSceneSpawner] Resurrected player health.");
            }
        }

        // --- Step 7: Activate and setup ---
        ActivateAndSetupPlayer(player);

        // --- Step 8: Delayed verification ---
        StartCoroutine(VerifyPlayerAfterFrame(player));
    }

    /// <summary>
    /// Find the player using singleton instances first, then tag search as fallback.
    /// </summary>
    private GameObject FindPersistentPlayer()
    {
        // Priority 1: Direct search for real Player in the active scene with SpriteRenderer
        GameObject realPlayerInScene = GameObject.Find("Player");
        if (realPlayerInScene != null && realPlayerInScene.GetComponentInChildren<SpriteRenderer>(true) != null)
        {
            Debug.Log($"[PlayerSceneSpawner] Found scene player '{realPlayerInScene.name}' at {realPlayerInScene.transform.position}");
            return realPlayerInScene;
        }

        // Priority 2: move.Instance if valid and has visual components
        if (move.Instance != null && !move.Instance.gameObject.name.StartsWith("Test") && move.Instance.GetComponentInChildren<SpriteRenderer>(true) != null)
        {
            Debug.Log($"[PlayerSceneSpawner] Found player via move.Instance: '{move.Instance.gameObject.name}' active={move.Instance.gameObject.activeInHierarchy}");
            return move.Instance.gameObject;
        }

        // Priority 3: Tag search
        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in taggedPlayers)
        {
            if (p != null && !p.name.StartsWith("Test") && p.GetComponentInChildren<SpriteRenderer>(true) != null)
            {
                Debug.Log($"[PlayerSceneSpawner] Found player via tag search: '{p.name}' at {p.transform.position}");
                return p;
            }
        }

        // Priority 4: FindObjectsOfType<move>
        move[] allMoves = FindObjectsOfType<move>(true);
        foreach (var m in allMoves)
        {
            if (m != null && !m.gameObject.name.StartsWith("Test") && m.GetComponentInChildren<SpriteRenderer>(true) != null)
            {
                Debug.Log($"[PlayerSceneSpawner] Found player via move component: '{m.gameObject.name}' at {m.transform.position}");
                return m.gameObject;
            }
        }

        Debug.LogWarning("[PlayerSceneSpawner] No persistent player found by any method.");
        return null;
    }

    /// <summary>
    /// Destroy any duplicate player objects, keeping only the primary player.
    /// </summary>
    private void CleanupDuplicatePlayers(ref GameObject primaryPlayer)
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log($"[PlayerSceneSpawner] Found {(allPlayers != null ? allPlayers.Length : 0)} objects tagged 'Player'");

        if (allPlayers == null || allPlayers.Length == 0)
        {
            return;
        }

        // If no primary player was found via singletons, pick the first tagged one
        if (primaryPlayer == null)
        {
            primaryPlayer = allPlayers[0];
            Debug.Log($"[PlayerSceneSpawner] Assigned '{primaryPlayer.name}' as primary player from tag search");
        }

        // Destroy any extras
        foreach (GameObject p in allPlayers)
        {
            if (p != null && p != primaryPlayer)
            {
                Debug.Log($"[PlayerSceneSpawner] DESTROYING duplicate player '{p.name}' (instanceID={p.GetInstanceID()})");
                Destroy(p);
            }
        }
    }

    /// <summary>
    /// Ensure the player is fully active, has DontDestroyOnLoad, controls enabled, and camera bound.
    /// </summary>
    private void ActivateAndSetupPlayer(GameObject player)
    {
        if (player == null)
        {
            Debug.LogError("[PlayerSceneSpawner] ActivateAndSetupPlayer called with null player!");
            return;
        }

        // Ensure the GameObject is active
        if (!player.activeInHierarchy)
        {
            player.SetActive(true);
            Debug.Log($"[PlayerSceneSpawner] Activated player '{player.name}'");
        }

        // Mark as persistent (must be root object for DontDestroyOnLoad)
        if (player.transform.parent != null)
        {
            player.transform.SetParent(null);
        }
        DontDestroyOnLoad(player);

        // Re-enable all controls
        EnablePlayerControl(player);

        // Bind camera
        SetupCameraFollow(player);

        Debug.Log($"[PlayerSceneSpawner] Player '{player.name}' setup complete at position {player.transform.position}");
    }

    private void EnablePlayerControl(GameObject player)
    {
        if (player == null) return;

        move.ExternalMovementLock = false;
        if (Time.timeScale == 0f) Time.timeScale = 1f;

        // Movement
        move movement = player.GetComponent<move>();
        if (movement != null)
        {
            movement.enabled = true;
            Debug.Log("[PlayerSceneSpawner] Enabled move component");
        }
        else
        {
            Debug.LogWarning("[PlayerSceneSpawner] move component NOT FOUND on player!");
        }

        // Combat
        MageCombat combat = player.GetComponent<MageCombat>();
        if (combat != null)
        {
            combat.enabled = true;
        }

        // Sprite visibility
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.enabled = true;
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f); // Ensure full alpha
            Debug.Log($"[PlayerSceneSpawner] SpriteRenderer enabled, sortingOrder={sr.sortingOrder}, sortingLayer={sr.sortingLayerName}");
        }
        else
        {
            Debug.LogWarning("[PlayerSceneSpawner] SpriteRenderer NOT FOUND on player root!");
        }

        // Also check child sprite renderers
        SpriteRenderer[] childRenderers = player.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer csr in childRenderers)
        {
            csr.enabled = true;
        }
        Debug.Log($"[PlayerSceneSpawner] Enabled {childRenderers.Length} total SpriteRenderer(s) on player hierarchy");

        // Physics
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
            rb.isKinematic = false; // Ensure physics are active (death sets isKinematic=true)
            Debug.Log("[PlayerSceneSpawner] Rigidbody2D reset: velocity=zero, simulated=true, isKinematic=false");
        }

        // Collider
        Collider2D col = player.GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
        }

        // Animator — ensure it's playing
        Animator anim = player.GetComponent<Animator>();
        if (anim != null)
        {
            anim.enabled = true;
        }
    }

    private void SetupCameraFollow(GameObject player)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = Object.FindFirstObjectByType<Camera>();
        }

        if (cam == null)
        {
            Debug.LogWarning("[PlayerSceneSpawner] No Camera found in scene! Cannot bind camera follow.");
            return;
        }

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
        {
            follow = cam.GetComponentInChildren<CameraFollow>(true);
        }
        if (follow == null)
        {
            follow = cam.gameObject.AddComponent<CameraFollow>();
            Debug.Log($"[PlayerSceneSpawner] Added CameraFollow component to camera '{cam.name}'.");
        }

        if (player != null)
        {
            follow.player = player.transform;
            follow.offset = new Vector3(0f, 0.5f, -10f); // Center character in viewport
            follow.SnapToTarget();
            Debug.Log($"[PlayerSceneSpawner] Camera bound to player. Camera pos={cam.transform.position}, Player pos={player.transform.position}");
        }
        else
        {
            follow.FindPlayer();
        }
    }

    /// <summary>
    /// Wait one frame then verify the player is alive and visible.
    /// If not, attempt emergency recovery.
    /// </summary>
    private IEnumerator VerifyPlayerAfterFrame(GameObject player)
    {
        yield return null; // Wait 1 frame

        if (player == null)
        {
            Debug.LogError("[PlayerSceneSpawner] VERIFICATION FAILED: Player was destroyed after 1 frame! Attempting emergency recovery...");

            // Emergency: try to find any surviving player
            GameObject recovered = FindPersistentPlayer();
            if (recovered != null)
            {
                Debug.Log($"[PlayerSceneSpawner] Emergency recovery found player: '{recovered.name}'");
                ActivateAndSetupPlayer(recovered);
            }
            else if (playerPrefab != null)
            {
                // Last resort: instantiate from prefab at default spawn
                GameObject spawnPoint = GameObject.Find(defaultSpawnPointName);
                Vector3 pos = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;
                GameObject emergency = Instantiate(playerPrefab, pos, Quaternion.identity);
                Debug.LogWarning($"[PlayerSceneSpawner] Emergency instantiated player at {pos}");
                ActivateAndSetupPlayer(emergency);
            }
            else
            {
                Debug.LogError("[PlayerSceneSpawner] FATAL: Cannot recover player — no prefab assigned.");
            }
        }
        else
        {
            Debug.Log($"[PlayerSceneSpawner] VERIFICATION OK: Player '{player.name}' alive at {player.transform.position}, active={player.activeInHierarchy}");

            // Double-check sprite renderer visibility
            SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Debug.Log($"[PlayerSceneSpawner] SpriteRenderer check: enabled={sr.enabled}, alpha={sr.color.a}, sortOrder={sr.sortingOrder}, sortLayer={sr.sortingLayerName}");
            }
        }
    }
}
