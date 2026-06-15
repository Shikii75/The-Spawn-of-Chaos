using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Pause menu that builds its entire Canvas and panel hierarchy from code in Awake().
/// No Inspector references needed — everything is created programmatically via UIFactory.
/// Canvas sort order 10 keeps it above other game UI.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    public bool isPaused { get; private set; }

    /// <summary>
    /// Scene name to load when the player selects "Quit to Menu".
    /// </summary>
    public string menuSceneName = "MainMenu";

    // ── Private UI references (built from code) ──
    private GameObject pauseOverlay;

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If any UI is active and can consume Escape, return early
            if (NyxarisManager.Instance != null && NyxarisManager.Instance.mainInterfacePanel != null && NyxarisManager.Instance.mainInterfacePanel.activeSelf)
            {
                return;
            }

            if (ShopUI.Instance != null && ShopUI.Instance.IsShopActive)
            {
                return;
            }

            if (NPCDialogueUI.Instance != null && NPCDialogueUI.Instance.IsDialogueActive)
            {
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    // ── UI Construction ────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas at sort order 10, above all other game UI
        Canvas canvas = UIFactory.CreateCanvas("PauseCanvas", 10);
        canvas.transform.SetParent(transform, false);

        // Full-screen dark overlay
        RectTransform overlayRT = UIFactory.CreateFullScreenPanel(
            canvas.transform, "PauseOverlay", UIFactory.OverlayDark);
        pauseOverlay = overlayRT.gameObject;

        // Vertical layout for centered content
        UIFactory.AddVerticalLayout(pauseOverlay, 20f,
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

        // ── PAUSED title ──
        TextMeshProUGUI titleText = UIFactory.CreateText(
            overlayRT, "PausedTitle", "PAUSED",
            48f, UIFactory.TextWhite, TextAlignmentOptions.Center);
        UIFactory.AddLayoutElement(titleText.gameObject, preferredHeight: 70f, preferredWidth: 400f);

        // Spacer
        RectTransform spacer = UIFactory.CreatePanel(overlayRT, "Spacer", Color.clear,
            Vector2.zero, Vector2.zero);
        UIFactory.AddLayoutElement(spacer.gameObject, preferredHeight: 20f, preferredWidth: 10f);

        // ── RESUME button ──
        Button resumeBtn = UIFactory.CreateButton(overlayRT, "ResumeButton", "RESUME", 22f,
            () => ResumeGame());
        UIFactory.AddLayoutElement(resumeBtn.gameObject, preferredWidth: 200f, preferredHeight: 50f);

        // ── RESTART LEVEL button ──
        Button restartBtn = UIFactory.CreateButton(overlayRT, "RestartButton", "RESTART LEVEL", 22f,
            () => RestartLevel());
        UIFactory.AddLayoutElement(restartBtn.gameObject, preferredWidth: 200f, preferredHeight: 50f);

        // ── QUIT TO MENU button ──
        Button quitBtn = UIFactory.CreateButton(overlayRT, "QuitButton", "QUIT TO MENU", 22f,
            () => QuitToMenu(menuSceneName));
        UIFactory.AddLayoutElement(quitBtn.gameObject, preferredWidth: 200f, preferredHeight: 50f);

        // Start hidden
        pauseOverlay.SetActive(false);
    }

    // ── Public API ─────────────────────────────────────────────────

    public void TogglePause()
    {
        isPaused = !isPaused;
        pauseOverlay?.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;
        TogglePause();
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu(string menuSceneName)
    {
        Time.timeScale = 1f;
        MainMenuController.isPlaying = false; // Reset play state so menu shows on reload

        if (Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName);
        }
        else
        {
            // If the menu scene doesn't exist, reload the current scene to go back to the start and show the menu
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
