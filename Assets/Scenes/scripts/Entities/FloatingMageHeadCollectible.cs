using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpawnOfChaos.Entities
{
    /// <summary>
    /// FloatingMageHeadCollectible - Animated floating head relic.
    /// Plays the 48-frame floating mage head sequence with smooth vertical bobbing,
    /// particle glow, and contact detection.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class FloatingMageHeadCollectible : MonoBehaviour
    {
        public static FloatingMageHeadCollectible Instance { get; private set; }

        public event Action OnCollected;

        [Header("Animation Settings")]
        public float fps = 18f;
        public float bobSpeed = 2.4f;
        public float bobHeight = 0.28f;

        private SpriteRenderer spriteRenderer;
        private CircleCollider2D circleCol;
        private Sprite[] frames;
        private int currentFrame = 0;
        private float frameTimer = 0f;
        private Vector3 basePosition;
        private bool isCollected = false;

        private void Awake()
        {
            Instance = this;
            spriteRenderer = GetComponent<SpriteRenderer>();
            circleCol = GetComponent<CircleCollider2D>();

            circleCol.isTrigger = true;
            if (circleCol.radius <= 0.01f)
            {
                circleCol.radius = 1.1f;
            }

            if (string.IsNullOrEmpty(spriteRenderer.sortingLayerName))
            {
                spriteRenderer.sortingLayerName = "Default";
            }
            if (spriteRenderer.sortingOrder == 0)
            {
                spriteRenderer.sortingOrder = 60; // Render cleanly in front of world environment
            }

            basePosition = transform.position;
            LoadFrames();
        }

        public void SetBasePosition(Vector3 pos)
        {
            basePosition = pos;
            transform.position = pos;
        }

        private void LoadFrames()
        {
            frames = Resources.LoadAll<Sprite>("FloatingMageHead");
            if (frames == null || frames.Length == 0)
            {
#if UNITY_EDITOR
                string folder = "Assets/Scenes/animations/frames/floatingmagehead-1-360823ba";
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folder });
                List<Sprite> list = new List<Sprite>();
                foreach (var g in guids)
                {
                    string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (s != null) list.Add(s);
                }
                list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                frames = list.ToArray();
#endif
            }

            if (frames != null && frames.Length > 0)
            {
                List<Sprite> cleanList = new List<Sprite>();
                foreach (var s in frames)
                {
                    if (s == null) continue;
                    if (s.name.EndsWith("_1") || s.name.EndsWith("_2") || s.name.EndsWith("_3")) continue;
                    cleanList.Add(s);
                }
                cleanList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                frames = cleanList.ToArray();

                if (frames.Length > 0)
                {
                    spriteRenderer.sprite = frames[0];
                }
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // 1. Frame Animation
            if (frames != null && frames.Length > 0)
            {
                frameTimer += Time.deltaTime;
                if (frameTimer >= (1f / fps))
                {
                    frameTimer = 0f;
                    currentFrame = (currentFrame + 1) % frames.Length;
                    spriteRenderer.sprite = frames[currentFrame];
                }
            }

            // 2. Vertical Sine-wave Bobbing
            float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(basePosition.x, basePosition.y + yOffset, basePosition.z);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;
            if (other.CompareTag("Player") || other.name.Contains("Player") || other.name.Contains("BasePlayer"))
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            Debug.Log("<color=#D47BFF>[FloatingMageHeadCollectible] Player touched Floating Mage Head! Triggering collection...</color>");
            OnCollected?.Invoke();

            // Small scale pop & vanish
            Destroy(gameObject);
        }
    }
}
