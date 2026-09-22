using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using SpawnOfChaos.Systems;

namespace SpawnOfChaos.EditorTools
{
    /// <summary>
    /// Editor utility for constructing and saving the complete Telepathy Orb Prefab.
    /// Accessible from the Unity Editor menu: 'Tools / Spawn of Chaos / Generate Telepathy Orb Prefab'
    /// </summary>
    public static class TelepathyOrbSetupTool
    {
        private const string PREFAB_PATH = "Assets/Prefabs/TelepathyOrb.prefab";
        private const string RESOURCE_PREFAB_PATH = "Assets/Resources/Prefabs/TelepathyOrb.prefab";
        private const string SPRITE_PATH = "Assets/Sprites/Orbs/ExpCosmicPurpleOrb.png";

        [MenuItem("Tools/Spawn of Chaos/Generate Telepathy Orb Prefab")]
        public static void GenerateTelepathyOrbPrefab()
        {
            // Ensure directories exist
            EnsureFolderExists("Assets/Prefabs");
            EnsureFolderExists("Assets/Resources/Prefabs");

            // Build hierarchy in memory
            GameObject rootGO = BuildTelepathyOrbHierarchy();

            // Save to Assets/Prefabs/
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(rootGO, PREFAB_PATH);
            Debug.Log($"<color=#D47BFF>[TelepathyOrbSetupTool] ✦ Saved prefab to {PREFAB_PATH}</color>");

            // Also save to Assets/Resources/Prefabs/ for runtime dynamic loading if needed
            PrefabUtility.SaveAsPrefabAsset(rootGO, RESOURCE_PREFAB_PATH);
            Debug.Log($"<color=#D47BFF>[TelepathyOrbSetupTool] ✦ Saved copy to {RESOURCE_PREFAB_PATH}</color>");

            // Clean up scene object
            Object.DestroyImmediate(rootGO);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefabAsset != null)
            {
                Selection.activeObject = prefabAsset;
                EditorGUIUtility.PingObject(prefabAsset);
            }
        }

        [MenuItem("Tools/Spawn of Chaos/Spawn Telepathy Orb In Current Scene")]
        public static void SpawnInCurrentScene()
        {
            GameObject orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            GameObject instance;

            if (orbPrefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(orbPrefab);
            }
            else
            {
                instance = BuildTelepathyOrbHierarchy();
            }

            // Center near scene camera or origin
            if (SceneView.lastActiveSceneView != null)
            {
                Vector3 camPos = SceneView.lastActiveSceneView.camera.transform.position;
                instance.transform.position = new Vector3(camPos.x, camPos.y, 0f);
            }
            else
            {
                instance.transform.position = new Vector3(121f, 11f, 0f);
            }

            Undo.RegisterCreatedObjectUndo(instance, "Spawn Telepathy Orb");
            Selection.activeGameObject = instance;
            Debug.Log("<color=#D47BFF>[TelepathyOrbSetupTool] ✦ Spawned Telepathy Orb into active scene.</color>");
        }

        public static GameObject BuildTelepathyOrbHierarchy()
        {
            // 1. Root GameObject
            GameObject root = new GameObject("TelepathyOrb");
            root.transform.position = Vector3.zero;

            // Load cosmic orb sprite
            Sprite orbSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PATH);

            // 2. AudioSource
            AudioSource audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;

            // 3. CircleCollider2D (Proximity Trigger)
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 2.0f;

            // 4. TelepathyOrbItem logic component
            TelepathyOrbItem orbItem = root.AddComponent<TelepathyOrbItem>();
            orbItem.requireKeyPress = true;
            orbItem.interactKey = KeyCode.E;
            orbItem.interactionRadius = 2.0f;
            orbItem.promptActionText = "Absorb Telepathy Orb";
            orbItem.hoverAmplitude = 0.25f;
            orbItem.hoverFrequency = 2.4f;
            orbItem.innerRotationSpeed = 45f;
            orbItem.outerRotationSpeed = 30f;
            orbItem.pulseAmplitude = 0.08f;
            orbItem.pulseFrequency = 3.0f;
            orbItem.soundVolume = 0.9f;

            // 5. VisualCore (Core Sprite)
            GameObject visualCoreGO = new GameObject("VisualCore");
            visualCoreGO.transform.SetParent(root.transform, false);
            visualCoreGO.transform.localScale = Vector3.one;

