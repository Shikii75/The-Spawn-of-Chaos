#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DaraPrefabBuilder - Automatically constructs and maintains the Dara NPC prefab
/// at Assets/Prefabs/Dara.prefab and Assets/Resources/Prefabs/Dara.prefab.
///
/// Ensures all 26 idle frames, 16 latest talk frames (from daratalk1-b881739e),
/// confused mage audio clips, BoxCollider2D trigger, SpriteRenderer, AudioSource,
/// and DaraNPC components are perfectly configured.
/// </summary>
[InitializeOnLoad]
public static class DaraPrefabBuilder
{
    private const string PREFAB_PATH_1 = "Assets/Prefabs/Dara.prefab";
    private const string PREFAB_PATH_2 = "Assets/Resources/Prefabs/Dara.prefab";

    static DaraPrefabBuilder()
    {
        EditorApplication.delayCall += EnsurePrefabExists;
    }

    [MenuItem("Tools/Spawn of Chaos/Build Dara NPC Prefab")]
    public static void EnsurePrefabExists()
    {
        bool exists1 = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(PREFAB_PATH_1));
        bool exists2 = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(PREFAB_PATH_2));

        if (!exists1 || !exists2)
        {
            BuildPrefab();
        }
    }

    [MenuItem("Tools/Spawn of Chaos/Place Dara in Current Scene")]
    public static void PlaceDaraInScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH_1);
        if (prefab == null)
        {
            BuildPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH_1);
        }

        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                instance.transform.position = player.transform.position + new Vector3(3f, 0f, 0f);
            }
            else
            {
                instance.transform.position = Vector3.zero;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Dara in Scene");
            Selection.activeGameObject = instance;
            Debug.Log($"<color=#D47BFF>[DaraPrefabBuilder] Placed Dara in scene at {instance.transform.position}</color>");
        }
    }

    [MenuItem("Tools/Spawn of Chaos/Force Rebuild Dara NPC Prefab")]
    public static void BuildPrefab()
    {
        Debug.Log("<color=#D47BFF>[DaraPrefabBuilder] Building Dara NPC Prefab...</color>");

        // 1. Ensure target folders exist
        if (!Directory.Exists("Assets/Prefabs")) Directory.CreateDirectory("Assets/Prefabs");
        if (!Directory.Exists("Assets/Resources/Prefabs")) Directory.CreateDirectory("Assets/Resources/Prefabs");

        // 2. Create the GameObject
        GameObject go = new GameObject("Dara");
        go.tag = "Untagged";
        go.layer = 0;
        go.transform.position = Vector3.zero;
        go.transform.localScale = new Vector3(0.52f, 0.52f, 1f); // Harmonious scale with human NPCs

        // 3. Load animation frames
        string idleFolder = "Assets/Scenes/animations/frames/Dara/daraidle-8c309fd7";
        string talkFolder = "Assets/Scenes/animations/frames/Dara/daratalk1-b881739e"; // Latest frame folder

        Sprite[] idleSprites = LoadSpritesSafely(idleFolder);
        Sprite[] talkSprites = LoadSpritesSafely(talkFolder);

        Debug.Log($"[DaraPrefabBuilder] Loaded {idleSprites.Length} idle frames, {talkSprites.Length} talk frames.");

        // 4. SpriteRenderer
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;
        if (idleSprites != null && idleSprites.Length > 0)
        {
            sr.sprite = idleSprites[0];
        }

        Material spriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (spriteMat != null)
        {
            sr.material = spriteMat;
        }

        // 5. BoxCollider2D (Trigger for proximity detection)
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2.6f, 4.6f);
        col.offset = Vector2.zero;

        // 6. AudioSource
        AudioSource audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = false;
        audio.spatialBlend = 0.5f;

        // 7. Load Confused Mage Audio clips
        AudioClip[] confusedClips = LoadConfusedMageClips();
        Debug.Log($"[DaraPrefabBuilder] Loaded {confusedClips.Length} confused mage audio clips.");

        // 8. DaraNPC Component
        DaraNPC dara = go.AddComponent<DaraNPC>();
        dara.characterName = "Dara";
        dara.characterTitle = "✦ Follower of Nyxaris";
        dara.idleFrames = idleSprites;
        dara.talkFrames = talkSprites;
        dara.confusedMageClips = confusedClips;
        dara.fps = 18f;
        dara.interactRange = 3.5f;
        dara.bubbleHeightAboveNPC = 3.2f;

        // 9. Save as Prefabs
        PrefabUtility.SaveAsPrefabAsset(go, PREFAB_PATH_1);
        PrefabUtility.SaveAsPrefabAsset(go, PREFAB_PATH_2);

        UnityEngine.Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#55FF88>[DaraPrefabBuilder] ★ Successfully created Dara NPC Prefab at '{PREFAB_PATH_1}' and '{PREFAB_PATH_2}'!</color>");
    }

    private static AudioClip[] LoadConfusedMageClips()
    {
        string[] searchPaths = new string[]
        {
            "Assets/Resources/Voice/mage/confused",
            "Assets/Audio/Voice/mage/confused"
        };

        var clips = new List<AudioClip>();

        foreach (string path in searchPaths)
        {
            if (!Directory.Exists(path)) continue;

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { path });
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip != null && !clips.Contains(clip))
                {
                    clips.Add(clip);
                }
            }
        }

        return clips.ToArray();
    }

    private static Sprite[] LoadSpritesSafely(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[DaraPrefabBuilder] Folder not found: {folderPath}");
            return new Sprite[0];
        }

        var pngFiles = Directory.GetFiles(folderPath, "*.png")
            .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        var result = new List<Sprite>();

        foreach (var file in pngFiles)
        {
            FileInfo fi = new FileInfo(file);
            // Skip tiny empty/placeholder frames (< 5 KB like frame_001.png)
            if (fi.Length < 5000) continue;

            string unityPath = file.Replace("\\", "/");
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(unityPath);
            var sprites = allAssets.OfType<Sprite>().ToList();

            if (sprites.Count > 0)
            {
                // Select the character sprite with the largest rect area to ignore stray sliced pixels
                Sprite mainSprite = sprites.OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
                if (mainSprite != null)
                {
                    result.Add(mainSprite);
                }
            }
        }

        return result.ToArray();
    }
}
#endif
