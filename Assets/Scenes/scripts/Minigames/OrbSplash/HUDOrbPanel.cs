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

        private TextMeshProUGUI levelBadgeText;
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
            FindPlayer();
            SubscribeEvents();
            UpdateVisibility();
        }

        public void FindPlayer()
        {
            UnsubscribeEvents();

            GameObject p = GameObject.FindGameObjectWithTag("Player");
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
                healthOrbUI.TriggerShake(1.5f, 0.4f);
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

            // Real-time Health Orb Fill level sync
            if (playerHealth != null && healthOrbUI != null)
            {
                healthOrbUI.fillAmount = (float)playerHealth.CurrentHealth / Mathf.Max(1, playerHealth.MaxHealth);
            }

            // Real-time Mana Orb Fill level sync
            if (playerCombat != null && manaOrbUI != null)
            {
                manaOrbUI.fillAmount = playerCombat.currentMana / Mathf.Max(1f, playerCombat.maxMana);
            }

            // Real-time Currency Orb Fill level sync (baseline 0.10f fill at 0 coins up to 1.0f as coins increase)
            if (currencyOrbUI != null)
            {
                int coins = playerCurrency != null ? playerCurrency.Coins : 0;
                float fill = Mathf.Clamp01(0.10f + (coins / 100f) * 0.90f);
                currencyOrbUI.fillAmount = fill;
            }

            // Real-time EP (EXP) Orb Fill level sync
            if (PlayerLevelSystem.Instance != null && epOrbUI != null)
            {
                epOrbUI.fillAmount = PlayerLevelSystem.Instance.ExpRatio;

                if (levelBadgeText != null)
                {
                    levelBadgeText.text = $"Lv. {PlayerLevelSystem.Instance.CurrentLevel}";
                }
            }
        }

        public void UpdateVisibility()
        {
            bool shouldShowHUD = HUDManager.IsGameplayActive();

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
            // Splash EP Orb violently
            if (epOrbUI != null)
            {
                epOrbUI.TriggerSplash(2.5f);
            }

            // Show Level Up Banner
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

            // Fade in
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / 0.4f);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1.8f);

            // Fade out
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
            if (canvas == null)
            {
                canvas = UIFactory.CreateCanvas("HUDOrbCanvas", -10);
                transform.SetParent(canvas.transform, false);
                DontDestroyOnLoad(canvas.gameObject);
            }

            // Top-Left Container Panel (Primary HUD Orbs)
            panelGO = new GameObject("HUDOrbPanel", typeof(RectTransform));
            panelGO.transform.SetParent(canvas.transform, false);

            RectTransform panelRT = panelGO.GetComponent<RectTransform>();
            UIFactory.SetRectFixed(panelRT, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(155f, -40f), new Vector2(280f, 75f));

            HorizontalLayoutGroup hlg = panelGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Create 4 Orbs
            healthOrbUI = CreateOrbItem(panelRT, OrbType.Health, "HP");
            manaOrbUI = CreateOrbItem(panelRT, OrbType.Mana, "MP");
            currencyOrbUI = CreateOrbItem(panelRT, OrbType.Currency, "GOLD");
            epOrbUI = CreateOrbItem(panelRT, OrbType.EP, "EXP");

            // Level Badge text under EXP Orb
            levelBadgeText = UIFactory.CreateText(epOrbUI.transform, "LevelBadge", "Lv. 1", 11f, new Color(0.85f, 0.4f, 1f, 1f), TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(levelBadgeText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(60f, 16f));
            levelBadgeText.fontStyle = FontStyles.Bold;

            // Level Up Banner (Center Screen)
            levelUpBanner = new GameObject("LevelUpBanner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            levelUpBanner.transform.SetParent(canvas.transform, false);
            RectTransform bannerRT = levelUpBanner.GetComponent<RectTransform>();
            UIFactory.SetRectFixed(bannerRT, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(420f, 90f));

            Image bannerBg = levelUpBanner.GetComponent<Image>();
            bannerBg.color = new Color(0.05f, 0.02f, 0.12f, 0.92f);

            levelUpText = UIFactory.CreateText(levelUpBanner.transform, "Text", "LEVEL UP!\nREACHED LEVEL 2", 22f, new Color(1f, 0.82f, 0.2f, 1f), TextAlignmentOptions.Center);
            UIFactory.SetRect(levelUpText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            levelUpText.fontStyle = FontStyles.Bold;

            levelUpBanner.SetActive(false);

            UpdateVisibility();
        }

        private ProceduralOrbUI CreateOrbItem(Transform parent, OrbType type, string label)
        {
            GameObject orbGO = new GameObject($"OrbItem_{type}", typeof(RectTransform), typeof(RawImage), typeof(ProceduralOrbUI));
            orbGO.transform.SetParent(parent, false);

            RectTransform rt = orbGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60f, 60f);

            ProceduralOrbUI orbUI = orbGO.GetComponent<ProceduralOrbUI>();
            orbUI.orbType = type;
            orbUI.textureResolution = 64;
            orbUI.autoUpdate = true;
            orbUI.fillAmount = 0.8f;

            TextMeshProUGUI tagText = UIFactory.CreateText(orbGO.transform, "Tag", label, 9f, Color.white, TextAlignmentOptions.Center);
            UIFactory.SetRectFixed(tagText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 8f), new Vector2(50f, 14f));

            return orbUI;
        }
    }
}
