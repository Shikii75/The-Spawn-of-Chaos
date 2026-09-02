using System;
using System.Collections;
using SpawnOfChaos.Minigames;
using SpawnOfChaos.Systems;
using UnityEngine;
using TMPro;

namespace SpawnOfChaos.Entities
{
    /// <summary>
    /// Collectible 2D World Orb item that drops in the environment or from enemies.
    /// Features:
    /// - Skybound-style floating bob and ambient sparkling trail.
    /// - Skybound-style explosive pickup burst: shockwave ring, radiant velocity shards, and floating text.
    /// - Distinct procedural 2D shapes (Heart, Arcane Tear, Diamond Coin, Cosmic Crystal) and scale variations.
    /// - Real-time stat restoration & HUD liquid splash synchronization.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class CollectibleOrb : MonoBehaviour
    {
        public OrbType orbType = OrbType.Currency;
        public int restoreAmount = 10;
        public AudioClip pickupSFX;

        [Header("Magnet Settings")]
        public float magnetRadius = 4.5f;
        public float moveSpeed = 8f;

        private Transform playerTransform;
        private SpriteRenderer spriteRenderer;
        private Vector3 basePosition;
        private float floatTimer;
        private float trailTimer;
        private bool isBeingCollected = false;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.45f;

            ApplyScaleByType();
            EnsureProceduralSprite();
        }

        void Start()
        {
            basePosition = transform.position;
            floatTimer = UnityEngine.Random.Range(0f, 10f);
            FindPlayer();
        }

        private void ApplyScaleByType()
        {
            switch (orbType)
            {
                case OrbType.Health:
                    transform.localScale = Vector3.one * 1.15f;
                    break;
                case OrbType.Currency:
                    transform.localScale = Vector3.one * 1.0f;
                    break;
                case OrbType.Mana:
                    transform.localScale = Vector3.one * 0.95f;
                    break;
                case OrbType.EP:
                    transform.localScale = Vector3.one * 0.85f;
                    break;
            }
        }

