using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ChibiNyxarisController - Drives Chibi Nyxaris in world space after being freed from the seal.
/// Loops her 22-frame idle animation and projects a cosmic purple aura.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ChibiNyxarisController : MonoBehaviour
{
    public static ChibiNyxarisController Instance { get; private set; }

    [Header("Animation Settings")]
    public float fps = 18f;
    public float scale = 1.35f;

    [Header("Aura & Visuals")]
    public Color auraColor = new Color(0.85f, 0.25f, 1.0f, 0.75f);

    private Sprite[] idleFrames;
    private SpriteRenderer sr;
    private SpriteRenderer auraSr;
    private int currentFrame = 0;
    private float frameTimer = 0f;

    void Awake()
    {
        Instance = this;
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        LoadFrames();
        SetupAura();
        transform.localScale = Vector3.one * scale;
    }

    private void LoadFrames()
    {
        idleFrames = Resources.LoadAll<Sprite>("Sprites/ChibiNyxaris/Idle");
        if (idleFrames != null && idleFrames.Length > 0 && sr != null)
        {
            sr.sprite = idleFrames[0];
        }
    }

    private void SetupAura()
    {
        GameObject auraGO = new GameObject("ChibiAura");
        auraGO.transform.SetParent(transform, false);
        auraGO.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        auraGO.transform.localScale = Vector3.one * 1.5f;

        auraSr = auraGO.AddComponent<SpriteRenderer>();
        auraSr.sprite = Resources.Load<Sprite>("Sprites/Nyxaris/Nyxaris_Aura");
        auraSr.color = auraColor;
        auraSr.sortingLayerName = sr.sortingLayerName;
        auraSr.sortingOrder = sr.sortingOrder - 1;
    }

    void Update()
    {
        if (idleFrames == null || idleFrames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(fps, 1f);

        if (frameTimer >= interval)
        {
            frameTimer -= interval;
            currentFrame = (currentFrame + 1) % idleFrames.Length;
            sr.sprite = idleFrames[currentFrame];
        }

        // Gentle breathing aura pulse
        if (auraSr != null)
        {
            float pulse = 1.35f + Mathf.Sin(Time.time * 2.5f) * 0.15f;
            auraSr.transform.localScale = Vector3.one * pulse;
        }
    }
}
