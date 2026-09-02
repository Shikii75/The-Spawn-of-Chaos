using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Software-rasterized 2D endless dino-runner engine (Shadow Runner).
    /// Renders at 800x400 resolution to Texture2D. Includes running legs animation,
    /// jump & double-jump, ducking/crouching, ground spikes, flying bats,
    /// mana crystal collectibles, particle bursts, and persistent high scores.
    /// </summary>
    public class ShadowRunnerEngine : MonoBehaviour
    {
        public static bool virtualJump = false;
        public static bool virtualDuck = false;
        [HideInInspector] public RawImage displayImage;
        [HideInInspector] public TextMeshProUGUI distanceText;
        [HideInInspector] public TextMeshProUGUI highScoreText;
        [HideInInspector] public GameObject gameOverPanel;
        [HideInInspector] public TextMeshProUGUI gameOverReasonText;
        [HideInInspector] public TextMeshProUGUI finalDistanceText;
        [HideInInspector] public TextMeshProUGUI bestRecordText;

        private const int W = 800;
        private const int H = 400;
        private const float GROUND_Y = 320f;

        private Texture2D tex;
        private Color32[] px;

        private bool playing;
        private float distance;
        private float best;
        private float baseSpeed = 5.5f;
        private float curSpeed;
        private int tick;

        // Player physics
        private float pxPos = 90f;
        private float pyPos = GROUND_Y;
        private float pvy = 0f;
        private const float GRAV = 0.65f;
        private const float JUMP_FORCE = -12.5f;
        private const float DBL_JUMP_FORCE = -11.0f;
        private bool grounded;
        private bool canDoubleJump;
        private bool isDucking;
        private bool shield;

        // Player dimensions
        private float pStandW = 32f, pStandH = 48f;
        private float pDuckW = 48f, pDuckH = 24f;

        // Data Structs
        private class Obstacle
        {
            public float x, y, w, h;
            public string type; // "spike", "bat", "doublespike"
            public float animFrame;
        }

        private class Item
        {
            public float x, y, r;
            public string type; // "gem", "shield"
            public float osc;
        }

        private class Particle
        {
            public float x, y, vx, vy, sz, life, maxLife;
            public byte r, g, b;
        }

        private class Star
        {
            public float x, y, speed, size;
        }

        private readonly List<Obstacle> obstacles = new List<Obstacle>();
        private readonly List<Item> items = new List<Item>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<Star> stars = new List<Star>();
        private float nextSpawnDist = 0f;

        public bool IsPlaying => playing;

        void Awake()
        {
            best = PlayerPrefs.GetFloat("ShadowRunner_HighScore", 0f);
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            px = new Color32[W * H];
            if (displayImage != null) displayImage.texture = tex;

            for (int i = 0; i < 35; i++)
            {
                stars.Add(new Star
                {
                    x = Random.Range(0f, W),
                    y = Random.Range(10f, GROUND_Y - 40f),
                    speed = Random.Range(0.4f, 1.5f),
                    size = Random.Range(1f, 2.5f)
                });
            }
        }

        public void StartNewGame()
        {
            if (displayImage != null && displayImage.texture == null)
                displayImage.texture = tex;

            distance = 0f;
            curSpeed = baseSpeed;
            tick = 0;
            shield = false;
            isDucking = false;
            grounded = true;
            canDoubleJump = true;
            pyPos = GROUND_Y;
            pvy = 0f;

            obstacles.Clear();
            items.Clear();
            particles.Clear();

            nextSpawnDist = 40f;
            playing = true;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            UpdateHUD();
        }

        void Update()
        {
            bool jumpKey = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space) || virtualJump;
            virtualJump = false;
            bool duckKey = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || virtualDuck;

            if (jumpKey && playing) HandleJump();
            if (!playing) { RenderFrame(); return; }

            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f) dt = 0.05f;

            tick++;
            distance += curSpeed * dt * 3.5f;
            curSpeed = baseSpeed + Mathf.Min(distance * 0.008f, 6.0f);

            // Ducking state
            isDucking = duckKey && grounded;

            // Player gravity & ground collision
            pvy += GRAV;
            pyPos += pvy;

            float curH = isDucking ? pDuckH : pStandH;
            if (pyPos >= GROUND_Y)
            {
                pyPos = GROUND_Y;
                pvy = 0f;
                if (!grounded)
                {
                    grounded = true;
                    canDoubleJump = true;
                    SpawnDust(pxPos + pStandW / 2f, GROUND_Y, 8);
                }
            }

            // Move stars
            foreach (var s in stars)
            {
                s.x -= s.speed * (curSpeed / baseSpeed);
                if (s.x < 0) { s.x = W; s.y = Random.Range(10f, GROUND_Y - 40f); }
            }

            // Spawn obstacles & items
            if (distance >= nextSpawnDist)
            {
                SpawnObstacleOrItem();
                nextSpawnDist = distance + Random.Range(35f, 65f);
            }

            // Move & check obstacles
            float pw = isDucking ? pDuckW : pStandW;
            float ph = curH;
            float px1 = pxPos;
            float py1 = pyPos - ph;

            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var o = obstacles[i];
                o.x -= curSpeed;
                o.animFrame += dt * 8f;

                // AABB Collision check
                if (CheckAABB(px1, py1, pw, ph, o.x, o.y, o.w, o.h))
                {
                    if (shield)
                    {
                        shield = false;
                        SpawnExplosion(o.x + o.w / 2f, o.y + o.h / 2f, 56, 189, 248);
                        obstacles.RemoveAt(i);
                        continue;
                    }
                    else
                    {
                        Die(o.type == "bat" ? "Struck by Flying Bat!" : "Impaled by Ground Spikes!");
                        return;
                    }
                }

                if (o.x + o.w < -20f) obstacles.RemoveAt(i);
            }

            // Move & check items
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                item.x -= curSpeed;
                item.osc = Mathf.Sin(Time.unscaledTime * 6f) * 5f;
                float iy = item.y + item.osc;

                if (CheckAABB(px1, py1, pw, ph, item.x - item.r, iy - item.r, item.r * 2f, item.r * 2f))
                {
                    if (item.type == "gem")
                    {
                        distance += 15f; // Bonus meters
                        SpawnExplosion(item.x, iy, 0, 255, 204);
                    }
                    else if (item.type == "shield")
                    {
                        shield = true;
                        SpawnExplosion(item.x, iy, 56, 189, 248);
                    }
                    items.RemoveAt(i);
                }
                else if (item.x < -20f) items.RemoveAt(i);
            }

            // Update particles
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.x += p.vx; p.y += p.vy; p.vy += 0.2f; p.life--;
                if (p.life <= 0) particles.RemoveAt(i);
            }

            UpdateHUD();
            RenderFrame();
        }

        private void HandleJump()
        {
            if (grounded)
            {
                pvy = JUMP_FORCE;
                grounded = false;
                SpawnDust(pxPos + pStandW / 2f, GROUND_Y, 6);
            }
            else if (canDoubleJump)
            {
                pvy = DBL_JUMP_FORCE;
                canDoubleJump = false;
                SpawnDust(pxPos + pStandW / 2f, pyPos, 6);
            }
        }

        private void SpawnObstacleOrItem()
        {
            float r = Random.value;
            if (r < 0.45f)
            {
                // Ground Spikes
                obstacles.Add(new Obstacle { x = W + 20f, y = GROUND_Y - 32f, w = 26f, h = 32f, type = "spike" });
            }
            else if (r < 0.70f)
            {
                // Flying Bat (must duck under)
                obstacles.Add(new Obstacle { x = W + 20f, y = GROUND_Y - 56f, w = 34f, h = 22f, type = "bat" });
            }
            else if (r < 0.85f)
            {
                // Double Spike Cluster
                obstacles.Add(new Obstacle { x = W + 20f, y = GROUND_Y - 32f, w = 50f, h = 32f, type = "doublespike" });
            }
            else
            {
                // Mana Gem or Shield
                string it = Random.value < 0.75f ? "gem" : "shield";
                float iy = Random.value < 0.5f ? GROUND_Y - 30f : GROUND_Y - 75f;
                items.Add(new Item { x = W + 20f, y = iy, r = 10f, type = it });
            }
        }

        private bool CheckAABB(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
        {
            return x1 < x2 + w2 && x1 + w1 > x2 && y1 < y2 + h2 && y1 + h1 > y2;
        }

        private void Die(string reason)
        {
            playing = false;
            SpawnExplosion(pxPos + pStandW / 2f, pyPos - pStandH / 2f, 255, 0, 85);

            if (distance > best)
            {
                best = distance;
                PlayerPrefs.SetFloat("ShadowRunner_HighScore", best);
                PlayerPrefs.Save();
            }
            if (AdManager.Instance != null) { AdManager.Instance.RecordMinigameLoss(); }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                if (gameOverReasonText != null) gameOverReasonText.text = reason;
                if (finalDistanceText != null) finalDistanceText.text = Mathf.FloorToInt(distance) + "m";
                if (bestRecordText != null) bestRecordText.text = Mathf.FloorToInt(best) + "m";
            }
        }

        private void UpdateHUD()
        {
            if (distanceText != null) distanceText.text = Mathf.FloorToInt(distance) + "m";
            if (highScoreText != null) highScoreText.text = Mathf.FloorToInt(best) + "m";
        }

        private void SpawnDust(float x, float y, int count)
        {
            for (int i = 0; i < count; i++)
            {
                particles.Add(new Particle
                {
                    x = x + Random.Range(-10f, 10f),
                    y = y,
                    vx = Random.Range(-2f, 0.5f),
                    vy = -Random.Range(0.5f, 2f),
                    sz = Random.Range(2f, 5f),
                    life = 15,
                    maxLife = 15,
                    r = 180, g = 180, b = 200
                });
            }
        }

        private void SpawnExplosion(float x, float y, byte r, byte g, byte b)
        {
            for (int i = 0; i < 25; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float spd = Random.Range(2f, 8f);
                particles.Add(new Particle
                {
                    x = x, y = y,
                    vx = Mathf.Cos(ang) * spd,
                    vy = Mathf.Sin(ang) * spd,
                    sz = Random.Range(3f, 8f),
                    life = Random.Range(20, 45),
                    maxLife = 45,
                    r = r, g = g, b = b
                });
            }
        }

        // ── Pixel Renderer ─────────────────────────────────────────

        private void RenderFrame()
        {
            // Dark night gradient background
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / H;
                byte br = (byte)Mathf.Lerp(8, 4, t);
                byte bg2 = (byte)Mathf.Lerp(12, 6, t);
                byte bb = (byte)Mathf.Lerp(25, 14, t);
                Color32 c = new Color32(br, bg2, bb, 255);
                int row = (H - 1 - y) * W;
                for (int x = 0; x < W; x++) px[row + x] = c;
            }

            // Parallax stars
            foreach (var s in stars) FillCircle((int)s.x, (int)s.y, Mathf.RoundToInt(s.size), 0, 240, 255, 180);

            // Ground & terrain line
            FillRect(0, (int)GROUND_Y, W, H - (int)GROUND_Y, 16, 22, 38, 255);
            FillRect(0, (int)GROUND_Y, W, 3, 0, 240, 255, 255);

            // Ground terrain tick marks
            int groundOffset = (int)(distance * 4f) % 40;
            for (int gx = -groundOffset; gx < W; gx += 40)
            {
                FillRect(gx, (int)GROUND_Y + 8, 12, 2, 0, 180, 220, 150);
            }

            // Obstacles
            foreach (var o in obstacles)
            {
                if (o.type == "spike" || o.type == "doublespike")
                {
                    // Draw neon red spikes
                    int count = o.type == "doublespike" ? 2 : 1;
                    int subW = (int)o.w / count;
                    for (int c = 0; c < count; c++)
                    {
                        int sx = (int)o.x + c * subW;
                        FillTri(sx, (int)o.y + (int)o.h, sx + subW / 2, (int)o.y, sx + subW, (int)o.y + (int)o.h, 255, 0, 85, 255);
                    }
                }
                else if (o.type == "bat")
                {
                    // Draw flying bat with flapping wings
                    int bx2 = (int)o.x;
                    int by2 = (int)o.y;
                    int flap = (Mathf.Sin(o.animFrame) > 0) ? -4 : 4;
                    FillRect(bx2 + 8, by2 + 6, 18, 10, 217, 70, 239, 255); // body
                    FillRect(bx2, by2 + 4 + flap, 10, 4, 217, 70, 239, 255); // L wing
                    FillRect(bx2 + 24, by2 + 4 + flap, 10, 4, 217, 70, 239, 255); // R wing
                    FillRect(bx2 + 12, by2 + 8, 3, 3, 255, 255, 255, 255); // Eye
                }
            }

            // Items
            foreach (var item in items)
            {
                int iy = (int)(item.y + item.osc);
                if (item.type == "gem")
                {
                    FillCircle((int)item.x, iy, (int)item.r, 0, 255, 204, 255);
                }
                else if (item.type == "shield")
                {
                    FillCircle((int)item.x, iy, (int)item.r, 56, 189, 248, 255);
                }
            }

            // Particles
            foreach (var p in particles)
            {
                float alpha = Mathf.Clamp01(p.life / p.maxLife);
                FillCircle((int)p.x, (int)p.y, Mathf.Max(1, (int)p.sz), p.r, p.g, p.b, (byte)(alpha * 255));
            }

            // Player Chibi Runner
            if (playing)
            {
                int pw = (int)(isDucking ? pDuckW : pStandW);
                int ph = (int)(isDucking ? pDuckH : pStandH);
                int px2 = (int)pxPos;
                int py2 = (int)(pyPos - ph);

                byte pr = shield ? (byte)56 : (byte)0;
                byte pg = shield ? (byte)189 : (byte)240;
                byte pb = shield ? (byte)248 : (byte)255;

                // Main body
                FillRect(px2, py2, pw, ph, pr, pg, pb, 255);
                FillRect(px2, py2, pw, 2, 255, 255, 255, 255); // top edge highlight

                // Eye
                int eyeX = px2 + (isDucking ? 34 : 20);
                int eyeY = py2 + (isDucking ? 6 : 10);
                FillRect(eyeX, eyeY, 4, 8, 9, 13, 22, 255);

                // Animated running legs (when grounded & not ducking)
                if (grounded && !isDucking)
                {
                    int legAnim = (tick / 4) % 2;
                    if (legAnim == 0)
                    {
                        FillRect(px2 + 6, py2 + ph, 6, 6, pr, pg, pb, 255);
                        FillRect(px2 + 20, py2 + ph, 6, 2, pr, pg, pb, 255);
                    }
                    else
                    {
                        FillRect(px2 + 6, py2 + ph, 6, 2, pr, pg, pb, 255);
                        FillRect(px2 + 20, py2 + ph, 6, 6, pr, pg, pb, 255);
                    }
                }

                // Shield Aura
                if (shield)
                {
                    DrawCircleOutline(px2 + pw / 2, py2 + ph / 2, (int)(pw * 0.8f), 56, 189, 248, 180);
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

        private void FillTri(int x1, int y1, int x2, int y2, int x3, int y3, byte r, byte g, byte b, byte a)
        {
            Color32 c = new Color32(r, g, b, a);
            int minY = Mathf.Max(0, Mathf.Min(y1, Mathf.Min(y2, y3)));
            int maxY = Mathf.Min(H - 1, Mathf.Max(y1, Mathf.Max(y2, y3)));
            if (maxY <= minY) return;

            for (int y = minY; y <= maxY; y++)
            {
                int lx = W, rx2 = 0;
                CheckEdge(y, x1, y1, x2, y2, ref lx, ref rx2);
                CheckEdge(y, x2, y2, x3, y3, ref lx, ref rx2);
                CheckEdge(y, x3, y3, x1, y1, ref lx, ref rx2);

                if (lx <= rx2)
                {
                    int row = (H - 1 - y) * W;
                    for (int x = Mathf.Max(0, lx); x <= Mathf.Min(W - 1, rx2); x++)
                        px[row + x] = Blend(px[row + x], c);
                }
            }
        }

        private void CheckEdge(int y, int x1, int y1, int x2, int y2, ref int lx, ref int rx)
        {
            if ((y1 <= y && y2 >= y) || (y2 <= y && y1 >= y))
            {
                if (y1 != y2)
                {
                    float t = (float)(y - y1) / (y2 - y1);
                    int x = (int)(x1 + t * (x2 - x1));
                    if (x < lx) lx = x;
                    if (x > rx) rx = x;
                }
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
