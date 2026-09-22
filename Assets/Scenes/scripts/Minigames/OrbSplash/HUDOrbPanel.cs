using System.Collections;
using System.Collections.Generic;
using TMPro;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Real-time HUD UI Widget displaying the 4 Procedural Liquid Orbs (Health, Mana, Currency, EP).
    /// Dynamically binds to player stats, auto-updates fill ratios, triggers liquid splashes when world orbs are collected or damage is taken,
    /// hides when on the main menu / title screen, and persists across scene transitions (e.g., Dojo screen).
    /// </summary>
    public class HUDOrbPanel : MonoBehaviour
    {
        public static HUDOrbPanel Instance { get; private set; }

        private ProceduralOrbUI healthOrbUI;
        private ProceduralOrbUI manaOrbUI;
        private ProceduralOrbUI currencyOrbUI;
        private ProceduralOrbUI epOrbUI;

        private TextMeshProUGUI healthValueText;
        private TextMeshProUGUI manaValueText;
        private TextMeshProUGUI currencyValueText;
        private TextMeshProUGUI epValueText;

        private GameObject levelUpBanner;
        private TextMeshProUGUI levelUpText;

        private Health playerHealth;
        private MageCombat playerCombat;
        private PlayerCurrency playerCurrency;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this && Instance.gameObject != null && Instance.gameObject.activeInHierarchy)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            BuildHUDWidget();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SubscribeEvents();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeEvents();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Start()
        {
            FindPlayer();
            SubscribeEvents();
            EnsureWidgetBuilt();
            UpdateVisibility();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Clean up any stray duplicate HUDOrbPanel components in the loaded scene
            var allPanels = FindObjectsByType<HUDOrbPanel>(FindObjectsSortMode.None);
            foreach (var p in allPanels)
            {
                if (p != null && p != this && p != Instance)
                {
                    if (Application.isPlaying) Destroy(p.gameObject);
                    else DestroyImmediate(p.gameObject);
                }
            }

            FindPlayer();
            SubscribeEvents();
            EnsureWidgetBuilt();
            UpdateVisibility();
        }

        public void FindPlayer()
        {
            UnsubscribeEvents();

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("Player");
            if (p == null) p = GameObject.Find("BasePlayer");
            if (p == null)
            {
                var allMoves = FindObjectsByType<move>(FindObjectsSortMode.None);
                foreach (var m in allMoves)
                {
                    if (m != null && m.gameObject.activeInHierarchy)
                    {
                        p = m.gameObject;
                        break;
                    }
                }
            }

            if (p != null)
            {
                playerHealth = p.GetComponent<Health>();
                playerCombat = p.GetComponent<MageCombat>();
                playerCurrency = p.GetComponent<PlayerCurrency>();
            }

            if (playerCurrency == null)
            {
                playerCurrency = PlayerCurrency.Instance;
            }

            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            UnsubscribeEvents();

            if (playerHealth != null)
            {
                playerHealth.onDamageTaken += OnPlayerDamageTaken;
            }

            if (playerCurrency != null)
            {
                playerCurrency.onCoinsChanged += OnCoinsChanged;
            }

            if (PlayerLevelSystem.Instance != null)
            {
                PlayerLevelSystem.Instance.onExpChanged += OnExpChanged;
                PlayerLevelSystem.Instance.onLevelUp += OnPlayerLevelUp;
            }
        }

        private void UnsubscribeEvents()
        {
            if (playerHealth != null)
            {
                playerHealth.onDamageTaken -= OnPlayerDamageTaken;
            }

            if (playerCurrency != null)
            {
                playerCurrency.onCoinsChanged -= OnCoinsChanged;
            }

            if (PlayerLevelSystem.Instance != null)
            {
                PlayerLevelSystem.Instance.onExpChanged -= OnExpChanged;
                PlayerLevelSystem.Instance.onLevelUp -= OnPlayerLevelUp;
            }
        }

        private void OnPlayerDamageTaken(int damageTaken)
        {
            if (damageTaken > 0 && healthOrbUI != null)
            {
                healthOrbUI.TriggerDamageEffect(1.5f, 0.4f);
            }
        }

        private void OnCoinsChanged(int coins)
        {
            if (currencyOrbUI != null)
            {
                currencyOrbUI.TriggerSplash(1.3f);
            }
        }

        private void OnExpChanged(int currentExp, int maxExp)
        {
            if (epOrbUI != null)
            {
                epOrbUI.TriggerSplash(1.3f);
            }
        }

        void Update()
        {
            UpdateVisibility();

            if (playerHealth == null || playerCombat == null)
            {
                FindPlayer();
            }

            if (healthOrbUI == null || healthValueText == null)
            {
                EnsureWidgetBuilt();
            }

            // Real-time Health Orb Fill & Value sync
            if (playerHealth != null)
            {
                float hpRatio = (float)playerHealth.CurrentHealth / Mathf.Max(1, playerHealth.MaxHealth);
                if (healthOrbUI != null) healthOrbUI.fillAmount = hpRatio;
                if (healthValueText != null) healthValueText.text = $"{playerHealth.CurrentHealth} / {playerHealth.MaxHealth}";
            }

            // Real-time Mana Orb Fill & Value sync
            if (playerCombat != null)
            {
                float mpRatio = playerCombat.currentMana / Mathf.Max(1f, playerCombat.maxMana);
                if (manaOrbUI != null) manaOrbUI.fillAmount = mpRatio;
                if (manaValueText != null) manaValueText.text = $"{Mathf.RoundToInt(playerCombat.currentMana)} / {Mathf.RoundToInt(playerCombat.maxMana)}";
            }

            // Real-time Currency Orb Fill & Value sync
            int coins = playerCurrency != null ? playerCurrency.Coins : 0;
            if (currencyOrbUI != null)
            {
                float fill = Mathf.Clamp01(0.12f + (coins / 100f) * 0.88f);
                currencyOrbUI.fillAmount = fill;
            }
            if (currencyValueText != null) currencyValueText.text = $"{coins}";

            // Real-time EP (EXP) Orb Fill & Value sync
            if (PlayerLevelSystem.Instance != null)
            {
                if (epOrbUI != null) epOrbUI.fillAmount = PlayerLevelSystem.Instance.ExpRatio;
                if (epValueText != null) epValueText.text = $"Lv. {PlayerLevelSystem.Instance.CurrentLevel}";
            }
        }

        public void UpdateVisibility()
        {
            if (this == null || gameObject == null) return;

            bool shouldShowHUD = HUDManager.IsGameplayActive();

            if (gameObject.activeSelf != shouldShowHUD)
            {
                gameObject.SetActive(shouldShowHUD);
            }
        }

        public void TriggerSplash(OrbType type, float intensity = 1.2f)
        {
            switch (type)
            {
                case OrbType.Health:
                    if (healthOrbUI != null) healthOrbUI.TriggerSplash(intensity);
                    break;
                case OrbType.Mana:
                    if (manaOrbUI != null) manaOrbUI.TriggerSplash(intensity);
                    break;
                case OrbType.Currency:
                    if (currencyOrbUI != null) currencyOrbUI.TriggerSplash(intensity);
                    break;
                case OrbType.EP:
                    if (epOrbUI != null) epOrbUI.TriggerSplash(intensity);
                    break;
            }
        }

        private void OnPlayerLevelUp(int newLevel)
        {
            if (epOrbUI != null) epOrbUI.TriggerSplash(2.5f);

            if (levelUpBanner != null)
            {
                if (levelUpText != null)
                {
                    levelUpText.text = $"LEVEL UP!\nREACHED LEVEL {newLevel}";
                }
                StopAllCoroutines();
                StartCoroutine(AnimateLevelUpBanner());
            }
        }

        private IEnumerator AnimateLevelUpBanner()
        {
            levelUpBanner.SetActive(true);
            CanvasGroup cg = levelUpBanner.GetComponent<CanvasGroup>();
            if (cg == null) cg = levelUpBanner.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            float t = 0f;

            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / 0.4f);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1.8f);

            t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / 0.5f);
                yield return null;
            }

            levelUpBanner.SetActive(false);
        }

        public void EnsureWidgetBuilt()
        {
            if (this == null || gameObject == null) return;
            BuildHUDWidget();
        }

        private void BuildHUDWidget()
        {
            // Strictly resolve HUDCanvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null && HUDManager.Instance != null)
            {
                canvas = HUDManager.Instance.Canvas;
            }
            if (canvas == null)
            {
                GameObject hudCanvasGO = GameObject.Find("HUDCanvas");
                if (hudCanvasGO != null) canvas = hudCanvasGO.GetComponent<Canvas>();
            }
            if (canvas == null)
            {
                canvas = UIFactory.CreateCanvas("HUDCanvas", 50);
            }
            if (canvas != null)
            {
                if (canvas.transform.parent != null) canvas.transform.SetParent(null, false);
                DontDestroyOnLoad(canvas.gameObject);
                canvas.sortingOrder = 50;

                if (transform.parent != canvas.transform)
                {
                    transform.SetParent(canvas.transform, false);
                }
            }

            // Name and configure this GameObject as the top-left HUD panel container
            gameObject.name = "HUDOrbPanel";

            RectTransform panelRT = GetComponent<RectTransform>();
            if (panelRT == null) panelRT = gameObject.AddComponent<RectTransform>();

            panelRT.pivot = new Vector2(0f, 1f);
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(40f, -40f);
            panelRT.sizeDelta = new Vector2(510f, 130f);

            // Dark Obsidian Backing Frame
            Image panelBg = GetComponent<Image>();
            if (panelBg == null) panelBg = gameObject.AddComponent<Image>();
            panelBg.color = new Color(0.02f, 0.02f, 0.05f, 0.75f);

            Outline panelOutline = GetComponent<Outline>();
            if (panelOutline == null) panelOutline = gameObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.12f, 0.18f, 0.32f, 0.65f);
            panelOutline.effectDistance = new Vector2(2f, 2f);

            // Check if all 4 orb items already exist and retrieve components if unassigned
            Transform healthTr = transform.Find("OrbItem_Health");
            Transform manaTr = transform.Find("OrbItem_Mana");
            Transform currTr = transform.Find("OrbItem_Currency");
            Transform epTr = transform.Find("OrbItem_EP");

            if (healthTr != null && healthOrbUI == null)
            {
                healthOrbUI = healthTr.GetComponent<ProceduralOrbUI>();
                Transform valTr = healthTr.Find("Value");
                if (valTr != null) healthValueText = valTr.GetComponent<TextMeshProUGUI>();
            }
            if (manaTr != null && manaOrbUI == null)
            {
                manaOrbUI = manaTr.GetComponent<ProceduralOrbUI>();
                Transform valTr = manaTr.Find("Value");
                if (valTr != null) manaValueText = valTr.GetComponent<TextMeshProUGUI>();
            }
            if (currTr != null && currencyOrbUI == null)
            {
                currencyOrbUI = currTr.GetComponent<ProceduralOrbUI>();
                Transform valTr = currTr.Find("Value");
                if (valTr != null) currencyValueText = valTr.GetComponent<TextMeshProUGUI>();
            }
            if (epTr != null && epOrbUI == null)
            {
                epOrbUI = epTr.GetComponent<ProceduralOrbUI>();
                Transform valTr = epTr.Find("Value");
                if (valTr != null) epValueText = valTr.GetComponent<TextMeshProUGUI>();
            }

            if (healthTr == null || manaTr == null || currTr == null || epTr == null ||
                healthOrbUI == null || manaOrbUI == null || currencyOrbUI == null || epOrbUI == null)
            {
                // Re-instantiate orbs cleanly
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                    else DestroyImmediate(transform.GetChild(i).gameObject);
                }

                healthOrbUI = CreateOrbItem(panelRT, OrbType.Health, "HEALTH", new Color(122f/255f, 9f/255f, 9f/255f, 1f), 60f, out healthValueText);
                manaOrbUI = CreateOrbItem(panelRT, OrbType.Mana, "MANA", new Color(0.35f, 0.35f, 0.40f, 1f), 180f, out manaValueText);
                currencyOrbUI = CreateOrbItem(panelRT, OrbType.Currency, "COINS", new Color(0.68f, 0.46f, 0.12f, 1f), 300f, out currencyValueText);
                epOrbUI = CreateOrbItem(panelRT, OrbType.EP, "EXP", new Color(0.48f, 0.16f, 0.72f, 1f), 420f, out epValueText);
            }

            // Level Up Banner (Center Screen)
            if (canvas != null && levelUpBanner == null)
            {
                Transform existingBanner = canvas.transform.Find("LevelUpBanner");
                if (existingBanner != null)
                {
                    levelUpBanner = existingBanner.gameObject;
                    levelUpText = levelUpBanner.GetComponentInChildren<TextMeshProUGUI>();
                }
                else
                {
                    levelUpBanner = new GameObject("LevelUpBanner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                    levelUpBanner.transform.SetParent(canvas.transform, false);
                    RectTransform bannerRT = levelUpBanner.GetComponent<RectTransform>();
                    UIFactory.SetRectFixed(bannerRT, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(440f, 95f));

                    Image bannerBg = levelUpBanner.GetComponent<Image>();
                    bannerBg.color = new Color(0.05f, 0.02f, 0.12f, 0.92f);

                    levelUpText = UIFactory.CreateText(levelUpBanner.transform, "Text", "LEVEL UP!\nREACHED LEVEL 2", 22f, new Color(1f, 0.82f, 0.2f, 1f), TextAlignmentOptions.Center);
                    UIFactory.SetRect(levelUpText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    levelUpText.fontStyle = FontStyles.Bold;

                    levelUpBanner.SetActive(false);
                }
            }

            UpdateVisibility();
        }

        private ProceduralOrbUI CreateOrbItem(Transform parent, OrbType type, string label, Color tagColor, float posX, out TextMeshProUGUI valueTextRef)
        {
            GameObject orbGO = new GameObject($"OrbItem_{type}", typeof(RectTransform));
            orbGO.transform.SetParent(parent, false);

            RectTransform rt = orbGO.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(posX, -65f);
            rt.sizeDelta = new Vector2(90f, 90f);

            RawImage rawImg = orbGO.AddComponent<RawImage>();

            ProceduralOrbUI orbUI = orbGO.AddComponent<ProceduralOrbUI>();
            orbUI.orbType = type;
            orbUI.textureResolution = 128;
            orbUI.autoUpdate = true;
            orbUI.fillAmount = 0.8f;
            orbUI.SetBasePosition(new Vector2(posX, -65f));
            orbUI.InitializeRenderer();

            // Tag Header above orb
            TextMeshProUGUI tagText = UIFactory.CreateText(orbGO.transform, "Tag", label, 12f, tagColor, TextAlignmentOptions.Center);
            tagText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tagText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            tagText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            tagText.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            tagText.rectTransform.sizeDelta = new Vector2(90f, 20f);
            tagText.fontStyle = FontStyles.Bold;

            // Readout Value underneath orb
            valueTextRef = UIFactory.CreateText(orbGO.transform, "Value", "100%", 18f, Color.white, TextAlignmentOptions.Center);
            valueTextRef.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            valueTextRef.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            valueTextRef.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            valueTextRef.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            valueTextRef.rectTransform.sizeDelta = new Vector2(90f, 26f);
            valueTextRef.fontStyle = FontStyles.Bold;

            return orbUI;
        }
    }
}
