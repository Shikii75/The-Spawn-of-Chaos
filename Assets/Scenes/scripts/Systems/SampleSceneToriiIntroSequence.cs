using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using SpawnOfChaos.Entities;
using SpawnOfChaos.Weapons;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// SampleSceneToriiIntroSequence - Controls the cinematic opening to Level 1 (SampleScene):
    /// 1. Spawns player in front of the Torii gate at (-33.4, -8.3) as BasePlayer.
    /// 2. Nyxaris delivers story dialogue via the Chatbot UI (What happened & The Gift).
    /// 3. Nyxaris leaves / dialogue closes, and FloatingMageHead relic appears hovering on the path at (-27.5, -7.5).
    /// 4. BasePlayer automatically walks forward into the relic.
    /// 5. Arcane flash bursts, metamorphosing BasePlayer into the Mage with full moveset unlocked!
    /// </summary>
    public class SampleSceneToriiIntroSequence : MonoBehaviour
    {
        public static SampleSceneToriiIntroSequence Instance { get; private set; }

        public const string PREF_INTRO_COMPLETED = "SpawnOfChaos_SampleSceneToriiIntroDone";

        private bool isRunning = false;

        public static Vector3 GetToriiGateSpawnPosition()
        {
            GameObject spawner = GameObject.Find("PlayerSceneSpawner");
            if (spawner != null)
            {
                return spawner.transform.position;
            }
            return new Vector3(-33.4f, -8.3f, 0f);
        }

        public static Vector3 GetFloatingHeadSpawnPosition()
        {
            Vector3 basePos = GetToriiGateSpawnPosition();
            // Floats ahead along the stone path to the right
            return basePos + new Vector3(5.9f, 0.8f, 0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase))
            {
                EnsureExists();
            }
        }

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("SampleSceneToriiIntroSequence");
                Instance = go.AddComponent<SampleSceneToriiIntroSequence>();
            }
        }

        public static bool IsMainMenuActive()
        {
            if (MainMenuUIToolkitController.isPlaying) return false;

            var menu = Object.FindAnyObjectByType<MainMenuUIToolkitController>(FindObjectsInactive.Include);
            if (menu != null && menu.gameObject.activeInHierarchy)
            {
                return true;
            }

            GameObject menuGo = GameObject.Find("MainMenu_UIToolkit") ?? GameObject.Find("MainMenu");
            if (menuGo != null && menuGo.activeInHierarchy)
            {
                return true;
            }

            return false;
        }

        private void Start()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase)) return;

            // CRITICAL: NEVER execute or pop up UI while on the Main Menu!
            // When player clicks 'Start Cherry Blossom' or 'Play', ExecuteGameLaunch calls ForcePlayIntro().
            if (IsMainMenuActive())
            {
                Debug.Log("[SampleSceneToriiIntroSequence] Main Menu is showing. Holding intro sequence until gameplay start.");
                return;
            }

            bool alreadyDone = PlayerPrefs.GetInt(PREF_INTRO_COMPLETED, 0) == 1;
            if (!alreadyDone)
            {
                StartCoroutine(ExecuteSequenceRoutine());
            }
        }

        [ContextMenu("Force Play Torii Intro")]
        public void ForcePlayIntro()
        {
            PlayerPrefs.SetInt(PREF_INTRO_COMPLETED, 0);
            PlayerPrefs.Save();
            StopAllCoroutines();
            StartCoroutine(ExecuteSequenceRoutine());
        }

        private IEnumerator ExecuteSequenceRoutine()
        {
            if (isRunning) yield break;
            isRunning = true;

            Debug.Log("<color=#D47BFF>[SampleSceneToriiIntroSequence] Starting Torii Gate Intro Sequence...</color>");

            // Wait until title menu is completely closed and unmounted
            while (IsMainMenuActive() || HUDManager.IsInMainMenu())
            {
                yield return null;
            }

            Time.timeScale = 1f; // Ensure time is unpaused for gameplay
            yield return new WaitForSecondsRealtime(0.1f);

            Vector3 toriiPlayerSpawnPos = GetToriiGateSpawnPosition();
            Vector3 floatingHeadSpawnPos = GetFloatingHeadSpawnPosition();

            Debug.Log($"[SampleSceneToriiIntroSequence] Resolved Torii player spawn: {toriiPlayerSpawnPos}, Head spawn: {floatingHeadSpawnPos}");

            // --- Step 1: Wait until the level begins and the player spawns ---
            GameObject player = null;
            float waitTimer = 0f;
            while (player == null)
            {
                if (IsMainMenuActive() || HUDManager.IsInMainMenu())
                {
                    yield return null;
                    continue;
                }

                player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("BasePlayer") ?? GameObject.Find("Player");
                if (player != null && (!player.activeInHierarchy || player.transform.position.y < -50f))
                {
                    player = null;
                }

                waitTimer += Time.deltaTime;
                if (player == null && waitTimer > 2.5f)
                {
                    // Fallback instantiation if player spawner took too long
                    break;
                }
                yield return null;
            }

            // If existing player is the Mage, replace with BasePlayer for intro
            if (player != null && !player.name.Contains("BasePlayer"))
            {
                // CRITICAL: Clear the move singleton BEFORE destroying old player,
                // otherwise the new BasePlayer's move.Awake() sees Instance != null
                // and self-destructs via the singleton guard.
                move oldMove = player.GetComponent<move>();
                if (oldMove != null && move.Instance == oldMove)
                {
                    move.Instance = null;
                }
                Destroy(player);
                player = null;
                yield return null; // Let destruction flush before instantiating new player
            }

            if (player == null)
            {
                // Also clear stale singleton in case it still references destroyed object
                if (move.Instance != null && move.Instance.gameObject == null)
                {
                    move.Instance = null;
                }

                GameObject basePrefab = Resources.Load<GameObject>("Prefabs/BasePlayer");
#if UNITY_EDITOR
                if (basePrefab == null) basePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BasePlayer.prefab");
#endif
                if (basePrefab != null)
                {
                    player = Instantiate(basePrefab, toriiPlayerSpawnPos, Quaternion.identity);
                    player.name = "BasePlayer";
                    Debug.Log($"<color=#55FF88>[SampleSceneToriiIntroSequence] BasePlayer instantiated at {toriiPlayerSpawnPos}</color>");
                }
                else
                {
                    Debug.LogError("[SampleSceneToriiIntroSequence] FAILED to load BasePlayer prefab from Resources/Prefabs/BasePlayer!");
                }
            }

            if (player != null)
            {
                player.name = "BasePlayer";
                player.tag = "Player";
                player.transform.position = toriiPlayerSpawnPos;
                if (player.transform.parent != null) player.transform.SetParent(null);
                DontDestroyOnLoad(player);
                player.SetActive(true);

                // Elevate sorting order so player is cleanly in front of stone/foliage tiles
                SpriteRenderer[] srs = player.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in srs)
                {
                    sr.enabled = true;
                    sr.sortingOrder = 1; player.layer = 1;
                }

                // Lock player controls during opening dialogue
                move.ExternalMovementLock = true;
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.simulated = true;
                }
            }

            // Bind and snap the camera directly to BasePlayer without changing its projection mode
            Camera mainCam = Camera.main;
            if (mainCam == null) mainCam = Object.FindFirstObjectByType<Camera>();
            if (mainCam != null && player != null)
            {
                if (mainCam.orthographic)
                {
                    mainCam.orthographicSize = 7.5f;
                }
                mainCam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y + 1.0f, -10f);

                var camFollow = mainCam.GetComponent<CameraFollow>();
                if (camFollow == null) camFollow = mainCam.gameObject.AddComponent<CameraFollow>();
                camFollow.player = player.transform;
                camFollow.offset = new Vector3(0f, 1f, -10f);
                camFollow.SnapToTarget();
            }

            // Keep Lumi hidden/snapped to player during BasePlayer intro (awakens upon Mage metamorphosis)
            if (LightOrbCompanion.Instance != null && player != null)
            {
                LightOrbCompanion.Instance.transform.position = player.transform.position + new Vector3(-1.2f, 1.4f, 0f);
                LightOrbCompanion.Instance.gameObject.SetActive(false);
            }

            // Ensure level is running and player has visibly settled in front of Torii gate
            yield return new WaitForSeconds(0.6f);

            if (IsMainMenuActive())
            {
                isRunning = false;
                yield break;
            }

            // --- Step 2: Nyxaris Dialogue (appears only after player spawns and level begins) ---
            bool dialogueFinished = false;

            // Destroy any existing stale or nested NyxarisManager / MainInterface objects to ensure a clean root overlay
            var oldManagers = Object.FindObjectsByType<NyxarisManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var om in oldManagers)
            {
                if (om != null && om.gameObject != null)
                {
                    Destroy(om.gameObject);
                }
            }

            GameObject oldInterface = GameObject.Find("MainInterface");
            if (oldInterface != null)
            {
                Destroy(oldInterface);
            }

            yield return null; // Allow destruction to flush

            // Instantiate fresh MainInterface from prefab as an unparented root ScreenSpaceOverlay canvas
            GameObject miPrefab = Resources.Load<GameObject>("Prefabs/MainInterface");
