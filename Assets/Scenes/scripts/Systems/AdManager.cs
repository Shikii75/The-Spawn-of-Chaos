using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_UNITY_ADS
using UnityEngine.Advertisements;
#endif

/// <summary>
/// AdManager - Monetization & Rewarded Ad System:
/// 1. Rewarded Revival: When player dies, option to revive on the spot with full HP & Shield.
/// 2. 5 Minigame Losses: Triggers rewarded offer (bonus coins / shield retry) or ad check after 5 game overs.
/// 3. 4 Nyxaris Casual Conversations: Triggers an ad prompt after 4 back-and-forth messages in casual chat (never during cutscenes).
/// 4. Universal Simulation: In Unity Editor or offline, renders a crisp 2-second Cyber-Gothic countdown ad simulator.
/// </summary>
public class AdManager : MonoBehaviour
#if ENABLE_UNITY_ADS
    , IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
#endif
{
    public static AdManager Instance { get; private set; }

    [Header("Platform Game IDs")]
    [SerializeField] private string androidGameId = "5000000";
    [SerializeField] private string iosGameId = "5000001";
    [SerializeField] private bool testMode = true;

    [Header("Ad Unit IDs")]
    private string rewardedPlacementId = "Rewarded_Android";
    private string interstitialPlacementId = "Interstitial_Android";

    [Header("Monetization Threshold Trackers")]
    public int minigameLossCount = 0;
    public const int MINIGAME_LOSS_THRESHOLD = 5;

    public int nyxarisCasualMessageCount = 0;
    public const int NYXARIS_CASUAL_THRESHOLD = 4;

    private bool isInitialized = false;
    private Action currentRewardCallback;
    private Action currentCloseCallback;

    // Simulation UI Elements
    private Canvas adCanvas;
    private GameObject adModal;
    private TextMeshProUGUI adStatusText;
    private TextMeshProUGUI adTimerText;
    private Coroutine adSimCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInit()
    {
        if (Instance == null && FindFirstObjectByType<AdManager>() == null)
        {
            GameObject go = new GameObject("AdManager");
            Instance = go.AddComponent<AdManager>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeAds();
        BuildSimulationUI();
    }

    private void InitializeAds()
    {
#if UNITY_IOS
        string gameId = iosGameId;
        rewardedPlacementId = "Rewarded_iOS";
        interstitialPlacementId = "Interstitial_iOS";
#else
        string gameId = androidGameId;
        rewardedPlacementId = "Rewarded_Android";
        interstitialPlacementId = "Interstitial_Android";
#endif

#if ENABLE_UNITY_ADS
        if (!Advertisement.isInitialized && Advertisement.isSupported)
        {
            Advertisement.Initialize(gameId, testMode, this);
        }
        else
        {
            isInitialized = true;
        }
#else
        isInitialized = true;
        Debug.Log("[AdManager] Native Unity Ads not enabled in current build target; using Cyber-Gothic Ad Simulator.");
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. MONETIZATION EVENT TRACKERS
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when player receives a Game Over in any minigame (Skybound, ShadowRunner, VoidSurge).
    /// </summary>
    public void RecordMinigameLoss()
    {
        minigameLossCount++;
        Debug.Log($"[AdManager] Minigame loss recorded: {minigameLossCount}/{MINIGAME_LOSS_THRESHOLD}");

        if (minigameLossCount >= MINIGAME_LOSS_THRESHOLD)
        {
            minigameLossCount = 0;
            TriggerMinigameBonusAd();
        }
    }

    private void TriggerMinigameBonusAd()
    {
        ShowAdPopup(
            "ARCADE REWARD VISION",
            "5 Arcade attempts completed!\nWatch a short vision to receive +200 Bonus Coins & an Arcade Shield?",
            () => {
                // Grant reward
                if (PlayerCurrency.Instance != null)
                {
                    PlayerCurrency.Instance.AddCoins(200);
                }
                Debug.Log("[AdManager] Granted +200 coins for minigame loss threshold!");
            },
            null
        );
    }

    /// <summary>
    /// Called after each completed casual chat exchange with Nyxaris (not during story cutscenes).
    /// </summary>
    public void RecordNyxarisCasualExchange()
    {
        nyxarisCasualMessageCount++;
        Debug.Log($"[AdManager] Nyxaris casual chat recorded: {nyxarisCasualMessageCount}/{NYXARIS_CASUAL_THRESHOLD}");

        if (nyxarisCasualMessageCount >= NYXARIS_CASUAL_THRESHOLD)
        {
            nyxarisCasualMessageCount = 0;
            TriggerNyxarisCommunionAd();
        }
    }

    private void TriggerNyxarisCommunionAd()
    {
        ShowAdPopup(
            "VOID HARMONY",
            "Deep communion with Nyxaris established (4 exchanges).\nRefill all Mana & receive a Spirit Ward?",
            () => {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    MageCombat mc = player.GetComponent<MageCombat>();
                    if (mc != null) mc.currentMana = mc.maxMana;
                }
                Debug.Log("[AdManager] Nyxaris communion reward granted (Full Mana restored)!");
            },
            null
        );
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. REWARDED REVIVAL (DEATH "SECOND WIND")
    // ─────────────────────────────────────────────────────────────────────────────

    public void ShowRewardedRevival(Action onRevived, Action onDeclined)
    {
        ShowAdPopup(
            "SECOND WIND",
            "Fallen in battle!\nCommune with the Void to revive on the spot with Full HP & Invulnerability Shield?",
            () => {
                onRevived?.Invoke();
            },
            () => {
                onDeclined?.Invoke();
            }
        );
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. AD DISPLAY & SIMULATION
    // ─────────────────────────────────────────────────────────────────────────────

    public void ShowAdPopup(string title, string description, Action onRewarded, Action onSkipped)
    {
        if (adModal == null) BuildSimulationUI();

        currentRewardCallback = onRewarded;
        currentCloseCallback = onSkipped;

        adStatusText.text = title;
        adTimerText.text = description;
        adModal.SetActive(true);

        if (adSimCoroutine != null) StopCoroutine(adSimCoroutine);
        adSimCoroutine = StartCoroutine(PlayAdSequence(title));
    }

    private IEnumerator PlayAdSequence(string title)
    {
        // 2-second simulated ad playback with countdown
        for (int i = 2; i > 0; i--)
        {
            adStatusText.text = $"[AD VISION STREAMING]\n{title}";
            adTimerText.text = $"Streaming sponsor message...\nRewarding in {i}s";
            yield return new WaitForSecondsRealtime(1.0f);
        }

        adStatusText.text = "VISION COMPLETE!";
        adTimerText.text = "Communion reward unlocked!";
        yield return new WaitForSecondsRealtime(0.5f);

        adModal.SetActive(false);
        currentRewardCallback?.Invoke();
        currentRewardCallback = null;
    }

    private void BuildSimulationUI()
    {
        GameObject canvasGO = new GameObject("AdSimulationCanvas");
        canvasGO.transform.SetParent(transform);
        adCanvas = canvasGO.AddComponent<Canvas>();
        adCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        adCanvas.sortingOrder = 150; // Above HUD, Pause, and Minigames

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Fullscreen backdrop
        GameObject modalGO = new GameObject("AdModalPanel");
        modalGO.transform.SetParent(canvasGO.transform, false);
        RectTransform modalRT = modalGO.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero;
        modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero;
        modalRT.offsetMax = Vector2.zero;
        Image bg = modalGO.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.06f, 0.94f);

        // Center card
        GameObject cardGO = new GameObject("Card");
        cardGO.transform.SetParent(modalGO.transform, false);
        RectTransform cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(650, 420);
        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = new Color(0.06f, 0.08f, 0.16f, 0.98f);
        Outline outline = cardGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.94f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(cardGO.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchoredPosition = new Vector2(0, 120);
        titleRT.sizeDelta = new Vector2(580, 80);
        adStatusText = titleGO.AddComponent<TextMeshProUGUI>();
        adStatusText.text = "SPONSORED VISION";
        adStatusText.fontSize = 28;
        adStatusText.fontStyle = FontStyles.Bold;
        adStatusText.alignment = TextAlignmentOptions.Center;
        adStatusText.color = new Color(0f, 0.94f, 1f, 1f);

        // Timer / Desc
        GameObject descGO = new GameObject("Description");
        descGO.transform.SetParent(cardGO.transform, false);
        RectTransform descRT = descGO.AddComponent<RectTransform>();
        descRT.anchoredPosition = new Vector2(0, 0);
        descRT.sizeDelta = new Vector2(580, 140);
        adTimerText = descGO.AddComponent<TextMeshProUGUI>();
        adTimerText.text = "Loading sponsored vision...";
        adTimerText.fontSize = 20;
        adTimerText.alignment = TextAlignmentOptions.Center;
        adTimerText.color = Color.white;

        // Skip / Close Button
        GameObject skipBtnGO = new GameObject("SkipButton");
        skipBtnGO.transform.SetParent(cardGO.transform, false);
        RectTransform skipRT = skipBtnGO.AddComponent<RectTransform>();
        skipRT.anchoredPosition = new Vector2(0, -140);
        skipRT.sizeDelta = new Vector2(240, 50);
        Image skipBg = skipBtnGO.AddComponent<Image>();
        skipBg.color = new Color(0.2f, 0.1f, 0.3f, 0.8f);
        Button btn = skipBtnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => {
            if (adSimCoroutine != null) StopCoroutine(adSimCoroutine);
            adModal.SetActive(false);
            currentCloseCallback?.Invoke();
            currentRewardCallback = null;
            currentCloseCallback = null;
        });

        GameObject skipTxtGO = new GameObject("SkipLabel");
        skipTxtGO.transform.SetParent(skipBtnGO.transform, false);
        RectTransform skipTxtRT = skipTxtGO.AddComponent<RectTransform>();
        skipTxtRT.anchorMin = Vector2.zero;
        skipTxtRT.anchorMax = Vector2.one;
        skipTxtRT.sizeDelta = Vector2.zero;
        TextMeshProUGUI skipTxt = skipTxtGO.AddComponent<TextMeshProUGUI>();
        skipTxt.text = "DECLINE / CLOSE";
        skipTxt.fontSize = 16;
        skipTxt.fontStyle = FontStyles.Bold;
        skipTxt.alignment = TextAlignmentOptions.Center;
        skipTxt.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);

        adModal = modalGO;
        adModal.SetActive(false);
    }

#if ENABLE_UNITY_ADS
    public void OnInitializationComplete() { isInitialized = true; }
    public void OnInitializationFailed(UnityAdsInitializationError error, string message) { isInitialized = false; }
    public void OnUnityAdsAdLoaded(string placementId) { }
    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message) { }
    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message) { }
    public void OnUnityAdsShowStart(string placementId) { }
    public void OnUnityAdsShowClick(string placementId) { }
    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            currentRewardCallback?.Invoke();
        }
        else
        {
            currentCloseCallback?.Invoke();
        }
        currentRewardCallback = null;
        currentCloseCallback = null;
    }
#endif
}
