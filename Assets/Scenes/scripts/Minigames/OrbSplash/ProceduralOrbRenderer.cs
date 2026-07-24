using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpawnOfChaos.Minigames
{
    public enum OrbType
    {
        Health,
        Mana,
        Currency,
        EP
    }

    public class SplashParticle
    {
        public float x;
        public float y;
        public float vx;
        public float vy;
        public float life;
        public float maxLife;
        public float size;
        public Color32 color;
    }

    /// <summary>
    /// Pure C# procedural texture renderer for Orbs (Health, Mana, Currency, EP).
    /// Features procedural dual-wave liquid sloshing, glass sphere lighting, 
    /// emblem motifs, and dynamic liquid splash particle physics.
    /// </summary>
    public class ProceduralOrbRenderer
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public OrbType CurrentOrbType = OrbType.Health;
        public float FillAmount = 0.75f; // 0.0 (empty) to 1.0 (full)
        public float WaveSpeed = 3.5f;
        public float WaveHeight = 4.5f;

        private Texture2D texture;
        private Color32[] pixelBuffer;
        private List<SplashParticle> particles = new List<SplashParticle>();
        private System.Random rand = new System.Random();

        private float animTime = 0f;

        public Texture2D Texture => texture;

        public ProceduralOrbRenderer(int width = 128, int height = 128)
        {
            Width = width;
            Height = height;
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            pixelBuffer = new Color32[Width * Height];
        }

        public void TriggerSplash(float intensity = 1f)
        {
            int count = Mathf.RoundToInt(20 * intensity);
            float cx = Width / 2f;
            float radius = (Width * 0.42f);
            float liquidY = GetLiquidBaseY();

            Color32 pColor = GetPrimaryColor(CurrentOrbType);
            Color32 hColor = GetHighlightColor(CurrentOrbType);

            for (int i = 0; i < count; i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 0.8f + Math.PI * 0.1f); // upward arc
                float speed = (float)(rand.NextDouble() * 35f + 15f) * intensity;
                float px = cx + (float)((rand.NextDouble() - 0.5) * radius * 0.7);

                Color32 c = (rand.Next(0, 2) == 0) ? pColor : hColor;
                c.a = (byte)(rand.Next(200, 255));

                particles.Add(new SplashParticle
                {
                    x = px,
                    y = liquidY + (float)(rand.NextDouble() * 6 - 3),
                    vx = (float)Math.Cos(angle) * speed * ((rand.Next(0, 2) == 0) ? 1f : -1f),
                    vy = (float)Math.Sin(angle) * speed,
                    life = 0f,
                    maxLife = (float)(rand.NextDouble() * 0.6 + 0.4),
                    size = (float)(rand.NextDouble() * 3.5 + 1.5),
                    color = c
                });
            }
        }

        public void UpdateAndRender(float deltaTime)
        {
            animTime += deltaTime * WaveSpeed;

            // Update splash particles
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.life += deltaTime;
                if (p.life >= p.maxLife)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                p.x += p.vx * deltaTime;
                p.y += p.vy * deltaTime;
                p.vy -= 70f * deltaTime; // gravity
            }

            // Auto spawn minor slosh splash droplets when wave crests high
            if (rand.NextDouble() < 0.15)
            {
                TriggerSplash(0.2f);
            }

            RenderBuffer();
            texture.SetPixels32(pixelBuffer);
            texture.Apply();
        }

        private float GetLiquidBaseY()
        {
            float margin = Height * 0.12f;
            float usableH = Height - (margin * 2f);
            return margin + (usableH * Mathf.Clamp01(FillAmount));
        }

        private void RenderBuffer()
        {
            // Clear buffer (transparent black)
            Array.Clear(pixelBuffer, 0, pixelBuffer.Length);

            float cx = Width / 2f;
            float cy = Height / 2f;
            float radius = Width * 0.40f;
            float glassRadius = Width * 0.42f;

            Color32 baseLiquidCol = GetPrimaryColor(CurrentOrbType);
            Color32 deepLiquidCol = GetSecondaryColor(CurrentOrbType);
            Color32 highlightCol = GetHighlightColor(CurrentOrbType);
            Color32 foamCol = GetFoamColor(CurrentOrbType);

            float liquidBaseY = GetLiquidBaseY();

            // 1. Draw Glass Frame & Shadow (Background)
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float distSq = dx * dx + dy * dy;

                    // Outer Metallic / Shadow Frame Ring
                    if (distSq <= glassRadius * glassRadius && distSq >= radius * radius)
                    {
                        float norm = (Mathf.Sqrt(distSq) - radius) / (glassRadius - radius);
                        byte alpha = (byte)((1f - norm) * 160 + 40);
                        pixelBuffer[row + x] = new Color32(25, 30, 42, alpha);
                    }
                    // Inner Dark Shadow base
                    else if (distSq < radius * radius)
                    {
                        pixelBuffer[row + x] = new Color32(10, 12, 18, 180);
                    }
                }
            }

            // 2. Render Secondary (Back) Wave Layer
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float distSq = dx * dx + dy * dy;

                    if (distSq < (radius - 1f) * (radius - 1f))
                    {
                        float waveY = liquidBaseY + Mathf.Sin((x * 0.12f) - (animTime * 1.3f)) * (WaveHeight * 0.8f) + 2f;
                        if (y <= waveY)
                        {
                            Color32 c = Blend(pixelBuffer[row + x], deepLiquidCol);
                            pixelBuffer[row + x] = c;
                        }
                    }
                }
            }

            // 3. Render Primary (Front) Wave Layer & Liquid Fill
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float distSq = dx * dx + dy * dy;

                    if (distSq < (radius - 1f) * (radius - 1f))
                    {
                        // Double sine wave equation
                        float waveY = liquidBaseY 
                            + Mathf.Sin((x * 0.16f) + animTime) * WaveHeight 
                            + Mathf.Cos((x * 0.08f) - (animTime * 0.7f)) * (WaveHeight * 0.5f);

                        // Main liquid body
                        if (y <= waveY)
                        {
                            float depthNorm = Mathf.Clamp01((waveY - y) / (radius * 1.5f));
                            Color32 bodyCol = Color32.Lerp(highlightCol, baseLiquidCol, depthNorm);

                            // Inner core gradient shadow towards center
                            float distFromCenter = Mathf.Sqrt(distSq) / radius;
                            bodyCol.a = (byte)(235 - (distFromCenter * 30));

                            pixelBuffer[row + x] = Blend(pixelBuffer[row + x], bodyCol);
                        }
                        // Wave top foam highlight
                        else if (y <= waveY + 2.5f)
                        {
                            pixelBuffer[row + x] = Blend(pixelBuffer[row + x], foamCol);
                        }
                    }
                }
            }

            // 4. Render Procedural Emblem Core Motif (Heart, Arcane Star, Coin, or EXP Spark)
            DrawEmblemMotif(cx, cy, radius * 0.45f, baseLiquidCol, highlightCol);

            // 5. Render Outer Glass Highlights & Specular Glint
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float distSq = dx * dx + dy * dy;

                    if (distSq < radius * radius)
                    {
                        float dist = Mathf.Sqrt(distSq);

                        // Top-Left Curved Glass Specular Highlight
                        float arcX = dx + (radius * 0.35f);
                        float arcY = dy - (radius * 0.35f);
                        float arcDist = Mathf.Sqrt(arcX * arcX + arcY * arcY);
                        if (arcDist < radius * 0.30f && arcDist > radius * 0.12f && dx < 0 && dy > 0)
                        {
                            float alphaNorm = 1f - (Mathf.Abs(arcDist - (radius * 0.21f)) / (radius * 0.09f));
                            if (alphaNorm > 0)
                            {
                                Color32 specCol = new Color32(255, 255, 255, (byte)(alphaNorm * 180));
                                pixelBuffer[row + x] = Blend(pixelBuffer[row + x], specCol);
                            }
                        }

                        // Outer Glass Rim Glow
                        if (dist > radius * 0.88f)
                        {
                            float rimNorm = (dist - (radius * 0.88f)) / (radius * 0.12f);
                            Color32 rimCol = new Color32(255, 255, 255, (byte)(rimNorm * 75));
                            pixelBuffer[row + x] = Blend(pixelBuffer[row + x], rimCol);
                        }
                    }
                }
            }

            // 6. Render Dynamic Splash Particles
            foreach (var p in particles)
            {
                int px = (int)p.x;
                int py = (int)p.y;
                int pSize = Mathf.Max(1, (int)p.size);
                float alphaRatio = 1f - (p.life / p.maxLife);
                Color32 pCol = p.color;
                pCol.a = (byte)(pCol.a * alphaRatio);

                FillCircleBuffer(px, py, pSize, pCol);
            }
        }

        private void DrawEmblemMotif(float cx, float cy, float emblemRad, Color32 primary, Color32 bright)
        {
            float pulseScale = 1f + Mathf.Sin(animTime * 2f) * 0.08f;
            float r = emblemRad * pulseScale;
            byte alpha = (byte)(160 + Mathf.Sin(animTime * 3f) * 40);

            Color32 motifCol = bright;
            motifCol.a = alpha;

            int minX = Mathf.Max(0, (int)(cx - r - 2));
            int maxX = Mathf.Min(Width - 1, (int)(cx + r + 2));
            int minY = Mathf.Max(0, (int)(cy - r - 2));
            int maxY = Mathf.Min(Height - 1, (int)(cy + r + 2));

            switch (CurrentOrbType)
            {
                case OrbType.Health:
                    // Procedural Heart shape: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
                    for (int y = minY; y <= maxY; y++)
                    {
                        int row = y * Width;
                        float ny = (y - cy) / r * 1.2f - 0.1f;
                        for (int x = minX; x <= maxX; x++)
                        {
                            float nx = (x - cx) / r * 1.2f;
                            float term1 = nx * nx + ny * ny - 1f;
                            if (term1 * term1 * term1 - (nx * nx * ny * ny * ny) <= 0)
                            {
                                pixelBuffer[row + x] = Blend(pixelBuffer[row + x], motifCol);
                            }
                        }
                    }
                    break;

                case OrbType.Mana:
                    // Procedural 4-pointed Arcane Star & Ring
                    for (int y = minY; y <= maxY; y++)
                    {
                        int row = y * Width;
                        float dy = y - cy;
                        for (int x = minX; x <= maxX; x++)
                        {
                            float dx = x - cx;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);

                            // Inner Ring
                            if (Mathf.Abs(d - r * 0.65f) < 1.8f)
                            {
                                pixelBuffer[row + x] = Blend(pixelBuffer[row + x], motifCol);
                            }
                            // 4 Star Rays
                            else if ((Mathf.Abs(dx) < 2f && Mathf.Abs(dy) < r) || (Mathf.Abs(dy) < 2f && Mathf.Abs(dx) < r))
                            {
                                pixelBuffer[row + x] = Blend(pixelBuffer[row + x], motifCol);
                            }
                        }
                    }
                    break;

                case OrbType.Currency:
                    // Procedural Diamond Coin Emblem
                    for (int y = minY; y <= maxY; y++)
                    {
                        int row = y * Width;
                        float dy = y - cy;
                        for (int x = minX; x <= maxX; x++)
                        {
                            float dx = x - cx;
                            if (Mathf.Abs(dx) / (r * 0.8f) + Mathf.Abs(dy) / (r * 1.1f) <= 1f)
                            {
                                pixelBuffer[row + x] = Blend(pixelBuffer[row + x], motifCol);
                            }
                        }
                    }
                    break;

                case OrbType.EP:
                    // Procedural 8-pointed Cosmic EXP Crystal
                    for (int y = minY; y <= maxY; y++)
                    {
                        int row = y * Width;
                        float dy = y - cy;
                        for (int x = minX; x <= maxX; x++)
                        {
                            float dx = x - cx;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float angle = Mathf.Atan2(dy, dx);
                            float ray = Mathf.Abs(Mathf.Cos(angle * 4f));
                            if (d <= r * (0.35f + ray * 0.65f))
                            {
                                pixelBuffer[row + x] = Blend(pixelBuffer[row + x], motifCol);
                            }
                        }
                    }
                    break;
            }
        }

        private void FillCircleBuffer(int cx, int cy, int rad, Color32 col)
        {
            if (col.a == 0) return;
            for (int y = cy - rad; y <= cy + rad; y++)
            {
                if (y < 0 || y >= Height) continue;
                int row = y * Width;
                for (int x = cx - rad; x <= cx + rad; x++)
                {
                    if (x < 0 || x >= Width) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= rad * rad)
                    {
                        pixelBuffer[row + x] = Blend(pixelBuffer[row + x], col);
                    }
                }
            }
        }

        private static Color32 GetPrimaryColor(OrbType type)
        {
            switch (type)
            {
                case OrbType.Health: return new Color32(220, 20, 60, 235);  // Crimson Red
                case OrbType.Mana: return new Color32(0, 140, 240, 235);    // Azure Blue
                case OrbType.Currency: return new Color32(245, 175, 20, 235);// Liquid Gold
                case OrbType.EP: return new Color32(160, 40, 220, 235);     // Cosmic Violet
                default: return new Color32(255, 255, 255, 255);
            }
        }

        private static Color32 GetSecondaryColor(OrbType type)
        {
            switch (type)
            {
                case OrbType.Health: return new Color32(130, 0, 30, 240);
                case OrbType.Mana: return new Color32(0, 70, 160, 240);
                case OrbType.Currency: return new Color32(180, 100, 0, 240);
                case OrbType.EP: return new Color32(80, 10, 140, 240);
                default: return new Color32(100, 100, 100, 255);
            }
        }

        private static Color32 GetHighlightColor(OrbType type)
        {
            switch (type)
            {
                case OrbType.Health: return new Color32(255, 100, 140, 240);
                case OrbType.Mana: return new Color32(80, 220, 255, 240);
                case OrbType.Currency: return new Color32(255, 230, 110, 240);
                case OrbType.EP: return new Color32(40, 240, 160, 240);     // Emerald splash accent
                default: return new Color32(255, 255, 255, 255);
            }
        }

        private static Color32 GetFoamColor(OrbType type)
        {
            switch (type)
            {
                case OrbType.Health: return new Color32(255, 210, 225, 255);
                case OrbType.Mana: return new Color32(210, 245, 255, 255);
                case OrbType.Currency: return new Color32(255, 250, 200, 255);
                case OrbType.EP: return new Color32(220, 255, 235, 255);
                default: return new Color32(255, 255, 255, 255);
            }
        }

        private static Color32 Blend(Color32 bg, Color32 fg)
        {
            float fa = fg.a / 255f;
            float ba = (1f - fa) * (bg.a / 255f);
            float outA = fa + ba;
            if (outA <= 0f) return new Color32(0, 0, 0, 0);

            return new Color32(
                (byte)((fg.r * fa + bg.r * ba) / outA),
                (byte)((fg.g * fa + bg.g * ba) / outA),
                (byte)((fg.b * fa + bg.b * ba) / outA),
                (byte)(outA * 255f)
            );
        }
    }
}