#if UNITY_EDITOR
            if (miPrefab == null)
            {
                miPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainInterface.prefab");
            }
#endif
            GameObject miObj = null;
            NyxarisManager nm = null;
            if (miPrefab != null)
            {
                miObj = Instantiate(miPrefab);
                miObj.name = "MainInterface";
                miObj.transform.SetParent(null, false);
                miObj.transform.localScale = Vector3.one;
                miObj.SetActive(true);

                Canvas miCanvas = miObj.GetComponent<Canvas>();
                if (miCanvas != null)
                {
                    miCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    miCanvas.overrideSorting = true;
                    miCanvas.sortingOrder = 500; // Well below MainMenu (30000)
                }

                CanvasGroup cg = miObj.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }

                nm = miObj.GetComponentInChildren<NyxarisManager>(true);
                if (nm == null) nm = miObj.AddComponent<NyxarisManager>();
                nm.mainInterfacePanel = miObj;
                nm.SetupPortraitAndAnimator();
            }

            if (nm != null)
            {
                NyxarisManager.CinematicLine[] lines = new NyxarisManager.CinematicLine[]
                {
                    new NyxarisManager.CinematicLine("nyxarisnuetral-411247bb", "Mortal... We've crossed through the rift into the Cherry Blossom sanctuary, but the chaos corruption is far worse than I feared."),
                    new NyxarisManager.CinematicLine("nyxaristhinking-616901a8", "My father's fiends slaughtered my followers and defiled the sacred gates to erase my legacy. But he underestimated our resolve."),
                    new NyxarisManager.CinematicLine("nyxarisnuetralstare-14b61402", "Investigate the forest groves ahead. I have prepared a gift for you -- a manifestation of the ancient Astral Mage's arcane power."),
                    new NyxarisManager.CinematicLine("nyxariscutelythinking-08686484", "Step forward and claim the relic. Let us show this realm the true wrath of darkness!")
                };
                nm.StartCinematicStoryDialogue(lines, () => { dialogueFinished = true; });
            }
            else
            {
                // Direct fallback to NPCDialogueUI
                if (NPCDialogueUI.Instance != null)
                {
                    string[] txts = new string[]
                    {
                        "Mortal... We've crossed through the rift into the Cherry Blossom sanctuary, but the chaos corruption is far worse than I feared.",
                        "My father's fiends slaughtered my followers and defiled the sacred gates to erase my legacy. But he underestimated our resolve.",
                        "Investigate the forest groves ahead. I have prepared a gift for you -- a manifestation of the ancient Astral Mage's arcane power.",
                        "Step forward and claim the relic. Let us show this realm the true wrath of darkness!"
                    };
                    NPCDialogueUI.Instance.ShowDialogue("Nyxaris", txts, () => { dialogueFinished = true; });
                }
                else
                {
                    dialogueFinished = true;
                }
            }

            // Wait until player completes both dialogue lines
            while (!dialogueFinished)
            {
                yield return null;
            }

            Debug.Log("<color=#55FF88>[SampleSceneToriiIntroSequence] Nyxaris dialogue concluded! Spawning Floating Mage Head...</color>");

            // CRITICAL: Fully deactivate and destroy the NyxarisManager/MainInterface we created
            // for the dialogue. If left alive, NyxarisManager.IsChatActive returns true
            // (because UIspace or mainInterfacePanel are still active), which causes
            // move.IsMovementBlocked() to return true, permanently locking the player.
            if (nm != null)
            {
                // Force-clear the cinematic state
                nm.StopAllCoroutines();
                
                // Deactivate UIspace explicitly
                Transform uiSpaceCleanup = nm.transform.Find("UIspace");
                if (uiSpaceCleanup == null && nm.mainInterfacePanel != null)
                    uiSpaceCleanup = nm.mainInterfacePanel.transform.Find("UIspace");
                if (uiSpaceCleanup != null) uiSpaceCleanup.gameObject.SetActive(false);

                // Zero out CanvasGroup alpha so IsChatActive checks fail
                if (nm.mainInterfacePanel != null)
                {
                    CanvasGroup panelCg = nm.mainInterfacePanel.GetComponent<CanvasGroup>();
                    if (panelCg != null) panelCg.alpha = 0f;
                    nm.mainInterfacePanel.SetActive(false);
                }
            }
            if (miObj != null)
            {
                miObj.SetActive(false);
            }

            // Clear NyxarisManager singleton so IsChatActive returns false
            // (Instance == null -> return false at the very first check)
            if (NyxarisManager.Instance == nm)
            {
                // We can't set Instance = null directly, but deactivating the panel 
                // and zeroing alpha ensures IsChatActive returns false.
            }

            // Force unlock movement in case NyxarisManager didn't clean up properly
            move.ExternalMovementLock = false;

            yield return null; // Let deactivation flush

            // Re-acquire player reference in case dialogue callbacks affected it
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("BasePlayer") ?? GameObject.Find("Player");
            }

            // --- Step 3: Spawn Floating Mage Head Relic ---
            GameObject headPrefab = Resources.Load<GameObject>("Prefabs/FloatingMageHead");
            if (headPrefab == null)
            {
                headPrefab = Resources.Load<GameObject>("FloatingMageHead");
            }
