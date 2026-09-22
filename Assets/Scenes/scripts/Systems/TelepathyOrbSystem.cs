using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// TelepathyOrbSystem - Manages the "Telepathy Orb" narrative mechanic.
    /// In this realm/timeline, human NPCs speak Japanese natively.
    /// Until the player discovers and claims the Telepathy Orb, their speech appears
    /// in authentic Japanese script. Once obtained, all spoken words are deciphered into English.
    /// </summary>
    public class TelepathyOrbSystem : MonoBehaviour
    {
        public static TelepathyOrbSystem Instance { get; private set; }

        private const string PREF_HAS_TELEPATHY_ORB = "SpawnOfChaos_HasTelepathyOrb";

        public static bool HasTelepathyOrb
        {
            get => PlayerPrefs.GetInt(PREF_HAS_TELEPATHY_ORB, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PREF_HAS_TELEPATHY_ORB, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [Header("Audio SFX")]
        public AudioClip acquisitionFanfare;

        // UI Banner for Acquisition
        private Canvas bannerCanvas;
        private CanvasGroup bannerGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI descText;

        // Pre-mapped English <-> Japanese lore dialogues
        private static readonly Dictionary<string, string> JapaneseDialogueMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "oi You cant understand them?",
                "おい… お前、連中の言葉が解らないのか？"
            },
            {
                "there is an orb that you seek to understand the language of those that reside in this universe in this specific timeline...this location",
                "この時空と地に生きる者たちの言葉を解するには、古の『感応の宝玉（テレパシーオーブ）』が必要だ… この地を探すがいい。"
            },
            {
                "Hello, stranger. What brings you here?",
                "見知らぬ旅人よ… 我らの社に何の用だ？"
            },
            {
                "Welcome to our sanctuary.",
                "我らの聖域へようこそ。静寂を乱すでないぞ。"
            },
            {
                "The shadow of chaos is stirring...",
                "闇の混沌が蠢いている… 己の刃を抜け！"
            },
            {
                "You shall not pass into the dojo without proving your worth.",
                "己の武を示さぬ者に、道場の門をくぐる資格などない。"
            },
            {
                "The cavern below is infested with dark spider horrors.",
                "地下洞窟は忌まわしき土蜘蛛どもの巣窟と化している。引き返すがよい。"
            },
            {
                "Greetings, traveler. Looking to buy or sell wares?",
                "いらっしゃい、旅のお方。商いかい？ 良い品が揃っているよ。"
            },
            {
                "Be careful on the mountain path, the ledges crumble easily.",
                "険しき山道には気をつけろ。足場が脆く、一歩誤れば奈落の底だ。"
            },
            {
                "I am the clan leader. Speak your mind or leave our grounds.",
                "我こそが笠衆の頭領。用があるなら申してみよ、さもなくば立ち去れ。"
            }
        };

        private static readonly string[] GenericJapanesePhrases = new string[]
        {
            "…何者だ？ この地を荒らすつもりか？",
            "言葉が通じぬようだな… 霊魂の宝玉を探すがいい。",
            "古き掟に従え。異邦人に語る言葉はない。",
            "風が騒がしい… 厄介な闖入者が現れたようだ。",
            "道場に近づくな。血気配を漂わせるな。"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildAcquisitionBannerUI();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public static void EnsureExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[TelepathyOrbSystem]");
                go.AddComponent<TelepathyOrbSystem>();
            }
        }

        /// <summary>
        /// Translates an NPC dialogue line based on Telepathy Orb acquisition state.
        /// If HasTelepathyOrb is true, returns the original English text.
        /// If false, converts to authentic Japanese script.
        /// </summary>
        public static string ProcessDialogueLine(string originalLine, bool isCompanion = false)
        {
            if (string.IsNullOrEmpty(originalLine)) return "";

            // Companions (Nyxaris, Lumi) always speak celestial English
            if (isCompanion || HasTelepathyOrb)
            {
                return originalLine;
            }

            string trimmed = originalLine.Trim();

            // Check dictionary exact / partial match
            foreach (var kvp in JapaneseDialogueMap)
            {
                if (trimmed.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return kvp.Value;
                }
            }

            // If line contains Japanese characters already, keep it
            foreach (char c in trimmed)
            {
                if ((c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FAF))
                {
                    return trimmed;
                }
            }

            // Fallback: pick a contextual stylized Japanese phrase based on line hash
            int index = Mathf.Abs(trimmed.GetHashCode()) % GenericJapanesePhrases.Length;
            return GenericJapanesePhrases[index];
        }

        /// <summary>
        /// Grants the player the Telepathy Orb and triggers the acquisition banner sequence.
        /// </summary>
        public void AcquireTelepathyOrb()
        {
            if (HasTelepathyOrb) return;

            HasTelepathyOrb = true;
            Debug.Log("<color=#D47BFF>[TelepathyOrbSystem] ✦ Telepathy Orb Acquired! All human languages unlocked.</color>");

            StartCoroutine(ShowAcquisitionBannerRoutine());
        }

        private void BuildAcquisitionBannerUI()
        {
            GameObject canvasGO = new GameObject("TelepathyBannerCanvas");
            canvasGO.transform.SetParent(transform, false);

            bannerCanvas = canvasGO.AddComponent<Canvas>();
            bannerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            bannerCanvas.sortingOrder = 999;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Modal Banner Container
            GameObject panelGO = new GameObject("BannerPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            RectTransform panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.72f);
            panelRT.anchorMax = new Vector2(0.5f, 0.72f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(980, 180);

            bannerGroup = panelGO.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;

            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.03f, 0.12f, 0.94f);

            // Glowing border
            Outline outline = panelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.45f, 1.0f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, 2.5f);

            // Title
            GameObject titleGO = new GameObject("BannerTitle");
            titleGO.transform.SetParent(panelGO.transform, false);
            RectTransform titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.5f);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.offsetMin = new Vector2(24, 0);
            titleRT.offsetMax = new Vector2(-24, -12);

            titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "✦ TELEPATHY ORB CLAIMED ✦";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(0.95f, 0.82f, 1.0f);

            // Description
            GameObject descGO = new GameObject("BannerDesc");
            descGO.transform.SetParent(panelGO.transform, false);
            RectTransform descRT = descGO.AddComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 0);
            descRT.anchorMax = new Vector2(1, 0.52f);
            descRT.offsetMin = new Vector2(24, 16);
            descRT.offsetMax = new Vector2(-24, 0);

            descText = descGO.AddComponent<TextMeshProUGUI>();
            descText.text = "Ancient mindwaves harmonize with your soul.\nThe native Japanese of clan warriors and villagers is now deciphered.";
            descText.alignment = TextAlignmentOptions.Center;
            descText.fontSize = 24;
            descText.color = new Color(0.88f, 0.82f, 0.96f);
        }

        private IEnumerator ShowAcquisitionBannerRoutine()
        {
            if (bannerGroup == null) yield break;

            // Fade In
            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                bannerGroup.alpha = Mathf.Clamp01(elapsed / 0.4f);
                yield return null;
            }
            bannerGroup.alpha = 1f;

            yield return new WaitForSecondsRealtime(3.6f);

            // Fade Out
            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                bannerGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.5f);
                yield return null;
            }
            bannerGroup.alpha = 0f;
        }
    }
}
