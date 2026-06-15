using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBar : MonoBehaviour
{
    public static BossHealthBar Instance { get; private set; }

    // ── Runtime-built UI references ──
    private Canvas canvas;
    private GameObject bossBarPanel;
    private Slider healthSlider;
    private TextMeshProUGUI bossNameText;

    private Health activeBossHealth;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        BuildUI();
    }

    // ══════════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION — entire hierarchy built from code
    // ══════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── Canvas (sort order 8) ──
        canvas = UIFactory.CreateCanvas("BossHealthBarCanvas", 8);
        canvas.transform.SetParent(transform, false);

        // ── Top-of-screen panel — full width, 60px tall ──
        RectTransform panelRT = UIFactory.CreatePanel(
            canvas.transform, "BossBarPanel", UIFactory.PanelBackground,
            new Vector2(0f, 1f), new Vector2(1f, 1f),          // anchor top-stretch
            new Vector2(0f, -60f), new Vector2(0f, 0f)          // 60px down from top
        );
        bossBarPanel = panelRT.gameObject;

        // ── Boss name text — centered above the slider, accent color, 20pt ──
        bossNameText = UIFactory.CreateText(
            panelRT, "BossNameText", "",
            20f, UIFactory.Accent, TextAlignmentOptions.Center
        );
        UIFactory.SetRect(bossNameText.rectTransform,
            new Vector2(0.25f, 0.55f), new Vector2(0.75f, 1f),
            Vector2.zero, Vector2.zero
        );
        bossNameText.fontStyle = FontStyles.Bold;
        bossNameText.enableWordWrapping = false;

        // ── Health slider — crimson fill on dark track ──
        healthSlider = UIFactory.CreateSlider(panelRT, "BossHealthSlider", UIFactory.SliderBossFill);
        UIFactory.SetRect(healthSlider.GetComponent<RectTransform>(),
            new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.5f),
            Vector2.zero, Vector2.zero
        );

        // Start hidden
        bossBarPanel.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════════

    public void ShowBossBar(Health bossHealth, string bossName)
    {
        if (bossHealth == null || bossBarPanel == null) return;

        activeBossHealth = bossHealth;
        bossNameText.text = bossName;

        healthSlider.maxValue = bossHealth.MaxHealth;
        healthSlider.value = bossHealth.CurrentHealth;

        // Subscribe to boss damage and death events
        activeBossHealth.onDamageTaken += OnBossDamageTaken;
        activeBossHealth.onDeath += OnBossDeath;

        bossBarPanel.SetActive(true);
        Debug.Log("Boss health bar active for: " + bossName);
    }

    public void HideBossBar()
    {
        if (activeBossHealth != null)
        {
            activeBossHealth.onDamageTaken -= OnBossDamageTaken;
            activeBossHealth.onDeath -= OnBossDeath;
            activeBossHealth = null;
        }

        if (bossBarPanel != null)
        {
            bossBarPanel.SetActive(false);
        }
    }

    private void OnBossDamageTaken(int damage)
    {
        if (activeBossHealth != null && healthSlider != null)
        {
            healthSlider.value = activeBossHealth.CurrentHealth;
        }
    }

    private void OnBossDeath()
    {
        // Keep the bar showing briefly, then turn off
        Invoke(nameof(HideBossBar), 2.0f);
    }

    void OnDestroy()
    {
        if (activeBossHealth != null)
        {
            activeBossHealth.onDamageTaken -= OnBossDamageTaken;
            activeBossHealth.onDeath -= OnBossDeath;
        }
    }
}