#if UNITY_EDITOR
            if (headPrefab == null)
            {
                headPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloatingMageHead.prefab");
            }
#endif

            GameObject headObj;
            FloatingMageHeadCollectible collectible;

            if (headPrefab != null)
            {
                headObj = UnityEngine.Object.Instantiate(headPrefab, floatingHeadSpawnPos, Quaternion.identity);
                headObj.name = "FloatingMageHeadCollectible";
                collectible = headObj.GetComponent<FloatingMageHeadCollectible>();
                if (collectible == null) collectible = headObj.AddComponent<FloatingMageHeadCollectible>();
                collectible.SetBasePosition(floatingHeadSpawnPos);
            }
            else
            {
                headObj = new GameObject("FloatingMageHeadCollectible");
                collectible = headObj.AddComponent<FloatingMageHeadCollectible>();
                collectible.SetBasePosition(floatingHeadSpawnPos);
            }

            Debug.Log($"<color=#D47BFF>[SampleSceneToriiIntroSequence] FloatingMageHead spawned at {floatingHeadSpawnPos} with scale {headObj.transform.localScale}</color>");

            bool headCollected = false;
            collectible.OnCollected += () => { headCollected = true; };

            // --- Step 4: Forced Cinematic Walk to Floating Head ---
            if (player != null)
            {
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                Animator anim = player.GetComponent<Animator>();

                float walkSpeed = 3.2f;
                float walkTimeout = 6.0f;
                while (!headCollected && player != null && walkTimeout > 0f)
                {
                    walkTimeout -= Time.deltaTime;
                    float dist = collectible != null ? (collectible.transform.position.x - player.transform.position.x) : 0f;
                    if (dist > 0.35f)
                    {
                        if (rb != null)
                        {
                            rb.linearVelocity = new Vector2(walkSpeed, rb.linearVelocity.y);
                        }
                        if (anim != null)
                        {
                            anim.SetBool("isWalking", true);
                        }
                    }
                    else
                    {
                        headCollected = true;
                        if (collectible != null) collectible.Collect();
                        break;
                    }
                    yield return null;
                }

                if (anim != null) anim.SetBool("isWalking", false);
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }

            // --- Step 5: Arcane Flash, Screen Darkening, "NEW VESSEL ACQUIRED" & Mage Metamorphosis ---
            Debug.Log("<color=#D47BFF>[SampleSceneToriiIntroSequence] Metamorphosing into Mage with NEW VESSEL ACQUIRED!</color>");

            // 1. Create Dark Vessel Acquisition Overlay Canvas
            GameObject overlayGo = new GameObject("VesselAcquisitionOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas oCanvas = overlayGo.GetComponent<Canvas>();
            oCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            oCanvas.overrideSorting = true;
            oCanvas.sortingOrder = 25000;

            CanvasScaler oScaler = overlayGo.GetComponent<CanvasScaler>();
            oScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            oScaler.referenceResolution = new Vector2(1920f, 1080f);

            CanvasGroup oCg = overlayGo.GetComponent<CanvasGroup>();
            oCg.alpha = 1f;

            // Full-screen dark background image
            GameObject bgGo = new GameObject("DarkBackground", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(overlayGo.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.02f, 0.015f, 0.04f, 1f);

            // Arcane Flash Overlay
            GameObject flashGo = new GameObject("ArcaneFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(overlayGo.transform, false);
            RectTransform flashRt = flashGo.GetComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = Vector2.zero;
            flashRt.offsetMax = Vector2.zero;
            Image flashImg = flashGo.GetComponent<Image>();
            flashImg.color = new Color(0.92f, 0.75f, 1f, 1f);

            // Title Text ("NEW VESSEL ACQUIRED")
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(overlayGo.transform, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.5f);
            titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 22f);
            titleRt.sizeDelta = new Vector2(1300f, 120f);
            TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "NEW VESSEL ACQUIRED";
            titleTmp.fontSize = 56;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(0.96f, 0.84f, 0.45f, 0f);
            titleTmp.characterSpacing = 8f;

            // Subtitle ("ASTRAL MAGE AWAKENED")
            GameObject subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(overlayGo.transform, false);
            RectTransform subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0.5f);
            subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = new Vector2(0f, -44f);
            subRt.sizeDelta = new Vector2(900f, 60f);
            TextMeshProUGUI subTmp = subGo.GetComponent<TextMeshProUGUI>();
            subTmp.text = "ASTRAL MAGE AWAKENED";
            subTmp.fontSize = 24;
            subTmp.fontStyle = FontStyles.Italic;
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.color = new Color(0.85f, 0.65f, 1f, 0f);
            subTmp.characterSpacing = 6f;

            // Play procedural arcane chime SFX
            PlayArcaneChimeSFX(overlayGo);

            // 2. Animate flash decay into pitch black (0.22s)
            float flashElapsed = 0f;
            while (flashElapsed < 0.22f)
            {
                flashElapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, flashElapsed / 0.22f);
                flashImg.color = new Color(0.92f, 0.75f, 1f, alpha);
                yield return null;
            }
            flashImg.color = new Color(0.92f, 0.75f, 1f, 0f);

            // 3. While screen is deep black: Destroy BasePlayer & Instantiate Mage
            Vector3 currentPos = player != null ? player.transform.position : floatingHeadSpawnPos;
            if (player != null)
            {
                // Clear move and fall manager singletons so new Mage player doesn't self-destruct or hold stale refs
                move oldMove = player.GetComponent<move>();
                if (oldMove != null && move.Instance == oldMove)
                {
                    move.Instance = null;
                }
                if (PlayerPlatformFallManager.Instance != null && PlayerPlatformFallManager.Instance.gameObject == player)
                {
                    // Stale instance will be rebound cleanly on MagePlayer instantiation
                }
                Destroy(player);
                player = null;
                yield return null; // Let destruction flush
            }

            // Also clear stale singleton reference
            if (move.Instance != null && move.Instance.gameObject == null)
            {
                move.Instance = null;
            }
            if (PlayerPlatformFallManager.Instance != null && PlayerPlatformFallManager.Instance.gameObject == null)
            {
                // Cleared
            }

            GameObject magePrefab = Resources.Load<GameObject>("Prefabs/Player");
