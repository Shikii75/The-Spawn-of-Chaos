using TMPro;
using UnityEngine;

namespace SpawnOfChaos.Entities
{
    /// <summary>
    /// A designated sacred altar where The Drifter can voluntarily sever her life to anchor time.
    /// Features proximity prompt ("Press [E] to Anchor Time") and an anchor point for her severed head.
    /// </summary>
    public class DrifterAltar : MonoBehaviour
    {
        [Header("Anchor Transform")]
        [Tooltip("Exact spot where the head rests during saving and rewinding.")]
        public Transform anchorPoint;

        [Header("Interaction Settings")]
        public float interactionRadius = 2.8f;
        public KeyCode interactKey = KeyCode.E;

        private bool playerInRange = false;
        private Transform playerTransform;

        // Visual In-World Prompt
        private GameObject promptCanvasGO;
        private TextMeshProUGUI promptText;

        public Transform AnchorPoint => anchorPoint != null ? anchorPoint : transform;

        private void Awake()
        {
            if (anchorPoint == null)
            {
                GameObject ap = new GameObject("AnchorPoint");
                ap.transform.SetParent(transform);
                ap.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                anchorPoint = ap.transform;
            }

            BuildPromptUI();
        }

        private void BuildPromptUI()
        {
            promptCanvasGO = new GameObject("AltarPromptCanvas");
            promptCanvasGO.transform.SetParent(transform, false);
            promptCanvasGO.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            Canvas c = promptCanvasGO.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 45;

            RectTransform rt = promptCanvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 60);
            rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            GameObject textGO = new GameObject("PromptText", typeof(RectTransform));
            textGO.transform.SetParent(promptCanvasGO.transform, false);
            promptText = textGO.AddComponent<TextMeshProUGUI>();
            promptText.text = "[E] Sever Life & Anchor Time";
            promptText.fontSize = 24f;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = new Color(0.95f, 0.85f, 1f, 1f); // Mystic lavender-white glow

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            promptCanvasGO.SetActive(false);
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayer();
            }

            if (playerTransform == null) return;

            float dist = Vector2.Distance(transform.position, playerTransform.position);
            bool inRange = dist <= interactionRadius;

            if (inRange != playerInRange)
            {
                playerInRange = inRange;
                if (promptCanvasGO != null) promptCanvasGO.SetActive(playerInRange);
            }

            if (playerInRange)
            {
                // Trigger voluntary death on 'E' keypress
                if (Input.GetKeyDown(interactKey))
                {
                    TriggerSaveRitual();
                }
            }
        }

        public void TriggerSaveRitual()
        {
            if (Systems.DrifterSaveManager.Instance != null)
            {
                Systems.DrifterSaveManager.Instance.ExecuteVoluntaryDeathRitual(this);
            }
        }

        private void FindPlayer()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (p != null) playerTransform = p.transform;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
