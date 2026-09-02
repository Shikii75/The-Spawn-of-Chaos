using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RevivalPromptUI - Cyber-Gothic Rewarded Ad Revival Prompt on Player Death:
/// Displays a 5-second decision modal:
/// - "WATCH VISION TO REVIVE ON THE SPOT" (Full HP + 3s Invulnerability + Shockwave)
/// - "ACCEPT DEFEAT" (Respawns at checkpoint)
/// </summary>
public class RevivalPromptUI : MonoBehaviour
{
    public static RevivalPromptUI Instance { get; private set; }

    private Canvas canvas;
    private GameObject modalPanel;
    private TextMeshProUGUI countdownText;
    private Coroutine countdownCoroutine;
    private Health targetPlayerHealth;
    private bool hasRevivedThisLife = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (Instance == null && FindFirstObjectByType<RevivalPromptUI>() == null)
        {
            GameObject go = new GameObject("RevivalPromptUI");
            Instance = go.AddComponent<RevivalPromptUI>();
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

        BuildUI();
    }

    public void PromptRevival(Health playerHealth, System.Action onDeclined)
    {
        targetPlayerHealth = playerHealth;

        // Limit to 1 revival per encounter so game balance is maintained
        if (hasRevivedThisLife)
        {
            onDeclined?.Invoke();
            return;
        }

        if (modalPanel == null) BuildUI();
        modalPanel.SetActive(true);

        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(RevivalCountdownRoutine(onDeclined));
    }

    private IEnumerator RevivalCountdownRoutine(System.Action onDeclined)
    {
        for (int i = 5; i > 0; i--)
        {
            if (countdownText != null)
            {
                countdownText.text = $"COMMUNING WITH THE VOID...\nDECIDE IN {i}s";
            }
            yield return new WaitForSecondsRealtime(1.0f);
        }

        // Timeout: player declined
        ClosePrompt();
        onDeclined?.Invoke();
    }

