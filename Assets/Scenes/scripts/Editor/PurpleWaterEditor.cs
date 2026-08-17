#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using SpawnOfChaos.Props;

/// <summary>
/// PurpleWaterEditor - Custom Unity Editor Inspector and 1-Click Spawner for PurpleWater2D.
/// Includes buttons for testing splash impulses, rebuilding water meshes, and saving prefabs.
/// </summary>
[CustomEditor(typeof(PurpleWater2D))]
public class PurpleWaterEditor : Editor
{
    [MenuItem("Tools/Create 2D Purple Water Body")]
    public static void CreatePurpleWaterObject()
    {
        GameObject waterGo = new GameObject("PurpleWater2D");
        waterGo.transform.position = Vector3.zero;

        PurpleWater2D water = waterGo.AddComponent<PurpleWater2D>();
        water.GenerateMeshAndNodes();

        Selection.activeGameObject = waterGo;
        Undo.RegisterCreatedObjectUndo(waterGo, "Create Purple Water Body");
        Debug.Log("[PurpleWaterEditor] Successfully created 2D Purple Water Body!");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PurpleWater2D water = (PurpleWater2D)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🔮 Purple Water Controls & Diagnostics", EditorStyles.boldLabel);

        if (GUILayout.Button("🔄 Rebuild Water Mesh & Spring Nodes", GUILayout.Height(32)))
        {
            water.GenerateMeshAndNodes();
            EditorUtility.SetDirty(water);
            Debug.Log("[PurpleWaterEditor] Rebuilt water surface mesh.");
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("💦 Test Splashdown Impulse (Center)", GUILayout.Height(32)))
        {
            water.SplashAtWorldPosition(water.transform.position, -8f);
            PurpleWaterSplashFX.SpawnSplash(water.transform.position, -8f, water.topSurfaceColor, water.foamGlowColor);
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("📦 Save as Prefab (Assets/Resources/Prefabs/Environment/)", GUILayout.Height(34)))
        {
            SaveAsPrefab(water.gameObject);
        }
    }

    public static void SaveAsPrefab(GameObject waterGo)
    {
        string dir = "Assets/Resources/Prefabs/Environment";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string prefabPath = $"{dir}/PurpleWater2D.prefab";
        PrefabUtility.SaveAsPrefabAsset(waterGo, prefabPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PurpleWaterEditor] Saved prefab successfully to '{prefabPath}'!");
        EditorUtility.DisplayDialog("Prefab Saved", $"Saved Purple Water prefab to:\n{prefabPath}", "OK");
    }
}
#endif
