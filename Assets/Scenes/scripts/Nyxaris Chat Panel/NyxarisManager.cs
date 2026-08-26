using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NyxarisManager : MonoBehaviour
{
    public static NyxarisManager Instance { get; private set; }

    [Header("UI Structure")]
    public GameObject mainInterfacePanel; 

    [Header("UI Elements")]
    public TMP_Text dialogueText;
    public TMP_InputField messageInput;
    public Image portrait;
    public NyxarisFrameAnimator frameAnimator;

    [Header("Direct Expression Sprites (Game Dev OS Fallback)")]
    public Sprite neutralSprite;
    public Sprite explainingSprite;
    public Sprite cuteSprite;

    [Header("Sprite Lists")]
    public Sprite[] neutralSprites;
    public Sprite[] explainingSprites;
    public Sprite[] thinkingSprites;
    public Sprite[] angrySprites;
    public Sprite[] cuteSprites;
    public Sprite[] motherlySprites;
    public Sprite[] kindSprites;

    [Header("Current Game State Connection")]
    [Tooltip("Options: idle, combat, story")]
    public string currentMode = "idle";
    [Range(0f, 1f)]
    public float currentTrust = 0.5f;

    [Header("API Server Config")]
    public string apiHost = "http://127.0.0.1:5000/nyxaris";
    public string apiHostFallback = "http://127.0.0.1:5001/nyxaris";

    [System.Serializable]
    public class NyxarisRequest 
    { 
        public string message; 
        public string mode; 
        public float trust; 
        public string level;
        public float player_hp;
        public float player_max_hp;
        public float player_mana;
        public float player_max_mana;
    }

    [System.Serializable]
    public class NyxarisResponse 
    { 
        public string response; 
        public string emotion; 
        public string animation; 
        public string sprite_key; 
        public string suggested_minigame; 
        public float new_trust; 
    }

    public static bool IsChatActive => Instance != null && Instance.mainInterfacePanel != null && Instance.mainInterfacePanel.activeSelf;

    public static bool IsTyping
    {
        get
        {
            if (IsChatActive) return true;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            {
                var go = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                if (go.GetComponent<TMPro.TMP_InputField>() != null || go.GetComponent<UnityEngine.UI.InputField>() != null)
                    return true;
            }
            return false;
        }
    }

    private Coroutine currentTypewriterCoroutine;

    void Awake()
    {
        Instance = this;
        AutoLoadExpressionSprites();
        EnsureCanvasScaling();

        if (mainInterfacePanel != null)
        {
            mainInterfacePanel.SetActive(false);
        }
    }

    void Start()
    {
        AutoLoadExpressionSprites();
        EnsureCanvasScaling();
        SetupPortraitAndAnimator();

        NyxarisUIStyler styler = GetStyler();
        if (styler != null)
        {
            styler.ApplyStyling();
        }

        if (messageInput != null)
        {
            messageInput.onSubmit.AddListener((text) => SendInputMessage());
        }

        HideInterface();
    }

    private void EnsureCanvasScaling()
    {
        Canvas rootCanvas = null;
        if (mainInterfacePanel != null)
        {
            Canvas c = mainInterfacePanel.GetComponentInParent<Canvas>();
            if (c != null) rootCanvas = c.rootCanvas;
        }
        if (rootCanvas == null)
        {
            Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (all != null && all.Length > 0) rootCanvas = all[0].rootCanvas;
        }

        if (rootCanvas != null)
        {
            CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1.0f;
        }
    }

    private void SetupPortraitAndAnimator()
    {
        // Cleanup any stray untextured overlay
        GameObject stray = GameObject.Find("NyxarisPortraitOverlay");
        if (stray != null)
        {
            Destroy(stray);
        }

        NyxarisUIStyler styler = GetStyler();
        if (styler != null && styler.portraitImage != null)
        {
            portrait = styler.portraitImage;
        }
        else if (portrait == null && mainInterfacePanel != null)
        {
            Transform p = mainInterfacePanel.transform.Find("UIspace/Portrait") ?? 
                          mainInterfacePanel.transform.Find("Portrait");
            if (p != null) portrait = p.GetComponent<Image>();
        }

        if (portrait != null)
        {
            portrait.color = Color.white;
            portrait.preserveAspect = true;

            frameAnimator = portrait.GetComponent<NyxarisFrameAnimator>();
            if (frameAnimator == null)
            {
                frameAnimator = portrait.gameObject.AddComponent<NyxarisFrameAnimator>();
            }
            frameAnimator.targetImage = portrait;
            frameAnimator.PlayAnimation("nuetral", 12f);
        }
    }

    public void EnsureDefaultPortrait()
    {
        if (portrait == null) SetupPortraitAndAnimator();
        if (portrait == null) return;

        portrait.enabled = true;
        portrait.gameObject.SetActive(true);
        portrait.color = Color.white;
        portrait.preserveAspect = true;

        if (frameAnimator != null)
        {
            frameAnimator.targetImage = portrait;
            if (!frameAnimator.IsPlaying && !frameAnimator.IsFrozenOnLastFrame)
            {
                frameAnimator.PlayAnimation("nuetral", 12f);
            }
        }
        else if (portrait.sprite == null)
        {
            AutoLoadExpressionSprites();
            Sprite def = neutralSprite ??
                        (neutralSprites != null && neutralSprites.Length > 0 ? neutralSprites[0] : null) ??
                        explainingSprite ?? cuteSprite;
            if (def != null) portrait.sprite = def;
        }
    }

    public void ShowInterface()
    {
        if (HUDManager.IsInMainMenu())
        {
            HideInterface();
            return;
        }

        if (mainInterfacePanel != null)
        {
            mainInterfacePanel.SetActive(true);
            EnsureCanvasScaling();

            NyxarisUIStyler styler = GetStyler();
            if (styler != null)
            {
                styler.ApplyStyling();
                styler.AnimateOpen();
            }

            EnsureDefaultPortrait();

            if (messageInput != null)
            {
                messageInput.text = "";
                messageInput.ActivateInputField();
            }
            HUDManager.Instance?.UpdateVisibility();
            SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
        }
    }

    public void HideInterface()
    {
        if (mainInterfacePanel != null)
        {
            NyxarisUIStyler styler = GetStyler();
            if (styler != null)
            {
                styler.AnimateClose();
            }
            else
            {
                mainInterfacePanel.SetActive(false);
            }
        }
        HUDManager.Instance?.UpdateVisibility();
        SpawnOfChaos.Minigames.HUDOrbPanel.Instance?.UpdateVisibility();
    }

    void Update()
    {
        if (HUDManager.IsInMainMenu())
        {
            if (mainInterfacePanel != null && mainInterfacePanel.activeSelf)
            {
                HideInterface();
            }
            return;
        }

        if (mainInterfacePanel != null && mainInterfacePanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideInterface();
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (messageInput != null && !string.IsNullOrWhiteSpace(messageInput.text))
                {
                    SendInputMessage();
                }
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (mainInterfacePanel != null)
            {
                CanvasGroup cg = mainInterfacePanel.GetComponent<CanvasGroup>();
                bool isVisible = mainInterfacePanel.activeSelf && (cg == null || cg.alpha > 0.01f);
                if (isVisible)
                    HideInterface();
                else
                    ShowInterface();
            }
        }
    }

    private NyxarisUIStyler GetStyler()
    {
        if (mainInterfacePanel == null) return null;
        return mainInterfacePanel.GetComponent<NyxarisUIStyler>() ?? 
               mainInterfacePanel.GetComponentInChildren<NyxarisUIStyler>(true);
    }

    // ═══════════════════════════════════════════════════════════
    //  MESSAGING & BRAIN INTEGRATION
    // ═══════════════════════════════════════════════════════════

    public void SendInputMessage()
    {
        string msg = messageInput.text;
        if (string.IsNullOrWhiteSpace(msg)) return;

        StartCoroutine(SendRequest(msg));
        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    IEnumerator SendRequest(string msg)
    {
        NyxarisUIStyler styler = GetStyler();
        string mode = currentMode;
        if (styler != null)
        {
            string dropdownMode = styler.GetSelectedMode();
            if (!string.IsNullOrEmpty(dropdownMode)) mode = dropdownMode;
        }

        // Live Player Stats Injection
        float hp = 100f, maxHp = 100f, mana = 100f, maxMana = 100f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Health h = player.GetComponent<Health>();
            if (h != null)
            {
                hp = h.CurrentHealth;
                maxHp = h.MaxHealth;
            }
            MageCombat mc = player.GetComponent<MageCombat>();
            if (mc != null)
            {
                mana = mc.currentMana;
                maxMana = mc.maxMana;
            }
        }

        NyxarisRequest requestData = new NyxarisRequest 
        { 
            message = msg, 
            mode = mode, 
            trust = currentTrust,
            level = SceneManager.GetActiveScene().name,
            player_hp = hp,
            player_max_hp = maxHp,
            player_mana = mana,
            player_max_mana = maxMana
        };
        string json = JsonUtility.ToJson(requestData);

        if (styler != null) styler.ShowLoading();

        bool success = false;
        NyxarisResponse response = null;

        // 1. Try Primary API Host (5000)
        yield return TryPostRequest(apiHost, json, (res) => { response = res; success = true; });

        // 2. Try Fallback Host (5001) if not successful
        if (!success)
        {
            yield return TryPostRequest(apiHostFallback, json, (res) => { response = res; success = true; });
        }

        if (styler != null) styler.HideLoading();

        // 3. Built-in Local Heuristic Mind Fallback
        if (!success || response == null || string.IsNullOrEmpty(response.response))
        {
            response = GetLocalFastResponse(msg, hp, maxHp, mana, maxMana);
        }

        // Apply Trust Delta
        if (response.new_trust > 0f)
        {
            currentTrust = Mathf.Clamp01(response.new_trust);
        }

        if (styler != null && !string.IsNullOrEmpty(response.sprite_key))
            styler.SetSpriteKeyDisplay(response.sprite_key);

        // Show/Hide Minigame Quick-Launch Button
        if (styler != null)
        {
            if (!string.IsNullOrEmpty(response.suggested_minigame))
                styler.ShowMinigameShortcut(response.suggested_minigame);
            else
                styler.HideMinigameShortcut();
        }

        // Play Frame Animation (smooth 20 FPS, freeze on last frame)
        PlayAnimation(response.animation, response.emotion);

        if (currentTypewriterCoroutine != null) StopCoroutine(currentTypewriterCoroutine);
        currentTypewriterCoroutine = StartCoroutine(TypeText(response.response));
    }

    private IEnumerator TryPostRequest(string url, string json, System.Action<NyxarisResponse> onParsed)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15; // Generous timeout for local AI model generation

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
            {
                try
                {
                    NyxarisResponse parsed = JsonUtility.FromJson<NyxarisResponse>(req.downloadHandler.text);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.response))
                    {
                        onParsed?.Invoke(parsed);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[NyxarisManager] Parse error from " + url + ": " + ex.Message);
                }
            }
        }
    }

    public void PlayAnimation(string animKey, string fallbackEmotion = "")
    {
        if (frameAnimator != null)
        {
            string key = !string.IsNullOrEmpty(animKey) ? animKey : fallbackEmotion;
            frameAnimator.PlayAnimation(key, 20f);
        }
        else
        {
            SetEmotionFallback(fallbackEmotion);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INSTANT C# OFFLINE HEURISTIC MIND ENGINE
    // ═══════════════════════════════════════════════════════════
    private NyxarisResponse GetLocalFastResponse(string userMsg, float hp, float maxHp, float mana, float maxMana)
    {
        string textLower = userMsg.ToLower();
        string activeScene = SceneManager.GetActiveScene().name;
        float hpPct = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp));
        float manaPct = Mathf.Clamp01(mana / Mathf.Max(1f, maxMana));

        string[] explainingPool = {
            "nyxarisexplaining0-c6c6f20e",
            "nyxarisexplaining1-a4a19986",
            "nyxarisexcitedarmsspreadexplaining-cb776e55",
            "nyxaristalkingeyesclosed-a238b88a",
            "nyxarisconfidently-64b9a921",
            "nyxarisconfidently0-146ef255"
        };

        string[] casualTalkPool = {
            "nyxarishappytosay-e0fd84be",
            "nyxarishappy-f4e5a565",
            "nyxarishappythinking-bd1f19af",
            "nyxarisshrug-82e20542",
            "nyxarisnuetral-411247bb",
            "nyxarisnuetralstare-14b61402"
        };

        string[] thinkingPool = {
            "nyxaristhinking-616901a8",
            "nyxariscutelythinking-08686484",
            "nyxarishappythinking-bd1f19af"
        };

        string[] tsundereTeasePool = {
            "nyxariscutelyannoyed-9f21f3fd",
            "nyxariseyesrolling-0c15d4fb",
            "nyxarisannoyedarmsfolded-6d8ef381",
            "nyxarisannoyed-1fd7f301",
            "nyxarischeeksfulloffoodormana-246d2ced"
        };

        string[] excitedPool = {
            "nyxarisexcited-f2ab5508",
            "nyxarisexcitedarmsspreadexplaining-cb776e55",
            "nyxarisinfactuation-56ed8338"
        };

        string[] affectionPool = {
            "nyxarisinlove-279c11ce",
            "nyxarisinfactuation-56ed8338",
            "nyxarishappytosay-e0fd84be"
        };

        string respText;
        string emotion;
        string anim;
        string suggestedMg = null;
        float trustDelta = 0.02f;

        // 1. Low HP / Hurt Reaction
        if (hpPct < 0.35f && (textLower.Contains("heal") || textLower.Contains("hurt") || textLower.Contains("hp") || textLower.Contains("health") || textLower.Contains("help") || textLower.Contains("dying") || textLower.Contains("pain") || textLower.Contains("ouch")))
        {
            string[] responses = {
                "You are battered, mortal! Do not throw your life away so recklessly. Take a breath or train in the Void Surge.",
                "Your wounds are severe! Step back and gather health orbs before you collapse!",
                "Look at you, barely standing! A fallen warrior is of no use to my mission. Recover immediately!"
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "cutely_upset";
            anim = "nyxariscutelyupset-72bf7d95";
            suggestedMg = "VoidSurge";
            trustDelta = 0.04f;
        }
        // 2. Low Mana Reaction
        else if (manaPct < 0.30f && (textLower.Contains("mana") || textLower.Contains("spell") || textLower.Contains("mp") || textLower.Contains("magic") || textLower.Contains("empty") || textLower.Contains("cast")))
        {
            string[] responses = {
                "Your mana is dangerously depleted. Refresh your arcane flow before engaging the next clan guardian.",
                "You cannot conjure spells on empty reserves! Collect mana orbs or meditate for a moment.",
                "The void essence within you runs thin. Recharging your magic is essential right now."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "warning";
            anim = "nyxarissternorimportantwarning-534901f6";
            suggestedMg = "OrbSplash";
            trustDelta = 0.03f;
        }
        // 3. Minigame & Training Inquiries
        else if (textLower.Contains("minigame") || textLower.Contains("arcade") || textLower.Contains("train") || textLower.Contains("practice") || textLower.Contains("void surge") || textLower.Contains("orb splash") || textLower.Contains("shadow runner") || textLower.Contains("skybound") || textLower.Contains("play"))
        {
            string[] games = { "VoidSurge", "OrbSplash", "ShadowRunner", "SkyboundBox" };
            suggestedMg = games[Random.Range(0, games.Length)];
            string[] responses = {
                $"Sharpen your instincts in {suggestedMg}! Gathering void essence now will make your strikes lethal.",
                $"A true warrior trains constantly. Test your reflexes in {suggestedMg} and return stronger!",
                $"Looking to hone your arcane mastery? Step into {suggestedMg} and collect divine rewards."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "excited";
            anim = excitedPool[Random.Range(0, excitedPool.Length)];
            trustDelta = 0.05f;
        }
        // 4. Identity & Lore of Nyxaris
        else if (textLower.Contains("who are you") || textLower.Contains("what are you") || textLower.Contains("your name") || textLower.Contains("goddess") || textLower.Contains("about yourself"))
        {
            string[] responses = {
                "I am Nyxaris, Goddess of the Dark Multiverse. Bound across timelines to restore cosmic balance—and uncover the truth behind my followers' demise.",
                "You stand before Nyxaris. Though my mortal form is diminished in this realm, the primordial void still answers my command.",
                "I am the sovereign of shadows and forgotten realms. Together, we are going to unravel the conspiracy consuming this forest."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "confidently";
            anim = explainingPool[Random.Range(0, explainingPool.Length)];
            trustDelta = 0.03f;
        }
        // 5. Followers Massacre & Mystery Lore
        else if (textLower.Contains("who") || textLower.Contains("killer") || textLower.Contains("massacre") || textLower.Contains("follower") || textLower.Contains("culprit") || textLower.Contains("murder") || textLower.Contains("died") || textLower.Contains("who did this"))
        {
            string[] responses = {
                "My followers were slaughtered in cold blood across this timeline. Clues point to a warrior hidden among the mountain clans—or something far darker impersonating them.",
                "Someone orchestrated the massacre to spark war between the Strawhat and Samurai clans. We must expose the imposter before more blood is spilled.",
                "The killer leaves a trail of deception. Investigate both dōjōs in the mountains—the evidence will lead us to the culprit."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "explaining";
            anim = explainingPool[Random.Range(0, explainingPool.Length)];
            trustDelta = 0.03f;
        }
        // 6. Strawhat Clan Lore
        else if (textLower.Contains("strawhat") || textLower.Contains("dojo 1") || textLower.Contains("dojo1") || textLower.Contains("straw"))
        {
            string[] responses = {
                "The Strawhat Clan claims innocence, insisting the killer is a shape-shifter in the Samurai Clan. Do not lower your guard in their dōjō.",
                "Their warriors fight with swift straw blades. Challenge their altar guardian and demand the truth about the killings.",
                "The Strawhat masters know more than they let on. Watch their movements carefully when you step past their gates."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "thinking";
            anim = thinkingPool[Random.Range(0, thinkingPool.Length)];
        }
        // 7. Samurai Clan Lore
        else if (textLower.Contains("samurai") || textLower.Contains("dojo 2") || textLower.Contains("dojo2") || textLower.Contains("master"))
        {
            string[] responses = {
                "Rumors say the Samurai Clan's Master was sighted alive, despite dying two years ago... Be vigilant; things are not as they appear.",
                "The Samurai Clan blames the Strawhats, but this resurrected Master suggests dark illusions are at play.",
                "Face the Samurai Clan guardian. We must determine if their fallen Master has truly returned from the grave."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "warning";
            anim = "nyxarissternorimportantwarning-534901f6";
        }
        // 8. Cave & Tsuchigumo Reveal
        else if (textLower.Contains("cave") || textLower.Contains("spider") || textLower.Contains("tsuchigumo") || textLower.Contains("web"))
        {
            string[] responses = {
                "The giant spiders multiplying in the regional cave serve Tsuchigumo—the true shape-shifting culprit behind the massacre!",
                "Tsuchigumo weaves webs of discord, impersonating both clans to fuel their hatred. Descend the cave and crush this beast!",
                "The bottom of the mountain cave holds the final answer. Steel yourself, mortal—Tsuchigumo will not surrender easily."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "pissed";
            anim = "nyxarispissed-7e387d67";
            trustDelta = 0.05f;
        }
        // 9. Guidance, Navigation & "What should I do?"
        else if (textLower.Contains("what should i do") || textLower.Contains("what now") || textLower.Contains("where do i go") || textLower.Contains("where to go") || textLower.Contains("next") || textLower.Contains("lost") || textLower.Contains("guide me") || textLower.Contains("direction"))
        {
            string[] responses = {
                "Head upward through the mountain path. We must visit both Dōjō 1 (Strawhat) and Dōjō 2 (Samurai) to gather clues before entering the cave.",
                "Explore the Cherry Blossom Village and test your blade against clan warriors. When you are ready, the cave depths await.",
                "Keep advancing along the stone path. Every enemy defeated brings us closer to uncovering Tsuchigumo's nest."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "explaining";
            anim = explainingPool[Random.Range(0, explainingPool.Length)];
        }
        // 10. Flirting / High Affinity
        else if (textLower.Contains("cute") || textLower.Contains("love") || textLower.Contains("pretty") || textLower.Contains("beautiful") || textLower.Contains("marry") || textLower.Contains("kiss") || textLower.Contains("gorgeous") || textLower.Contains("sweet"))
        {
            if (currentTrust > 0.65f)
            {
                string[] responses = {
                    "H-hush, mortal! A goddess does not get swayed by simple sweet-talking... Though, I suppose your company isn't entirely dreadful.",
                    "Flattery from you is surprisingly pleasant... Not that I'm getting attached or anything! Keep your eyes on the road.",
                    "You truly are bold to speak to a deity like that. Just make sure you stay alive so I can keep hearing it."
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "in_love";
                anim = affectionPool[Random.Range(0, affectionPool.Length)];
                trustDelta = 0.08f;
            }
            else
            {
                string[] responses = {
                    "Flattery will not distract me from our mission, mortal. Focus on the investigation at hand!",
                    "Do not think cheap compliments will earn you divine favor so easily. Prove your worth in battle first!",
                    "A goddess has no time for idle flirtation. Keep your blade sharp and your mind focused."
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "cutely_annoyed";
                anim = tsundereTeasePool[Random.Range(0, tsundereTeasePool.Length)];
            }
        }
        // 11. Playful, Teasing, or Humorous
        else if (textLower.Contains("tease") || textLower.Contains("annoying") || textLower.Contains("bossy") || textLower.Contains("funny") || textLower.Contains("joke") || textLower.Contains("short") || textLower.Contains("horns") || textLower.Contains("lazy") || textLower.Contains("food") || textLower.Contains("eat") || textLower.Contains("hungry") || textLower.Contains("baka"))
        {
            string[] responses = {
                "Who are you calling bossy?! I am guiding you so you don't wander off a cliff, ungrateful mortal!",
                "Keep making remarks like that and I might just let the next spider have a nibble of your cloak!",
                "My mana reserves require constant replenishment... which totally includes delicious festival treats, obviously!"
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "cutely_annoyed";
            anim = tsundereTeasePool[Random.Range(0, tsundereTeasePool.Length)];
            trustDelta = 0.03f;
        }
        // 12. Gratitude, Agreement & Affirmations
        else if (textLower.Contains("thank") || textLower.Contains("thanks") || textLower.Contains("ok") || textLower.Contains("okay") || textLower.Contains("alright") || textLower.Contains("got it") || textLower.Contains("understood") || textLower.Contains("yes") || textLower.Contains("yeah") || textLower.Contains("sure") || textLower.Contains("will do"))
        {
            string[] responses = {
                "Good. As long as we understand each other, nothing in this forest can stand in our way.",
                "I expect nothing less from my companion. Let us proceed with haste.",
                "Very well. Lead onward, and strike true when the moment comes."
            };
            respText = responses[Random.Range(0, responses.Length)];
            emotion = "happy_to_say";
            anim = casualTalkPool[Random.Range(0, casualTalkPool.Length)];
            trustDelta = 0.02f;
        }
        // 13. General Readiness & "How are you?"
        else if (textLower.Contains("ready") || textLower.Contains("how are you") || textLower.Contains("how r u") || textLower.Contains("what's up") || textLower.Contains("whats up") || textLower.Contains("how do you feel") || textLower.Contains("are you okay") || textLower.Contains("you ready"))
        {
            if (currentTrust > 0.6f)
            {
                string[] responses = {
                    "I am primed for battle and eager to see what we uncover next. How are you holding up?",
                    "My arcane senses are tingling with anticipation. Whenever you are ready to move, I am with you.",
                    "Feeling stronger by your side, mortal. Let us see what secrets this mountain still hides."
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "happy_to_say";
                anim = casualTalkPool[Random.Range(0, casualTalkPool.Length)];
                trustDelta = 0.03f;
            }
            else
            {
                string[] responses = {
                    "My senses are focused on the investigation. Make sure your reflexes are just as sharp.",
                    "I am ready when you are. Do not let your guard down for a single moment.",
                    "Standing by. Speak your intent or lead the way toward our next objective."
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "neutral";
                anim = casualTalkPool[Random.Range(0, casualTalkPool.Length)];
            }
        }
        // 14. Greetings & Casual Status
        else if (textLower.Contains("hello") || textLower.Contains("hi") || textLower.Contains("hey") || textLower.Contains("nyxaris") || textLower.Contains("greetings") || textLower.Contains("yo"))
        {
            if (hpPct < 0.4f)
            {
                respText = "I am with you, mortal. But you look exhausted—rest a moment before charging into danger.";
                emotion = "worried_upset";
                anim = "nyxarisworriedupsetthinkingmp4-bd2148fc";
                suggestedMg = "OrbSplash";
            }
            else if (currentTrust > 0.6f)
            {
                string[] responses = {
                    "Greetings! It is good to see you standing tall. What shall we investigate next?",
                    "Ah, there you are. I was wondering when you would seek my counsel again.",
                    "Hello, companion. Ready to turn this mountain upside down?"
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "happy_to_say";
                anim = casualTalkPool[Random.Range(0, casualTalkPool.Length)];
                trustDelta = 0.03f;
            }
            else
            {
                string[] responses = {
                    "I am with you, mortal. Speak your mind or ask for guidance on our investigation.",
                    "Greetings. What observations do you have to share from your journey?",
                    "I am listening. What direction shall we take?"
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "neutral";
                anim = casualTalkPool[Random.Range(0, casualTalkPool.Length)];
            }
        }
        // 15. Dynamic Conversational Fallback
        else
        {
            if (activeScene.Contains("Dojo1"))
            {
                string[] responses = {
                    "We are at the Strawhat Dōjō. Stay alert and watch for any hidden clues near their altar.",
                    "The Strawhat warriors are sizing you up. Speak with their master or challenge their champions.",
                    "Look around this dōjō carefully. The killer may have left traces of their presence."
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "explaining";
                anim = explainingPool[Random.Range(0, explainingPool.Length)];
            }
            else if (activeScene.Contains("Dojo2"))
            {
                string[] responses = {
                    "This dōjō reeks of deception. The Master who stands before you is a false illusion!",
                    "Keep your distance from the Samurai guards until we verify who is commanding them.",
                    "Something is unnatural about this place. Be ready to draw your weapon at a moment's notice."
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "thinking";
                anim = thinkingPool[Random.Range(0, thinkingPool.Length)];
            }
            else if (activeScene.Contains("Cave"))
            {
                string[] responses = {
                    "We stand at the threshold of truth. Tsuchigumo awaits below in the webs. Prepare yourself for battle!",
                    "The webs grow thicker here. Watch the shadows above as we descend into the lair.",
                    "Tsuchigumo's venomous presence is heavy in the air. Let us cleanse this cave together!"
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "angry";
                anim = "nyxarisangry-e56db4b1";
            }
            else
            {
                string[] responses = {
                    "I hear you, mortal. Keep moving through the Cherry Blossom Forest—the clues we need are waiting ahead.",
                    "Interesting thought. Let us press onward to the mountain dōjōs and see what we can find.",
                    "Indeed. Stay vigilant and keep your blade ready. Every step brings us closer to the truth.",
                    "A curious remark. Keep your focus on our quest, and let us unveil what lies in the cave."
                };
                respText = responses[Random.Range(0, responses.Length)];
                emotion = "explaining";
                anim = casualTalkPool[Random.Range(0, casualTalkPool.Length)];
            }
        }

        return new NyxarisResponse
        {
            response = respText,
            emotion = emotion,
            animation = anim,
            sprite_key = emotion,
            suggested_minigame = suggestedMg,
            new_trust = Mathf.Clamp01(currentTrust + trustDelta)
        };
    }

    private void SetEmotionFallback(string emotion)
    {
        Sprite selectedSprite = neutralSprite;
        string e = emotion.ToLower();
        if (e.Contains("explain")) selectedSprite = explainingSprite ?? neutralSprite;
        else if (e.Contains("annoy") || e.Contains("cute") || e.Contains("love")) selectedSprite = cuteSprite ?? neutralSprite;

        if (portrait != null && selectedSprite != null)
        {
            portrait.sprite = selectedSprite;
        }
    }

    IEnumerator TypeText(string text)
    {
        NyxarisUIStyler styler = GetStyler();
        if (styler != null) styler.BouncePortrait();

        if (dialogueText == null) yield break;

        dialogueText.text = "";
        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSecondsRealtime(0.008f); // Realtime typewriter
        }
        dialogueText.text = text;
    }

    public void AutoLoadExpressionSprites()
    {
        if (neutralSprite == null) neutralSprite = Resources.Load<Sprite>("NyxarisExpressions/neutral");
        if (explainingSprite == null) explainingSprite = Resources.Load<Sprite>("NyxarisExpressions/explaining");
        if (cuteSprite == null) cuteSprite = Resources.Load<Sprite>("NyxarisExpressions/cutely-annoyed");
    }

    [System.Serializable]
    public class CinematicLine
    {
        public string animationKey;
        public string text;

        public CinematicLine(string anim, string t)
        {
            animationKey = anim;
            text = t;
        }
    }

    public static NyxarisManager EnsureInstanceInScene()
    {
        if (Instance != null) return Instance;

        GameObject prefab = Resources.Load<GameObject>("Prefabs/MainInterface") ?? 
                            Resources.Load<GameObject>("MainInterface");
        if (prefab != null)
        {
            GameObject go = Object.Instantiate(prefab);
            go.name = "MainInterface";
            return go.GetComponentInChildren<NyxarisManager>(true);
        }
        return null;
    }

    private Coroutine cinematicCoroutine;

    /// <summary>
    /// Starts a one-way cinematic dialogue sequence where player presses [E] to advance.
    /// Input typing is disabled, and animated character portraits react to each line.
    /// </summary>
    public void StartCinematicStoryDialogue(CinematicLine[] lines, System.Action onComplete)
    {
        if (cinematicCoroutine != null) StopCoroutine(cinematicCoroutine);
        cinematicCoroutine = StartCoroutine(CinematicStoryRoutine(lines, onComplete));
    }

    private IEnumerator CinematicStoryRoutine(CinematicLine[] lines, System.Action onComplete)
    {
        try
        {
            if (mainInterfacePanel != null)
            {
                mainInterfacePanel.SetActive(true);
                Canvas c = mainInterfacePanel.GetComponentInParent<Canvas>();
                if (c != null)
                {
                    c.sortingOrder = 950;
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }

            EnsureCanvasScaling();
            EnsureDefaultPortrait();

            // Hide message input & buttons during cinematic dialogue
            if (messageInput != null) messageInput.gameObject.SetActive(false);

            Transform uiSpace = mainInterfacePanel != null ? mainInterfacePanel.transform.Find("UIspace") : null;
            if (uiSpace != null)
            {
                Transform sendBtn = uiSpace.Find("SendButton") ?? uiSpace.Find("SubmitButton");
                if (sendBtn != null) sendBtn.gameObject.SetActive(false);
            }

            // Lock player movement during conversation
            move.ExternalMovementLock = true;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // 1. Play Portrait Expression Animation
                if (frameAnimator != null && !string.IsNullOrEmpty(line.animationKey))
                {
                    frameAnimator.PlayAnimation(line.animationKey, 14f);
                }
                else
                {
                    SetEmotionFallback(line.animationKey);
                }

                // 2. Typewriter line
                if (dialogueText != null) dialogueText.text = "";
                string fullText = line.text;
                int charIndex = 0;
                float charInterval = 0.02f;

                while (charIndex < fullText.Length)
                {
                    if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                    {
                        if (dialogueText != null) dialogueText.text = fullText;
                        yield return new WaitForSeconds(0.12f);
                        break;
                    }

                    if (dialogueText != null) dialogueText.text += fullText[charIndex];
                    charIndex++;
                    yield return new WaitForSeconds(charInterval);
                }

                if (dialogueText != null)
                {
                    dialogueText.text = fullText + "\n\n<color=#D47BFF><size=70%>► Press [E] to continue</size></color>";
                }

                // 3. Wait for Player to press [E] or any key to advance
                yield return new WaitForSeconds(0.1f);
                float lineWaitTimer = 0f;
                while (lineWaitTimer < 8f)
                {
                    lineWaitTimer += Time.deltaTime;
                    if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                    {
                        break;
                    }
                    yield return null;
                }
                yield return new WaitForSeconds(0.08f);
            }
        }
        finally
        {
            HideInterface();
            move.ExternalMovementLock = false;
            onComplete?.Invoke();
        }
    }

    void OnDisable()
    {
        move.ExternalMovementLock = false;
    }

    void OnDestroy()
    {
        move.ExternalMovementLock = false;
    }

}
