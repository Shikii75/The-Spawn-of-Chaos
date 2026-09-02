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
    [ExecuteAlways]
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

        private GameObject panelGO;

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

        void Start()
        {
            FindPlayer();
            SubscribeEvents();
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
                    Destroy(p.gameObject);
                }
            }

            FindPlayer();
            SubscribeEvents();
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

            // Real-time Health Orb Fill & Value sync
            if (playerHealth != null)
            {
                float hpRatio = (float)playerHealth.CurrentHealth / Mathf.Max(1, playerHealth.MaxHealth);
                if (healthOrbUI != null) healthOrbUI.fillAmount = hpRatio;
                if (healthValueText != null) healthValueText.text = $"{playerHealth.CurrentHealth}%";
            }

            // Real-time Mana Orb Fill & Value sync
            if (playerCombat != null)
            {
                float mpRatio = playerCombat.currentMana / Mathf.Max(1f, playerCombat.maxMana);
                if (manaOrbUI != null) manaOrbUI.fillAmount = mpRatio;
                if (manaValueText != null) manaValueText.text = $"{Mathf.RoundToInt(mpRatio * 100f)}%";
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

            bool shouldShowHUD = !Application.isPlaying || HUDManager.IsGameplayActive();

            if (panelGO != null && panelGO.activeSelf != shouldShowHUD)
            {
                panelGO.SetActive(shouldShowHUD);
            }

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas != HUDManager.Instance?.Canvas && parentCanvas.gameObject.activeSelf != shouldShowHUD)
            {
                parentCanvas.gameObject.SetActive(shouldShowHUD);
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

        private void BuildHUDWidget()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                canvas = UIFactory.CreateCanvas("HUDOrbCanvas", -10);
                transform.SetParent(canvas.transform, false);
                DontDestroyOnLoad(canvas.gameObject);
            }

            // Create fresh container panel with top-left pivot
            if (panelGO != null) Destroy(panelGO);

            panelGO = new GameObject("HUDOrbPanel", typeof(RectTransform));
            panelGO.transform.SetParent(canvas.transform, false);

            RectTransform panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.pivot = new Vector2(0f, 1f);
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(50f, -50f);
            panelRT.sizeDelta = new Vector2(520f, 160f);

            HorizontalLayoutGroup hlg = panelGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.spacing = 26f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            // Dark Obsidian Backing Frame for the 4 containers
            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.02f, 0.02f, 0.05f, 0.70f);
            Outline panelOutline = panelGO.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.10f, 0.14f, 0.25f, 0.55f);
            panelOutline.effectDistance = new Vector2(2f, 2f);

            healthOrbUI = CreateOrbItem(panelRT, OrbType.Health, "HEALTH", new Color(0.95f, 0.2f, 0.35f, 1f), out healthValueText);
            manaOrbUI = CreateOrbItem(panelRT, OrbType.Mana, "MANA", new Color(0f, 0.85f, 1f, 1f), out manaValueText);
            currencyOrbUI = CreateOrbItem(panelRT, OrbType.Currency, "COINS", new Color(0.92f, 0.15f, 0.22f, 1f), out currencyValueText);
            epOrbUI = CreateOrbItem(panelRT, OrbType.EP, "EXP", new Color(0.85f, 0.4f, 1f, 1f), out epValueText);

            // Level Up Banner (Center Screen)
            if (levelUpBanner == null)
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

            UpdateVisibility();
        }

        private ProceduralOrbUI CreateOrbItem(Transform parent, OrbType type, string label, Color tagColor, out TextMeshProUGUI valueTextRef)
        {
            GameObject orbGO = new GameObject($"OrbItem_{type}", typeof(RectTransform), typeof(RawImage), typeof(ProceduralOrbUI), typeof(LayoutElement));
            orbGO.transform.SetParent(parent, false);

            RectTransform rt = orbGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(90f, 90f);

            LayoutElement le = orbGO.GetComponent<LayoutElement>();
            le.preferredWidth = 90f;
            le.preferredHeight = 90f;
            le.minWidth = 90f;
            le.minHeight = 90f;

            ProceduralOrbUI orbUI = orbGO.GetComponent<ProceduralOrbUI>();
            orbUI.orbType = type;
            orbUI.textureResolution = 128;
            orbUI.autoUpdate = true;
            orbUI.fillAmount = 0.8f;

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