        void FindPlayer()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("Player");
            if (p == null) p = GameObject.Find("BasePlayer");
            if (p != null) playerTransform = p.transform;
            else if (PlayerCurrency.Instance != null) playerTransform = PlayerCurrency.Instance.transform;
        }

        void Update()
        {
            if (HUDManager.IsInMainMenu()) return;

            if (playerTransform == null)
            {
                FindPlayer();
            }

            // Skybound-style floating oscillation bob
            floatTimer += Time.deltaTime * 5f;
            float bobY = Mathf.Sin(floatTimer) * 0.16f;

            // Ambient sparkle trail tick
            trailTimer += Time.deltaTime;
            if (trailTimer >= 0.25f && !isBeingCollected)
            {
                trailTimer = 0f;
                SpawnAmbientTrailSpark();
            }

            if (playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, playerTransform.position);

                // Magnetic homing towards player when close
                if (dist <= magnetRadius)
                {
                    transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, moveSpeed * Time.deltaTime);
                    moveSpeed += Time.deltaTime * 14f; // rapid acceleration

                    if (dist < 0.6f && !isBeingCollected)
                    {
                        Collect(playerTransform.gameObject);
                    }
                    return;
                }
            }

            transform.position = basePosition + new Vector3(0f, bobY, 0f);
        }

        private void SpawnAmbientTrailSpark()
        {
            Color32 orbCol = GetOrbColor(orbType);
            GameObject spark = new GameObject("TrailSpark");
            spark.transform.position = transform.position + new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), UnityEngine.Random.Range(-0.15f, 0.15f), 0f);
            spark.transform.localScale = Vector3.one * 0.12f;

            SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = spriteRenderer.sprite;
            sr.color = new Color(orbCol.r / 255f, orbCol.g / 255f, orbCol.b / 255f, 0.65f);
            sr.sortingOrder = 5;

            StartCoroutine(FadeAndDestroy(spark, 0.35f));
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (HUDManager.IsInMainMenu()) return;
            if (isBeingCollected) return;

            if (other.CompareTag("Player") || other.name.Contains("Player") || other.GetComponent<Health>() != null || other.GetComponent<PlayerCurrency>() != null)
            {
                Collect(other.gameObject);
            }
        }

        public void Collect(GameObject player)
        {
            if (isBeingCollected) return;
            isBeingCollected = true;

            string rewardText = "";
            Color textColor = Color.white;

            // Apply stat restoration based on OrbType
            switch (orbType)
            {
                case OrbType.Health:
                    Health h = player.GetComponent<Health>();
                    if (h != null) h.Heal(restoreAmount);
                    rewardText = $"+{restoreAmount} HP!";
                    textColor = new Color(1f, 0.2f, 0.3f, 1f);
                    break;

                case OrbType.Mana:
                    MageCombat mc = player.GetComponent<MageCombat>();
                    if (mc != null) mc.currentMana = Mathf.Min(mc.maxMana, mc.currentMana + restoreAmount);
                    rewardText = $"+{restoreAmount} MANA!";
                    textColor = new Color(0f, 0.85f, 1f, 1f);
                    break;

                case OrbType.Currency:
                    if (PlayerCurrency.Instance != null)
                    {
                        PlayerCurrency.Instance.AddCoins(restoreAmount);
                    }
                    rewardText = $"+{restoreAmount} COINS!";
                    textColor = new Color(1f, 0.15f, 0.25f, 1f);
                    break;

                case OrbType.EP:
                    if (PlayerLevelSystem.Instance != null)
                    {
                        PlayerLevelSystem.Instance.AddExperience(restoreAmount);
                    }
                    rewardText = $"+{restoreAmount} EXP!";
                    textColor = new Color(0.85f, 0.35f, 1f, 1f);
                    break;
            }

            // Skybound-style explosive shockwave & velocity shard burst
            SpawnSkyboundExplosion();

            // Floating reward popup text
            SpawnFloatingRewardText(rewardText, textColor);

            // Trigger HUD Orb Splash in real-time
            if (HUDOrbPanel.Instance != null)
            {
                HUDOrbPanel.Instance.TriggerSplash(orbType, 1.8f);
            }

            // Play pickup sound
            if (pickupSFX != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(pickupSFX);
            }

            Debug.Log($"[CollectibleOrb] Collected {orbType} Orb ({rewardText})!");

            // Hide visual immediately while particles finish, then destroy
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.enabled = false;

            Destroy(gameObject, 0.8f);
        }

        private void SpawnSkyboundExplosion()
        {
            Vector3 pos = transform.position;
            Color32 pCol = GetOrbColor(orbType);

            // 1. Concentric shockwave ring
            GameObject ring = new GameObject("ShockwaveRing");
            ring.transform.position = pos;
            ring.transform.localScale = Vector3.one * 0.2f;
            SpriteRenderer ringSR = ring.AddComponent<SpriteRenderer>();
            ringSR.sprite = spriteRenderer.sprite;
            ringSR.color = new Color(pCol.r / 255f, pCol.g / 255f, pCol.b / 255f, 0.9f);
            ringSR.sortingOrder = 10;
            StartCoroutine(AnimateShockwaveRing(ring, 0.45f));

            // 2. Radiant velocity shards (14 particles)
            for (int i = 0; i < 14; i++)
            {
                float angle = (i / 14f) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                float speed = UnityEngine.Random.Range(3.5f, 8.5f);
                Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

                GameObject shard = new GameObject("Shard");
                shard.transform.position = pos;
                shard.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.10f, 0.18f);

                SpriteRenderer shardSR = shard.AddComponent<SpriteRenderer>();
                shardSR.sprite = spriteRenderer.sprite;
                shardSR.color = new Color(pCol.r / 255f, pCol.g / 255f, pCol.b / 255f, 1f);
                shardSR.sortingOrder = 11;

                StartCoroutine(AnimateShard(shard, vel, UnityEngine.Random.Range(0.35f, 0.6f)));
            }
        }

        private IEnumerator AnimateShockwaveRing(GameObject ring, float duration)
        {
            SpriteRenderer sr = ring.GetComponent<SpriteRenderer>();
            Color startColor = sr.color;
            Vector3 startScale = Vector3.one * 0.2f;
            Vector3 targetScale = Vector3.one * 1.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                ring.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, (1f - t) * startColor.a);
                yield return null;
            }

            Destroy(ring);
        }

        private IEnumerator AnimateShard(GameObject shard, Vector2 velocity, float lifetime)
        {
            SpriteRenderer sr = shard.GetComponent<SpriteRenderer>();
            Color startColor = sr.color;
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                shard.transform.position += (Vector3)velocity * Time.deltaTime;
                velocity *= 0.92f; // drag friction
                velocity.y -= 4f * Time.deltaTime; // slight gravity

                sr.color = new Color(startColor.r, startColor.g, startColor.b, (1f - t));
                yield return null;
            }

            Destroy(shard);
        }

        private void SpawnFloatingRewardText(string text, Color textColor)
        {
            GameObject txtGO = new GameObject("RewardText");
            txtGO.transform.position = transform.position + Vector3.up * 0.4f;

            TextMeshPro tmp = txtGO.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 4.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.sortingOrder = 20;

            StartCoroutine(AnimateFloatingText(txtGO, 0.7f));
        }

        private IEnumerator AnimateFloatingText(GameObject txtGO, float duration)
        {
            TextMeshPro tmp = txtGO.GetComponent<TextMeshPro>();
            Color startCol = tmp.color;
            Vector3 startPos = txtGO.transform.position;
            Vector3 endPos = startPos + Vector3.up * 1.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                txtGO.transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                tmp.color = new Color(startCol.r, startCol.g, startCol.b, 1f - (t * t));
                yield return null;
            }

            Destroy(txtGO);
        }

        private IEnumerator FadeAndDestroy(GameObject target, float duration)
        {
            SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
            Color c = sr.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                sr.color = new Color(c.r, c.g, c.b, (1f - t) * c.a);
                yield return null;
            }

            Destroy(target);
        }

        /// <summary>
        /// Generates distinct procedural glowing shapes (Heart, Arcane Tear, Diamond Coin, Cosmic Crystal).
        /// </summary>
        private void EnsureProceduralSprite()
        {
            if (spriteRenderer.sprite != null) return;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32 pCol = GetOrbColor(orbType);
            Color32 hCol = new Color32(255, 255, 255, 250);

            float radius = size * 0.40f;
            float cx = size / 2f;
            float cy = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    bool inside = false;
                    float shapeRatio = 1f;

                    switch (orbType)
                    {
                        case OrbType.Currency:
                            // Red Diamond Coin Shape (Rhombus facets)
                            float dDiam = (Mathf.Abs(dx) / (radius * 0.85f)) + (Mathf.Abs(dy) / (radius * 1.15f));
                            if (dDiam <= 1f)
                            {
                                inside = true;
                                shapeRatio = dDiam;
                            }
                            break;

                        case OrbType.Health:
                            // Obsidian Crimson Heart Shape
                            float nx = dx / (radius * 0.95f);
                            float ny = (dy / (radius * 0.95f)) * 1.15f - 0.1f;
                            float term = nx * nx + ny * ny - 1f;
                            if (term * term * term - (nx * nx * ny * ny * ny) <= 0f)
                            {
                                inside = true;
                                shapeRatio = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / radius);
                            }
                            break;

                        case OrbType.Mana:
                            // Arcane Teardrop with Radiant Rays
                            float distM = Mathf.Sqrt(dx * dx + dy * dy);
                            if (distM <= radius * 0.85f || (Mathf.Abs(dx) < 2f && Mathf.Abs(dy) < radius) || (Mathf.Abs(dy) < 2f && Mathf.Abs(dx) < radius))
                            {
                                inside = true;
                                shapeRatio = distM / radius;
                            }
                            break;

                        case OrbType.EP:
                            // Cosmic 8-Pointed Crystal Star
                            float distEP = Mathf.Sqrt(dx * dx + dy * dy);
                            float ang = Mathf.Atan2(dy, dx);
                            float ray = Mathf.Abs(Mathf.Cos(ang * 4f));
                            if (distEP <= radius * (0.45f + ray * 0.55f))
                            {
                                inside = true;
                                shapeRatio = distEP / radius;
                            }
                            break;
                    }

                    if (inside)
                    {
                        Color32 c = Color32.Lerp(hCol, pCol, shapeRatio);
                        c.a = (byte)((1f - shapeRatio * 0.6f) * 255);
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
                case OrbType.Health: return new Color32(235, 25, 45, 255);   // Crimson Heart
                case OrbType.Mana: return new Color32(0, 190, 255, 255);     // Electric Neon Azure
                case OrbType.Currency: return new Color32(220, 20, 35, 255); // Rich Dark Blood Red Coin
                case OrbType.EP: return new Color32(175, 45, 245, 255);      // Cosmic Purple Crystal
                default: return new Color32(255, 255, 255, 255);
            }
        }
    }
}