            SpriteRenderer coreSR = visualCoreGO.AddComponent<SpriteRenderer>();
            coreSR.sprite = orbSprite;
            coreSR.color = new Color(1f, 0.94f, 1f, 1f);
            coreSR.sortingOrder = 22;

            // 6. InnerGlow (Counter-rotating luminous ring)
            GameObject innerGlowGO = new GameObject("InnerGlow");
            innerGlowGO.transform.SetParent(root.transform, false);
            innerGlowGO.transform.localScale = new Vector3(1.38f, 1.38f, 1f);

            SpriteRenderer innerSR = innerGlowGO.AddComponent<SpriteRenderer>();
            innerSR.sprite = orbSprite;
            innerSR.color = new Color(0.78f, 0.25f, 1.0f, 0.58f);
            innerSR.sortingOrder = 21;

            // 7. OuterHalo (Counter-rotating soft radiant halo)
            GameObject outerHaloGO = new GameObject("OuterHalo");
            outerHaloGO.transform.SetParent(root.transform, false);
            outerHaloGO.transform.localScale = new Vector3(1.85f, 1.85f, 1f);

            SpriteRenderer outerSR = outerHaloGO.AddComponent<SpriteRenderer>();
            outerSR.sprite = orbSprite;
            outerSR.color = new Color(0.50f, 0.10f, 0.95f, 0.30f);
            outerSR.sortingOrder = 20;

            // 8. StardustParticles (Ambient rising violet motes)
            GameObject stardustGO = new GameObject("StardustParticles");
            stardustGO.transform.SetParent(root.transform, false);

            ParticleSystem stardustPS = stardustGO.AddComponent<ParticleSystem>();
            ConfigureStardustParticles(stardustPS);

            // 9. ShockwaveBurst (Radial burst upon collection)
            GameObject shockwaveGO = new GameObject("ShockwaveBurst");
            shockwaveGO.transform.SetParent(root.transform, false);

            ParticleSystem shockwavePS = shockwaveGO.AddComponent<ParticleSystem>();
            ConfigureShockwaveParticles(shockwavePS);

            // 10. PromptCanvas (World-space floating UI)
            GameObject canvasGO = new GameObject("PromptCanvas");
            canvasGO.transform.SetParent(root.transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 1.85f, 0f);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            CanvasGroup canvasGroup = canvasGO.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(460, 75);
            canvasRT.localScale = new Vector3(0.018f, 0.018f, 0.018f);

            // Panel Background
            GameObject panelGO = new GameObject("PanelBackground");
            panelGO.transform.SetParent(canvasGO.transform, false);
            RectTransform panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;

            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.03f, 0.14f, 0.88f);

            Outline outline = panelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.80f, 0.45f, 1.0f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, 2.5f);

            // Prompt Text
            GameObject textGO = new GameObject("PromptText");
            textGO.transform.SetParent(panelGO.transform, false);
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();
            tmpText.text = "✦ [E] Absorb Telepathy Orb ✦";
            tmpText.fontSize = 32f;
            tmpText.fontStyle = FontStyles.Bold;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = new Color(0.96f, 0.88f, 1f, 1f);

            // 11. Wire references to TelepathyOrbItem
            orbItem.visualCore = visualCoreGO.transform;
            orbItem.innerHalo = innerGlowGO.transform;
            orbItem.outerHalo = outerHaloGO.transform;
            orbItem.promptCanvasGroup = canvasGroup;
            orbItem.promptText = tmpText;
            orbItem.ambientStardustParticles = stardustPS;
            orbItem.shockwaveBurstParticles = shockwavePS;

            return root;
        }

        private static void ConfigureStardustParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.45f, 1f, 0.85f),
                new Color(0.55f, 0.20f, 0.95f, 0.40f)
            );
            main.loop = true;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 12f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;

            main.gravityModifier = -0.04f;
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = false;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.2f);
            curve.AddKey(0.5f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void ConfigureShockwaveParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 1f, 1f),
                new Color(0.70f, 0.20f, 1f, 0.85f)
            );
            main.loop = false;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
                string folderName = Path.GetFileName(folderPath);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolderExists(parent);
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
