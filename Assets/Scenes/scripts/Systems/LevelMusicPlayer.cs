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
        EnsureTrackAssigned();

        // Only play the gameplay music if we are actively playing the game (not on the title screen)
        if (MainMenuUIToolkitController.isPlaying)
        {
            StartLevelMusic();
        }
    }

    public void StartLevelMusic()
    {
        EnsureTrackAssigned();

        if (levelMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(levelMusic, fadeOnStart);
            Debug.Log("[LevelMusicPlayer] Started level music: " + levelMusic.name);
        }
    }

    private void EnsureTrackAssigned()
    {
        if (levelMusic == null)
        {
            levelMusic = Resources.Load<AudioClip>("Audio/bamboo-incense");
            if (levelMusic == null)
            {
                levelMusic = Resources.Load<AudioClip>("Audio/temple-thunder");
            }
#if UNITY_EDITOR
            if (levelMusic == null)
            {
                levelMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bamboo-incense.mp3");
            }
#endif
        }
    }
}