    private void OnAcceptClicked()
    {
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);

        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewardedRevival(
                onRevived: () => {
                    ExecuteRevival();
                },
                onDeclined: () => {
                    ClosePrompt();
                    targetPlayerHealth?.SendMessage("ExecuteStandardDeathRespawn", SendMessageOptions.DontRequireReceiver);
                }
            );
        }
        else
        {
            ExecuteRevival();
        }
    }

    private void OnDeclineClicked()
    {
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        ClosePrompt();
        targetPlayerHealth?.SendMessage("ExecuteStandardDeathRespawn", SendMessageOptions.DontRequireReceiver);
    }

    private void ExecuteRevival()
    {
        hasRevivedThisLife = true;
        ClosePrompt();

        if (targetPlayerHealth != null)
        {
            targetPlayerHealth.Resurrect();

            // Apply 3-second invulnerability
            move m = targetPlayerHealth.GetComponent<move>();
            if (m != null)
            {
                // Trigger shockwave blast pushing nearby enemies away
                Collider2D[] enemies = Physics2D.OverlapCircleAll(targetPlayerHealth.transform.position, 6f);
                foreach (var col in enemies)
                {
                    if (col != null && col.CompareTag("enemy"))
                    {
                        Rigidbody2D erb = col.GetComponent<Rigidbody2D>();
                        if (erb != null)
                        {
                            Vector2 blastDir = ((Vector2)col.transform.position - (Vector2)targetPlayerHealth.transform.position).normalized;
                            erb.linearVelocity = blastDir * 18f;
                        }
                    }
                }
            }

            Debug.Log("[RevivalPromptUI] Player successfully revived via Rewarded Ad with Full HP & Shockwave!");
        }
    }

    private void ClosePrompt()
    {
        if (modalPanel != null) modalPanel.SetActive(false);
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("RevivalPromptCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Backdrop
        modalPanel = new GameObject("RevivalModalPanel");
        modalPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform modalRT = modalPanel.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero;
        modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero;
        modalRT.offsetMax = Vector2.zero;
        Image bg = modalPanel.AddComponent<Image>();
        bg.color = new Color(0.01f, 0.02f, 0.05f, 0.92f);

        // Center card
        GameObject cardGO = new GameObject("Card");
        cardGO.transform.SetParent(modalPanel.transform, false);
        RectTransform cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(680, 440);
        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = new Color(0.05f, 0.04f, 0.12f, 0.98f);
        Outline outline = cardGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.05f, 0.45f, 0.85f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(cardGO.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchoredPosition = new Vector2(0, 140);
        titleRT.sizeDelta = new Vector2(620, 60);
        TextMeshProUGUI title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text = "FALLEN IN BATTLE";
        title.fontSize = 32;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(1f, 0.1f, 0.45f, 1f);

        // Subtitle / Countdown
        GameObject cdGO = new GameObject("Countdown");
        cdGO.transform.SetParent(cardGO.transform, false);
        RectTransform cdRT = cdGO.AddComponent<RectTransform>();
        cdRT.anchoredPosition = new Vector2(0, 50);
        cdRT.sizeDelta = new Vector2(600, 80);
        countdownText = cdGO.AddComponent<TextMeshProUGUI>();
        countdownText.text = "COMMUNING WITH THE VOID...\nDECIDE IN 5s";
        countdownText.fontSize = 20;
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.color = new Color(0f, 0.94f, 1f, 1f);

        // Revive Button
        GameObject reviveBtn = new GameObject("BtnRevive");
        reviveBtn.transform.SetParent(cardGO.transform, false);
        RectTransform revRT = reviveBtn.AddComponent<RectTransform>();
        revRT.anchoredPosition = new Vector2(0, -50);
        revRT.sizeDelta = new Vector2(480, 60);
        Image revImg = reviveBtn.AddComponent<Image>();
        revImg.color = new Color(0.05f, 0.55f, 0.45f, 0.95f);
        Outline revOutline = reviveBtn.AddComponent<Outline>();
        revOutline.effectColor = new Color(0.2f, 1f, 0.7f, 1f);
        Button btnR = reviveBtn.AddComponent<Button>();
        btnR.onClick.AddListener(OnAcceptClicked);

        GameObject revTxtGO = new GameObject("Label");
        revTxtGO.transform.SetParent(reviveBtn.transform, false);
        RectTransform revTxtRT = revTxtGO.AddComponent<RectTransform>();
        revTxtRT.anchorMin = Vector2.zero;
        revTxtRT.anchorMax = Vector2.one;
        revTxtRT.sizeDelta = Vector2.zero;
        TextMeshProUGUI revTxt = revTxtGO.AddComponent<TextMeshProUGUI>();
        revTxt.text = "⚡ WATCH VISION & REVIVE (FULL HP)";
        revTxt.fontSize = 18;
        revTxt.fontStyle = FontStyles.Bold;
        revTxt.alignment = TextAlignmentOptions.Center;
        revTxt.color = Color.white;

        // Decline Button
        GameObject decBtn = new GameObject("BtnDecline");
        decBtn.transform.SetParent(cardGO.transform, false);
        RectTransform decRT = decBtn.AddComponent<RectTransform>();
        decRT.anchoredPosition = new Vector2(0, -135);
        decRT.sizeDelta = new Vector2(300, 45);
        Image decImg = decBtn.AddComponent<Image>();
        decImg.color = new Color(0.12f, 0.08f, 0.16f, 0.9f);
        Button btnD = decBtn.AddComponent<Button>();
        btnD.onClick.AddListener(OnDeclineClicked);

        GameObject decTxtGO = new GameObject("Label");
        decTxtGO.transform.SetParent(decBtn.transform, false);
        RectTransform decTxtRT = decTxtGO.AddComponent<RectTransform>();
        decTxtRT.anchorMin = Vector2.zero;
        decTxtRT.anchorMax = Vector2.one;
        decTxtRT.sizeDelta = Vector2.zero;
        TextMeshProUGUI decTxt = decTxtGO.AddComponent<TextMeshProUGUI>();
        decTxt.text = "ACCEPT DEFEAT (RESPAWN)";
        decTxt.fontSize = 14;
        decTxt.alignment = TextAlignmentOptions.Center;
        decTxt.color = new Color(0.65f, 0.65f, 0.75f, 0.9f);

        modalPanel.SetActive(false);
    }
}
