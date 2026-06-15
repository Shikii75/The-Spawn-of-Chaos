using UnityEngine;

/// <summary>
/// Lightweight player that triggers level background music only when active gameplay starts.
/// Works seamlessly within single-scene architectures.
/// </summary>
[AddComponentMenu("Systems/Level Music Player")]
public class LevelMusicPlayer : MonoBehaviour
{
    [Header("Level Music Settings")]
    [Tooltip("The music track to play during gameplay.")]
    public AudioClip levelMusic;
    
    [Tooltip("Whether to fade the music track in smoothly.")]
    public bool fadeOnStart = true;

    void Start()
    {
        // Only play the gameplay music if we are actively playing the game (not on the title screen)
        if (MainMenuController.isPlaying)
        {
            if (levelMusic != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(levelMusic, fadeOnStart);
                Debug.Log("LevelMusicPlayer: Started level music: " + levelMusic.name);
            }
        }
    }
}
