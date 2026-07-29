using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerPolygonColliderAnimator - Auto-synchronizes PolygonCollider2D with the active SpriteRenderer frame during animations.
/// Generates a detailed multi-vertex character silhouette polygon contour that dynamically morphs with animation frames,
/// working alongside a Capsule ground collider on the parent object.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class PlayerPolygonColliderAnimator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If true, automatically updates the PolygonCollider2D shape whenever the SpriteRenderer sprite changes during animations.")]
    public bool autoUpdatePolygonWithAnimation = true;

    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D polyCollider;
    private Sprite lastSprite;

    // Cache generated physics paths for performance during repeating animation loops
    public Dictionary<int, List<Vector2[]>> spritePathCache = new Dictionary<int, List<Vector2[]>>();

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInParent<SpriteRenderer>();
        polyCollider = GetComponent<PolygonCollider2D>();
    }

    void LateUpdate()
    {
        if (!autoUpdatePolygonWithAnimation || polyCollider == null)
            return;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInParent<SpriteRenderer>();
        }

        if (spriteRenderer == null) return;

        Sprite currentSprite = spriteRenderer.sprite;
        if (currentSprite != null && currentSprite != lastSprite)
        {
            UpdatePolygonColliderShape(currentSprite);
            lastSprite = currentSprite;
        }
    }

    /// <summary>
    /// Recalculates and updates the PolygonCollider2D paths to match the specified sprite.
    /// </summary>
    public void UpdatePolygonColliderShape(Sprite sprite)
    {
        if (sprite == null || polyCollider == null) return;

        int spriteId = sprite.GetInstanceID();

        if (spritePathCache.TryGetValue(spriteId, out List<Vector2[]> cachedPaths))
        {
            ApplyPathsToCollider(cachedPaths);
            return;
        }

        List<Vector2[]> newPaths = new List<Vector2[]>();

        // Generate detailed multi-vertex character silhouette contour path
        Vector2[] contourPath = GenerateTightContourPath(sprite);
        newPaths.Add(contourPath);

        // Cache and apply
        spritePathCache[spriteId] = newPaths;
        ApplyPathsToCollider(newPaths);
    }

    /// <summary>
    /// Generates a detailed multi-vertex character silhouette polygon contour that matches the character sprite dimensions and pivot.
    /// </summary>
    private Vector2[] GenerateTightContourPath(Sprite sprite)
    {
        Rect rect = sprite.rect;
        float pPU = sprite.pixelsPerUnit;
        Vector2 pivot = sprite.pivot;

        float minX = (0f - pivot.x) / pPU;
        float maxX = (rect.width - pivot.x) / pPU;
        float minY = (0f - pivot.y) / pPU;
        float maxY = (rect.height - pivot.y) / pPU;

        float w = maxX - minX;
        float h = maxY - minY;
        float centerX = (minX + maxX) * 0.5f;

        // 11-point detailed character silhouette contour (head, shoulders, torso, hips, feet)
        return new Vector2[]
        {
            new Vector2(centerX, maxY),                                      // Head Top
            new Vector2(centerX + w * 0.24f, minY + h * 0.88f),              // Head Right
            new Vector2(centerX + w * 0.44f, minY + h * 0.72f),              // Shoulder Right
            new Vector2(centerX + w * 0.48f, minY + h * 0.45f),              // Arm/Torso Right
            new Vector2(centerX + w * 0.42f, minY + h * 0.20f),              // Hip/Leg Right
            new Vector2(centerX + w * 0.35f, minY + h * 0.02f),              // Foot Right
            new Vector2(centerX - w * 0.35f, minY + h * 0.02f),              // Foot Left
            new Vector2(centerX - w * 0.42f, minY + h * 0.20f),              // Hip/Leg Left
            new Vector2(centerX - w * 0.48f, minY + h * 0.45f),              // Arm/Torso Left
            new Vector2(centerX - w * 0.45f, minY + h * 0.72f),              // Shoulder Left
            new Vector2(centerX - w * 0.24f, minY + h * 0.88f)               // Head Left
        };
    }

    private void ApplyPathsToCollider(List<Vector2[]> paths)
    {
        if (polyCollider == null || paths == null) return;

        polyCollider.isTrigger = true; // Ensure Polygon Hurtbox does NOT wedge into floor physics
        polyCollider.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
        {
            polyCollider.SetPath(i, paths[i]);
        }
    }
}
