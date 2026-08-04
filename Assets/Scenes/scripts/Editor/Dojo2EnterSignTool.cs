#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dojo2EnterSignTool - Editor Menu Tool to duplicate the 'press E' entersign
/// and attach it to Dojo 2 in SampleScene so players can enter Dojo 2.
/// </summary>
public class Dojo2EnterSignTool : EditorWindow
{
    [MenuItem("Tools/Dojo 2/Duplicate Enter Sign for Dojo 2")]
    public static void ShowWindow()
    {
        GetWindow<Dojo2EnterSignTool>("Dojo 2 Enter Sign Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Dojo 2 Enter Sign Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Duplicates the original 'press E' entersign and attaches it to the Dojo 2 entrance " +
            "at the top of the parkour path (X: 1476.5, Y: 323.5).\n" +
            "Creates 'Dojo2_SpawnPoint' inside Dojo 2 arena.", 
            MessageType.Info);

        if (GUILayout.Button("Duplicate & Attach Enter Sign to Dojo 2", GUILayout.Height(40)))
        {
            DuplicateAndAttachEnterSign();
        }
    }

    public static void DuplicateAndAttachEnterSign()
    {
        // 1. Find or create Dojo 2 Spawn Point inside the arena
        GameObject dojo2Root = GameObject.Find("Dojo 2") ?? GameObject.Find("Dojo2");
        Vector3 dojo2ArenaPos = new Vector3(1480.9f, 323.5f, 0f);
        Vector3 dojo2EntrancePos = new Vector3(1476.5f, 323.5f, 0f);

        if (dojo2Root != null)
        {
            dojo2ArenaPos = dojo2Root.transform.position;
            dojo2EntrancePos = dojo2Root.transform.position + new Vector3(-4.4f, 0f, 0f);
        }

        GameObject spawnPointObj = GameObject.Find("Dojo2_SpawnPoint");
        if (spawnPointObj == null)
        {
            spawnPointObj = new GameObject("Dojo2_SpawnPoint");
            if (dojo2Root != null) spawnPointObj.transform.SetParent(dojo2Root.transform);
            spawnPointObj.transform.position = dojo2ArenaPos + new Vector3(0f, 0.5f, 0f);
            Undo.RegisterCreatedObjectUndo(spawnPointObj, "Create Dojo2_SpawnPoint");
        }

        // 2. Find existing entersign to duplicate
        GameObject originalEnterSign = GameObject.Find("press\"E\"") ?? GameObject.FindWithTag("entersign");
        GameObject newEnterSign = null;

        if (originalEnterSign != null)
        {
            Debug.Log($"[Dojo2EnterSignTool] Duplicating existing enter sign '{originalEnterSign.name}' for Dojo 2.");
            newEnterSign = Instantiate(originalEnterSign);
            newEnterSign.name = "Dojo2_EnterSign";
            Undo.RegisterCreatedObjectUndo(newEnterSign, "Duplicate Enter Sign for Dojo 2");
        }
        else
        {
            Debug.Log("[Dojo2EnterSignTool] Creating new Enter Sign for Dojo 2.");
            newEnterSign = new GameObject("Dojo2_EnterSign");
            newEnterSign.tag = "entersign";
            BoxCollider2D col = newEnterSign.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(4f, 3f);
            Undo.RegisterCreatedObjectUndo(newEnterSign, "Create Enter Sign for Dojo 2");
        }

        // Parent under Dojo 2 if available
        if (dojo2Root != null)
        {
            newEnterSign.transform.SetParent(dojo2Root.transform);
        }
        newEnterSign.transform.position = dojo2EntrancePos;

        // Configure entersign component
        entersign enterSignComp = newEnterSign.GetComponent<entersign>();
        if (enterSignComp == null)
        {
            enterSignComp = newEnterSign.AddComponent<entersign>();
        }

        enterSignComp.targetSceneName = "Dojo2Scene";
        enterSignComp.targetSpawnPointName = "Dojo2_SpawnPoint";
        enterSignComp.interactKey = KeyCode.E;

        // Configure BoxCollider2D trigger
        BoxCollider2D box = newEnterSign.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            if (box.size.x < 2f) box.size = new Vector2(4f, 3.5f);
        }

        EditorUtility.SetDirty(newEnterSign);
        EditorUtility.SetDirty(enterSignComp);

        Debug.Log("[Dojo2EnterSignTool] Successfully attached Dojo 2 Enter Sign at position " + dojo2EntrancePos + "!");
        Selection.activeGameObject = newEnterSign;
    }
}
#endif
