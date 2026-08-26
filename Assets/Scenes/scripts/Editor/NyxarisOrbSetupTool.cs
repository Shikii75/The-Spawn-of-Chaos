using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Nyxaris Editor Setup & Prefab Generation Tools.
/// </summary>
public static class NyxarisOrbSetupTool
{
    [MenuItem("Tools/Setup Nyxaris Orb Guide In Scene")]
    public static void SetupNyxarisGuide()
    {
        NyxarisOrbGuide guide = Object.FindFirstObjectByType<NyxarisOrbGuide>();
        GameObject masterGO;

        if (guide == null)
        {
            masterGO = new GameObject("Nyxaris_Guide_Master");
            guide = masterGO.AddComponent<NyxarisOrbGuide>();
            Undo.RegisterCreatedObjectUndo(masterGO, "Create Nyxaris Guide Master");
            Debug.Log("<color=#D47BFF>[NyxarisSetup] Created 'Nyxaris_Guide_Master' with NyxarisOrbGuide component.</color>");
        }
        else
        {
            masterGO = guide.gameObject;
            Debug.Log("<color=#D47BFF>[NyxarisSetup] Found existing NyxarisOrbGuide on '" + masterGO.name + "'.</color>");
        }

        // Link custom artwork sprite if available
        Sprite coreArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Nyxaris/Nyxaris_CoreArtwork.png");
        if (coreArt != null)
        {
            guide.customCoreArtworkSprite = coreArt;
        }

        guide.RebuildVisualsInEditMode();

        Selection.activeGameObject = masterGO;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Generate Nyxaris Orb Guide Prefab (Save to Assets)")]
    public static void GenerateNyxarisPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject tempGO = new GameObject("NyxarisOrbGuide");
        NyxarisOrbGuide guide = tempGO.AddComponent<NyxarisOrbGuide>();

        Sprite coreArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Nyxaris/Nyxaris_CoreArtwork.png");
        if (coreArt != null)
        {
            guide.customCoreArtworkSprite = coreArt;
        }

        guide.RebuildVisualsInEditMode();

        string prefabPath = "Assets/Prefabs/NyxarisOrbGuide.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
        Object.DestroyImmediate(tempGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#55FF88>[NyxarisSetup] Successfully generated physical Prefab with custom Core Artwork at '{prefabPath}'.</color>");
        EditorGUIUtility.PingObject(prefab);
    }

    [MenuItem("Tools/Setup Tutorial Fight Area Mobs")]
    public static void SetupFightAreaMobs()
    {
        GameObject fightGO = GameObject.Find("fight");
        if (fightGO == null) fightGO = GameObject.Find("Fight");

        if (fightGO == null)
        {
            Debug.LogWarning("[NyxarisSetup] Could not find 'fight' GameObject in scene hierarchy. Make sure it exists under 'Nyxaris Guide Locations'.");
            return;
        }

        TutorialCombatArea combatArea = fightGO.GetComponent<TutorialCombatArea>();
        if (combatArea == null)
        {
            combatArea = Undo.AddComponent<TutorialCombatArea>(fightGO);
            combatArea.mobCount = 2;
            combatArea.triggerRadius = 6.5f;
            Debug.Log("<color=#55FF88>[NyxarisSetup] Attached TutorialCombatArea to 'fight' GameObject.</color>");
        }

        NyxarisTutorialWaypoint wp = fightGO.GetComponent<NyxarisTutorialWaypoint>();
        if (wp == null)
        {
            wp = Undo.AddComponent<NyxarisTutorialWaypoint>(fightGO);
        }
        wp.dialogueText = "Corrupted shadows ahead! Draw your weapon and strike them down!";
        wp.promptBadgeText = "Press [J] to attack (Defeat Chaos Shades)";
        wp.requiredAction = NyxarisTutorialWaypoint.TutorialActionType.Attack;

        Selection.activeGameObject = fightGO;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("<color=#D47BFF>[NyxarisSetup] Configured 'fight' tutorial area with procedural Chaos Shade mobs.</color>");
    }

    [MenuItem("Tools/Generate Fox Nyxaris Prefab (Save to Assets)")]
    public static void GenerateFoxPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }

        GameObject tempFox = new GameObject("FoxNyxaris_Companion");
        SpriteRenderer sr = tempFox.AddComponent<SpriteRenderer>();
        Rigidbody2D rb = tempFox.AddComponent<Rigidbody2D>();
        CapsuleCollider2D col = tempFox.AddComponent<CapsuleCollider2D>();
        FoxNyxarisController fox = tempFox.AddComponent<FoxNyxarisController>();

        // Set default sprite
        Sprite defaultSprite = Resources.Load<Sprite>("Sprites/FoxNyxaris/Idle/Idle_000");
        if (defaultSprite != null) sr.sprite = defaultSprite;

        string p1 = "Assets/Prefabs/FoxNyxaris_Companion.prefab";
        string p2 = "Assets/Resources/Prefabs/FoxNyxaris_Companion.prefab";

        GameObject prefab1 = PrefabUtility.SaveAsPrefabAsset(tempFox, p1);
        GameObject prefab2 = PrefabUtility.SaveAsPrefabAsset(tempFox, p2);
        Object.DestroyImmediate(tempFox);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#55FF88>[NyxarisSetup] Generated editable Fox Nyxaris Prefabs at '{p1}' and '{p2}'.</color>");
        EditorGUIUtility.PingObject(prefab1);
    }


    [MenuItem("Tools/Setup Tutorial Seal & Finale Sequence")]
    public static void SetupTutorialSealSequence()
    {
        // 1. Search for seal or shrine cage
        GameObject sealGO = GameObject.Find("seal") ?? GameObject.Find("Seal") ?? 
                            GameObject.Find("NyxarisShrineCage") ?? GameObject.Find("ShrineCage") ??
                            GameObject.Find("shrine") ?? GameObject.Find("Shrine");

        if (sealGO == null)
        {
            // Find object near the webs
            var firstWeb = Object.FindFirstObjectByType<BreakableObject>();
            if (firstWeb != null)
            {
                sealGO = firstWeb.transform.parent != null ? firstWeb.transform.parent.gameObject : firstWeb.gameObject;
            }
        }

        if (sealGO == null)
        {
            sealGO = new GameObject("Nyxaris_TutorialSeal");
            sealGO.transform.position = new Vector3(85f, 2.5f, 0f);
        }

        NyxarisSealSequence seq = sealGO.GetComponent<NyxarisSealSequence>();
        if (seq == null) seq = Undo.AddComponent<NyxarisSealSequence>(sealGO);
        seq.intactSealGO = sealGO;

        // Auto-detect webs
        seq.coveringWebs.Clear();
        var allBreakables = Object.FindObjectsByType<BreakableObject>(FindObjectsSortMode.None);
        foreach (var b in allBreakables)
        {
            if (b != null) seq.coveringWebs.Add(b);
        }

        // Search for broken seal
        GameObject brokenSeal = GameObject.Find("broken seal") ?? GameObject.Find("Broken_Seal") ?? GameObject.Find("broken");
        if (brokenSeal != null)
        {
            seq.brokenSealGO = brokenSeal;
            brokenSeal.SetActive(false);
        }

        // Search for exit portal
        seq.exitPortalGO = GameObject.Find("Tutorial_ExitPortal") ?? GameObject.FindGameObjectWithTag("Finish");

        Selection.activeGameObject = sealGO;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"<color=#55FF88>[NyxarisSetup] Successfully attached and configured NyxarisSealSequence on '{sealGO.name}' with {seq.coveringWebs.Count} webs.</color>");
    }

}
