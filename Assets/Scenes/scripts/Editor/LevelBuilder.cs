using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class LevelBuilder : EditorWindow
{
    [MenuItem("Tools/Build Project Scenes")]
    public static void BuildAllScenes()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Rebuild All Scenes?",
            "Warning: This will completely regenerate all scenes (MountainPathScene, Dojo1Scene, Dojo2Scene, CaveScene) from scratch, OVERWRITING and ERASING any custom designs, objects, and decorations you have made. Are you sure you want to proceed?",
            "Yes, Overwrite Everything",
            "Cancel"
        );
        
        if (!confirm)
        {
            Debug.Log("[LevelBuilder] Rebuild cancelled. Custom designs are safe.");
            return;
        }

        // 1. Build SampleScene (Outdoor and mountain navigation)
        BuildOutdoorScene();

        // 2. Build Dojo1Scene (Interior combat)
        BuildDojoScene("Assets/Scenes/Dojo1Scene.unity", "Dojo 1 Interior", 1);

        // 3. Build Dojo2Scene (Interior combat)
        BuildDojoScene("Assets/Scenes/Dojo2Scene.unity", "Dojo 2 Interior", 2);

        // 4. Build CaveScene (Cave dungeon and Boss arena)
        BuildCaveScene();

        Debug.Log("[LevelBuilder] All game scenes generated and saved successfully!");
    }

    private static void BuildOutdoorScene()
    {
        // Load or create MountainPathScene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "MountainPathScene";

        int count = 0;
        Color32 groundDirt = new Color32(100, 75, 50, 255);
        Color32 mountainLight = new Color32(110, 112, 120, 255);
        Color32 orbPurple = new Color32(80, 60, 130, 255);
        Color32 mountainGray = new Color32(80, 80, 90, 255);

        // ── SECTION 1 : Telepathy Orb Platform ──
        M("OrbApproach_Ground", 99, -4.5f, 20, 2, groundDirt, "Ground", ref count);
        M("OrbStep1", 95, 1, 6, 1, mountainLight, "Ground", ref count);
        M("OrbStep2", 101, 3, 6, 1, mountainLight, "Ground", ref count);
        M("OrbStep3", 107, 5, 6, 1, mountainLight, "Ground", ref count);
        M("OrbPlatform", 113, 9, 16, 1.5f, orbPurple, "Ground", ref count);
        M("OrbPillar_L", 113, 4.5f, 1, 5f, orbPurple, "Untagged", ref count);
        M("OrbPillar_R", 128, 4.5f, 1, 5f, orbPurple, "Untagged", ref count);
        M("TelepathyOrb", 121, 11, 2, 2, new Color32(180, 120, 255, 255), "Untagged", ref count);

        // ── SECTION 2 : Mountain Path → Dojo 1 ──
        M("Mt1_CliffFace", 130, 18, 2.5f, 60, mountainGray, "Ground", ref count);
        float[] lx1 = { 132, 139, 146, 153, 160, 167, 174, 181, 188, 195, 202, 209 };
        float[] ly1 = { 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31 };
        for (int i = 0; i < lx1.Length; i++)
            M("Mt1_Ledge" + i, lx1[i], ly1[i], 9f, 1f, mountainLight, "Ground", ref count);

        // Dojo 1 Door (Outdoor Portal)
        var door1 = new GameObject("Dojo1_EntranceDoor");
        door1.transform.position = new Vector3(209, 32.5f, 0f);
        var trigger1 = door1.AddComponent<BoxCollider2D>();
        trigger1.isTrigger = true;
        trigger1.size = new Vector2(2f, 3f);
        var trans1 = door1.AddComponent<DojoDoorTransition>();
        trans1.doorName = "Dojo 1 (Air Clan)";
        trans1.loadScene = true;
        trans1.targetSceneName = "Dojo1Scene";
        trans1.spawnPointName = "Dojo1_SpawnPoint";
        trans1.availablePromptText = "Press E to enter Dojo 1";

        // ── SECTION 4 : Mountain Path → Dojo 2 ──
        M("Mt2_CliffFace", 259, 45, 2.5f, 60, mountainGray, "Ground", ref count);
        float[] lx2 = { 261, 269, 277, 285, 293, 301, 309, 317, 325, 333, 341, 349, 357, 365 };
        float[] ly2 = { 31, 33, 35, 37, 39, 41, 43, 45, 47, 49, 51, 53, 55, 57 };
        for (int i = 0; i < lx2.Length; i++)
            M("Mt2_Ledge" + i, lx2[i], ly2[i], 10f, 1f, mountainLight, "Ground", ref count);

        // Dojo 2 Door (Outdoor Portal)
        var door2 = new GameObject("Dojo2_EntranceDoor");
        door2.transform.position = new Vector3(365, 58.5f, 0f);
        var trigger2 = door2.AddComponent<BoxCollider2D>();
        trigger2.isTrigger = true;
        trigger2.size = new Vector2(2f, 3f);
        var trans2 = door2.AddComponent<DojoDoorTransition>();
        trans2.doorName = "Dojo 2 (Earth Clan)";
        trans2.loadScene = true;
        trans2.targetSceneName = "Dojo2Scene";
        trans2.spawnPointName = "Dojo2_SpawnPoint";
        trans2.availablePromptText = "Press E to enter Dojo 2";

        // ── SECTION 6 : Mountain Descent to Cave ──
        M("Descent_CliffFace", 418, 30, 2.5f, 60, mountainGray, "Ground", ref count);
        float[] dx = { 420, 428, 436, 444, 452, 460, 468, 476, 484, 492, 500 };
        float[] dy = { 54, 48, 42, 36, 29, 22, 15, 8, 2, -3, -6 };
        for (int i = 0; i < dx.Length; i++)
            M("Descent_Ledge" + i, dx[i], dy[i], 10f, 1f, mountainLight, "Ground", ref count);

        // Cave Entrance Portal (Outdoor Trigger)
        var caveEntrance = new GameObject("Cave_EntrancePortal");
        caveEntrance.transform.position = new Vector3(500, -5f, 0f);
        var triggerCave = caveEntrance.AddComponent<BoxCollider2D>();
        triggerCave.isTrigger = true;
        triggerCave.size = new Vector2(3f, 4f);
        var transCave = caveEntrance.AddComponent<DojoDoorTransition>();
        transCave.doorName = "Spider Cave";
        transCave.loadScene = true;
        transCave.targetSceneName = "CaveScene";
        transCave.spawnPointName = "Cave_EntranceSpawn";
        transCave.availablePromptText = "Press E to enter the Cave";

        // Setup Scene Spawner & persistent components
        SetupPlayerSpawnerInScene("DefaultSpawnPoint", new Vector3(85, -3f, 0f));

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MountainPathScene.unity");
    }

    private static void BuildDojoScene(string scenePath, string dojoName, int dojoNum)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = System.IO.Path.GetFileNameWithoutExtension(scenePath);

        int count = 0;
        Color32 dojoWood = new Color32(139, 90, 43, 255);
        Color32 dojoRoof = new Color32(60, 40, 20, 255);

        // Dojo Interior Structure
        float dX = 0, dY = 0;
        M("Dojo_Floor", dX, dY, 50, 1.5f, dojoWood, "Ground", ref count);
        M("Dojo_WallL", dX - 24, dY + 6.5f, 2, 13, dojoWood, "Ground", ref count);
        M("Dojo_WallR", dX + 24, dY + 6.5f, 2, 13, dojoWood, "Ground", ref count);
        M("Dojo_BackWall", dX, dY + 6.5f, 46, 0.5f, dojoWood, "Untagged", ref count);
        M("Dojo_RoofL", dX - 12, dY + 14, 28, 2, dojoRoof, "Untagged", ref count);
        M("Dojo_RoofR", dX + 12, dY + 14, 28, 2, dojoRoof, "Untagged", ref count);
        M("Dojo_RoofPeak", dX, dY + 16, 20, 1.5f, dojoRoof, "Untagged", ref count);
        M("Dojo_Pillar1", dX - 22, dY + 6.5f, 1.5f, 11, dojoRoof, "Untagged", ref count);
        M("Dojo_Pillar2", dX - 8, dY + 6.5f, 1.5f, 11, dojoRoof, "Untagged", ref count);
        M("Dojo_Pillar3", dX + 8, dY + 6.5f, 1.5f, 11, dojoRoof, "Untagged", ref count);
        M("Dojo_Pillar4", dX + 22, dY + 6.5f, 1.5f, 11, dojoRoof, "Untagged", ref count);
        M("Dojo_Mat", dX, dY + 0.8f, 40, 0.5f, dojoRoof, "Untagged", ref count);

        // Dojo Spawn Point
        GameObject spawnPt = new GameObject("Dojo" + dojoNum + "_SpawnPoint");
        spawnPt.transform.position = new Vector3(dX - 20, dY + 2f, 0f);

        // Dojo Exit Door
        GameObject exitDoor = new GameObject("Dojo" + dojoNum + "_ExitDoor");
        exitDoor.transform.position = new Vector3(dX - 22f, dY + 2f, 0f);
        var trigger = exitDoor.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(2f, 3f);
        var trans = exitDoor.AddComponent<DojoDoorTransition>();
        trans.doorName = "Exit Dojo";
        trans.loadScene = true;
        trans.targetSceneName = "MountainPathScene";
        trans.spawnPointName = dojoNum == 1 ? "Dojo1_ExitSpawnPoint" : "Dojo2_ExitSpawnPoint";
        trans.availablePromptText = "Press E to leave Dojo";

        // Add Dojo Wave Challenge Trigger
        GameObject challengeObj = new GameObject("DojoWaveChallenge");
        challengeObj.transform.position = new Vector3(dX, dY + 2f, 0f);
        var col = challengeObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(4f, 4f);
        var waveMgr = challengeObj.AddComponent<DojoWaveManager>();

        // Create Dojo Gates (colliders to block exit during challenge)
        GameObject gateL = new GameObject("Gate_L");
        gateL.transform.position = new Vector3(dX - 23f, dY + 3f, 0f);
        gateL.AddComponent<BoxCollider2D>();
        M("Gate_L_Visual", dX - 23f, dY + 3f, 1f, 5f, new Color32(200, 50, 50, 255), "Untagged", ref count).transform.SetParent(gateL.transform);

        GameObject gateR = new GameObject("Gate_R");
        gateR.transform.position = new Vector3(dX + 23f, dY + 3f, 0f);
        gateR.AddComponent<BoxCollider2D>();
        M("Gate_R_Visual", dX + 23f, dY + 3f, 1f, 5f, new Color32(200, 50, 50, 255), "Untagged", ref count).transform.SetParent(gateR.transform);

        waveMgr.dojoGates = new GameObject[] { gateL, gateR };

        // Setup spawner
        SetupPlayerSpawnerInScene("Dojo" + dojoNum + "_SpawnPoint", new Vector3(dX - 20, dY + 2f, 0f));

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static void BuildCaveScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "CaveScene";

        int count = 0;
        Color32 caveRock = new Color32(55, 50, 60, 255);
        Color32 caveDark = new Color32(35, 30, 40, 255);
        Color32 saveCrystal = new Color32(0, 200, 255, 255);
        Color32 stoneTrap = new Color32(90, 88, 80, 255);
        Color32 bossCave = new Color32(40, 20, 30, 255);

        float cx = 0, cy = 0;

        // ── Cave Network ──
        M("Cave_Floor", cx + 65, cy - 7, 130, 2, caveRock, "Ground", ref count);
        M("Cave_Ceiling", cx + 65, cy + 14, 130, 3, caveDark, "Ground", ref count);
        M("Cave_EntranceWall", cx - 1, cy + 4, 3, 22, caveRock, "Ground", ref count);

        // Exit door back to SampleScene
        GameObject exitPortal = new GameObject("Cave_ExitPortal");
        exitPortal.transform.position = new Vector3(cx + 2f, cy - 5f, 0f);
        var trigger = exitPortal.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(2f, 4f);
        var trans = exitPortal.AddComponent<DojoDoorTransition>();
        trans.doorName = "Leave Cave";
        trans.loadScene = true;
        trans.targetSceneName = "MountainPathScene";
        trans.spawnPointName = "Cave_ExitSpawnPoint";
        trans.availablePromptText = "Press E to return to Mountain";

        // Stalactites and stalagmites
        M("Cave_Stal1", cx + 15, cy + 11, 1.5f, 4, caveDark, "Untagged", ref count);
        M("Cave_Stal2", cx + 30, cy + 12, 1.2f, 5, caveDark, "Untagged", ref count);
        M("Cave_Stal3", cx + 48, cy + 11, 1.8f, 4, caveDark, "Untagged", ref count);
        M("Cave_Stal4", cx + 63, cy + 12, 1.0f, 5, caveDark, "Untagged", ref count);
        M("Cave_Stalag1", cx + 20, cy - 7, 1f, 2.5f, caveRock, "Untagged", ref count);
        M("Cave_Stalag2", cx + 40, cy - 7, 0.8f, 2f, caveRock, "Untagged", ref count);

        // Dividers and ledges
        M("Cave_Wall1", cx + 45, cy - 6, 2, 22, caveRock, "Ground", ref count);
        M("Cave_Wall2", cx + 65, cy - 6, 2, 22, caveRock, "Ground", ref count);
        M("Cave_Ledge1", cx + 20, cy + 4, 10, 1, caveRock, "Ground", ref count);
        M("Cave_Ledge2", cx + 55, cy + 0, 10, 1, caveRock, "Ground", ref count);
        M("Cave_Ledge3", cx + 78, cy + 6, 10, 1, caveRock, "Ground", ref count);

        // ── Save Point Room (Pulley Trap) ──
        float sx = cx + 68;
        M("Save_Floor", sx + 30, cy - 7, 62, 2, caveRock, "Ground", ref count);
        M("Save_Ceiling", sx + 30, cy + 16, 62, 2.5f, caveDark, "Ground", ref count);
        M("Save_WallL", sx, cy + 4, 2.5f, 24, caveRock, "Ground", ref count);
        M("Save_WallR", sx + 60, cy + 4, 2.5f, 24, caveRock, "Ground", ref count);
        M("SavePoint_Crystal", sx + 27, cy - 5.5f, 3, 4, saveCrystal, "Untagged", ref count);
        M("SavePoint_Glow", sx + 27, cy - 1.5f, 6, 0.3f, saveCrystal, "Untagged", ref count);
        M("Pulley_Track", sx + 20, cy + 14, 24, 0.6f, stoneTrap, "Untagged", ref count);
        M("Pulley_RopeL", sx + 23, cy + 6, 0.3f, 9, stoneTrap, "Untagged", ref count);
        M("Pulley_RopeR", sx + 39, cy + 6, 0.3f, 9, stoneTrap, "Untagged", ref count);
        M("CrushingStone", sx + 29, cy + 10, 8, 5, stoneTrap, "Untagged", ref count);

        // ── Tsuchigumo's Cave (Boss Arena) ──
        float bx = sx + 60;
        M("Boss_Tunnel_Floor", bx + 22, cy - 7, 45, 2, bossCave, "Ground", ref count);
        M("Boss_Tunnel_Ceiling", bx + 22, cy + 16, 45, 2, bossCave, "Ground", ref count);
        M("Boss_Tunnel_WallR", bx + 45, cy + 4, 2.5f, 24, bossCave, "Ground", ref count);
        M("Boss_Descent1", bx + 47, cy + 0, 10, 1, bossCave, "Ground", ref count);
        M("Boss_Descent2", bx + 57, cy - 7, 10, 1, bossCave, "Ground", ref count);
        M("Boss_Descent3", bx + 67, cy - 14, 10, 1, bossCave, "Ground", ref count);

        float bax = bx + 70, bay = cy - 22;
        M("Boss_Floor", bax + 55, bay, 110, 2, bossCave, "Ground", ref count);
        M("Boss_Ceiling", bax + 55, bay + 42, 110, 3, bossCave, "Ground", ref count);
        M("Boss_WallL", bax, bay + 20, 3, 44, bossCave, "Ground", ref count);
        M("Boss_WallR", bax + 107, bay + 20, 3, 44, bossCave, "Ground", ref count);
        M("Boss_Platform1", bax + 12, bay + 10, 16, 1, caveRock, "Ground", ref count);
        M("Boss_Platform2", bax + 45, bay + 18, 20, 1, caveRock, "Ground", ref count);
        M("Boss_Platform3", bax + 78, bay + 10, 16, 1, caveRock, "Ground", ref count);
        M("Boss_WebL1", bax + 3, bay + 35, 9, 0.4f, bossCave, "Untagged", ref count);
        M("Boss_WebL2", bax + 3, bay + 28, 6, 0.4f, bossCave, "Untagged", ref count);
        M("Boss_Altar", bax + 49, bay + 1.5f, 12, 2.5f, new Color32(80, 0, 30, 255), "Untagged", ref count);

        // Setup spawner
        SetupPlayerSpawnerInScene("Cave_EntranceSpawn", new Vector3(cx + 4f, cy - 5f, 0f));

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/CaveScene.unity");
    }

    private static void SetupPlayerSpawnerInScene(string defaultSpawnPointName, Vector3 defaultPos)
    {
        // Spawner configuration
        GameObject spawnerObj = GameObject.Find("PlayerSceneSpawner");
        if (spawnerObj == null)
        {
            spawnerObj = new GameObject("PlayerSceneSpawner");
            var spawner = spawnerObj.AddComponent<PlayerSceneSpawner>();
            spawner.defaultSpawnPointName = defaultSpawnPointName;
        }

        // Spawn point marker
        GameObject defaultSpawn = GameObject.Find(defaultSpawnPointName);
        if (defaultSpawn == null)
        {
            defaultSpawn = new GameObject(defaultSpawnPointName);
            defaultSpawn.transform.position = defaultPos;
        }

        // Exit points configurations
        if (defaultSpawnPointName == "DefaultSpawnPoint")
        {
            if (GameObject.Find("Dojo1_ExitSpawnPoint") == null)
            {
                var pt = new GameObject("Dojo1_ExitSpawnPoint");
                pt.transform.position = new Vector3(212f, 31f, 0f);
            }
            if (GameObject.Find("Dojo2_ExitSpawnPoint") == null)
            {
                var pt = new GameObject("Dojo2_ExitSpawnPoint");
                pt.transform.position = new Vector3(368f, 57f, 0f);
            }
            if (GameObject.Find("Cave_ExitSpawnPoint") == null)
            {
                var pt = new GameObject("Cave_ExitSpawnPoint");
                pt.transform.position = new Vector3(496f, -4f, 0f);
            }
        }
    }

    private static GameObject M(string name, float cx, float cy, float w, float h, Color32 col, string tag, ref int count)
    {
        var go = MakeLevelBlock(name, cx, cy, w, h, col, tag);
        count++;
        return go;
    }

    private static GameObject MakeLevelBlock(string name, float cx, float cy, float w, float h, Color32 color, string tag)
    {
        var go = new GameObject(name);

        try { go.tag = tag; }
        catch { go.tag = "Untagged"; }

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sr.sprite == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite Square");
            if (guids.Length > 0)
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        sr.color = color;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 0;

        if (tag == "Ground")
            go.AddComponent<BoxCollider2D>();

        go.transform.position = new Vector3(cx, cy, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);

        Undo.RegisterCreatedObjectUndo(go, "LevelBuilder: " + name);
        return go;
    }
}
