using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FloatingDamageNumber - Dynamic floating damage text popup.
/// Spawns damage popups in world-space, animating pop scale, upward float, and smooth fade out.
/// </summary>
public class FloatingDamageNumber : MonoBehaviour
{
    private TextMesh textMesh;
    private CanvasGroup canvasGroup;
    private Text uiText;

    private Vector3 velocity;
    private float lifetime = 0.65f;
    private float elapsed = 0f;
    private Vector3 initialScale;

    /// <summary>
    /// Factory method to spawn a floating damage number in world space.
    /// </summary>
    public static FloatingDamageNumber Spawn(Vector3 worldPos, int damageAmount, bool isHeavyHit = false, Color? customColor = null)
    {
        GameObject popObj = new GameObject($"DamagePopup_{damageAmount}");
        popObj.transform.position = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.5f), -1f);

        FloatingDamageNumber pop = popObj.AddComponent<FloatingDamageNumber>();
        pop.Initialize(damageAmount, isHeavyHit, customColor);
        return pop;
    }

    private void Initialize(int damageAmount, bool isHeavyHit, Color? customColor)
    {
        textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.text = damageAmount.ToString();
        textMesh.characterSize = 0.15f;
        textMesh.fontSize = isHeavyHit ? 36 : 28;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;

        // Use built-in font supported in Unity 6
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 28);
        }

        if (defaultFont != null)
        {
            textMesh.font = defaultFont;
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = defaultFont.material;
                mr.sortingOrder = 50; // Render above sprites
            }
        }

        // Color coding
        if (customColor.HasValue)
        {
            textMesh.color = customColor.Value;
        }
        else if (isHeavyHit)
        {
            textMesh.color = new Color(1.0f, 0.25f, 0.1f); // Vibrant fiery orange-red for heavy hit
        }
        else
        {
            textMesh.color = new Color(1.0f, 0.92f, 0.3f); // Bright yellow for normal hit
        }

        // Pop scale and float velocity setup
        float baseScale = isHeavyHit ? 1.3f : 1.0f;
        initialScale = new Vector3(baseScale, baseScale, baseScale);
        transform.localScale = initialScale * 0.4f; // Start small for pop effect

        velocity = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(2.2f, 3.2f), 0f);

        StartCoroutine(AnimateRoutine());
    }

    private IEnumerator AnimateRoutine()
    {
        while (elapsed < lifetime)
        {
            float dt = Time.unscaledDeltaTime; // Unscaled time so hitstop doesn't freeze damage text movement
            elapsed += dt;
            float progress = elapsed / lifetime;

            // 1. Move upwards with deceleration
            transform.position += velocity * dt;
            velocity.y = Mathf.Lerp(velocity.y, 0.5f, dt * 4f);

            // 2. Pop scale effect (scale up quickly in first 20%, then settle)
            if (progress < 0.2f)
            {
                float popT = progress / 0.2f;
                transform.localScale = Vector3.Lerp(initialScale * 0.4f, initialScale * 1.35f, popT);
            }
            else if (progress < 0.4f)
            {
                float settleT = (progress - 0.2f) / 0.2f;
                transform.localScale = Vector3.Lerp(initialScale * 1.35f, initialScale, settleT);
            }

            // 3. Fade out in second half
            if (progress > 0.4f)
            {
                float alpha = Mathf.Clamp01(1f - ((progress - 0.4f) / 0.6f));
                if (textMesh != null)
                {
                    Color c = textMesh.color;
                    c.a = alpha;
                    textMesh.color = c;
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
