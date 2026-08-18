using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Dedicated frame-by-frame animation sequencer for Nyxaris companion portrait.
/// Plays dynamic sprite sequences from Assets/Scenes/animations/frames/Nyxaris/
/// and freezes on the final frame when completing dialogue/response poses.
/// </summary>
public class NyxarisFrameAnimator : MonoBehaviour
{
    [Header("Target Display Image")]
    public Image targetImage;

    [Header("Animation Settings")]
    public float defaultFPS = 20f;
    public string defaultIdleAnimation = "nyxarisnuetral-411247bb";

    [System.Serializable]
    public class NamedFrameSequence
    {
        public string animationKey;
        public List<Sprite> frames = new List<Sprite>();
    }

    [Header("Pre-loaded Frame Sequences")]
    public List<NamedFrameSequence> sequences = new List<NamedFrameSequence>();

    private Dictionary<string, List<Sprite>> sequenceDict = new Dictionary<string, List<Sprite>>(System.StringComparer.OrdinalIgnoreCase);
    private Coroutine playRoutine;
    
    public bool IsPlaying { get; private set; }
    public bool IsFrozenOnLastFrame { get; private set; }
    public string CurrentAnimationKey { get; private set; }

    void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
        BuildSequenceDictionary();
    }

    void Start()
    {
        if (sequences.Count == 0)
        {
            AutoScanAndIndexAllNyxarisFrames();
        }
        BuildSequenceDictionary();
    }

    public void BuildSequenceDictionary()
    {
        sequenceDict.Clear();
        foreach (var seq in sequences)
        {
            if (!string.IsNullOrEmpty(seq.animationKey) && seq.frames != null && seq.frames.Count > 0)
            {
                sequenceDict[seq.animationKey] = seq.frames;

                // Also index simplified alias (e.g. "explaining" -> "nyxarisexplaining-30d28246")
                string clean = CleanKey(seq.animationKey);
                if (!sequenceDict.ContainsKey(clean))
                {
                    sequenceDict[clean] = seq.frames;
                }
            }
        }
    }

    private string CleanKey(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string s = raw.ToLower();
        if (s.StartsWith("nyxaris")) s = s.Substring(7);
        int dashIdx = s.IndexOf('-');
        if (dashIdx > 0) s = s.Substring(0, dashIdx);
        return s.Replace("_", "").Replace(" ", "").Replace("mp4", "").Trim();
    }

    private string pendingAnimation = null;
    private float pendingFps = 20f;

    void OnEnable()
    {
        if (!string.IsNullOrEmpty(pendingAnimation))
        {
            string anim = pendingAnimation;
            float fps = pendingFps;
            pendingAnimation = null;
            PlayAnimation(anim, fps);
        }
    }

    /// <summary>
    /// Plays the matching frame sequence and holds/freezes on the last frame indefinitely.
    /// </summary>
    public void PlayAnimation(string animKey, float fps = 20f)
    {
        if (string.IsNullOrEmpty(animKey)) return;

        if (!gameObject.activeInHierarchy)
        {
            pendingAnimation = animKey;
            pendingFps = fps;
            return;
        }

        List<Sprite> frames = FindFrames(animKey);
        if (frames == null || frames.Count == 0)
        {
            frames = FindFrames("neutral") ?? FindFrames("nuetral");
        }

        if (frames == null || frames.Count == 0) return;

        if (targetImage != null && frames[0] != null)
        {
            targetImage.sprite = frames[0];
            targetImage.enabled = true;
            targetImage.color = Color.white;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        CurrentAnimationKey = animKey;
        playRoutine = StartCoroutine(PlayFrameSequenceRoutine(frames, fps > 0 ? fps : defaultFPS));
    }

    private IEnumerator PlayFrameSequenceRoutine(List<Sprite> frames, float fps)
    {
        IsPlaying = true;
        IsFrozenOnLastFrame = false;
        float frameDelay = 1f / Mathf.Max(1f, fps);

        for (int i = 0; i < frames.Count; i++)
        {
            if (targetImage != null && frames[i] != null)
            {
                targetImage.sprite = frames[i];
                targetImage.enabled = true;
            }
            yield return new WaitForSecondsRealtime(frameDelay);
        }

        // Freeze on final frame
        if (frames.Count > 0 && targetImage != null)
        {
            targetImage.sprite = frames[frames.Count - 1];
        }

        IsPlaying = false;
        IsFrozenOnLastFrame = true;
    }

    /// <summary>
    /// Finds frames using direct or fuzzy alias matching across all 34 folders.
    /// </summary>
    public List<Sprite> FindFrames(string query)
    {
        if (string.IsNullOrEmpty(query)) return null;
        if (sequenceDict.Count == 0 || sequences.Count == 0)
        {
            AutoScanAndIndexAllNyxarisFrames();
            BuildSequenceDictionary();
        }

        string q = query.ToLower().Trim();
        if (q == "neutral" || q == "nuetral")
        {
            if (sequenceDict.TryGetValue("nyxarisnuetral-411247bb", out var nList)) return nList;
            if (sequenceDict.TryGetValue("nyxarisnuetralstare-14b61402", out var nList2)) return nList2;
        }
        else if (q == "explaining" || q.Contains("explain"))
        {
            if (sequenceDict.TryGetValue("nyxarisexplaining0-c6c6f20e", out var exp0)) return exp0;
            if (sequenceDict.TryGetValue("nyxarisexplaining1-a4a19986", out var exp1)) return exp1;
            if (sequenceDict.TryGetValue("nyxarisexcitedarmsspreadexplaining-cb776e55", out var expSpread)) return expSpread;
            if (sequenceDict.TryGetValue("nyxaristalkingeyesclosed-a238b88a", out var talkClosed)) return talkClosed;
        }

        // 1. Direct match
        if (sequenceDict.TryGetValue(q, out var directList) && directList.Count > 0)
            return directList;

        // 2. Clean alias match
        string cleanQuery = CleanKey(q);
        if (cleanQuery == "neutral" || cleanQuery == "nuetral")
        {
            if (sequenceDict.TryGetValue("nuetral", out var nList)) return nList;
            if (sequenceDict.TryGetValue("neutral", out var nList2)) return nList2;
        }
        else if (cleanQuery == "explaining" || cleanQuery.Contains("explain"))
        {
            if (sequenceDict.TryGetValue("explaining0", out var exp0)) return exp0;
            if (sequenceDict.TryGetValue("explaining1", out var exp1)) return exp1;
            if (sequenceDict.TryGetValue("excitedarmsspreadexplaining", out var expSpread)) return expSpread;
        }

        if (sequenceDict.TryGetValue(cleanQuery, out var cleanList) && cleanList.Count > 0)
            return cleanList;

        // 3. Partial keyword contains
        foreach (var kvp in sequenceDict)
        {
            if (kvp.Key.ToLower().Contains(cleanQuery) || cleanQuery.Contains(CleanKey(kvp.Key)))
            {
                if (kvp.Value != null && kvp.Value.Count > 0)
                    return kvp.Value;
            }
        }

        return null;
    }

    [ContextMenu("Scan & Index All Nyxaris Frames")]
    public void AutoScanAndIndexAllNyxarisFrames()
    {
#if UNITY_EDITOR
        string rootPath = "Assets/Scenes/animations/frames/Nyxaris";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogWarning($"[NyxarisFrameAnimator] Folder not found at {rootPath}");
            return;
        }

        sequences.Clear();
        string[] subDirs = System.IO.Directory.GetDirectories(rootPath);

        foreach (string dir in subDirs)
        {
            string folderName = System.IO.Path.GetFileName(dir);
            string unityPath = rootPath + "/" + folderName;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { unityPath });
            if (guids == null || guids.Length == 0) continue;

            List<Sprite> frameList = new List<Sprite>();
            // Load and sort frames numerically (frame_001, frame_002, etc.)
            List<string> assetPaths = new List<string>();
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".png") || p.EndsWith(".jpg"))
                {
                    assetPaths.Add(p);
                }
            }
            assetPaths.Sort(System.StringComparer.OrdinalIgnoreCase);

            foreach (string p in assetPaths)
            {
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (s != null && !frameList.Contains(s))
                {
                    frameList.Add(s);
                }
            }

            if (frameList.Count > 0)
            {
                NamedFrameSequence seq = new NamedFrameSequence
                {
                    animationKey = folderName,
                    frames = frameList
                };
                sequences.Add(seq);
            }
        }

        BuildSequenceDictionary();
        Debug.Log($"[NyxarisFrameAnimator] Successfully indexed {sequences.Count} animation frame sequences for Nyxaris!");
        EditorUtility.SetDirty(this);
#endif
    }
}
