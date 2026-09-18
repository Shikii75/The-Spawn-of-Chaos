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
    public float defaultFPS = 24f;
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

        // High priority semantic alias matching
        if (q.Contains("stern") || q.Contains("warn") || q.Contains("grave") || q.Contains("serious"))
        {
            if (sequenceDict.TryGetValue("nyxarissternorimportantwarning-534901f6", out var wList)) return wList;
            if (sequenceDict.TryGetValue("sternorimportantwarning", out var wList2)) return wList2;
            if (sequenceDict.TryGetValue("nyxarisworriedupsetthinkingmp4-bd2148fc", out var wList3)) return wList3;
        }
        else if (q.Contains("angr") || q.Contains("rage") || q.Contains("piss") || q.Contains("fury"))
        {
            if (sequenceDict.TryGetValue("nyxarisangry-e56db4b1", out var aList)) return aList;
            if (sequenceDict.TryGetValue("nyxarismildanger-3d4eb3ea", out var aList2)) return aList2;
            if (sequenceDict.TryGetValue("nyxarispissed-7e387d67", out var aList3)) return aList3;
            if (sequenceDict.TryGetValue("angry", out var aList4)) return aList4;
        }
        else if (q.Contains("spread") || q.Contains("armsspread") || q.Contains("gift"))
        {
            if (sequenceDict.TryGetValue("nyxarisexcitedarmsspreadexplaining-cb776e55", out var sList)) return sList;
            if (sequenceDict.TryGetValue("excitedarmsspreadexplaining", out var sList2)) return sList2;
            if (sequenceDict.TryGetValue("nyxarisexplaining0-c6c6f20e", out var sList3)) return sList3;
        }
        else if (q.Contains("excit") || q.Contains("power") || q.Contains("wrath"))
        {
            if (sequenceDict.TryGetValue("nyxarisexcited-f2ab5508", out var exList)) return exList;
            if (sequenceDict.TryGetValue("nyxarisconfidently0-146ef255", out var exList2)) return exList2;
            if (sequenceDict.TryGetValue("nyxarishappytosay-e0fd84be", out var exList3)) return exList3;
            if (sequenceDict.TryGetValue("excited", out var exList4)) return exList4;
        }
        else if (q.Contains("confiden") || q.Contains("smug") || q.Contains("proud"))
        {
            if (sequenceDict.TryGetValue("nyxarisconfidently-64b9a921", out var cList)) return cList;
            if (sequenceDict.TryGetValue("nyxarisconfidently0-146ef255", out var cList2)) return cList2;
            if (sequenceDict.TryGetValue("confidently", out var cList3)) return cList3;
        }
        else if (q.Contains("happ") || q.Contains("smile") || q.Contains("glad"))
        {
            if (sequenceDict.TryGetValue("nyxarishappy-f4e5a565", out var hList)) return hList;
            if (sequenceDict.TryGetValue("nyxarishappytosay-e0fd84be", out var hList2)) return hList2;
            if (sequenceDict.TryGetValue("happy", out var hList3)) return hList3;
        }
        else if (q.Contains("explain") || q.Contains("talk"))
        {
            if (sequenceDict.TryGetValue("nyxarisexplaining0-c6c6f20e", out var exp0)) return exp0;
            if (sequenceDict.TryGetValue("nyxarisexplaining1-a4a19986", out var exp1)) return exp1;
            if (sequenceDict.TryGetValue("nyxarisexcitedarmsspreadexplaining-cb776e55", out var expSpread)) return expSpread;
            if (sequenceDict.TryGetValue("nyxaristalkingeyesclosed-a238b88a", out var talkClosed)) return talkClosed;
            if (sequenceDict.TryGetValue("explaining", out var expAlias)) return expAlias;
        }
        else if (q.Contains("think"))
        {
            if (sequenceDict.TryGetValue("nyxaristhinking-616901a8", out var tList)) return tList;
            if (sequenceDict.TryGetValue("nyxariscutelythinking-08686484", out var tList2)) return tList2;
            if (sequenceDict.TryGetValue("nyxarishappythinking-bd1f19af", out var tList3)) return tList3;
            if (sequenceDict.TryGetValue("nyxarisworriedupsetthinkingmp4-bd2148fc", out var tList4)) return tList4;
            if (sequenceDict.TryGetValue("thinking", out var tList5)) return tList5;
        }
        else if (q.Contains("neutr") || q.Contains("nuetr"))
        {
            if (sequenceDict.TryGetValue("nyxarisnuetral-411247bb", out var nList)) return nList;
            if (sequenceDict.TryGetValue("nyxarisnuetralstare-14b61402", out var nList2)) return nList2;
            if (sequenceDict.TryGetValue("nuetral", out var nList3)) return nList3;
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
