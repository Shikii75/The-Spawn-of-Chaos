using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// OrientationManager - Dynamically enforces screen orientation across the game:
/// - Main Game & Normal Scenes: Strictly Landscape (LandscapeLeft / LandscapeRight).
/// - Specific Minigames (Skybound Box, Void Surge): Switches to Portrait for optimal mobile thumb reach.
/// - Restores Landscape automatically upon exiting minigames or changing scenes.
/// </summary>
public class OrientationManager : MonoBehaviour
{
    public static OrientationManager Instance { get; private set; }

    [Header("Current State")]
    public ScreenOrientation currentOrientation = ScreenOrientation.LandscapeLeft;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInit()
    {
        if (Instance == null && FindFirstObjectByType<OrientationManager>() == null)
        {
            GameObject go = new GameObject("OrientationManager");
            Instance = go.AddComponent<OrientationManager>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        SetLandscape();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Always enforce Landscape when loading any main game scene
        SetLandscape();
    }

    public static void SetPortrait()
    {
        if (Instance != null)
        {
            Instance.currentOrientation = ScreenOrientation.Portrait;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Debug.Log("[OrientationManager] Switched orientation to PORTRAIT for minigame.");
        }
        else
        {
            Screen.orientation = ScreenOrientation.Portrait;
        }
    }

    public static void SetLandscape()
    {
        if (Instance != null)
        {
            Instance.currentOrientation = ScreenOrientation.LandscapeLeft;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Debug.Log("[OrientationManager] Switched orientation to LANDSCAPE for main game.");
        }
        else
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
