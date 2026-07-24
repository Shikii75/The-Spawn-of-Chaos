using System;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;
using UnityEngine;

namespace SpawnOfChaos.Entities
{
    /// <summary>
    /// Collectible 2D World Orb item that drops in the environment or from enemies.
    /// Features magnetic attraction to player, procedural 2D glowing graphics, 
    /// pickup stat restores (Health, Mana, Currency, EP), and triggers real-time HUD liquid splash.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class CollectibleOrb : MonoBehaviour
    {
        public OrbType orbType = OrbType.Health;
        public int restoreAmount = 15;
        public AudioClip pickupSFX;

        [Header("Magnet Settings")]
        public float magnetRadius = 4.5f;
        public float moveSpeed = 8f;

        private Transform playerTransform;
        private SpriteRenderer spriteRenderer;
        private Vector3 basePosition;
        private float floatTimer;
        private bool isBeingCollected = false;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            EnsureProceduralSprite();
        }

        void Start()
        {
            basePosition = transform.position;
            floatTimer = UnityEngine.Random.Range(0f, 10f);
            FindPlayer();
        }

        void FindPlayer()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else if (PlayerCurrency.Instance != null) playerTransform = PlayerCurrency.Instance.transform;
        }

        void Update()
        {
            if (playerTransform == null)
            {
                FindPlayer();
            }

            // Gentle vertical floating bob
            floatTimer += Time.deltaTime * 3.5f;
            float bobY = Mathf.Sin(floatTimer) * 0.12f;

            if (playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, playerTransform.position);

                // Magnetic homing towards player when close
                if (dist <= magnetRadius)
                {
                    transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, moveSpeed * Time.deltaTime);
                    moveSpeed += Time.deltaTime * 12f; // accelerate towards player

                    if (dist < 0.6f && !isBeingCollected)
                    {
                        Collect(playerTransform.gameObject);
                    }
                    return;
                }
            }

            transform.position = basePosition + new Vector3(0f, bobY, 0f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (isBeingCollected) return;

            if (other.CompareTag("Player") || other.GetComponent<Health>() != null || other.GetComponent<PlayerCurrency>() != null)
            {
                Collect(other.gameObject);
            }
        }

        public void Collect(GameObject player)
        {
            if (isBeingCollected) return;
            isBeingCollected = true;

            // Apply stat restoration based on OrbType
            switch (orbType)
            {
                case OrbType.Health:
                    Health h = player.GetComponent<Health>();
                    if (h != null) h.Heal(restoreAmount);
                    break;

                case OrbType.Mana:
                    MageCombat mc = player.GetComponent<MageCombat>();
                    if (mc != null) mc.currentMana = Mathf.Min(mc.maxMana, mc.currentMana + restoreAmount);
                    break;

                case OrbType.Currency:
                    if (PlayerCurrency.Instance != null)
                    {
                        PlayerCurrency.Instance.AddCoins(restoreAmount);
                    }
                    break;

                case OrbType.EP:
                    if (PlayerLevelSystem.Instance != null)
                    {
                        PlayerLevelSystem.Instance.AddExperience(restoreAmount);
                    }
                    break;
            }

            // Trigger HUD Orb Splash in real-time
            if (HUDOrbPanel.Instance != null)
            {
                HUDOrbPanel.Instance.TriggerSplash(orbType, 1.4f);
            }

            // Play optional pickup sound
            if (pickupSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(pickupSFX);
            }

            Debug.Log($"[CollectibleOrb] Player collected {orbType} Orb (+{restoreAmount})!");
            Destroy(gameObject);
        }

        /// <summary>
        /// Generates a crisp, procedural glowing orb sprite if no sprite is assigned.
        /// </summary>
        private void EnsureProceduralSprite()
        {
            if (spriteRenderer.sprite != null) return;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32 pCol = GetOrbColor(orbType);
            Color32 hCol = new Color32(255, 255, 255, 240);

            float radius = size * 0.42f;
            float cx = size / 2f;
            float cy = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                    {
                        float norm = dist / radius;
                        Color32 c = Color32.Lerp(hCol, pCol, norm);
                        c.a = (byte)((1f - norm * norm) * 255);
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32);
        }

        public static Color32 GetOrbColor(OrbType type)
        {
            switch (type)
            {
                case OrbType.Health: return new Color32(240, 20, 60, 255);   // Ruby Crimson
                case OrbType.Mana: return new Color32(0, 160, 255, 255);     // Azure Sapphire
                case OrbType.Currency: return new Color32(255, 200, 20, 255); // Liquid Gold
                case OrbType.EP: return new Color32(180, 50, 240, 255);      // Cosmic Purple
                default: return new Color32(255, 255, 255, 255);
            }
        }
    }
}
