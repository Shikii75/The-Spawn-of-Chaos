using System.Collections;
using UnityEngine;

/// <summary>
/// LowResBlackOrb - Central generator and manager for pixel-art low-resolution black void orbs.
/// Replaces all legacy untextured/purple particle squares with crisp, low-res black orbs (16x16 Point filtered).
/// </summary>
public static class LowResBlackOrb
{
    private static Texture2D s_texture;
    private static Sprite s_sprite;
    private static Material s_material;

    public static Texture2D GetTexture()
    {
        if (s_texture == null)
        {
            int size = 16;
            s_texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            s_texture.filterMode = FilterMode.Point;
            s_texture.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size * 0.5f) - 0.75f;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x,y), center);
                    if (dist <= radius)
                    {
                        bool isRim = dist > (radius - 1.35f);
                        bool isGlint = (x == 5 && y == 10) || (x == 6 && y == 10);

                        if (isGlint)
                        {
                            pixels[y * size + x] = new Color(0.20f, 0.20f, 0.26f, 1.0f);
                        }
                        else if (isRim)
                        {
                            pixels[y * size + x] = new Color(0.04f, 0.04f, 0.07f, 0.95f);
                        }
                        else
                        {
                            pixels[y * size + x] = new Color(0.01f, 0.01f, 0.02f, 1.0f);
                        }
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            s_texture.SetPixels(pixels);
            s_texture.Apply();
        }
        return s_texture;
    }

    public static Sprite GetSprite()
    {
        if (s_sprite == null)
        {
            Texture2D tex = GetTexture();
            s_sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f);
        }
        return s_sprite;
    }

    public static Material GetMaterial()
    {
        if (s_material == null)
        {
            Shader s = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit")
                    ?? Shader.Find("Unlit/Transparent");
            if (s != null)
            {
                s_material = new Material(s) { hideFlags = HideFlags.DontSave };
                s_material.mainTexture = GetTexture();
                s_material.color = Color.white;
            }
        }
        return s_material;
    }

    public static GameObject SpawnOrb(Vector3 position, float scale = 0.35f, float lifetime = 0.22f, Transform parent = null, int sortingOrder = 20, Vector2? driftVelocity = null)
    {
        GameObject go = new GameObject("LowResBlackOrb");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSprite();
        sr.material = GetMaterial();
        sr.sortingOrder = sortingOrder;
        sr.color = new Color(0.015f, 0.015f, 0.025f, 0.95f);

        Vector2 drift = driftVelocity ?? (Random.insideUnitCircle * 0.4f);
        var mote = go.AddComponent<LowResOrbMote>();
        mote.Init(sr, scale, lifetime, drift);

        return go;
    }

    public static void ConfigureParticleRenderer(ParticleSystemRenderer psRenderer, int sortingOrder = 20, string sortingLayer = "Default")
    {
        if (psRenderer == null) return;
        psRenderer.material = GetMaterial();
        if (!string.IsNullOrEmpty(sortingLayer)) psRenderer.sortingLayerName = sortingLayer;
        psRenderer.sortingOrder = sortingOrder;
    }
}

public class LowResOrbMote : MonoBehaviour
{
    private SpriteRenderer sr;
    private float lifetime;
    private float elapsed;
    private Vector3 initialScale;
    private Vector3 velocity;

    public void Init(SpriteRenderer renderer, float scale, float duration, Vector2 drift)
    {
        sr = renderer;
        lifetime = duration;
        initialScale = Vector3.one * scale;
        velocity = new Vector3(drift.x, drift.y, 0f);
        transform.localScale = initialScale;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        transform.position += velocity * Time.deltaTime;
        transform.localScale = Vector3.Lerp(initialScale, initialScale * 1.25f, t);

        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(0.95f, 0f, t * t);
            sr.color = c;
        }

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
