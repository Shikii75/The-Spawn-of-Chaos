#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using SpawnOfChaos.Systems;

/// <summary>
/// AIAnimationStudio - Unity Editor Tool for designing, baking animations, and generating prefabs
/// for Cuttable Plants (Bamboo, Cherry Blossoms, Grass, Vines) and Hit-Reactive Objects (Crates, Pots, Dummies, Lanterns).
/// Provides 1-click procedural clip baking (.anim & .controller) and full scene test bench generation!
/// </summary>
public class AIAnimationStudio : EditorWindow
{
    private int selectedTab = 0;
    private readonly string[] tabTitles = new string[] { "🌿 Cuttable Plant Studio", "📦 Hit-Reactive Object Studio", "⚡ Scene Test Bench" };

    // Cuttable Plant State
    private CuttablePlant.PlantType selectedPlantType = CuttablePlant.PlantType.CherryBlossomBush;
    private int plantHealth = 1;
    private float cutHeightRatio = 0.5f;
    private bool enableSwayOnTouch = true;
    private bool autoRegrow = false;
    private float regrowDelay = 8f;
    private int plantLootCount = 2;
    private Color leafColor = new Color(1f, 0.55f, 0.75f, 1f);

    // Hit Reactive Object State
    private HitReactiveObject.ObjectType selectedObjectType = HitReactiveObject.ObjectType.BreakableCrate;
    private int objectHealth = 3;
    private int objectLootCount = 3;
    private float wobbleIntensity = 0.25f;
    private bool flashWhiteOnHit = true;
    private Color debrisColor = new Color(0.6f, 0.4f, 0.2f, 1f);

    private Vector2 scrollPosition;

    [MenuItem("Tools/AI Animation Studio")]
    [MenuItem("Tools/Dojo 2/AI Animation Studio")]
    public static void ShowWindow()
    {
        AIAnimationStudio window = GetWindow<AIAnimationStudio>("AI Animation Studio");
        window.minSize = new Vector2(520, 640);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        DrawHeaderBanner();

        EditorGUILayout.Space(10);
        selectedTab = GUILayout.Toolbar(selectedTab, tabTitles, GUILayout.Height(32));

        EditorGUILayout.Space(10);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        switch (selectedTab)
        {
            case 0:
                DrawCuttablePlantStudio();
                break;
            case 1:
                DrawHitReactiveObjectStudio();
                break;
            case 2:
                DrawSceneTestBench();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeaderBanner()
    {
        Rect bannerRect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(bannerRect, new Color(0.12f, 0.14f, 0.18f, 1f));

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.4f, 0.85f, 1f) }
        };

        GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.7f, 0.75f, 0.85f) }
        };

        GUI.Label(new Rect(bannerRect.x, bannerRect.y + 4, bannerRect.width, 24), "🎬 AI ANIMATION STUDIO", headerStyle);
        GUI.Label(new Rect(bannerRect.x, bannerRect.y + 26, bannerRect.width, 18), "Procedural Animations & Mechanics Generator for Plants & Breakables", subStyle);
    }

    // ── TAB 1: CUTTABLE PLANT STUDIO ───────────────────────────────────
    private void DrawCuttablePlantStudio()
    {
        EditorGUILayout.LabelField("🌿 Cuttable Plant Generator & Animation Studio", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Configure interactive foliage that cuts in half when slashed by attacks, emits leaf/petal bursts, sways on touch, and drops loot.", MessageType.Info);
        EditorGUILayout.Space(5);

        GameObject targetGo = Selection.activeGameObject;
        if (targetGo != null)
        {
            EditorGUILayout.LabelField($"Selected GameObject: {targetGo.name}", EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("Select a GameObject in Hierarchy to configure, or click 'Create New Cuttable Plant' below.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);
        selectedPlantType = (CuttablePlant.PlantType)EditorGUILayout.EnumPopup("Plant Preset:", selectedPlantType);
        plantHealth = EditorGUILayout.IntSlider("Plant Health (Hits):", plantHealth, 1, 10);
        cutHeightRatio = EditorGUILayout.Slider("Cut Height Ratio:", cutHeightRatio, 0.2f, 0.8f);
        enableSwayOnTouch = EditorGUILayout.Toggle("Enable Touch Sway:", enableSwayOnTouch);
        autoRegrow = EditorGUILayout.Toggle("Auto Regrow:", autoRegrow);
        if (autoRegrow)
        {
            regrowDelay = EditorGUILayout.FloatField("Regrow Delay (sec):", regrowDelay);
        }
        plantLootCount = EditorGUILayout.IntSlider("Loot Orbs Dropped:", plantLootCount, 0, 10);
        leafColor = EditorGUILayout.ColorField("Leaf / Petal Particle Color:", leafColor);

        EditorGUILayout.Space(15);
        if (GUILayout.Button("🌿 Attach & Configure CuttablePlant Component", GUILayout.Height(36)))
        {
            if (targetGo == null)
            {
                targetGo = CreateDummyPlantObject(selectedPlantType.ToString());
                Selection.activeGameObject = targetGo;
            }
            ConfigureCuttablePlant(targetGo);
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("🎬 Bake Plant Animation Clips & Controller (.anim & .controller)", GUILayout.Height(34)))
        {
            BakePlantAnimationAssets(selectedPlantType.ToString());
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("📦 Save Plant as Prefab (Assets/Resources/Prefabs/Props/)", GUILayout.Height(34)))
        {
            if (targetGo != null)
            {
                SaveObjectAsPrefab(targetGo, "CuttablePlants");
            }
            else
            {
                EditorUtility.DisplayDialog("Selection Required", "Please select or create a Cuttable Plant object first.", "OK");
            }
        }
    }

    // ── TAB 2: HIT-REACTIVE OBJECT STUDIO ─────────────────────────────
    private void DrawHitReactiveObjectStudio()
    {
        EditorGUILayout.LabelField("📦 Hit-Reactive Object & Breakables Studio", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Configure breakables (Crates, Pots, Barrels) and props (Training Dummies, Hanging Lanterns, Signs) with procedural squash-and-stretch wobble and debris shatters.", MessageType.Info);
        EditorGUILayout.Space(5);

        GameObject targetGo = Selection.activeGameObject;
        if (targetGo != null)
        {
            EditorGUILayout.LabelField($"Selected GameObject: {targetGo.name}", EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("Select a GameObject in Hierarchy to configure, or click 'Create New Hit-Reactive Object' below.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);
        selectedObjectType = (HitReactiveObject.ObjectType)EditorGUILayout.EnumPopup("Object Preset:", selectedObjectType);
        objectHealth = EditorGUILayout.IntSlider("Durability (HP):", objectHealth, 1, 20);
        objectLootCount = EditorGUILayout.IntSlider("Loot Orbs Dropped:", objectLootCount, 0, 15);
        wobbleIntensity = EditorGUILayout.Slider("Hit Wobble Intensity:", wobbleIntensity, 0.05f, 0.8f);
        flashWhiteOnHit = EditorGUILayout.Toggle("Flash White On Hit:", flashWhiteOnHit);
        debrisColor = EditorGUILayout.ColorField("Debris Particle Color:", debrisColor);

        EditorGUILayout.Space(15);
        if (GUILayout.Button("📦 Attach & Configure HitReactiveObject Component", GUILayout.Height(36)))
        {
            if (targetGo == null)
            {
                targetGo = CreateDummyHitObject(selectedObjectType.ToString());
                Selection.activeGameObject = targetGo;
            }
            ConfigureHitObject(targetGo);
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("🎬 Bake Object Animation Clips & Controller (.anim & .controller)", GUILayout.Height(34)))
        {
            BakeObjectAnimationAssets(selectedObjectType.ToString());
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("📦 Save Object as Prefab (Assets/Resources/Prefabs/Props/)", GUILayout.Height(34)))
        {
            if (targetGo != null)
            {
                SaveObjectAsPrefab(targetGo, "Breakables");
            }
            else
            {
                EditorUtility.DisplayDialog("Selection Required", "Please select or create a Hit-Reactive object first.", "OK");
            }
        }
    }

    // ── TAB 3: SCENE TEST BENCH ───────────────────────────────────────
    private void DrawSceneTestBench()
    {
        EditorGUILayout.LabelField("⚡ One-Click Scene Test Bench Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Instantly spawns a complete interactive cluster of cuttable plants (Cherry Blossom, Bamboo, Grass) and hit-reactive objects (Crate, Pot, Training Dummy, Hanging Lantern) right in front of the active scene player or camera!", MessageType.Info);
        EditorGUILayout.Space(15);

        if (GUILayout.Button("🚀 Spawn Interactive Animation Test Bench in Active Scene", GUILayout.Height(48)))
        {
            SpawnFullAnimationTestBench();
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("🧹 Clean Up Test Bench Objects", GUILayout.Height(32)))
        {
            CleanUpTestBenchObjects();
        }
    }

    // ── CONFIGURATION & CREATION HELPERS ──────────────────────────────
    private GameObject CreateDummyPlantObject(string name)
    {
        GameObject go = new GameObject($"Plant_{name}");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateProceduralBoxSprite(new Color(0.4f, 0.8f, 0.4f, 1f), 32, 64);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 2f);
        col.isTrigger = true;
        return go;
    }

    private GameObject CreateDummyHitObject(string name)
    {
        GameObject go = new GameObject($"Prop_{name}");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateProceduralBoxSprite(new Color(0.7f, 0.5f, 0.3f, 1f), 48, 48);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.5f, 1.5f);
        return go;
    }

    private void ConfigureCuttablePlant(GameObject go)
    {
        CuttablePlant plant = go.GetComponent<CuttablePlant>();
        if (plant == null) plant = go.AddComponent<CuttablePlant>();

        plant.plantType = selectedPlantType;
        plant.health = plantHealth;
        plant.cutHeightRatio = cutHeightRatio;
        plant.enableSwayOnTouch = enableSwayOnTouch;
        plant.autoRegrow = autoRegrow;
        plant.regrowDelay = regrowDelay;
        plant.lootOrbCount = plantLootCount;
        plant.leafParticleColor = leafColor;
        plant.ApplyPresetColors();

        EditorUtility.SetDirty(go);
        Debug.Log($"[AIAnimationStudio] CuttablePlant successfully configured on '{go.name}'!");
    }

    private void ConfigureHitObject(GameObject go)
    {
        HitReactiveObject hitObj = go.GetComponent<HitReactiveObject>();
        if (hitObj == null) hitObj = go.AddComponent<HitReactiveObject>();

        hitObj.objectType = selectedObjectType;
        hitObj.health = objectHealth;
        hitObj.lootOrbCount = objectLootCount;
        hitObj.wobbleIntensity = wobbleIntensity;
        hitObj.flashWhiteOnHit = flashWhiteOnHit;
        hitObj.debrisParticleColor = debrisColor;

        EditorUtility.SetDirty(go);
        Debug.Log($"[AIAnimationStudio] HitReactiveObject successfully configured on '{go.name}'!");
    }

    // ── ANIMATION CLIP & CONTROLLER BAKING ────────────────────────────
    public static void BakePlantAnimationAssets(string plantName)
    {
        string dir = "Assets/Scenes/animations/props";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // 1. Bake Plant_Sway.anim
        AnimationClip swayClip = new AnimationClip();
        swayClip.name = $"{plantName}_Sway";
        AnimationCurve rotZ = new AnimationCurve();
        rotZ.AddKey(0f, 0f);
        rotZ.AddKey(0.2f, 12f);
        rotZ.AddKey(0.5f, -8f);
        rotZ.AddKey(0.8f, 4f);
        rotZ.AddKey(1f, 0f);
        AnimationUtility.SetEditorCurve(swayClip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localRotation.z"), rotZ);

        string swayPath = $"{dir}/{plantName}_Sway.anim";
        AssetDatabase.CreateAsset(swayClip, swayPath);

        // 2. Bake Plant_Cut.anim
        AnimationClip cutClip = new AnimationClip();
        cutClip.name = $"{plantName}_Cut";
        AnimationCurve scaleY = new AnimationCurve();
        scaleY.AddKey(0f, 1f);
        scaleY.AddKey(0.3f, 0.4f);
        AnimationUtility.SetEditorCurve(cutClip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.y"), scaleY);

        string cutPath = $"{dir}/{plantName}_Cut.anim";
        AssetDatabase.CreateAsset(cutClip, cutPath);

        // 3. Create AnimatorController
        string controllerPath = $"{dir}/{plantName}_Controller.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        var rootState = controller.layers[0].stateMachine.AddState("Idle");
        var swayState = controller.layers[0].stateMachine.AddState("Sway");
        var cutState = controller.layers[0].stateMachine.AddState("Cut");

        swayState.motion = swayClip;
        cutState.motion = cutClip;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AIAnimationStudio] Baked Plant animation clips and controller to '{dir}'!");
    }

    public static void BakeObjectAnimationAssets(string objectName)
    {
        string dir = "Assets/Scenes/animations/props";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // 1. Bake Object_HitWobble.anim
        AnimationClip wobbleClip = new AnimationClip();
        wobbleClip.name = $"{objectName}_HitWobble";

        AnimationCurve scaleX = new AnimationCurve();
        scaleX.AddKey(0f, 1f);
        scaleX.AddKey(0.1f, 1.25f);
        scaleX.AddKey(0.25f, 0.85f);
        scaleX.AddKey(0.35f, 1.05f);
        scaleX.AddKey(0.45f, 1f);

        AnimationCurve scaleY = new AnimationCurve();
        scaleY.AddKey(0f, 1f);
        scaleY.AddKey(0.1f, 0.8f);
        scaleY.AddKey(0.25f, 1.15f);
        scaleY.AddKey(0.35f, 0.95f);
        scaleY.AddKey(0.45f, 1f);

        AnimationUtility.SetEditorCurve(wobbleClip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.x"), scaleX);
        AnimationUtility.SetEditorCurve(wobbleClip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.y"), scaleY);

        string wobblePath = $"{dir}/{objectName}_HitWobble.anim";
        AssetDatabase.CreateAsset(wobbleClip, wobblePath);

        // 2. Create AnimatorController
        string controllerPath = $"{dir}/{objectName}_Controller.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        var rootState = controller.layers[0].stateMachine.AddState("Idle");
        var wobbleState = controller.layers[0].stateMachine.AddState("HitWobble");
        wobbleState.motion = wobbleClip;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AIAnimationStudio] Baked Object hit reaction clips and controller to '{dir}'!");
    }

    // ── SCENE TEST BENCH SPAWNER ─────────────────────────────────────
    public static void SpawnFullAnimationTestBench()
    {
        Transform spawnOrigin = null;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            spawnOrigin = player.transform;
        }
        else if (Camera.main != null)
        {
            spawnOrigin = Camera.main.transform;
        }

        Vector3 basePos = spawnOrigin != null ? spawnOrigin.position + new Vector3(3f, -1f, 0f) : Vector3.zero;

        GameObject container = new GameObject("--- AI_ANIMATION_TEST_BENCH ---");
        container.transform.position = basePos;

        // 1. Cuttable Cherry Blossom Bush
        GameObject cherry = new GameObject("Cuttable_CherryBlossom");
        cherry.transform.SetParent(container.transform);
        cherry.transform.position = basePos + new Vector3(0f, 0f, 0f);
        SpriteRenderer srCherry = cherry.AddComponent<SpriteRenderer>();
        srCherry.sprite = CreateProceduralBoxSprite(new Color(1f, 0.6f, 0.8f, 1f), 48, 64);
        BoxCollider2D colCherry = cherry.AddComponent<BoxCollider2D>();
        colCherry.size = new Vector2(1.2f, 1.8f);
        colCherry.isTrigger = true;
        CuttablePlant plantCherry = cherry.AddComponent<CuttablePlant>();
        plantCherry.plantType = CuttablePlant.PlantType.CherryBlossomBush;
        plantCherry.leafParticleColor = new Color(1f, 0.55f, 0.75f, 1f);
        plantCherry.lootOrbCount = 3;

        // 2. Cuttable Bamboo Stalk
        GameObject bamboo = new GameObject("Cuttable_BambooStalk");
        bamboo.transform.SetParent(container.transform);
        bamboo.transform.position = basePos + new Vector3(2.5f, 0.3f, 0f);
        SpriteRenderer srBamboo = bamboo.AddComponent<SpriteRenderer>();
        srBamboo.sprite = CreateProceduralBoxSprite(new Color(0.4f, 0.75f, 0.35f, 1f), 24, 96);
        BoxCollider2D colBamboo = bamboo.AddComponent<BoxCollider2D>();
        colBamboo.size = new Vector2(0.8f, 2.4f);
        colBamboo.isTrigger = true;
        CuttablePlant plantBamboo = bamboo.AddComponent<CuttablePlant>();
        plantBamboo.plantType = CuttablePlant.PlantType.Bamboo;
        plantBamboo.leafParticleColor = new Color(0.4f, 0.75f, 0.35f, 1f);
        plantBamboo.lootOrbCount = 2;

        // 3. Breakable Crate
        GameObject crate = new GameObject("HitReactive_BreakableCrate");
        crate.transform.SetParent(container.transform);
        crate.transform.position = basePos + new Vector3(5f, -0.2f, 0f);
        SpriteRenderer srCrate = crate.AddComponent<SpriteRenderer>();
        srCrate.sprite = CreateProceduralBoxSprite(new Color(0.65f, 0.45f, 0.25f, 1f), 48, 48);
        BoxCollider2D colCrate = crate.AddComponent<BoxCollider2D>();
        colCrate.size = new Vector2(1.4f, 1.4f);
        HitReactiveObject hitCrate = crate.AddComponent<HitReactiveObject>();
        hitCrate.objectType = HitReactiveObject.ObjectType.BreakableCrate;
        hitCrate.health = 3;
        hitCrate.lootOrbCount = 4;

        // 4. Clay Pot
        GameObject pot = new GameObject("HitReactive_ClayPot");
        pot.transform.SetParent(container.transform);
        pot.transform.position = basePos + new Vector3(7.2f, -0.3f, 0f);
        SpriteRenderer srPot = pot.AddComponent<SpriteRenderer>();
        srPot.sprite = CreateProceduralBoxSprite(new Color(0.8f, 0.45f, 0.3f, 1f), 36, 42);
        BoxCollider2D colPot = pot.AddComponent<BoxCollider2D>();
        colPot.size = new Vector2(1.1f, 1.3f);
        HitReactiveObject hitPot = pot.AddComponent<HitReactiveObject>();
        hitPot.objectType = HitReactiveObject.ObjectType.ClayPot;
        hitPot.health = 1;
        hitPot.lootOrbCount = 3;

        // 5. Training Dummy
        GameObject dummy = new GameObject("HitReactive_TrainingDummy");
        dummy.transform.SetParent(container.transform);
        dummy.transform.position = basePos + new Vector3(9.5f, 0.2f, 0f);
        SpriteRenderer srDummy = dummy.AddComponent<SpriteRenderer>();
        srDummy.sprite = CreateProceduralBoxSprite(new Color(0.9f, 0.8f, 0.5f, 1f), 40, 72);
        BoxCollider2D colDummy = dummy.AddComponent<BoxCollider2D>();
        colDummy.size = new Vector2(1.2f, 2.2f);
        HitReactiveObject hitDummy = dummy.AddComponent<HitReactiveObject>();
        hitDummy.objectType = HitReactiveObject.ObjectType.TrainingDummy;

        // 6. Hanging Lantern
        GameObject lantern = new GameObject("HitReactive_HangingLantern");
        lantern.transform.SetParent(container.transform);
        lantern.transform.position = basePos + new Vector3(11.8f, 1.8f, 0f);
        SpriteRenderer srLantern = lantern.AddComponent<SpriteRenderer>();
        srLantern.sprite = CreateProceduralBoxSprite(new Color(1f, 0.8f, 0.2f, 1f), 32, 40);
        CircleCollider2D colLantern = lantern.AddComponent<CircleCollider2D>();
        colLantern.radius = 0.7f;
        HitReactiveObject hitLantern = lantern.AddComponent<HitReactiveObject>();
        hitLantern.objectType = HitReactiveObject.ObjectType.HangingLantern;

        Selection.activeGameObject = container;
        Debug.Log("[AIAnimationStudio] Spawned complete Animation Test Bench in scene!");
    }

    public static void CleanUpTestBenchObjects()
    {
        GameObject container = GameObject.Find("--- AI_ANIMATION_TEST_BENCH ---");
        if (container != null)
        {
            DestroyImmediate(container);
            Debug.Log("[AIAnimationStudio] Cleaned up Test Bench container.");
        }
    }

    private static Sprite CreateProceduralBoxSprite(Color color, int width, int height)
    {
        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = (x == 0 || x == width - 1 || y == 0 || y == height - 1);
                pixels[y * width + x] = isBorder ? color * 0.7f : color;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
    }

    private void SaveObjectAsPrefab(GameObject go, string subFolder)
    {
        string baseDir = $"Assets/Resources/Prefabs/Props/{subFolder}";
        if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

        string prefabPath = $"{baseDir}/{go.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AIAnimationStudio] Saved prefab successfully at '{prefabPath}'!");
        EditorUtility.DisplayDialog("Prefab Saved", $"Saved prefab to:\n{prefabPath}", "OK");
    }
}
#endif
