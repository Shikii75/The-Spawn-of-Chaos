using System.Collections;
using UnityEngine;

/// <summary>
/// Checkpoint system. Place on a "SavePoint_Crystal" trigger object.
/// When the player touches the crystal they "harm themselves" as a lore ritual,
/// taking a small amount of damage (or showing a VFX) and setting the checkpoint.
/// On death, health.cs reloads the scene. This manager uses DontDestroyOnLoad
/// to persist the last checkpoint position across scene reloads.
/// </summary>
public class CheckpointSystem : MonoBehaviour
{
    public static CheckpointSystem Instance { get; private set; }

    [Header("Checkpoint State")]
    [Tooltip("The world position to respawn the player at.")]
    public static Vector3 respawnPosition;
    public static bool hasCheckpoint = false;

    [Header("Lore Self-Harm Settings")]
    [Tooltip("HP the player loses when activating a checkpoint (the lore ritual).")]
    public int activationCost = 1;

    [Header("VFX")]
    public GameObject activationVFXPrefab;

    private bool alreadyActivated = false;

    void Awake()
    {
        // One persistent manager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // If we have a checkpoint, make sure the player spawns there
        if (hasCheckpoint)
        {
            StartCoroutine(RespawnPlayerAtCheckpoint());
        }
    }

    private IEnumerator RespawnPlayerAtCheckpoint()
    {
        // Wait one frame for scene objects to be ready
        yield return null;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = respawnPosition;
            Debug.Log($"[CheckpointSystem] Player respawned at checkpoint: {respawnPosition}");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (alreadyActivated) return;

        ActivateCheckpoint(other.gameObject);
    }

    public void ActivateCheckpoint(GameObject player)
    {
        if (alreadyActivated) return;
        alreadyActivated = true;

        // Record the checkpoint position (slightly above the crystal so player lands on floor)
        respawnPosition = transform.position + Vector3.left * 2f + Vector3.up * 0.5f;
        hasCheckpoint = true;

        Debug.Log($"[CheckpointSystem] Checkpoint set at: {respawnPosition}");

        // Lore: the player harms themselves to set the bond
        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth != null && activationCost > 0)
        {
            playerHealth.TakeDamage(activationCost);
            Debug.Log($"[CheckpointSystem] Player offered {activationCost} HP to seal the checkpoint bond.");
        }

        // Play VFX if configured
        if (activationVFXPrefab != null)
            Instantiate(activationVFXPrefab, transform.position, Quaternion.identity);

        // Visual feedback — flash the crystal
        StartCoroutine(FlashCrystal());
    }

    private IEnumerator FlashCrystal()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color original = sr.color;
        Color bright   = Color.white;

        for (int i = 0; i < 4; i++)
        {
            sr.color = bright;
            yield return new WaitForSeconds(0.08f);
            sr.color = original;
            yield return new WaitForSeconds(0.08f);
        }
    }

    /// <summary>
    /// Called by external systems (e.g. the pulley/stone trap) to demonstrate
    /// the lore: the stone crushes the player, but they respawn at the checkpoint.
    /// </summary>
    public static void ResetCheckpoint()
    {
        hasCheckpoint = false;
        respawnPosition = Vector3.zero;
    }
}
