#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SpawnOfChaos.Props;
using SpawnOfChaos.Systems;

/// <summary>
/// TutorialLevelBuilder - Automated 1-Click Editor Tool to construct the complete multi-tier Tutorial Level in TutorialScene.unity.
/// Section 1: Water Region (Purple Water & Dynamic Pink Bridge)
/// Section 2: Subterranean Blob Crawlspace (Low 1.2-unit Rock Tunnel & Tutorial Sign)
/// Section 3: Cavern Arena Wave Trial (3 Combat Waves: Melee J, Magic K Unlock, Shift Dash)
/// Section 4: High Mountain Peak Nyxaris Shrine (Divine Crystal Shrine Cage, Nyxaris Freedom, Portal to MountainPathScene)
/// </summary>
public class TutorialLevelBuilder : EditorWindow
{
    [MenuItem("Tools/Build Complete Tutorial Level")]
    [MenuItem("Tools/Dojo 2/Build Complete Tutorial Level")]
    public static void ShowWindow()
    {
        GetWindow<TutorialLevelBuilder>("Tutorial Level Builder");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🏰 Complete Tutorial Level Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Constructs all 4 multi-tier elevation sections in TutorialScene.unity with 1 click:\n" +
            "1. Water Region (Purple Water & Dynamic Pink Bridge)\n" +
            "2. Subterranean Blob Crawlspace (Low Rock Tunnel & Hold 'M' Tutorial Sign)\n" +
            "3. Cavern Arena Wave Trial (3 Combat Waves: Melee, Magic K Unlock, Shift Dash)\n" +
            "4. High Mountain Peak Nyxaris Shrine (Divine Shrine Break & Freedom Portal)", 
            MessageType.Info);

