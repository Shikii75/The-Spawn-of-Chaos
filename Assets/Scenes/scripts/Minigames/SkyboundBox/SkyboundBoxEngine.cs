using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpawnOfChaos.Minigames
{
    /// <summary>
    /// Exact 1:1 C# Unity port of the HTML5 Canvas Skybound Box game engine (js/game.js).
    /// Renders to a 600x800 Texture2D displayed via RawImage. Uses Time.unscaledDeltaTime 
    /// so the minigame runs while the main game is paused. Every physics constant, collision 
    /// rule, platform type, particle effect, and visual matches the localhost web version.
    /// </summary>
    public class SkyboundBoxEngine : MonoBehaviour
    {
        public static bool virtualLeft = false;
        public static bool virtualRight = false;
        public static bool virtualJump = false;
        // ── Public References (set by SkyboundArcadeUI) ────────────
        [HideInInspector] public RawImage displayImage;
        [HideInInspector] public TextMeshProUGUI timerText;
        [HideInInspector] public TextMeshProUGUI highScoreText;
        [HideInInspector] public GameObject gameOverPanel;
        [HideInInspector] public TextMeshProUGUI gameOverReasonText;
        [HideInInspector] public TextMeshProUGUI finalTimeText;
        [HideInInspector] public TextMeshProUGUI bestRecordText;

        // ── Virtual Canvas (matches HTML5 600x800) ─────────────────
        private const int W = 600;
        private const int H = 800;

        private Texture2D tex;
        private Color32[] px;

        // ── Game State ─────────────────────────────────────────────
        private bool playing;
        private float elapsed;
        private float best;
        private float baseSpd = 2.2f;
        private float curSpd;
        private int shake;
        private float shakeAmt;
        private int trailTick;

        private const float CEIL = 24f;
        private const float FLOOR = 24f;

        public bool IsPlaying => playing;

        // ── Box (1:1 JS values) ────────────────────────────────────
        private float bx, by, bw = 36f, bh = 36f;
        private float bvx, bvy;
        private const float GRAV = 0.48f;
        private const float MOVE = 1.1f;
        private const float MAXVX = 8.5f;
        private const float FRIC = 0.84f;
        private bool grounded, dblJump;
        private float sqX = 1f, sqY = 1f;
        private bool shield;
        private float slowMo;

        // ── Platform Data ──────────────────────────────────────────
        private class Plat
        {
            public float x, y, w, h, vx;
            public string t; // standard, spring, crumble, conveyor, oscillator
            public int crumble;
            public byte cr, cg, cb;
        }

        // ── Collectible Data ───────────────────────────────────────
        private class Item
        {
            public float x, y, r;
            public string t; // gem, shield, slowmo
            public float osc;
            public byte cr, cg, cb;
        }

        // ── Particle Data ──────────────────────────────────────────
        private class Part
        {
            public float x, y, vx, vy, sz, life, maxLife, grav;
            public byte cr, cg, cb;
            public string t;
        }

        private class FText
        {
            public string s;
            public float x, y, life, maxLife;
            public byte cr, cg, cb;
        }

        private class Star
        {
            public float x, y, sz, spd, a;
        }

        private readonly List<Plat> plats = new List<Plat>();
        private readonly List<Item> items = new List<Item>();
        private readonly List<Part> parts = new List<Part>();
        private readonly List<FText> ftexts = new List<FText>();
        private readonly List<Star> stars = new List<Star>();

        // ── Lifecycle ──────────────────────────────────────────────

        void Awake()
        {
            best = PlayerPrefs.GetFloat("Skybound_HighScore", 0f);
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            px = new Color32[W * H];
            if (displayImage != null) displayImage.texture = tex;

            for (int i = 0; i < 45; i++)
                stars.Add(new Star
                {
                    x = Random.Range(0f, W),
                    y = Random.Range(0f, H),
                    sz = Random.Range(0.5f, 2.5f),
                    spd = Random.Range(0.3f, 1.1f),
                    a = Random.Range(0.2f, 0.7f)
                });
        }

        public void StartNewGame()
        {
            if (displayImage != null && displayImage.texture == null)
                displayImage.texture = tex;

            elapsed = 0f;
            curSpd = baseSpd;
            shake = 0;
            trailTick = 0;
            shield = false;
            slowMo = 0f;

            bx = W / 2f - 18f;
            by = H / 3f;
            bvx = 0f;
            bvy = 0f;
            sqX = 1f;
            sqY = 1f;
            grounded = false;
            dblJump = true;

            plats.Clear();
            items.Clear();
            parts.Clear();
            ftexts.Clear();

            // Starter platform directly under the player
            plats.Add(new Plat { x = W / 2f - 70f, y = H / 3f + 120f, w = 140f, h = 18f, t = "standard", vx = 0, crumble = -1, cr = 0, cg = 240, cb = 255 });

            // Fill initial platforms downward
            float cy = H / 3f + 260f;
            while (cy < H + 100f)
            {
                SpawnPlat(cy);
                cy += Random.Range(110f, 160f);
            }

            playing = true;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            UpdateHUD();
        }

        // ── Platform Spawner ───────────────────────────────────────

        private void SpawnPlat(float yy)
        {
            float pw = Random.Range(95f, 135f);
            float px2 = Random.Range(20f, W - pw - 20f);
            float r = Random.value;
            string tp = "standard";
            byte cr = 0, cg = 240, cb = 255;
            float vx = 0f;

            if (r < 0.20f) { tp = "spring"; cr = 217; cg = 70; cb = 239; }
            else if (r < 0.38f) { tp = "crumble"; cr = 249; cg = 115; cb = 22; }
            else if (r < 0.52f) { tp = "conveyor"; cr = 234; cg = 179; cb = 8; vx = Random.value < 0.5f ? 2.5f : -2.5f; }
            else if (r < 0.65f) { tp = "oscillator"; cr = 59; cg = 130; cb = 246; vx = Random.value < 0.5f ? 2f : -2f; }

            plats.Add(new Plat { x = px2, y = yy, w = pw, h = 18f, t = tp, vx = vx, crumble = -1, cr = cr, cg = cg, cb = cb });

            if (Random.value < 0.28f)
            {
                string it = Random.value < 0.75f ? "gem" : (Random.value < 0.6f ? "shield" : "slowmo");
                byte ir, ig, ib;
                if (it == "gem") { ir = 0; ig = 255; ib = 204; }
                else if (it == "shield") { ir = 56; ig = 189; ib = 248; }
                else { ir = 168; ig = 85; ib = 247; }
                items.Add(new Item { x = px2 + pw / 2f, y = yy - 25f, r = 10f, t = it, osc = 0f, cr = ir, cg = ig, cb = ib });
            }
        }

        // ── Main Loop ──────────────────────────────────────────────

        void Update()
        {
            bool kl = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || virtualLeft;
            bool kr = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || virtualRight;
            bool jmp = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space) || virtualJump;
            virtualJump = false;

            if (jmp && playing) Jump();
            if (!playing) { RenderFrame(); return; }

            float dt = Time.unscaledDeltaTime;
            if (dt > 0.05f) dt = 0.05f;

            elapsed += dt;

            float tgt = baseSpd + Mathf.Min(elapsed * 0.045f, 5.5f);
            if (slowMo > 0f) { slowMo -= dt; tgt *= 0.55f; }
            curSpd = tgt;

            // Box physics
            if (kl) bvx -= MOVE;
            if (kr) bvx += MOVE;
            bvx = Mathf.Clamp(bvx, -MAXVX, MAXVX);
            bvx *= FRIC;
            bx += bvx;
            bvy += GRAV;
            by += bvy;
            sqX += (1f - sqX) * 0.15f;
            sqY += (1f - sqY) * 0.15f;

            // Safe walls
            if (bx <= 0f) { bx = 0f; bvx = -bvx * 0.4f; Dust(bx, by + bh / 2f, bh, 0, 240, 255); }
            else if (bx + bw >= W) { bx = W - bw; bvx = -bvx * 0.4f; Dust(bx + bw, by + bh / 2f, bh, 0, 240, 255); }

            trailTick++;
            if (trailTick % 3 == 0)
            {
                byte tr = shield ? (byte)56 : (byte)0, tg = shield ? (byte)189 : (byte)240, tb = shield ? (byte)248 : (byte)255;
                parts.Add(new Part { x = bx + bw / 2f, y = by + bh / 2f, vx = Random.Range(-0.25f, 0.25f), vy = Random.Range(-0.25f, 0.25f), cr = tr, cg = tg, cb = tb, sz = bw * 0.6f, life = 12, maxLife = 12, t = "trail" });
            }

            // Platforms
            bool landed = false;
            for (int i = plats.Count - 1; i >= 0; i--)
            {
                var p = plats[i];
                p.y -= curSpd;

                if (p.t == "oscillator" || p.t == "conveyor")
                {
                    p.x += p.vx;
                    if (p.x <= 10f || p.x + p.w >= W - 10f) p.vx = -p.vx;
                }

                if (p.crumble > 0)
                {
                    p.crumble--;
                    if (p.crumble <= 0) { GlassBreak(p.x, p.y, p.w, p.h, p.cr, p.cg, p.cb); plats.RemoveAt(i); continue; }
                }

                float prevY = by - bvy;
                if (bvy >= 0f && prevY + bh <= p.y + 12f && by + bh >= p.y && bx + bw >= p.x && bx <= p.x + p.w)
                {
                    by = p.y - bh;
                    bvy = -curSpd;
                    grounded = true;
                    dblJump = true;
                    landed = true;

                    if (Mathf.Abs(bvy) > 2f)
                    {
                        sqX = 1.35f;
                        sqY = 0.65f;
                        Dust(bx + bw / 2f, by + bh, bw, p.cr, p.cg, p.cb);
                    }

                    if (p.t == "spring")
                    {
                        bvy = -14.5f;
                        grounded = false;
                        sqX = 0.6f;
                        sqY = 1.4f;
                        ftexts.Add(new FText { s = "SUPER BOUNCE!", x = bx + bw / 2f, y = by - 15f, life = 50, maxLife = 50, cr = 217, cg = 70, cb = 239 });
                        Dust(bx + bw / 2f, by + bh, bw * 1.5f, 217, 70, 239);
                    }
                    else if (p.t == "crumble" && p.crumble < 0)
                    {
                        p.crumble = 22;
                        ftexts.Add(new FText { s = "CRUMBLING!", x = p.x + p.w / 2f, y = p.y - 10f, life = 50, maxLife = 50, cr = 249, cg = 115, cb = 22 });
                    }
                    else if (p.t == "conveyor")
                    {
                        bx += p.vx * 1.2f;
                    }
                }

                if (p.y + p.h < -50f) { plats.RemoveAt(i); }
            }
            if (!landed && grounded) grounded = false;

            // Collectibles
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var c = items[i];
                c.y -= curSpd;
                c.osc = Mathf.Sin(Time.unscaledTime * 5f) * 4f;
                float cx = c.x, cy = c.y + c.osc, mx = bx + bw / 2f, my = by + bh / 2f;
                float d = Mathf.Sqrt((cx - mx) * (cx - mx) + (cy - my) * (cy - my));
                if (d < c.r + bw / 2f)
                {
                    if (c.t == "gem") { elapsed += 2.5f; ftexts.Add(new FText { s = "+2.5s BONUS!", x = mx, y = my - 15f, life = 50, maxLife = 50, cr = 0, cg = 255, cb = 204 }); }
                    else if (c.t == "shield") { shield = true; ftexts.Add(new FText { s = "SHIELD ACTIVE!", x = mx, y = my - 15f, life = 50, maxLife = 50, cr = 56, cg = 189, cb = 248 }); }
                    else if (c.t == "slowmo") { slowMo = 5f; ftexts.Add(new FText { s = "SLOW-MO!", x = mx, y = my - 15f, life = 50, maxLife = 50, cr = 168, cg = 85, cb = 247 }); }
                    Boom(cx, cy, c.cr, c.cg, c.cb, 12f);
                    items.RemoveAt(i);
                }
                else if (c.y < -30f) items.RemoveAt(i);
            }

            // Particles & floating text
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var q = parts[i];
                q.x += q.vx; q.y += q.vy; q.vy += q.grav; q.life--;
                if (q.life <= 0) parts.RemoveAt(i);
            }
            for (int i = ftexts.Count - 1; i >= 0; i--)
            {
                var f = ftexts[i];
                f.y -= 1.2f; f.life--;
                if (f.life <= 0) ftexts.RemoveAt(i);
            }
            foreach (var s in stars) { s.y += s.spd; if (s.y > H) { s.y = -10f; s.x = Random.Range(0f, (float)W); } }

            // Fatal boundaries
            bool hitC = by <= CEIL;
            bool hitF = by + bh >= H - FLOOR;
            if (hitC || hitF)
            {
                if (shield)
                {
                    shield = false;
                    shake = 12; shakeAmt = 10f;
                    if (hitC) { by = CEIL + 5f; bvy = 4f; } else { by = H - FLOOR - bh - 5f; bvy = -12f; }
                    ftexts.Add(new FText { s = "SHIELD SAVED YOU!", x = bx + bw / 2f, y = by, life = 50, maxLife = 50, cr = 56, cg = 189, cb = 248 });
                    Boom(bx + bw / 2f, by + bh / 2f, 56, 189, 248, bw);
                }
                else
                {
                    Die(hitC ? "Crushed by Ceiling!" : "Fell into the Abyss!");
                }
            }

            // Spawn new platforms
            float lowest = 0f;
            foreach (var p in plats) if (p.y > lowest) lowest = p.y;
            if (lowest < H - 40f) SpawnPlat(H + Random.Range(40f, 80f));

            if (shake > 0) shake--;

            UpdateHUD();
            RenderFrame();
        }

        // ── Jump ───────────────────────────────────────────────────

        private void Jump()
        {
            if (grounded)
            {
                bvy = -10.5f; grounded = false; sqX = 0.7f; sqY = 1.3f;
                Dust(bx + bw / 2f, by + bh, bw, 0, 240, 255);
            }
            else if (dblJump)
            {
                bvy = -9.5f; dblJump = false; sqX = 0.8f; sqY = 1.2f;
                ftexts.Add(new FText { s = "DOUBLE JUMP!", x = bx + bw / 2f, y = by - 10f, life = 50, maxLife = 50, cr = 56, cg = 189, cb = 248 });
            }
        }

        // ── Death ──────────────────────────────────────────────────

        private void Die(string reason)
        {
            playing = false;
            shake = 30; shakeAmt = 16f;
            Boom(bx + bw / 2f, by + bh / 2f, 0, 240, 255, bw);

            if (elapsed > best) { best = elapsed; PlayerPrefs.SetFloat("Skybound_HighScore", best); PlayerPrefs.Save(); }
            if (AdManager.Instance != null) { AdManager.Instance.RecordMinigameLoss(); }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                if (gameOverReasonText != null) gameOverReasonText.text = reason;
                if (finalTimeText != null) finalTimeText.text = elapsed.ToString("F2") + "s";
                if (bestRecordText != null) bestRecordText.text = best.ToString("F2") + "s";
            }
        }

        // ── Particle Spawners (Match JS particle system) ───────────

        private void Boom(float x, float y, byte r, byte g, byte b, float sz)
        {
            parts.Add(new Part { x = x, y = y, sz = sz * 1.5f, life = 30, maxLife = 30, t = "ring", cr = r, cg = g, cb = b });
            for (int i = 0; i < 35; i++)
            {
                float a = (Mathf.PI * 2f * i) / 35f + Random.Range(-0.5f, 0.5f);
                float sp = Random.Range(4f, 13f);
                parts.Add(new Part { x = x, y = y, vx = Mathf.Cos(a) * sp, vy = Mathf.Sin(a) * sp - 2f, cr = r, cg = g, cb = b, sz = Random.Range(4f, sz / 3f + 4f), life = Random.Range(35, 75), maxLife = Random.Range(35, 75), t = "shatter", grav = 0.35f });
            }
            for (int i = 0; i < 25; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(2f, 12f);
                parts.Add(new Part { x = x, y = y, vx = Mathf.Cos(a) * sp, vy = Mathf.Sin(a) * sp, cr = 255, cg = 255, cb = 255, sz = Random.Range(1f, 4f), life = Random.Range(15, 40), maxLife = Random.Range(15, 40), t = "spark" });
            }
        }

        private void Dust(float x, float y, float w, byte r, byte g, byte b)
        {
            for (int i = 0; i < 12; i++)
                parts.Add(new Part { x = x + Random.Range(-w / 2f, w / 2f), y = y, vx = Random.Range(-3f, 3f), vy = -Random.Range(0.5f, 2.5f), cr = r, cg = g, cb = b, sz = Random.Range(2f, 5f), life = 20, maxLife = 20, t = "dust" });
        }

        private void GlassBreak(float x, float y, float w, float h, byte r, byte g, byte b)
        {
            for (int i = 0; i < 20; i++)
                parts.Add(new Part { x = x + Random.Range(0, w), y = y + Random.Range(0, h), vx = Random.Range(-2.5f, 2.5f), vy = Random.Range(-2f, 2f), cr = r, cg = g, cb = b, sz = Random.Range(4f, 12f), life = 30, maxLife = 30, t = "glass", grav = 0.4f });
        }

        // ── HUD ────────────────────────────────────────────────────

        private void UpdateHUD()
        {
            if (timerText != null) timerText.text = elapsed.ToString("F2") + "s";
            if (highScoreText != null) highScoreText.text = best.ToString("F2") + "s";
        }

        // ── Render (1:1 pixel replication of HTML5 Canvas) ─────────

        private void RenderFrame()
        {
            // Background gradient
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / H;
                byte br, bg2, bb;
                if (t < 0.5f) { float l = t / 0.5f; br = (byte)Mathf.Lerp(9, 5, l); bg2 = (byte)Mathf.Lerp(13, 7, l); bb = (byte)Mathf.Lerp(22, 13, l); }
                else { float l = (t - 0.5f) / 0.5f; br = (byte)Mathf.Lerp(5, 12, l); bg2 = (byte)Mathf.Lerp(7, 8, l); bb = (byte)Mathf.Lerp(13, 20, l); }
                Color32 c = new Color32(br, bg2, bb, 255);
                int row = (H - 1 - y) * W;
                for (int x = 0; x < W; x++) px[row + x] = c;
            }

            // Bg stars
            foreach (var s in stars) FillCircle((int)s.x, (int)s.y, Mathf.Max(1, Mathf.RoundToInt(s.sz)), 0, 240, 255, (byte)(s.a * 255));

            // Fatal ceiling hazard (red stripe)
            FillRect(0, 0, W, (int)CEIL, 255, 0, 85, 255);
            // Warning stripes on ceiling
            for (int sx = 0; sx < W; sx += 30)
                FillTri(sx, 0, sx + 15, (int)CEIL, sx - 10, 0, sx + 5, (int)CEIL, 255, 255, 255, 51);

            // Fatal floor hazard (red stripe)
            FillRect(0, H - (int)FLOOR, W, (int)FLOOR, 255, 0, 85, 255);
            // Warning stripes on floor
            for (int sx = 0; sx < W; sx += 30)
                FillTri(sx, H - (int)FLOOR, sx + 15, H, sx - 10, H - (int)FLOOR, sx + 5, H, 255, 255, 255, 51);

            // Safe left wall (cyan line)
            FillRect(0, (int)CEIL, 4, H - (int)CEIL - (int)FLOOR, 0, 240, 255, 153);
            // Safe right wall (cyan line)
            FillRect(W - 4, (int)CEIL, 4, H - (int)CEIL - (int)FLOOR, 0, 240, 255, 153);

            // Platforms
            foreach (var p in plats)
            {
                // Platform body with gradient
                int py1 = (int)p.y;
                int py2 = (int)(p.y + p.h);
                for (int y = py1; y < py2; y++)
                {
                    if (y < 0 || y >= H) continue;
                    float lp = (float)(y - py1) / Mathf.Max(1, py2 - py1);
                    byte rr = (byte)Mathf.Lerp(p.cr, 17, lp);
                    byte gg = (byte)Mathf.Lerp(p.cg, 24, lp);
                    byte bb = (byte)Mathf.Lerp(p.cb, 39, lp);
                    int row = (H - 1 - y) * W;
                    for (int x = (int)p.x; x < (int)(p.x + p.w); x++)
                    {
                        if (x < 0 || x >= W) continue;
                        px[row + x] = new Color32(rr, gg, bb, 255);
                    }
                }
                // Highlight top edge
                FillRect((int)p.x + 4, (int)p.y, (int)p.w - 8, 2, 255, 255, 255, 153);

                // Type visual indicators
                if (p.t == "spring")
                    FillRect((int)(p.x + p.w / 2f - 12f), (int)p.y + 3, 24, 4, 255, 255, 255, 255);
                else if (p.t == "conveyor")
                {
                    int dir = p.vx > 0 ? 1 : -1;
                    for (int ax = (int)p.x + 15; ax < (int)(p.x + p.w) - 10; ax += 25)
                        FillRect(ax, (int)p.y + 5, 6, 8, 255, 255, 255, 200);
                }
            }

            // Collectibles
            foreach (var c in items)
            {
                int cy = (int)(c.y + c.osc);
                if (c.t == "gem")
                {
                    // Diamond shape
                    for (int dy = -(int)c.r; dy <= (int)c.r; dy++)
                    {
                        int ry = cy + dy;
                        if (ry < 0 || ry >= H) continue;
                        int hw = (int)c.r - Mathf.Abs(dy);
                        int row = (H - 1 - ry) * W;
                        for (int dx = -hw; dx <= hw; dx++)
                        {
                            int rx = (int)c.x + dx;
                            if (rx >= 0 && rx < W) px[row + rx] = new Color32(c.cr, c.cg, c.cb, 255);
                        }
                    }
                }
                else
                    FillCircle((int)c.x, cy, (int)c.r, c.cr, c.cg, c.cb, 255);
            }

            // Particles
            foreach (var q in parts)
            {
                float alpha = Mathf.Clamp01(q.life / q.maxLife);
                byte a = (byte)(alpha * 255);
                int sz = Mathf.Max(1, (int)q.sz);
                if (q.t == "shatter" || q.t == "glass")
                    FillRect((int)q.x - sz / 2, (int)q.y - sz / 2, sz, sz, q.cr, q.cg, q.cb, a);
                else
                    FillCircle((int)q.x, (int)q.y, sz, q.cr, q.cg, q.cb, a);
            }

            // Player Box
            if (playing)
            {
                int ix = (int)bx, iy = (int)by, iw = (int)bw, ih = (int)bh;
                byte boxR = shield ? (byte)56 : (byte)0;
                byte boxG = shield ? (byte)189 : (byte)240;
                byte boxB = shield ? (byte)248 : (byte)255;

                // Box body gradient (white to cyan to darker blue)
                for (int y = iy; y < iy + ih; y++)
                {
                    if (y < 0 || y >= H) continue;
                    float lt = (float)(y - iy) / ih;
                    byte rr, gg, bb;
                    if (lt < 0.3f) { float l2 = lt / 0.3f; rr = (byte)Mathf.Lerp(255, boxR, l2); gg = (byte)Mathf.Lerp(255, boxG, l2); bb = (byte)Mathf.Lerp(255, boxB, l2); }
                    else { float l2 = (lt - 0.3f) / 0.7f; rr = (byte)Mathf.Lerp(boxR, 2, l2); gg = (byte)Mathf.Lerp(boxG, 132, l2); bb = (byte)Mathf.Lerp(boxB, 199, l2); }
                    int row = (H - 1 - y) * W;
                    for (int x = ix; x < ix + iw; x++) { if (x >= 0 && x < W) px[row + x] = new Color32(rr, gg, bb, 255); }
                }

                // Neon border (white outline)
                FillRect(ix, iy, iw, 2, 255, 255, 255, 255);
                FillRect(ix, iy + ih - 2, iw, 2, 255, 255, 255, 255);
                FillRect(ix, iy, 2, ih, 255, 255, 255, 255);
                FillRect(ix + iw - 2, iy, 2, ih, 255, 255, 255, 255);

                // Cute animated eyes
                int eo = (int)(bvx * 0.4f);
                FillRect(ix + iw / 2 - 8 + eo, iy + 10, 4, 8, 9, 13, 22, 255);
                FillRect(ix + iw / 2 + 4 + eo, iy + 10, 4, 8, 9, 13, 22, 255);

                // Shield aura circle
                if (shield)
                {
                    int srad = (int)(bw * 0.8f);
                    int scx = ix + iw / 2, scy = iy + ih / 2;
                    DrawCircleOutline(scx, scy, srad, 56, 189, 248, 180);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
        }

        // ── Drawing Primitives ─────────────────────────────────────

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
            int steps = Mathf.Max(32, rad * 6);
            for (int i = 0; i < steps; i++)
            {
                float ang = (Mathf.PI * 2f * i) / steps;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(ang) * rad);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(ang) * rad);
                if (x >= 0 && x < W && y >= 0 && y < H)
                    px[(H - 1 - y) * W + x] = Blend(px[(H - 1 - y) * W + x], c);
            }
        }

        private void FillTri(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, byte r, byte g, byte b, byte a)
        {
            // Simple hazard stripe fill between two parallelogram edges
            Color32 c = new Color32(r, g, b, a);
            int minY = Mathf.Max(0, Mathf.Min(Mathf.Min(y1, y2), Mathf.Min(y3, y4)));
            int maxY = Mathf.Min(H - 1, Mathf.Max(Mathf.Max(y1, y2), Mathf.Max(y3, y4)));
            if (maxY <= minY) return;

            for (int y = minY; y <= maxY; y++)
            {
                float t = (float)(y - y1) / Mathf.Max(1, y2 - y1);
                t = Mathf.Clamp01(t);
                int lx = (int)Mathf.Lerp(x3, x4, t);
                int rx2 = (int)Mathf.Lerp(x1, x2, t);
                if (lx > rx2) { int tmp = lx; lx = rx2; rx2 = tmp; }
                int row = (H - 1 - y) * W;
                for (int x = Mathf.Max(0, lx); x <= Mathf.Min(W - 1, rx2); x++)
                    px[row + x] = Blend(px[row + x], c);
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
