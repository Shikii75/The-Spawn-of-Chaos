using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[RequireComponent(typeof(BoxCollider2D))]
public class LevelGoalTrigger : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Scene to load when player clicks 'NEXT LEVEL'.")]
    public string nextLevelSceneName = "SampleScene";

    [Tooltip("Scene to load when player clicks 'MAIN MENU'.")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Audio Settings")]
    [Tooltip("Victory audio jingle to play when level is complete.")]
    public AudioClip victorySFX;

    private bool isTriggered = false;

    void Start()
    {
        // Enforce trigger collider settings
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        // Check if the collider belongs to the Player
        if (other.CompareTag("Player"))
        {
            TriggerVictory(other.gameObject);
        }
    }

    private void TriggerVictory(GameObject player)
    {
        isTriggered = true;
        Debug.Log("LevelGoalTrigger: Goal reached! Level Complete.");

        // Try to disable player controls so they cannot move during the screen
        MonoBehaviour playerMove = player.GetComponent("move") as MonoBehaviour;
        if (playerMove != null)
        {
            playerMove.enabled = false;
        }

        // Stop player rigid body velocity if present
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Build and show the Victory UI overlay from code
        BuildVictoryUI();

        // Play the victory sound clip via the AudioManager
        if (victorySFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(victorySFX);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  VICTORY UI — built dynamically from code on trigger
    // ══════════════════════════════════════════════════════════════════

    private void BuildVictoryUI()
    {
        // ── Canvas (sort order 50 — above everything) ──
        Canvas canvas = UIFactory.CreateCanvas("VictoryCanvas", 50);

        // ── Full-screen dark overlay ──
        RectTransform overlay = UIFactory.CreateFullScreenPanel(
            canvas.transform, "Overlay", UIFactory.OverlayDark
        );

        // ── 'LEVEL COMPLETE' title — 44pt, accent color, centered ──
        TextMeshProUGUI titleText = UIFactory.CreateText(
            overlay, "TitleText", "LEVEL COMPLETE",
            44f, UIFactory.Accent, TextAlignmentOptions.Center
        );
        UIFactory.SetRectFixed(titleText.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 80f), new Vector2(600f, 60f)
        );
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;

        // ── Button container — centered, horizontal layout ──
        RectTransform btnContainer = UIFactory.CreatePanel(
            overlay, "ButtonContainer", Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero
        );
        UIFactory.SetRectFixed(btnContainer,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -20f), new Vector2(500f, 55f)
        );
        UIFactory.AddHorizontalLayout(btnContainer.gameObject, 30f,
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

        // ── 'NEXT LEVEL' button ──
        Button nextBtn = UIFactory.CreateButton(
            btnContainer, "NextLevelButton", "NEXT LEVEL",
            20f, () => LoadNextLevel(nextLevelSceneName)
        );
        UIFactory.AddLayoutElement(nextBtn.gameObject, preferredHeight: 50f, preferredWidth: 220f);

        // ── 'MAIN MENU' button ──
        Button menuBtn = UIFactory.CreateButton(
            btnContainer, "MainMenuButton", "MAIN MENU",
            20f, () => QuitToMainMenu(mainMenuSceneName)
        );
        UIFactory.AddLayoutElement(menuBtn.gameObject, preferredHeight: 50f, preferredWidth: 220f);
    }

    // ══════════════════════════════════════════════════════════════════
    //  SCENE NAVIGATION
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// UI Button hook to load a different level or retry.
    /// </summary>
    public void LoadNextLevel(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// UI Button hook to return to the title screen.
    /// </summary>
    public void QuitToMainMenu(string mainMenuSceneName)
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
