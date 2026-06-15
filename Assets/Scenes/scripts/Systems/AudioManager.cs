using System.Collections;
using UnityEngine;

[AddComponentMenu("Systems/Audio Manager")]
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<AudioManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("Audio Manager (Auto)");
                    instance = go.AddComponent<AudioManager>();
                }
            }
            return instance;
        }
    }

    [Header("Audio Sources")]
    [Tooltip("AudioSource used for playing background music.")]
    public AudioSource musicSource;
    [Tooltip("AudioSource used for playing general sound effects.")]
    public AudioSource sfxSource;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1.0f;
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;

    [Header("Transition Settings")]
    [Tooltip("Duration in seconds for fading music tracks in and out.")]
    public float fadeDuration = 1.0f;

    private Coroutine musicFadeCoroutine;

    void Awake()
    {
        // Enforce Singleton instance pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Silence any other AudioSources in the scene if we are on the main menu
        if (!MainMenuController.isPlaying)
        {
            AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var source in allSources)
            {
                if (source != musicSource && source != sfxSource)
                {
                    source.playOnAwake = false;
                    if (source.isPlaying)
                    {
                        source.Stop();
                    }
                }
            }
        }
    }

    private void InitializeAudioSources()
    {
        // Create audio source components if they are not already assigned
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        else
        {
            // Safeguard: Disable playOnAwake and stop any auto-playing clip on assigned music source
            musicSource.playOnAwake = false;
            if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
        else
        {
            sfxSource.playOnAwake = false;
        }
    }

    /// <summary>
    /// Play background music track, with optional smooth cross-fade.
    /// </summary>
    public void PlayBGM(AudioClip clip, bool fade = true)
    {
        if (musicSource.clip == clip) return; // Already playing this clip

        if (fade && musicSource.isPlaying)
        {
            if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = StartCoroutine(FadeMusic(clip));
        }
        else
        {
            musicSource.clip = clip;
            musicSource.volume = GetRealMusicVolume();
            if (clip != null)
            {
                musicSource.Play();
            }
            else
            {
                musicSource.Stop();
            }
        }
    }

    /// <summary>
    /// Plays a 2D sound effect one-shot.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volumeScale * GetRealSFXVolume());
    }

    /// <summary>
    /// Plays a 3D spatial sound effect at the specified position coordinates.
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1.0f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volumeScale * GetRealSFXVolume());
    }

    /// <summary>
    /// Smoothly fades out the current BGM track and fades in the new BGM clip.
    /// </summary>
    private IEnumerator FadeMusic(AudioClip newClip)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        // Fade out current track
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = newClip;

        if (newClip != null)
        {
            musicSource.Play();
            elapsed = 0f;
            float targetVolume = GetRealMusicVolume();

            // Fade in new track
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / fadeDuration);
                yield return null;
            }

            musicSource.volume = targetVolume;
        }
    }

    // Vol helpers
    private float GetRealMusicVolume() => musicVolume * masterVolume;
    private float GetRealSFXVolume() => sfxVolume * masterVolume;

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.volume = GetRealMusicVolume();
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.volume = GetRealMusicVolume();
        }
    }
}
