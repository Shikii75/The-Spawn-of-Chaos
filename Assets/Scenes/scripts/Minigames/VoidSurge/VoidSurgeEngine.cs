using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Software-rasterized 2D Neon Space Combat Shooter engine (Void Surge).
    /// Features neon aesthetics, combo multiplier, screen shake, nova bombs, and wave formations.
    /// </summary>
    public class VoidSurgeEngine : MonoBehaviour
    {
        [HideInInspector] public RawImage displayImage;
        [HideInInspector] public TextMeshProUGUI scoreText;
        [HideInInspector] public TextMeshProUGUI highScoreText;
        [HideInInspector] public GameObject gameOverPanel;
        [HideInInspector] public TextMeshProUGUI gameOverReasonText;
        [HideInInspector] public TextMeshProUGUI finalScoreText;
        [HideInInspector] public TextMeshProUGUI bestRecordText;
        [HideInInspector] public TextMeshProUGUI waveText;
        [HideInInspector] public TextMeshProUGUI waveReachedText;

        private const int W = 600;
        private const int H = 800;

        private Texture2D tex;
        private Color32[] px;

        private bool playing;
        private int score;
        private int best;
        private int waveNumber = 1;
        private float fireTimer = 0f;
        private int novaBombs = 1;
        private int shakeFrames = 0;
        private float shakeAmt = 0f;
        private float hitStopTimer = 0f;

        // Player Ship State
        private float vx = W / 2f;
        private float vy = H - 90f;
        private float recoilY = 0f;
        private float moveSpeed = 6.5f;
        private int weaponLevel = 1; // 1 = Single, 2 = Double, 3 = Triple Spread
        private bool shield;

        // Combo
        private float multiplier = 1.0f;
        private float comboTimer = 0f;
        private const float MAX_COMBO_TIMER = 3f;

        // Structs
        private class Bullet
        {
            public float x, y, vx, vy, w, h;
            public bool isEnemy;
            public byte r, g, b;
        }

        private class Enemy
        {
            public float x, y, w, h, vx, vy, hp, maxHp;
            public string type;
            public float animTick;
            public float fireCooldown;
            public int phase; // For boss
            public float phaseTimer; // For boss
        }

        private class Item
        {
            public float x, y, r;
            public string type;
            public float rot;
        }

        private class Particle
        {
            public float x, y, vx, vy, sz, life, maxLife;
            public byte r, g, b, a;
            public bool isRing;
        }

        private class Star
        {
            public float x, y, spd, sz, alpha;
            public byte r, g, b;
        }

        private readonly List<Bullet> bullets = new List<Bullet>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Item> items = new List<Item>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<Star> stars = new List<Star>();
        private float spawnTimer = 0f;

        public bool IsPlaying => playing;

        void Awake()
        {
            best = PlayerPrefs.GetInt("VoidSurge_HighScore", 0);
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            px = new Color32[W * H];
            if (displayImage != null) displayImage.texture = tex;

            for (int i = 0; i < 60; i++)
            {
                stars.Add(new Star
                {
                    x = Random.Range(0f, W),
                    y = Random.Range(0f, H),
                    spd = Random.Range(0.8f, 3.5f),
                    sz = Random.Range(0.8f, 2.5f),
                    alpha = Random.Range(0.2f, 0.8f),
                    r = (byte)Random.Range(180, 255),
                    g = (byte)Random.Range(180, 255),
                    b = (byte)Random.Range(200, 255)
                });
            }
        }

        public void StartNewGame()
        {
            if (displayImage != null && displayImage.texture == null)
                displayImage.texture = tex;

            score = 0;
            waveNumber = 1;
            novaBombs = 0; // The player must pick them up, or start with 1? User said "bomb (red): +1 nova bomb". Let's start with 1.
            novaBombs = 1; 
            weaponLevel = 1;
            shield = false;
            vx = W / 2f;
            vy = H - 90f;
            recoilY = 0f;
            fireTimer = 0f;
            hitStopTimer = 0f;
            shakeFrames = 0;
            multiplier = 1.0f;
            comboTimer = 0f;

            bullets.Clear();
            enemies.Clear();
            items.Clear();
            particles.Clear();

            spawnTimer = 1.0f;
            playing = true;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            UpdateHUD();
        }

        void Update()
        {
            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool fire = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.J) || Input.GetMouseButton(0);
            bool bombKey = Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.LeftShift);

            if (bombKey && playing) TriggerNovaBomb();
            if (!playing) { RenderFrame(); return; }

            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f) dt = 0.05f;

            if (hitStopTimer > 0f)
            {
                hitStopTimer -= dt;
                RenderFrame();
                return;
            }

            // Movement
            if (left) vx -= moveSpeed;
            if (right) vx += moveSpeed;
            if (up) vy -= moveSpeed;
            if (down) vy += moveSpeed;

            vx = Mathf.Clamp(vx, 20f, W - 20f);
            vy = Mathf.Clamp(vy, H / 2f, H - 30f);

            recoilY += (0f - recoilY) * 0.3f;

            // Engine trail particle
            if (Random.value < 0.6f)
            {
                particles.Add(new Particle
                {
                    x = vx + Random.Range(-4f, 4f),
                    y = vy + 12f,
                    vx = 0f,
                    vy = Random.Range(1f, 3f),
                    sz = Random.Range(2f, 4f),
                    life = 10,
                    maxLife = 10,
                    r = 0, g = 240, b = 255, a = 255, isRing = false
                });
            }

            // Combo decay
            if (comboTimer > 0f)
            {
                comboTimer -= dt;
                if (comboTimer <= 0f)
                {
                    multiplier = 1.0f;
                }
            }

            // Auto-fire
            fireTimer += dt;
            if (fire && fireTimer >= 0.12f)
            {
                fireTimer = 0f;
                ShootWeapon();
            }

            // Scroll stars
            foreach (var s in stars)
            {
                s.y += s.spd;
                if (s.y > H) { s.y = 0f; s.x = Random.Range(0f, W); }
            }

            // Spawning
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                SpawnWaveFormation();
                spawnTimer = Mathf.Max(2f, 5f - waveNumber * 0.1f);
            }

            // Bullets
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                b.x += b.vx;
                b.y += b.vy;

                if (b.y < -20f || b.y > H + 20f || b.x < -20f || b.x > W + 20f)
                {
                    bullets.RemoveAt(i);
                }
            }

            // Enemies
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                UpdateEnemy(e, dt);

                // Collision with player
                if (CheckAABB(vx - 12f, vy - 12f, 24f, 24f, e.x, e.y, e.w, e.h))
                {
                    if (shield)
                    {
                        shield = false;
                        TriggerScreenShake(15, 10f);
                        SpawnExplosion(vx, vy, 0, 240, 255, 30);
                        enemies.RemoveAt(i);
                        continue;
                    }
                    else
                    {
                        Die("Ship Destroyed by Enemy Contact!");
                        return;
                    }
                }

                // Check bullets vs enemy
                for (int j = bullets.Count - 1; j >= 0; j--)
                {
                    var b = bullets[j];
                    if (!b.isEnemy && CheckAABB(b.x, b.y, b.w, b.h, e.x, e.y, e.w, e.h))
                    {
                        e.hp -= 1f;
                        bullets.RemoveAt(j);
                        SpawnExplosion(b.x, b.y, 0, 240, 255, 5);

                        if (e.hp <= 0f)
                        {
                            KillEnemy(e, i);
                            break;
                        }
                    }
                }
                
                if (i < enemies.Count && e.y > H + 50f) enemies.RemoveAt(i);
            }

            // Enemy bullets vs player
            for (int j = bullets.Count - 1; j >= 0; j--)
            {
                var b = bullets[j];
                if (b.isEnemy && CheckAABB(vx - 6f, vy - 6f, 12f, 12f, b.x, b.y, b.w, b.h))
                {
                    bullets.RemoveAt(j);
                    if (shield)
                    {
                        shield = false;
                        TriggerScreenShake(10, 6f);
                        SpawnExplosion(vx, vy, 0, 240, 255, 20);
                    }
                    else
                    {
                        Die("Ship Destroyed by Enemy Fire!");
                        return;
                    }
                }
            }

            // Update Items
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                item.y += 2.0f;
                item.rot += dt * 3f;

                if (CheckAABB(vx - 16f, vy - 16f, 32f, 32f, item.x - item.r, item.y - item.r, item.r * 2f, item.r * 2f))
                {
                    if (item.type == "upgrade") weaponLevel = Mathf.Min(3, weaponLevel + 1);
                    else if (item.type == "shield") shield = true;
                    else if (item.type == "bomb") novaBombs++;

                    SpawnExplosion(item.x, item.y, 255, 255, 255, 15);
                    items.RemoveAt(i);
                }
                else if (item.y > H + 30f) items.RemoveAt(i);
            }

            // Update Particles
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.x += p.vx; p.y += p.vy; 
                p.life--;
                if (p.isRing) p.sz += 2f;
                else p.sz *= 0.95f;
                if (p.life <= 0) particles.RemoveAt(i);
            }

            if (shakeFrames > 0) shakeFrames--;

            UpdateHUD();
            RenderFrame();
        }
        
        private void UpdateEnemy(Enemy e, float dt)
        {
            e.x += e.vx;
            e.y += e.vy;
            e.animTick += dt;

            // Movement logic
            if (e.type == "drone")
            {
                e.x += Mathf.Sin(e.animTick * 3f) * 1.5f;
            }
            else if (e.type == "fighter")
            {
                if (e.animTick > 1f) { e.vx = -e.vx; e.animTick = 0f; }
                
                e.fireCooldown -= dt;
                if (e.fireCooldown <= 0f)
                {
                    float dx = vx - (e.x + e.w / 2f);
                    float dy = vy - (e.y + e.h / 2f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bullets.Add(new Bullet { x = e.x + e.w / 2f - 3f, y = e.y + e.h / 2f, vx = (dx / dist) * 4f, vy = (dy / dist) * 4f, w = 6f, h = 6f, isEnemy = true, r = 255, g = 102, b = 0 });
                    e.fireCooldown = 2f;
                }
            }
            else if (e.type == "heavy")
            {
                e.fireCooldown -= dt;
                if (e.fireCooldown <= 0f)
                {
                    bullets.Add(new Bullet { x = e.x + e.w / 2f - 4f, y = e.y + e.h + 5f, vx = 0f, vy = 5f, w = 8f, h = 12f, isEnemy = true, r = 139, g = 0, b = 255 });
                    e.fireCooldown = 1.5f;
                }
            }
            else if (e.type == "swarm")
            {
                float dx = vx - (e.x + e.w / 2f);
                float dy = vy - (e.y + e.h / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                e.vx = (dx / dist) * 3f;
                e.vy = (dy / dist) * 3f;
            }
            else if (e.type == "sniper")
            {
                if (e.x <= 30f || e.x + e.w >= W - 30f) e.vx = -e.vx;
                e.fireCooldown -= dt;
                if (e.fireCooldown <= 0f)
                {
                    float dx = vx - (e.x + e.w / 2f);
                    float dy = vy - (e.y + e.h / 2f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bullets.Add(new Bullet { x = e.x + e.w / 2f - 2f, y = e.y + e.h, vx = (dx / dist) * 8f, vy = (dy / dist) * 8f, w = 4f, h = 16f, isEnemy = true, r = 0, g = 255, b = 136 });
                    e.fireCooldown = 1.8f;
                }
            }
            else if (e.type == "boss")
            {
                if (e.x <= 40f || e.x + e.w >= W - 40f) e.vx = -e.vx;
                e.phaseTimer += dt;
                
                if (e.phaseTimer > 4f)
                {
                    e.phase = (e.phase + 1) % 3;
                    e.phaseTimer = 0f;
                }

                e.fireCooldown -= dt;
                if (e.fireCooldown <= 0f)
                {
                    float cx = e.x + e.w / 2f;
                    float cy = e.y + e.h;

                    if (e.phase == 0) // Aimed
                    {
                        float dx = vx - cx;
                        float dy = vy - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        bullets.Add(new Bullet { x = cx - 4f, y = cy, vx = (dx / dist) * 6f, vy = (dy / dist) * 6f, w = 8f, h = 16f, isEnemy = true, r = 255, g = 0, b = 170 });
                        e.fireCooldown = 0.8f;
                    }
                    else if (e.phase == 1) // 5-spread
                    {
                        for (int k = -2; k <= 2; k++)
                        {
                            bullets.Add(new Bullet { x = cx - 3f, y = cy, vx = k * 1.5f, vy = 5f, w = 6f, h = 6f, isEnemy = true, r = 255, g = 0, b = 170 });
                        }
                        e.fireCooldown = 1.2f;
                    }
                    else if (e.phase == 2) // Ring
                    {
                        for (int k = 0; k < 12; k++)
                        {
                            float ang = (Mathf.PI * 2f * k) / 12f;
                            bullets.Add(new Bullet { x = cx - 3f, y = cy, vx = Mathf.Cos(ang) * 4f, vy = Mathf.Sin(ang) * 4f, w = 6f, h = 6f, isEnemy = true, r = 255, g = 0, b = 170 });
                        }
                        e.fireCooldown = 1.5f;
                    }
                }
            }
            
            // Keep on screen if non-boss zigzags
            if (e.type != "swarm" && e.type != "drone" && e.type != "boss")
            {
                if (e.x <= 10f || e.x + e.w >= W - 10f) e.vx = -e.vx;
            }
        }

        private void KillEnemy(Enemy e, int index)
        {
            int pts = 50;
            if (e.type == "fighter") pts = 200;
            else if (e.type == "heavy") pts = 350;
            else if (e.type == "sniper") pts = 300;
            else if (e.type == "boss") pts = 2000 + waveNumber * 500;

            score += (int)(pts * multiplier);
            
            multiplier = Mathf.Min(10.0f, multiplier + 0.2f);
            comboTimer = MAX_COMBO_TIMER;

            if (e.type == "boss")
            {
                TriggerScreenShake(30, 15f);
                hitStopTimer = 0.1f;
                SpawnExplosion(e.x + e.w / 2f, e.y + e.h / 2f, 255, 0, 170, 60);
                SpawnShockwave(e.x + e.w / 2f, e.y + e.h / 2f, 255, 0, 170);
            }
            else
            {
                TriggerScreenShake(8, 4f);
                hitStopTimer = 0.04f;
                SpawnExplosion(e.x + e.w / 2f, e.y + e.h / 2f, 0, 240, 255, 20);
            }

            if (Random.value < 0.18f)
            {
                string it = Random.value < 0.5f ? "upgrade" : (Random.value < 0.8f ? "shield" : "bomb");
                items.Add(new Item { x = e.x + e.w / 2f, y = e.y + e.h / 2f, r = 12f, type = it, rot = 0f });
            }

            enemies.RemoveAt(index);
        }

        private void ShootWeapon()
        {
            recoilY = 4f; 

            if (weaponLevel == 1)
            {
                bullets.Add(new Bullet { x = vx - 3f, y = vy - 24f, vx = 0f, vy = -18f, w = 6f, h = 18f, isEnemy = false, r = 0, g = 240, b = 255 });
            }
            else if (weaponLevel == 2)
            {
                bullets.Add(new Bullet { x = vx - 12f, y = vy - 20f, vx = 0f, vy = -18f, w = 6f, h = 18f, isEnemy = false, r = 0, g = 240, b = 255 });
                bullets.Add(new Bullet { x = vx + 6f, y = vy - 20f, vx = 0f, vy = -18f, w = 6f, h = 18f, isEnemy = false, r = 0, g = 240, b = 255 });
            }
            else if (weaponLevel >= 3)
            {
                bullets.Add(new Bullet { x = vx - 3f, y = vy - 24f, vx = 0f, vy = -18f, w = 6f, h = 18f, isEnemy = false, r = 0, g = 240, b = 255 });
                bullets.Add(new Bullet { x = vx - 14f, y = vy - 20f, vx = -2.5f, vy = -17f, w = 6f, h = 18f, isEnemy = false, r = 0, g = 255, b = 136 });
                bullets.Add(new Bullet { x = vx + 8f, y = vy - 20f, vx = 2.5f, vy = -17f, w = 6f, h = 18f, isEnemy = false, r = 0, g = 255, b = 136 });
            }
        }

        private void TriggerNovaBomb()
        {
            if (novaBombs <= 0) return;
            novaBombs--;

            TriggerScreenShake(25, 15f);
            hitStopTimer = 0.1f;
            SpawnShockwave(W / 2f, H / 2f, 255, 0, 170);

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.type != "boss")
                {
                    KillEnemy(e, i);
                }
                else
                {
                    e.hp -= 50f;
                    if (e.hp <= 0f) KillEnemy(e, i);
                }
            }
            bullets.Clear();
        }

        private void SpawnWaveFormation()
        {
            if (waveNumber % 5 == 0)
            {
                // Boss wave
                enemies.Add(new Enemy { x = W / 2f - 30f, y = -60f, w = 60f, h = 48f, vx = 2f, vy = 0.5f, hp = 300f + waveNumber * 150f, maxHp = 300f + waveNumber * 150f, type = "boss", phase = 0, phaseTimer = 0f });
            }
            else
            {
                int count = Mathf.Min(30, 5 + waveNumber * 2);
                for (int i = 0; i < count; i++)
                {
                    string t = "drone";
                    if (waveNumber >= 3 && Random.value < 0.3f) t = "fighter";
                    if (waveNumber >= 4 && Random.value < 0.2f) t = "heavy";
                    if (waveNumber >= 6 && Random.value < 0.2f) t = "swarm";
                    if (waveNumber >= 8 && Random.value < 0.15f) t = "sniper";

                    float ex = Random.Range(40f, W - 40f);
                    float ey = -40f - Random.Range(0f, 200f);

                    if (t == "drone") enemies.Add(new Enemy { x = ex, y = ey, w = 18f, h = 18f, vx = 0f, vy = 2.5f, hp = 1f, maxHp = 1f, type = t });
                    else if (t == "fighter") enemies.Add(new Enemy { x = ex, y = ey, w = 22f, h = 24f, vx = 3f, vy = 2f, hp = 2f, maxHp = 2f, type = t });
                    else if (t == "heavy") enemies.Add(new Enemy { x = ex, y = ey, w = 30f, h = 30f, vx = 0f, vy = 1f, hp = 4f, maxHp = 4f, type = t });
                    else if (t == "swarm") enemies.Add(new Enemy { x = ex, y = ey, w = 12f, h = 14f, vx = 0f, vy = 0f, hp = 1f, maxHp = 1f, type = t });
                    else if (t == "sniper") enemies.Add(new Enemy { x = ex, y = ey, w = 20f, h = 26f, vx = 2.5f, vy = 1.5f, hp = 2f, maxHp = 2f, type = t });
                }
            }
            waveNumber++;
        }

        private bool CheckAABB(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
        {
            return x1 < x2 + w2 && x1 + w1 > x2 && y1 < y2 + h2 && y1 + h1 > y2;
        }

        private void Die(string reason)
        {
            playing = false;
            TriggerScreenShake(30, 20f);
            SpawnExplosion(vx, vy, 0, 240, 255, 50);

            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt("VoidSurge_HighScore", best);
                PlayerPrefs.Save();
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                if (gameOverReasonText != null) gameOverReasonText.text = reason;
                if (finalScoreText != null) finalScoreText.text = score.ToString();
                if (bestRecordText != null) bestRecordText.text = "BEST: " + best.ToString();
                if (waveReachedText != null) waveReachedText.text = "WAVE REACHED: " + waveNumber.ToString();
            }
        }

        private void UpdateHUD()
        {
            if (scoreText != null) scoreText.text = $"{score} (x{multiplier:F1})";
            if (highScoreText != null) highScoreText.text = best.ToString();
            if (waveText != null) waveText.text = waveNumber.ToString();
        }

        private void TriggerScreenShake(int frames, float amt)
        {
            shakeFrames = frames;
            shakeAmt = amt;
        }

        private void SpawnExplosion(float x, float y, byte r, byte g, byte b, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float spd = Random.Range(1f, 8f);
                particles.Add(new Particle
                {
                    x = x, y = y,
                    vx = Mathf.Cos(ang) * spd,
                    vy = Mathf.Sin(ang) * spd,
                    sz = Random.Range(3f, 8f),
                    life = Random.Range(10, 25),
                    maxLife = 25,
                    r = r, g = g, b = b, a = 255, isRing = false
                });
            }
        }

        private void SpawnShockwave(float x, float y, byte r, byte g, byte b)
        {
            particles.Add(new Particle
            {
                x = x, y = y, vx = 0, vy = 0, sz = 10f, life = 30, maxLife = 30, r = r, g = g, b = b, a = 255, isRing = true
            });
        }

        // ── Pixel Renderer (Neon Aesthetics) ─────────────────────────

        private void RenderFrame()
        {
            int ox = 0, oy = 0;
            if (shakeFrames > 0)
            {
                ox = Mathf.RoundToInt(Random.Range(-shakeAmt, shakeAmt));
                oy = Mathf.RoundToInt(Random.Range(-shakeAmt, shakeAmt));
            }

            // Dark Navy to Darker Gradient Background
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / H;
                byte br = (byte)Mathf.Lerp(10, 5, t);
                byte bg2 = (byte)Mathf.Lerp(10, 5, t);
                byte bb = (byte)Mathf.Lerp(32, 16, t);
                Color32 c = new Color32(br, bg2, bb, 255);
                int row = (H - 1 - y) * W;
                for (int x = 0; x < W; x++) px[row + x] = c;
            }

            // Stars
            foreach (var s in stars)
            {
                int sx = Mathf.Clamp((int)s.x + ox, 0, W - 1);
                int sy = Mathf.Clamp((int)s.y + oy, 0, H - 1);
                FillCircle(sx, sy, Mathf.RoundToInt(s.sz), s.r, s.g, s.b, (byte)(s.alpha * 255));
            }

            // Particles & Rings
            foreach (var p in particles)
            {
                float alpha = Mathf.Clamp01(p.life / p.maxLife);
                int px2 = (int)p.x + ox;
                int py2 = (int)p.y + oy;
                if (p.isRing)
                {
                    DrawCircleOutline(px2, py2, (int)p.sz, p.r, p.g, p.b, (byte)(alpha * 150));
                }
                else
                {
                    FillCircle(px2, py2, Mathf.Max(1, (int)p.sz), p.r, p.g, p.b, (byte)(alpha * p.a));
                }
            }

            // Enemies
            foreach (var e in enemies)
            {
                int ex = (int)e.x + ox;
                int ey = (int)e.y + oy;
                int ew = (int)e.w;
                int eh = (int)e.h;

                if (e.type == "drone") // Circle r=9
                {
                    FillCircle(ex + ew/2, ey + eh/2, 9, 10, 10, 20, 255);
                    DrawCircleOutline(ex + ew/2, ey + eh/2, 9, 0, 240, 255, 255);
                }
                else if (e.type == "fighter") // Diamond 22x24
                {
                    FillRect(ex, ey, ew, eh, 10, 10, 20, 255);
                    DrawOutlineRect(ex, ey, ew, eh, 255, 102, 0, 255);
                }
                else if (e.type == "heavy") // Square 30x30
                {
                    FillRect(ex, ey, ew, eh, 10, 10, 20, 255);
                    DrawOutlineRect(ex, ey, ew, eh, 139, 0, 255, 255);
                }
                else if (e.type == "swarm") // Tiny rect 12x14
                {
                    FillRect(ex, ey, ew, eh, 10, 10, 20, 255);
                    DrawOutlineRect(ex, ey, ew, eh, 255, 238, 0, 255);
                }
                else if (e.type == "sniper") // Tall rect 20x26
                {
                    FillRect(ex, ey, ew, eh, 10, 10, 20, 255);
                    DrawOutlineRect(ex, ey, ew, eh, 0, 255, 136, 255);
                }
                else if (e.type == "boss") // Large 60x48
                {
                    FillRect(ex, ey, ew, eh, 20, 0, 20, 255);
                    DrawOutlineRect(ex, ey, ew, eh, 255, 0, 170, 255);
                    FillRect(ex + 10, ey + 10, ew - 20, eh - 20, 255, 0, 170, 80); // inner glow
                    
                    // Health bar
                    float hpPct = e.hp / e.maxHp;
                    FillRect(ex, ey - 8, ew, 4, 50, 50, 50, 255);
                    FillRect(ex, ey - 8, (int)(ew * hpPct), 4, 255, 0, 170, 255);
                }
            }

            // Items
            foreach (var item in items)
            {
                int ix = (int)item.x + ox;
                int iy = (int)item.y + oy;
                int r = (int)item.r;
                
                byte cR = 255, cG = 255, cB = 255;
                if (item.type == "upgrade") { cR = 255; cG = 215; cB = 0; } // Gold
                else if (item.type == "shield") { cR = 0; cG = 240; cB = 255; } // Cyan
                else if (item.type == "bomb") { cR = 255; cG = 50; cB = 50; } // Red

                FillRect(ix - r/2, iy - r/2, r, r, 20, 20, 20, 255);
                DrawOutlineRect(ix - r/2, iy - r/2, r, r, cR, cG, cB, 255);
            }

            // Bullets
            foreach (var b in bullets)
            {
                int bx2 = Mathf.Clamp((int)b.x + ox, 0, W - 1);
                int by2 = Mathf.Clamp((int)b.y + oy, 0, H - 1);
                FillRect(bx2, by2, (int)b.w, (int)b.h, b.r, b.g, b.b, 255);
                // Simple glow
                FillRect(bx2 - 2, by2 - 2, (int)b.w + 4, (int)b.h + 4, b.r, b.g, b.b, 60);
            }

            // Player Ship
            if (playing)
            {
                int px2 = (int)vx - 12 + ox;
                int py2 = (int)(vy + recoilY) - 12 + oy;

                // Dark body with cyan outline
                FillRect(px2, py2, 24, 24, 10, 15, 25, 255);
                DrawOutlineRect(px2, py2, 24, 24, 0, 240, 255, 255);
                // Nose
                FillRect(px2 + 8, py2 - 8, 8, 8, 0, 240, 255, 255);

                if (shield)
                {
                    DrawCircleOutline((int)vx + ox, (int)(vy + recoilY) + oy, 26, 0, 240, 255, 200);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
        }

        // ── Primitives ─────────────────────────────────────────────

        private void FillRect(int rx, int ry, int rw, int rh, byte r, byte g, byte b, byte a)
        {
            if (a == 0) return;
            Color32 c = new Color32(r, g, b, a);
            for (int y = ry; y < ry + rh; y++)
            {
                if (y < 0 || y >= H) continue;
                int row = (H - 1 - y) * W;
                for (int x = rx; x < rx + rw; x++)
                {
                    if (x < 0 || x >= W) continue;
                    if (a == 255) px[row + x] = c;
                    else px[row + x] = Blend(px[row + x], c);
                }
            }
        }

        private void DrawOutlineRect(int rx, int ry, int rw, int rh, byte r, byte g, byte b, byte a)
        {
            FillRect(rx, ry, rw, 1, r, g, b, a);
            FillRect(rx, ry + rh - 1, rw, 1, r, g, b, a);
            FillRect(rx, ry, 1, rh, r, g, b, a);
            FillRect(rx + rw - 1, ry, 1, rh, r, g, b, a);
        }

        private void FillCircle(int cx, int cy, int rad, byte r, byte g, byte b, byte a)
        {
            if (a == 0 || rad <= 0) return;
            Color32 c = new Color32(r, g, b, a);
            for (int y = cy - rad; y <= cy + rad; y++)
            {
                if (y < 0 || y >= H) continue;
                int row = (H - 1 - y) * W;
                for (int x = cx - rad; x <= cx + rad; x++)
                {
                    if (x < 0 || x >= W) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= rad * rad)
                    {
                        if (a == 255) px[row + x] = c;
                        else px[row + x] = Blend(px[row + x], c);
                    }
                }
            }
        }

        private void DrawCircleOutline(int cx, int cy, int rad, byte r, byte g, byte b, byte a)
        {
            Color32 c = new Color32(r, g, b, a);
            int steps = Mathf.Max(24, rad * 5);
            for (int i = 0; i < steps; i++)
            {
                float ang = (Mathf.PI * 2f * i) / steps;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(ang) * rad);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(ang) * rad);
                if (x >= 0 && x < W && y >= 0 && y < H)
                    px[(H - 1 - y) * W + x] = Blend(px[(H - 1 - y) * W + x], c);
            }
        }

        private static Color32 Blend(Color32 bg, Color32 fg)
        {
            float fa = fg.a / 255f;
            float ba = 1f - fa;
            return new Color32(
                (byte)(fg.r * fa + bg.r * ba),
                (byte)(fg.g * fa + bg.g * ba),
                (byte)(fg.b * fa + bg.b * ba),
                255
            );
        }
    }
}