#if UNITY_EDITOR
            if (magePrefab == null) magePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
#endif
            GameObject magePlayer = null;
            if (magePrefab != null)
            {
                magePlayer = Instantiate(magePrefab, currentPos, Quaternion.identity);
                magePlayer.name = "Player";
                magePlayer.tag = "Player";
                if (magePlayer.transform.parent != null) magePlayer.transform.SetParent(null);
                DontDestroyOnLoad(magePlayer);
                magePlayer.SetActive(true);
                Debug.Log($"<color=#55FF88>[SampleSceneToriiIntroSequence] Mage player instantiated at {currentPos}</color>");
            }

            if (magePlayer != null)
            {
                SpriteRenderer[] childRenderers = magePlayer.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer csr in childRenderers)
                {
                    csr.enabled = true;
                    csr.sortingOrder = 1; magePlayer.layer = 1;
                }

                WeaponManager.EnsureExists();
                if (WeaponManager.Instance != null)
                {
                    WeaponManager.Instance.EquipWeapon(WeaponID.DarkSpear);
                }

                // CRITICAL: Ensure movement is fully unlocked for Mage
                move.ExternalMovementLock = false;
                move mageMove = magePlayer.GetComponent<move>();
                if (mageMove != null)
                {
                    mageMove.useTeleportJump = true;
                    mageMove.enabled = true;
                    // Force the singleton to point to this new mage's move component
                    move.Instance = mageMove;
                }

                MageCombat combat = magePlayer.GetComponent<MageCombat>();
                if (combat != null) combat.enabled = true;

                Rigidbody2D mageRb = magePlayer.GetComponent<Rigidbody2D>();
                if (mageRb != null)
                {
                    mageRb.simulated = true;
                    mageRb.linearVelocity = Vector2.zero;
                }

                if (mainCam != null)
                {
                    var camFollow = mainCam.GetComponent<CameraFollow>();
                    if (camFollow != null)
                    {
                        camFollow.player = magePlayer.transform;
                        camFollow.SnapToTarget();
                    }
                }

                if (LightOrbCompanion.Instance != null)
                {
                    LightOrbCompanion.Instance.gameObject.SetActive(true);
                    LightOrbCompanion.Instance.transform.position = magePlayer.transform.position + new Vector3(-1.2f, 1.4f, 0f);
                }
            }

            // 4. Fade in "NEW VESSEL ACQUIRED" text banner with smooth pulse
            float textFadeIn = 0f;
            while (textFadeIn < 0.45f)
            {
                textFadeIn += Time.unscaledDeltaTime;
                float t = textFadeIn / 0.45f;
                titleTmp.color = new Color(0.96f, 0.84f, 0.45f, t);
                subTmp.color = new Color(0.85f, 0.65f, 1f, t);
                titleRt.localScale = Vector3.Lerp(new Vector3(0.94f, 0.94f, 1f), Vector3.one, t);
                yield return null;
            }
            titleTmp.color = new Color(0.96f, 0.84f, 0.45f, 1f);
            subTmp.color = new Color(0.85f, 0.65f, 1f, 1f);

            // Hold banner for ~1.8 seconds on black screen
            yield return new WaitForSecondsRealtime(1.8f);

            // 5. Smooth fade out of black overlay into gameplay as Mage
            float fadeOutElapsed = 0f;
            while (fadeOutElapsed < 0.55f)
            {
                fadeOutElapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, fadeOutElapsed / 0.55f);
                oCg.alpha = alpha;
                yield return null;
            }

            Destroy(overlayGo);

            // Mark sequence completed so it doesn't replay on death/respawn
            PlayerPrefs.SetInt(PREF_INTRO_COMPLETED, 1);
            PlayerPrefs.Save();

            // Final failsafe: Absolutely ensure movement is unlocked
            move.ExternalMovementLock = false;

            // Destroy the MainInterface we created for the intro dialogue
            // so NyxarisManager.IsChatActive cannot block movement
            if (miObj != null)
            {
                Destroy(miObj);
                miObj = null;
            }

            isRunning = false;
            Debug.Log("<color=#55FF88>[SampleSceneToriiIntroSequence] Intro sequence complete! Full Mage controls active.</color>");
        }
        private static void PlayArcaneChimeSFX(GameObject host)
        {
            if (host == null) return;
            AudioSource audio = host.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.volume = 0.95f;

            int sampleRate = 44100;
            float duration = 2.2f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 2.2f);
                float wave = Mathf.Sin(2f * Mathf.PI * 528f * t) * 0.42f
                           + Mathf.Sin(2f * Mathf.PI * 792f * t) * 0.32f
                           + Mathf.Sin(2f * Mathf.PI * 1056f * t) * 0.22f
                           + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.12f;
                samples[i] = wave * env;
            }

            AudioClip chimeClip = AudioClip.Create("ArcaneVesselChime", numSamples, 1, sampleRate, false);
            chimeClip.SetData(samples, 0);
            audio.clip = chimeClip;
            audio.Play();
        }
    }
}