        EditorGUILayout.Space(15);
        if (GUILayout.Button("🚀 Build Complete Tutorial Level Layout", GUILayout.Height(48)))
        {
            BuildCompleteTutorialLevel();
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("🧹 Clean Up Tutorial Level Layout", GUILayout.Height(32)))
        {
            CleanUpTutorialLayout();
        }
    }

    public static void BuildCompleteTutorialLevel()
    {
        GameObject root = GameObject.Find("--- TUTORIAL_LEVEL_STRUCTURES ---");
        if (root != null)
        {
            DestroyImmediate(root);
        }

        root = new GameObject("--- TUTORIAL_LEVEL_STRUCTURES ---");
        int count = 0;

        // ── SECTION 1: WATER REGION (X = 0 to 25, Y = 0) ─────────────────
        GameObject sec1 = new GameObject("Section1_WaterRegion");
        sec1.transform.SetParent(root.transform);

        // Ground Platform before water
        MakePlatform("Water_StartGround", -5f, -1.5f, 10f, 3f, Color.gray, "Ground", sec1.transform);
        // Purple Water Body
        GameObject waterObj = new GameObject("PurpleWater_Chasm");
        waterObj.transform.SetParent(sec1.transform);
        waterObj.transform.position = new Vector3(12f, -1f, 0f);
        PurpleWater2D waterComp = waterObj.AddComponent<PurpleWater2D>();
        waterComp.width = 16f;
        waterComp.depth = 5f;
        waterComp.GenerateMeshAndNodes();

        // Dynamic Pink Bridge Cluster
        GameObject bridgeCluster = new GameObject("PinkBridge_Cluster");
        bridgeCluster.transform.SetParent(sec1.transform);
        bridgeCluster.transform.position = new Vector3(5f, -0.5f, 0f);

        for (int i = 0; i < 6; i++)
        {
            GameObject piece = MakePlatform($"BridgePiece_{i+1}", 5f + i * 2.6f, -0.5f, 2.2f, 0.6f, new Color(1f, 0.25f, 0.75f, 1f), "Untagged", bridgeCluster.transform);
        }
        DynamicPinkBridgeCluster pinkBridge = bridgeCluster.AddComponent<DynamicPinkBridgeCluster>();
        pinkBridge.buildDistance = 7f;
        pinkBridge.InitializeChildPlatforms();

        // ── SECTION 2: SUBTERRANEAN BLOB CRAWLSPACE (X = 25 to 55, Y = -6) ──
        GameObject sec2 = new GameObject("Section2_SubterraneanBlobCrawlspace");
        sec2.transform.SetParent(root.transform);

        // Subterranean Floor
        MakePlatform("Blob_Floor", 38f, -7.5f, 30f, 3f, new Color(0.25f, 0.2f, 0.3f), "Ground", sec2.transform);

        // Low Rock Overhang Ceiling (1.2 units height gap)
        MakePlatform("Blob_CeilingOverhang", 38f, -4.8f, 24f, 3f, new Color(0.2f, 0.15f, 0.25f), "Ground", sec2.transform);

        // Tutorial Signboard for Blob Form
        GameObject signObj = new GameObject("Blob_TutorialSign");
        signObj.transform.SetParent(sec2.transform);
        signObj.transform.position = new Vector3(26f, -5.2f, 0f);
        SpriteRenderer signSr = signObj.AddComponent<SpriteRenderer>();
        signSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        signSr.color = new Color(0.9f, 0.8f, 0.3f, 1f);
        signObj.transform.localScale = new Vector3(2.5f, 1.2f, 1f);

        // ── SECTION 3: CAVERN ARENA WAVE TRIAL (X = 60 to 95, Y = 4) ─────
        GameObject sec3 = new GameObject("Section3_CavernArenaWaveTrial");
        sec3.transform.SetParent(root.transform);

        // Cavern Arena Floor
        MakePlatform("Cavern_ArenaFloor", 77f, 2.5f, 35f, 3f, new Color(0.3f, 0.25f, 0.2f), "Ground", sec3.transform);
        // Cavern Roof
        MakePlatform("Cavern_Roof", 77f, 12f, 35f, 3f, new Color(0.25f, 0.2f, 0.15f), "Ground", sec3.transform);

        // Wave Manager
        GameObject waveManagerObj = new GameObject("TutorialCaveWaveManager");
        waveManagerObj.transform.SetParent(sec3.transform);
        waveManagerObj.transform.position = new Vector3(77f, 4f, 0f);
        TutorialCaveWaveManager waveManager = waveManagerObj.AddComponent<TutorialCaveWaveManager>();

        // Wave Spawn Points
        GameObject spL = new GameObject("WaveSpawn_Left");
        spL.transform.SetParent(waveManagerObj.transform);
        spL.transform.position = new Vector3(64f, 4.2f, 0f);

        GameObject spR = new GameObject("WaveSpawn_Right");
        spR.transform.SetParent(waveManagerObj.transform);
        spR.transform.position = new Vector3(90f, 4.2f, 0f);

        waveManager.waveSpawnPointLeft = spL.transform;
        waveManager.waveSpawnPointRight = spR.transform;

        // Cavern Exit Gate
        GameObject exitGate = MakePlatform("Cavern_ExitGate", 94f, 6f, 1.5f, 5f, new Color(0.8f, 0.2f, 0.2f, 1f), "Ground", sec3.transform);
        waveManager.cavernExitGate = exitGate;

        // Arena Trigger Zone
        GameObject arenaTrigger = new GameObject("CavernArena_Trigger");
        arenaTrigger.transform.SetParent(sec3.transform);
        arenaTrigger.transform.position = new Vector3(66f, 5f, 0f);
        BoxCollider2D trigCol = arenaTrigger.AddComponent<BoxCollider2D>();
        trigCol.isTrigger = true;
        trigCol.size = new Vector2(3f, 6f);

        // ── SECTION 4: HIGH MOUNTAIN PEAK NYXARIS SHRINE (X = 100 to 135, Y = 12)
        GameObject sec4 = new GameObject("Section4_NyxarisShrineAltar");
        sec4.transform.SetParent(root.transform);

        // High Mountain Altar Platform
        MakePlatform("Nyxaris_PeakAltarFloor", 115f, 10.5f, 30f, 3f, new Color(0.35f, 0.3f, 0.45f), "Ground", sec4.transform);

        // Divine Crystal Shrine Cage
        GameObject shrineCageObj = new GameObject("Nyxaris_ShrineCage");
        shrineCageObj.transform.SetParent(sec4.transform);
        shrineCageObj.transform.position = new Vector3(115f, 12.8f, 0f);

        SpriteRenderer srShrine = shrineCageObj.AddComponent<SpriteRenderer>();
        srShrine.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        srShrine.color = new Color(0.9f, 0.2f, 0.95f, 0.85f); // Glowing Magenta Crystal
        shrineCageObj.transform.localScale = new Vector3(2.5f, 3.8f, 1f);

        BoxCollider2D colShrine = shrineCageObj.AddComponent<BoxCollider2D>();
        colShrine.size = new Vector2(1f, 1f);

        NyxarisShrineCage shrineComp = shrineCageObj.AddComponent<NyxarisShrineCage>();
        shrineComp.health = 50;

        // Nyxaris NPC Sprite inside Cage
        GameObject nyxarisNPC = new GameObject("Nyxaris_NPC_Trapped");
        nyxarisNPC.transform.SetParent(shrineCageObj.transform);
        nyxarisNPC.transform.localPosition = new Vector3(0f, 0f, 0f);
        nyxarisNPC.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        SpriteRenderer srNyx = nyxarisNPC.AddComponent<SpriteRenderer>();
        srNyx.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        srNyx.color = new Color(0.4f, 0.85f, 1f, 1f); // Cyan goddess glow

        shrineComp.nyxarisNPCObject = nyxarisNPC;

        // Level Exit Portal (entersign leading to MountainPathScene)
        GameObject portalObj = new GameObject("Nyxaris_FreedomPortal");
        portalObj.transform.SetParent(sec4.transform);
        portalObj.transform.position = new Vector3(124f, 12.8f, 0f);
        portalObj.tag = "entersign";

        SpriteRenderer srPortal = portalObj.AddComponent<SpriteRenderer>();
        srPortal.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        srPortal.color = new Color(0.2f, 0.9f, 0.4f, 1f); // Green portal
        portalObj.transform.localScale = new Vector3(2f, 3.5f, 1f);

        BoxCollider2D colPortal = portalObj.AddComponent<BoxCollider2D>();
        colPortal.isTrigger = true;
        colPortal.size = new Vector2(1f, 1f);

        entersign sign = portalObj.AddComponent<entersign>();
        sign.targetSceneName = "MountainPathScene";
        sign.targetSpawnPointName = "MountainPath_Entrance";

        shrineComp.exitPortalObject = portalObj;

        Undo.RegisterCreatedObjectUndo(root, "Build Tutorial Level Structures");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[TutorialLevelBuilder] Successfully constructed complete Tutorial Level structures!");
    }

    public static void CleanUpTutorialLayout()
    {
        GameObject root = GameObject.Find("--- TUTORIAL_LEVEL_STRUCTURES ---");
        if (root != null)
        {
            DestroyImmediate(root);
            Debug.Log("[TutorialLevelBuilder] Cleaned up Tutorial Level structures.");
        }
    }

    private static GameObject MakePlatform(string name, float cx, float cy, float w, float h, Color color, string tag, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(cx, cy, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);

        try { go.tag = tag; } catch { go.tag = "Untagged"; }

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        sr.color = color;

        if (tag == "Ground")
        {
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        }

        return go;
    }
}
#endif
