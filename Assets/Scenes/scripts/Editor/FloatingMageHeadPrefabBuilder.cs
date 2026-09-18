#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SpawnOfChaos.Entities;

/// <summary>
/// FloatingMageHeadPrefabBuilder - Automatically builds and keeps updated the FloatingMageHead prefab
/// at Assets/Prefabs/FloatingMageHead.prefab and Assets/Resources/Prefabs/FloatingMageHead.prefab
/// so designers/developers can adjust size, scale, colliders, and animation parameters directly in the editor.
/// </summary>
[InitializeOnLoad]
public static class FloatingMageHeadPrefabBuilder
{
    static FloatingMageHeadPrefabBuilder()
    {
        EditorApplication.delayCall += EnsurePrefabExists;
    }

    [MenuItem("Tools/Spawn of Chaos/Build Floating Mage Head Prefab")]
    public static void EnsurePrefabExists()
    {
        string prefabPath1 = "Assets/Prefabs/FloatingMageHead.prefab";
        string prefabPath2 = "Assets/Resources/Prefabs/FloatingMageHead.prefab";

        bool exists1 = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath1));
        bool exists2 = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath2));

        if (exists1 && exists2) return;

        BuildPrefab();
    }

    [MenuItem("Tools/Spawn of Chaos/Force Rebuild Floating Mage Head Prefab")]
    public static void BuildPrefab()
    {
        string prefabPath1 = "Assets/Prefabs/FloatingMageHead.prefab";
        string prefabPath2 = "Assets/Resources/Prefabs/FloatingMageHead.prefab";

        GameObject go = new GameObject("FloatingMageHead");
        go.tag = "Untagged";

        // 1. SpriteRenderer
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 60;

        // Load first frame sprite
        Sprite firstFrame = Resources.Load<Sprite>("FloatingMageHead/frame_001");
        if (firstFrame == null)
        {
            var sprites = Resources.LoadAll<Sprite>("FloatingMageHead");
            if (sprites != null && sprites.Length > 0) firstFrame = sprites[0];
        }
        if (firstFrame == null)
        {
            firstFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/FloatingMageHead/frame_001.png");
        }
        if (firstFrame != null)
        {
            sr.sprite = firstFrame;
        }

        // 2. CircleCollider2D
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 1.2f;

        // 3. FloatingMageHeadCollectible
        FloatingMageHeadCollectible comp = go.AddComponent<FloatingMageHeadCollectible>();
        comp.fps = 18f;
        comp.bobSpeed = 2.4f;
        comp.bobHeight = 0.22f;

        // Default transform scale
        go.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);

        // Ensure directories exist
        if (!System.IO.Directory.Exists("Assets/Prefabs")) System.IO.Directory.CreateDirectory("Assets/Prefabs");
        if (!System.IO.Directory.Exists("Assets/Resources/Prefabs")) System.IO.Directory.CreateDirectory("Assets/Resources/Prefabs");

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath1);
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath2);

        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#55FF88>[FloatingMageHeadPrefabBuilder] Successfully created FloatingMageHead prefab at " + prefabPath1 + " and " + prefabPath2 + "</color>");
    }
}
#endif
