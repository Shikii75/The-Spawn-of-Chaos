using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Reusable on-screen touch button for arcade minigames.
    /// Supports hold-to-move, tap-to-jump/shoot, and tactile scale punch.
    /// </summary>
    public class ArcadeTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private System.Action<bool> holdCallback;
        private System.Action tapCallback;
        private bool isHeld = false;
        private RectTransform rectTransform;

        public void Init(System.Action<bool> onHold, System.Action onTap = null)
        {
            holdCallback = onHold;
            tapCallback = onTap;
            rectTransform = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isHeld = true;
            holdCallback?.Invoke(true);
            tapCallback?.Invoke();
            transform.localScale = Vector3.one * 0.90f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Keeps pointer capture locked on mobile touchscreens
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isHeld = false;
            holdCallback?.Invoke(false);
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (isHeld)
            {
                holdCallback?.Invoke(true);
            }
        }

        public static GameObject Create(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, Color accentColor, System.Action<bool> onHold, System.Action onTap = null)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.18f, 0.88f);
            img.raycastTarget = true;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = accentColor;
            outline.effectDistance = new Vector2(2f, 2f);

            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            ArcadeTouchButton atb = btnObj.AddComponent<ArcadeTouchButton>();
            atb.Init(onHold, onTap);

            return btnObj;
        }
    }
}
