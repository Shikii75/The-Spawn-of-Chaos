using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

[RequireComponent(typeof(BoxCollider2D))]
public class LevelGoalTrigger : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Scene to load when player clicks 'NEXT LEVEL'.")]
    public string nextLevelSceneName = "SampleScene";

    [Tooltip("Scene to load when player clicks 'MAIN MENU'.")]
    public string mainMenuSceneName = "MainMenu";

    [Tooltip("Button text for proceeding to the next chapter.")]
    public string nextLevelButtonText = "JOURNEY TO CHERRY BLOSSOM FOREST";

    [Header("Nyxaris Level Complete Celebration")]
    [Tooltip("Dialogue spoken by Nyxaris when completing this level.")]
    [TextArea(2, 5)]
    public string victoryDialogueText = "Impressive, mortal! You've mastered the fundamentals and survived the fractured rift. Beyond lies the Cherry Blossom Forest—where the true trial begins!";

    [Tooltip("Expression animation for Nyxaris on level complete (e.g. excited, happy, confidently, proud).")]
    public string victoryAnimationKey = "excited";

    [Header("Audio Settings")]
    [Tooltip("Victory audio jingle to play when level is complete.")]
    public AudioClip victorySFX;

    private bool isTriggered = false;
    private AudioSource audioSource;
    private AudioClip speechChirpClip;

    void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        SynthesizeAudio();
    }

    private void SynthesizeAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        int rate = 44100;
        int chirpSamples = Mathf.FloorToInt(rate * 0.045f);
        float[] cSamples = new float[chirpSamples];
        for (int i = 0; i < chirpSamples; i++)
        {
            float t = (float)i / chirpSamples;
            float freq = Mathf.Lerp(540f, 400f, t);
            float env = Mathf.Sin(t * Mathf.PI);
            cSamples[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)rate)) * env * 0.4f;
        }
        speechChirpClip = AudioClip.Create("Nyxaris_VictoryChirp", chirpSamples, 1, rate, false);
        speechChirpClip.SetData(cSamples, 0);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        if (other.CompareTag("Player") || other.name.Contains("Player") || other.GetComponent<move>() != null)
        {
            TriggerVictory(other.gameObject);
        }
    }

    private void TriggerVictory(GameObject player)
    {
        isTriggered = true;
        Debug.Log("[LevelGoalTrigger] Goal reached! Level Complete with Nyxaris celebration.");

        // Disable player controls
        move playerMove = player.GetComponent<move>() ?? player.GetComponentInParent<move>();
        if (playerMove != null)
        {
            playerMove.enabled = false;
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>() ?? player.GetComponentInParent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Play victory sound
        if (victorySFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(victorySFX);
        }

        // Build celebratory Victory UI
        BuildVictoryUI();
    }

    // ══════════════════════════════════════════════════════════════════
    //  VICTORY UI WITH ANIMATED NYXARIS PORTRAIT & CELEBRATION SPEECH
    // ══════════════════════════════════════════════════════════════════

    private void BuildVictoryUI()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        StartCoroutine(VictoryInputRoutine());
        // ── Canvas (sort order 600 — above everything) ──
        Canvas canvas = UIFactory.CreateCanvas("VictoryCanvas", 600);

        // ── Full-screen dark celestial overlay ──
        RectTransform overlay = UIFactory.CreateFullScreenPanel(
            canvas.transform, "Overlay", new Color(0.04f, 0.01f, 0.08f, 0.92f)
        );

        // ── '★ LEVEL COMPLETE ★' Title ──
        TextMeshProUGUI titleText = UIFactory.CreateText(
            overlay, "TitleText", "★ LEVEL COMPLETE ★",
            46f, new Color(1f, 0.85f, 0.35f, 1f), TextAlignmentOptions.Center
        );
        UIFactory.SetRectFixed(titleText.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 190f), new Vector2(700f, 65f)
        );
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;

        // ── Subtitle ──
        TextMeshProUGUI subText = UIFactory.CreateText(
            overlay, "SubTitle", "Tutorial Chapter Cleared",
            20f, new Color(0.85f, 0.6f, 1f, 0.9f), TextAlignmentOptions.Center
        );
        UIFactory.SetRectFixed(subText.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 150f), new Vector2(500f, 35f)
        );

        // ── Nyxaris Dialogue Card (Glassmorphic Obsidian) ──
        GameObject cardGO = new GameObject("NyxarisDialogueCard");
        cardGO.transform.SetParent(overlay, false);
        RectTransform cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(800f, 180f);
        cardRT.anchoredPosition = new Vector2(0f, 20f);

        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = new Color(0.07f, 0.02f, 0.14f, 0.95f);

        Outline cardOutline = cardGO.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.85f, 0.35f, 1f, 0.95f);
        cardOutline.effectDistance = new Vector2(2.5f, -2.5f);

        // ── Nyxaris Animated Portrait ──
        GameObject portraitGO = new GameObject("NyxarisPortrait");
        portraitGO.transform.SetParent(cardGO.transform, false);
        RectTransform portraitRT = portraitGO.AddComponent<RectTransform>();
        portraitRT.sizeDelta = new Vector2(140f, 140f);
        portraitRT.anchoredPosition = new Vector2(-305f, 0f);

        Image portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.preserveAspect = true;
        portraitImg.color = Color.white;

        NyxarisFrameAnimator frameAnim = portraitGO.AddComponent<NyxarisFrameAnimator>();
        frameAnim.targetImage = portraitImg;
        frameAnim.PlayAnimation(victoryAnimationKey, 14f);

        Outline portOutline = portraitGO.AddComponent<Outline>();
        portOutline.effectColor = new Color(1f, 0.8f, 0.3f, 0.95f);
        portOutline.effectDistance = new Vector2(2f, -2f);

        // ── Nyxaris Name Label ──
        TextMeshProUGUI nameLabel = UIFactory.CreateText(
            cardRT, "NameLabel", "NYXARIS",
            20f, new Color(0.95f, 0.45f, 1f, 1f), TextAlignmentOptions.TopLeft
        );
        UIFactory.SetRectFixed(nameLabel.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(55f, 50f), new Vector2(520f, 30f)
        );
        nameLabel.fontStyle = FontStyles.Bold;

        // ── Dialogue Text with Typewriter Effect ──
        TextMeshProUGUI dialogueComp = UIFactory.CreateText(
            cardRT, "DialogueText", "",
            21f, Color.white, TextAlignmentOptions.TopLeft
        );
        UIFactory.SetRectFixed(dialogueComp.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(55f, -15f), new Vector2(520f, 95f)
        );
        dialogueComp.enableWordWrapping = true;

        StartCoroutine(TypewriterRoutine(dialogueComp, victoryDialogueText));

        // ── Button Container ──
        RectTransform btnContainer = UIFactory.CreatePanel(
            overlay, "ButtonContainer", Color.clear,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero
        );
        UIFactory.SetRectFixed(btnContainer,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -125f), new Vector2(760f, 60f)
        );
        UIFactory.AddHorizontalLayout(btnContainer.gameObject, 30f,
            new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

        // ── Next Level Button (loads SampleScene e.g. Cherry Blossom Forest) ──
        Button nextBtn = UIFactory.CreateButton(
            btnContainer, "NextLevelButton", nextLevelButtonText,
            18f, () => LoadNextLevel(nextLevelSceneName)
        );
        UIFactory.AddLayoutElement(nextBtn.gameObject, preferredHeight: 52f, preferredWidth: 400f);

        // ── Main Menu Button ──
        Button menuBtn = UIFactory.CreateButton(
            btnContainer, "MainMenuButton", "MAIN MENU",
            18f, () => QuitToMainMenu(mainMenuSceneName)
        );
        UIFactory.AddLayoutElement(menuBtn.gameObject, preferredHeight: 52f, preferredWidth: 220f);
    }

    private IEnumerator TypewriterRoutine(TextMeshProUGUI label, string text)
    {
        label.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            label.text += text[i];

            if (char.IsLetterOrDigit(text[i]) && audioSource != null && speechChirpClip != null && (i % 2 == 0))
            {
                audioSource.pitch = Random.Range(1.35f, 1.75f);
                audioSource.PlayOneShot(speechChirpClip, 0.45f);
            }

            yield return new WaitForSecondsRealtime(0.024f);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  SCENE NAVIGATION
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator VictoryInputRoutine()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E))
            {
                LoadNextLevel(nextLevelSceneName);
                yield break;
            }
            yield return null;
        }
    }

    public void LoadNextLevel(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            Time.timeScale = 1f;
            SpawnOfChaos.Systems.ArcaneLoadingScreen.LoadScene(sceneName);
        }
    }

    public void QuitToMainMenu(string menuScene)
    {
        if (!string.IsNullOrEmpty(menuScene))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuScene);
        }
    }
}
