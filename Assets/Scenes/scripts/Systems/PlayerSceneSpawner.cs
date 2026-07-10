using UnityEngine;

public class PlayerSceneSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public string defaultSpawnPointName = "DefaultSpawnPoint";

    private void Start()
    {
        string targetName = PlayerSpawnPointManager.targetSpawnPointName;
        bool isRespawning = PlayerSpawnPointManager.isRespawning;
        
        // Clear immediately so it does not persist across future play tests/restarts
        PlayerSpawnPointManager.targetSpawnPointName = "";
        PlayerSpawnPointManager.isRespawning = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // If targetName is empty, not respawning, and the player exists, leave them at their placed editor position.
        if (string.IsNullOrEmpty(targetName) && !isRespawning && player != null)
        {
            Debug.Log("[PlayerSceneSpawner] No target spawn point set, not respawning, and Player exists. Leaving player at their placed editor position.");
            SetupCameraFollow(player);
            return;
        }

        if (string.IsNullOrEmpty(targetName))
        {
            targetName = defaultSpawnPointName;
        }

        GameObject spawnPoint = GameObject.Find(targetName);
        Vector3 spawnPosition = Vector3.zero;

        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.transform.position;
        }
        else
        {
            Debug.LogWarning($"[PlayerSceneSpawner] Spawn point '{targetName}' not found. Spawning at origin.");
        }

        if (player == null)
        {
            if (playerPrefab != null)
            {
                player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
                Debug.Log($"[PlayerSceneSpawner] Instantiated player prefab at '{targetName}'.");
            }
            else
            {
                Debug.LogError("[PlayerSceneSpawner] Player prefab is not assigned and no Player found in scene!");
            }
        }
        else
        {
            player.transform.position = spawnPosition;
            Debug.Log($"[PlayerSceneSpawner] Moved existing player to '{targetName}'.");

            // Resurrect/Reset the player since they died/respawned
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.Resurrect();
            }
        }

        if (player != null)
        {
            DontDestroyOnLoad(player);
        }

        SetupCameraFollow(player);
    }

    private void SetupCameraFollow(GameObject player)
    {
        var cam = Camera.main;
        if (cam != null && player != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = cam.GetComponentInChildren<CameraFollow>(true);
            }
            if (follow != null)
            {
                follow.player = player.transform;
            }
        }
    }
}
