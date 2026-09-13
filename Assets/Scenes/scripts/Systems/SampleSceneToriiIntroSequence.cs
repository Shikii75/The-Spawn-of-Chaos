using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpawnOfChaos.Entities;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// SampleSceneToriiIntroSequence - Controls the cinematic opening to Level 1 (SampleScene):
    /// 1. Spawns player near the Torii gate as BasePlayer.
    /// 2. Nyxaris delivers story dialogue via the Chatbot UI (What happened & The Gift).
    /// 3. Nyxaris leaves / dialogue closes, and FloatingMageHead appears hovering on the path.
    /// 4. BasePlayer automatically walks forward into the relic.
    /// 5. Arcane flash bursts, metamorphosing BasePlayer into the Mage with full moveset unlocked!
    /// </summary>
    public class SampleSceneToriiIntroSequence : MonoBehaviour
    {
        public static SampleSceneToriiIntroSequence Instance { get; private set; }

        public const string PREF_INTRO_COMPLETED = "SpawnOfChaos_SampleSceneToriiIntroDone";

        [Header("Positions")]
        // Torii gate coordinates in SampleScene: x: 1073.5, y: 167.75
        public Vector3 toriiPlayerSpawnPos = new Vector3(1070f, 167.5f, 0f);
        public Vector3 floatingHeadSpawnPos = new Vector3(1076.5f, 168.2f, 0f);

        private bool isRunning = false;

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

        private void Start()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase)) return;

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

            // --- Step 1: Locate or Spawn BasePlayer at the Torii Gate ---
            GameObject player = null;
            float timeout = 2.0f;
            while (player == null && timeout > 0f)
            {
                player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("BasePlayer") ?? GameObject.Find("Player");
                timeout -= Time.deltaTime;
                yield return null;
            }

            // If existing player is the Mage, replace with BasePlayer for intro
            if (player != null && !player.name.Contains("BasePlayer"))
            {
                Vector3 pPos = player.transform.position;
                Destroy(player);
                player = null;

                GameObject basePrefab = Resources.Load<GameObject>("Prefabs/BasePlayer");
#if UNITY_EDITOR
                if (basePrefab == null) basePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BasePlayer.prefab");
#endif
                if (basePrefab != null)
                {
                    player = Instantiate(basePrefab, toriiPlayerSpawnPos, Quaternion.identity);
                    player.name = "BasePlayer";
                }
            }

            if (player == null)
            {
                GameObject basePrefab = Resources.Load<GameObject>("Prefabs/BasePlayer");
#if UNITY_EDITOR
                if (basePrefab == null) basePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BasePlayer.prefab");
#endif
                if (basePrefab != null)
                {
                    player = Instantiate(basePrefab, toriiPlayerSpawnPos, Quaternion.identity);
                    player.name = "BasePlayer";
                }
            }

            if (player != null)
            {
                player.transform.position = toriiPlayerSpawnPos;
            }

            // Lock player controls during opening dialogue
            move.ExternalMovementLock = true;
            if (player != null)
            {
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }

            yield return new WaitForSeconds(0.4f);

            // --- Step 2: Nyxaris Chatbot Dialogue ---
            bool dialogueFinished = false;

            NyxarisManager nm = NyxarisManager.EnsureInstanceInScene();
            if (nm == null)
            {
                nm = FindFirstObjectByType<NyxarisManager>(FindObjectsInactive.Include);
            }

            string line1 = "Mortal... The chaos corruption has fractured this sanctuary. Our temple was overwhelmed, and dark fiends now roam the blossom groves.";
            string line2 = "You must venture forward and investigate the depths of the forest. Take this gift—a manifestation of the ancient Astral Mage's arcane power. Claim it, and awaken your true combat essence.";

            if (nm != null)
            {
                NyxarisManager.CinematicLine[] lines = new NyxarisManager.CinematicLine[]
                {
                    new NyxarisManager.CinematicLine("explaining", line1),
                    new NyxarisManager.CinematicLine("happy", line2)
                };
                nm.StartCinematicStoryDialogue(lines, () => { dialogueFinished = true; });
            }
            else
            {
                // Fallback: if NyxarisManager UI not active in scene, show via NPCDialogueUI
                if (NPCDialogueUI.Instance != null)
                {
                    string[] txts = new string[] { line1, line2 };
                    NPCDialogueUI.Instance.ShowDialogue("Nyxaris", txts, () => { dialogueFinished = true; });
                }
                else
                {
                    dialogueFinished = true;
                }
            }

            while (!dialogueFinished)
            {
                yield return null;
            }

            Debug.Log("<color=#55FF88>[SampleSceneToriiIntroSequence] Nyxaris dialogue concluded! Spawning Floating Mage Head...</color>");

            // --- Step 3: Spawn Floating Mage Head Relic ---
            GameObject headObj = new GameObject("FloatingMageHeadCollectible");
            FloatingMageHeadCollectible collectible = headObj.AddComponent<FloatingMageHeadCollectible>();
            collectible.SetBasePosition(floatingHeadSpawnPos);

            bool headCollected = false;
            collectible.OnCollected += () => { headCollected = true; };

            // --- Step 4: Forced Cinematic Walk to Floating Head ---
            if (player != null)
            {
                move playerMove = player.GetComponent<move>();
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                Animator anim = player.GetComponent<Animator>();

                float walkSpeed = 3.5f;
                while (!headCollected && player != null)
                {
                    float dist = collectible != null ? (collectible.transform.position.x - player.transform.position.x) : 0f;
                    if (dist > 0.2f)
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

            // --- Step 5: Arcane Flash & Mage Metamorphosis ---
            Debug.Log("<color=#D47BFF>[SampleSceneToriiIntroSequence] Metamorphosing into Mage!</color>");

            // Flash screen purple/white
            if (DrifterFlashOverlay.Instance != null)
            {
                DrifterFlashOverlay.Instance.PlayRespawnAwakenSequence(null, null);
            }

            Vector3 currentPos = player != null ? player.transform.position : floatingHeadSpawnPos;

            // Destroy BasePlayer
            if (player != null)
            {
                Destroy(player);
                player = null;
            }

            // Instantiate full Mage Player prefab
            GameObject magePrefab = Resources.Load<GameObject>("Prefabs/Player");
#if UNITY_EDITOR
            if (magePrefab == null) magePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
#endif
            GameObject magePlayer = null;
            if (magePrefab != null)
            {
                magePlayer = Instantiate(magePrefab, currentPos, Quaternion.identity);
                magePlayer.name = "Player";
            }

            if (magePlayer != null)
            {
                // Ensure active weapon is equipped (Base spear: DarkSpear)
                if (SpawnOfChaos.Weapons.WeaponManager.Instance != null)
                {
                    SpawnOfChaos.Weapons.WeaponManager.Instance.EquipWeapon(SpawnOfChaos.Weapons.WeaponID.DarkSpear);
                }

                // Unlock player controls
                move.ExternalMovementLock = false;
                move mageMove = magePlayer.GetComponent<move>();
                if (mageMove != null)
                {
                    mageMove.enabled = true;
                }

                // Bind camera
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    var camFollow = mainCam.GetComponent<CameraFollowPlayer>();
                    if (camFollow != null)
                    {
                        camFollow.target = magePlayer.transform;
                    }
                }
            }

            // Mark sequence completed so it doesn't replay on death/respawn
            PlayerPrefs.SetInt(PREF_INTRO_COMPLETED, 1);
            PlayerPrefs.Save();

            isRunning = false;
            Debug.Log("<color=#55FF88>[SampleSceneToriiIntroSequence] Intro sequence complete! Full Mage controls active.</color>");
        }
    }
}
