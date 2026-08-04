#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor setup tool to automatically build & populate the Strawhat Leader NPC in Dojo 1 scene.
/// Menu: Tools -> Setup Strawhat Leader NPC
/// </summary>
public static class StrawhatLeaderSetupTool
{
    [MenuItem("Tools/Setup Strawhat Leader NPC")]
    public static void SetupStrawhatLeader()
    {
        // 1. Locate or create Strawhat Leader GameObject
        StrawhatLeaderNPC leader = Object.FindFirstObjectByType<StrawhatLeaderNPC>();
        GameObject go;
        if (leader != null)
        {
            go = leader.gameObject;
        }
        else
        {
            go = new GameObject("Strawhat Leader");
            leader = go.AddComponent<StrawhatLeaderNPC>();
            go.transform.position = new Vector3(8f, -1.2f, 0f); // Default placement on altar
        }

        // Ensure SpriteRenderer
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;

        // Ensure SpeechBubbleDialogue
        SpeechBubbleDialogue dialogue = go.GetComponent<SpeechBubbleDialogue>();
        if (dialogue == null) dialogue = go.AddComponent<SpeechBubbleDialogue>();
        dialogue.characterName = "Strawhat Leader";

        // 2. Load animation frame folders
        string basePath = "Assets/Scenes/animations/frames/strawleader";

        leader.idleFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleaderidle-6bd176a7"));
        leader.beginWalkFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleaderbeginwalk-b8c36e54"));
        leader.keepWalkingFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleaderkeepwalking-d9827282"));
        leader.stopWalkingFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleaderstopwalking-32778928"));
        
        string talkPath = Path.Combine(basePath, "talkinganimationframes");
        leader.casualTalkFrames = LoadSpritesFromFolder(Path.Combine(talkPath, "strawleadercasualtalk-b27731fd"));
        leader.explainingFrames = LoadSpritesFromFolder(Path.Combine(talkPath, "strawleaderexplaining-cc9fc453"));
        leader.explainingLosingInterestFrames = LoadSpritesFromFolder(Path.Combine(talkPath, "strawleaderexplainingandlosinginterest-123a9311"));

        leader.endOfConversationFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleaderendofconversation-0bc2dda8"));
        leader.startSittingFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleaderstartsitting-b50e7fe7"));
        leader.sitIdleFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleadersitidle-7ce88497"));
        leader.sitLookDownFrames = LoadSpritesFromFolder(Path.Combine(basePath, "strawleaderoccasionallookdownwhilesitting-1b36fe4c"));

        // Assign dialogue background music (concrete-syntax.mp3)
        leader.dialogueBGM = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/concrete-syntax.mp3");

        // Check for Strawleaderspawnpoint in scene
        GameObject spawnGo = GameObject.Find("Strawleaderspawnpoint") ?? GameObject.Find("StrawLeaderSpawnPoint") ?? GameObject.Find("strawleaderspawnpoint");
        if (spawnGo != null)
        {
            go.transform.position = spawnGo.transform.position;
            leader.customSpawnPoint = spawnGo.transform;
            Debug.Log($"[StrawhatLeaderSetupTool] Assigned spawn point '{spawnGo.name}' at {spawnGo.transform.position}");
        }

        // Assign initial sprite preview
        if (leader.idleFrames != null && leader.idleFrames.Length > 0)
        {
            sr.sprite = leader.idleFrames[0];
        }

        EditorUtility.SetDirty(leader);
        EditorUtility.SetDirty(go);
        Undo.RegisterCreatedObjectUndo(go, "Setup Strawhat Leader NPC");

        // Save as prefab in Assets/Prefabs/
        string prefabsFolder = "Assets/Prefabs";
        if (!Directory.Exists(prefabsFolder))
        {
            Directory.CreateDirectory(prefabsFolder);
            AssetDatabase.Refresh();
        }
        string prefabPath = Path.Combine(prefabsFolder, "Strawhat Leader.prefab").Replace("\\", "/");
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.UserAction);
        AssetDatabase.SaveAssets();

        Debug.Log($"[StrawhatLeaderSetupTool] ★ Strawhat Leader NPC successfully configured on '{go.name}' and saved prefab to '{prefabPath}' with all 11 animation frame sets!");
    }

    private static Sprite[] LoadSpritesFromFolder(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[StrawhatLeaderSetupTool] Folder not found: {folderPath}");
            return new Sprite[0];
        }

        string[] fileGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        return fileGuids
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
            .Where(sprite => sprite != null)
            .OrderBy(sprite => sprite.name)
            .ToArray();
    }
}
#endif
