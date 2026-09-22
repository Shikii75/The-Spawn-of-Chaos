#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dojo2ParkourBuilder — Editor tool to procedurally generate a complex, player-movement-aware
/// parkour climbing section from the base (X:1187, Y:193) up to Dojo 2 (X:1480.9, Y:323.5).
///
/// ══════════════════════════════════════════════════════════════════════════════
/// PLAYER PHYSICS REFERENCE (calibrated from move.cs + Player.prefab):
///   Collider world-space:  ~1.72 × 3.9 units
///   Move speed:            6 u/s
///   Standard jump peak:    ~7.3u height, ~4.5u horizontal at full run
///   Teleport jump:         7.35u instant vertical (increased by 75% from 4.2u)
///   Dash:                  3.2u horizontal burst (16 u/s × 0.2s), zero gravity, invulnerable
///   Dash cooldown:         0.8s
/// ══════════════════════════════════════════════════════════════════════════════
///
/// Layout:
///   Zone 1 "The Gauntlet"       (Y+0  → Y+30)  — Staggered jumps + firebars + crumbling
///   Zone 2 "The Dash Corridor"  (Y+30 → Y+60)  — Dash-gap platforms + spike pits
///   Zone 3 "The Spiral"         (Y+60 → Y+95)  — Zigzag switchbacks + wind + mixed hazards
///   Zone 4 "The Gauntlet Finale"(Y+95 → Y+130) — Moving platforms + dual firebars + mob arenas
/// </summary>
public class Dojo2ParkourBuilder : EditorWindow
{
    // ── World Anchor Points ──────────────────────────────────────────
    private static readonly Vector2 BASE_POS   = new Vector2(1187f, 193f);
    private static readonly Vector2 DOJO2_POS  = new Vector2(1480.9f, 323.5f);

    // ── Player Physics Constants (for gap/height calculations) ───────
    private const float PLAYER_WIDTH       = 1.72f;  // World-space collider width
    private const float PLAYER_HEIGHT      = 3.9f;   // World-space collider height
    private const float TELEPORT_JUMP_MAX  = 7.35f;  // Max teleport jump height (increased by 75% from 4.2f)
    private const float STD_JUMP_PEAK      = 7.3f;   // Standard jump apex
    private const float STD_JUMP_HORIZ     = 4.5f;   // Max horizontal distance at full run-jump
    private const float DASH_DISTANCE      = 3.2f;   // Dash travel distance
    private const float MOVE_SPEED         = 6f;

    [MenuItem("Tools/Dojo 2/Build Dojo 2 Parkour Section")]
    public static void ShowWindow()
    {
        GetWindow<Dojo2ParkourBuilder>("Dojo 2 Parkour Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Dojo 2 Parkour Section Generator (V2 — Complex)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generates a complex, player-movement-calibrated parkour ascent from base " +
            "(1187, 193) to Dojo 2 (1480.9, 323.5).\n\n" +
            "Zone 1: The Gauntlet — Staggered jumps + firebars\n" +
            "Zone 2: The Dash Corridor — Dash-gap platforms + spike pits\n" +
            "Zone 3: The Spiral — Zigzag switchbacks + wind gusts\n" +
            "Zone 4: The Finale — Moving platforms + mob arenas",
            MessageType.Info);

        if (GUILayout.Button("Build / Rebuild Parkour Section", GUILayout.Height(40)))
        {
            BuildParkourSection();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  MAIN BUILDER
    // ══════════════════════════════════════════════════════════════════

    public static void BuildParkourSection()
    {
        // Destroy old root if it exists
        GameObject oldRoot = GameObject.Find("Dojo2_Parkour_Section");
        if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

        GameObject root = new GameObject("Dojo2_Parkour_Section");
        Undo.RegisterCreatedObjectUndo(root, "Build Dojo 2 Parkour V2");

        // Zone containers
        GameObject zone1 = CreateContainer(root.transform, "Zone1_TheGauntlet");
        GameObject zone2 = CreateContainer(root.transform, "Zone2_TheDashCorridor");
        GameObject zone3 = CreateContainer(root.transform, "Zone3_TheSpiral");
        GameObject zone4 = CreateContainer(root.transform, "Zone4_TheFinale");
        GameObject gateway = CreateContainer(root.transform, "Dojo2_Gateway");

        Sprite platformSprite = CreatePlatformSprite();

        // Compute the total horizontal and vertical span
        // Base:  (1187, 193)
        // Dojo2: (1480.9, 323.5)
        // Delta: (293.9, 130.5)
        // We'll distribute X across zones: Z1 gets ~60, Z2 gets ~80, Z3 gets ~75, Z4 gets ~79

        float baseX = BASE_POS.x;
        float baseY = BASE_POS.y;

        // ═══════════════════════════════════════════════════════════════
        //  ZONE 1: "THE GAUNTLET" (baseY → baseY+30)
        //  Staggered jumps, counter-rotating firebars, crumbling platform
        // ═══════════════════════════════════════════════════════════════

        // Starting ledge — narrow (3u wide, just wider than player's 1.72u)
        CreateStaticPlatform(zone1.transform, "Z1_Start_Ledge",
            new Vector3(baseX + 10f, baseY + 5f, 0f),
            new Vector3(3f, 0.6f, 1f), platformSprite,
            new Color(0.55f, 0.45f, 0.35f));

        // Platform 1 — standard jump up-right (3.5u vertical, 3.5u horizontal)
        CreateStaticPlatform(zone1.transform, "Z1_Plat_1",
            new Vector3(baseX + 17f, baseY + 9f, 0f),
            new Vector3(2.8f, 0.5f, 1f), platformSprite,
            new Color(0.55f, 0.45f, 0.35f));

        // Platform 2 — jump up-left (zigzag) — 4u vertical, 2.5u horizontal back
        CreateStaticPlatform(zone1.transform, "Z1_Plat_2",
            new Vector3(baseX + 13f, baseY + 13.5f, 0f),
            new Vector3(2.5f, 0.5f, 1f), platformSprite,
            new Color(0.55f, 0.45f, 0.35f));

        // Counter-rotating firebars blocking the gap between P2 and P3
        CreateRotatingHazard(zone1.transform, "Z1_Firebar_CW",
            new Vector3(baseX + 18f, baseY + 16f, 0f), 75f, true, 3, 1.3f);
        CreateRotatingHazard(zone1.transform, "Z1_Firebar_CCW",
            new Vector3(baseX + 18f, baseY + 16f, 0f), 65f, false, 3, 1.3f);

        // Platform 3 — past the firebars, must time the jump
        CreateStaticPlatform(zone1.transform, "Z1_Plat_3",
            new Vector3(baseX + 23f, baseY + 18f, 0f),
            new Vector3(3f, 0.5f, 1f), platformSprite,
            new Color(0.55f, 0.45f, 0.35f));

        // Crumbling platform — forces commitment, can't idle here
        CreateCrumblingPlatform(zone1.transform, "Z1_Crumble_1",
            new Vector3(baseX + 28f, baseY + 22f, 0f),
            new Vector3(2.5f, 0.5f, 1f), platformSprite);

        // Platform 4 — teleport-jump required (4u vertical from crumbling)
        CreateStaticPlatform(zone1.transform, "Z1_Plat_4",
            new Vector3(baseX + 33f, baseY + 26f, 0f),
            new Vector3(3.2f, 0.5f, 1f), platformSprite,
            new Color(0.55f, 0.45f, 0.35f));

        // Single firebar guarding the exit of Zone 1
        CreateRotatingHazard(zone1.transform, "Z1_Firebar_Exit",
            new Vector3(baseX + 40f, baseY + 28f, 0f), 85f, true, 4, 1.1f);

        // Zone 1 landing — transition platform to Zone 2
        CreateStaticPlatform(zone1.transform, "Z1_Exit_Platform",
            new Vector3(baseX + 48f, baseY + 30f, 0f),
            new Vector3(4f, 0.6f, 1f), platformSprite,
            new Color(0.5f, 0.5f, 0.4f));

        // ═══════════════════════════════════════════════════════════════
        //  ZONE 2: "THE DASH CORRIDOR" (baseY+30 → baseY+60)
        //  Narrow platforms spaced at dash-range, spike pits below
        // ═══════════════════════════════════════════════════════════════

        float z2StartX = baseX + 48f;
        float z2StartY = baseY + 30f;

        // Respawn anchor for the spike pit (teleports player back to zone 2 start)
        GameObject z2Respawn = new GameObject("Z2_RespawnPoint");
        z2Respawn.transform.SetParent(zone2.transform);
        z2Respawn.transform.position = new Vector3(z2StartX, z2StartY + 1.5f, 0f);

        // Spike pit damage zone spanning the entire dash corridor floor
        CreateDamageZone(zone2.transform, "Z2_SpikePit",
            new Vector3(z2StartX + 40f, z2StartY - 8f, 0f),
            new Vector2(90f, 4f), 30f, z2Respawn.transform);

        // Dash platform sequence — alternating heights
        // Gap = 2.8–3.0u (within dash range of 3.2u but tight)
        // Width = 2.2–2.8u (barely wider than player's 1.72u)

        float[] dashPlatX = { 0f, 5.5f, 9f, 14f, 18.5f, 23f, 28.5f, 33f };
        float[] dashPlatY = { 0f, 1.2f, -0.5f, 2f, 0.5f, 3f, 1.5f, 4f };
        float[] dashPlatW = { 3.5f, 2.5f, 2.2f, 2.8f, 2.2f, 2.5f, 2.2f, 3f };

        for (int i = 0; i < dashPlatX.Length; i++)
        {
            string pName = i == 0 ? "Z2_DashStart" : (i == dashPlatX.Length - 1 ? "Z2_DashEnd" : $"Z2_DashPlat_{i}");
            CreateStaticPlatform(zone2.transform, pName,
                new Vector3(z2StartX + dashPlatX[i], z2StartY + dashPlatY[i], 0f),
                new Vector3(dashPlatW[i], 0.5f, 1f), platformSprite,
                new Color(0.4f, 0.35f, 0.5f));
        }

        // Moving platform at end of dash corridor — requires timing + dash
        CreateMovingPlatform(zone2.transform, "Z2_MovingPlat",
            new Vector3(z2StartX + 39f, z2StartY + 7f, 0f),
            new Vector3(3f, 0.5f, 1f), platformSprite,
            Vector2.up, 2.5f, 3f, 0f);

        // Transition to Zone 3 — wider landing
        CreateStaticPlatform(zone2.transform, "Z2_Exit_Platform",
            new Vector3(z2StartX + 46f, z2StartY + 13f, 0f),
            new Vector3(4f, 0.6f, 1f), platformSprite,
            new Color(0.5f, 0.5f, 0.4f));

        // Firebar mid-corridor to prevent pure speed-dash through
        CreateRotatingHazard(zone2.transform, "Z2_Firebar_Mid",
            new Vector3(z2StartX + 16f, z2StartY + 1.5f, 0f), 100f, true, 3, 1.4f);

        // ═══════════════════════════════════════════════════════════════
        //  ZONE 3: "THE SPIRAL" (baseY+60 → baseY+95)
        //  Zigzag switchbacks, wind gusts, rotating hazards at turns
        // ═══════════════════════════════════════════════════════════════

        float z3StartX = z2StartX + 46f;
        float z3StartY = z2StartY + 13f;

        // Switchback layout: platforms alternate left and right
        // Player must reverse direction each jump
        //
        //       P6 ──
        //  ── P5
        //       P4 ──
        //  ── P3
        //       P2 ──
        //  ── P1
        //       Entry ──

        float switchWidth = 18f; // horizontal distance between left and right columns
        float switchVStep = 3.9f; // vertical gap — just within teleport jump (4.2u max)

        Vector3[] spiralPositions = new Vector3[]
        {
            new Vector3(z3StartX + 5f,               z3StartY + switchVStep * 1, 0f), // P1 (right)
            new Vector3(z3StartX + 5f - switchWidth,  z3StartY + switchVStep * 2, 0f), // P2 (left)
            new Vector3(z3StartX + 5f,               z3StartY + switchVStep * 3, 0f), // P3 (right)
            new Vector3(z3StartX + 5f - switchWidth,  z3StartY + switchVStep * 4, 0f), // P4 (left)
            new Vector3(z3StartX + 5f,               z3StartY + switchVStep * 5, 0f), // P5 (right)
            new Vector3(z3StartX + 5f - switchWidth,  z3StartY + switchVStep * 6, 0f), // P6 (left)
            new Vector3(z3StartX + 5f,               z3StartY + switchVStep * 7, 0f), // P7 exit (right)
        };

        float[] spiralWidths = { 3f, 2.5f, 3.2f, 2.5f, 2.8f, 3f, 4f };
        bool[] isCrumbling = { false, false, true, false, true, false, false };

        for (int i = 0; i < spiralPositions.Length; i++)
        {
            string name = $"Z3_Spiral_{i + 1}";
            if (isCrumbling[i])
            {
                CreateCrumblingPlatform(zone3.transform, name,
                    spiralPositions[i],
                    new Vector3(spiralWidths[i], 0.5f, 1f), platformSprite);
            }
            else
            {
                CreateStaticPlatform(zone3.transform, name,
                    spiralPositions[i],
                    new Vector3(spiralWidths[i], 0.5f, 1f), platformSprite,
                    new Color(0.45f, 0.5f, 0.35f));
            }
        }

        // Rotating hazards at switchback turn points (P2, P4, P6)
        CreateRotatingHazard(zone3.transform, "Z3_Firebar_Turn1",
            spiralPositions[1] + new Vector3(switchWidth * 0.5f, 1.5f, 0f), 70f, true, 3, 1.2f);
        CreateRotatingHazard(zone3.transform, "Z3_Firebar_Turn2",
            spiralPositions[3] + new Vector3(switchWidth * 0.5f, 1.5f, 0f), 80f, false, 4, 1.1f);
        CreateRotatingHazard(zone3.transform, "Z3_Firebar_Turn3",
            spiralPositions[5] + new Vector3(switchWidth * 0.5f, 1.5f, 0f), 90f, true, 3, 1.3f);

        // Wind gust zones on tight platforms P3 and P5 — pushes player toward the edge
        CreateWindGustZone(zone3.transform, "Z3_Wind_P3",
            spiralPositions[2] + new Vector3(0f, 2.5f, 0f),
            new Vector2(5f, 5f), new Vector2(-1f, 0f), 5f);
        CreateWindGustZone(zone3.transform, "Z3_Wind_P5",
            spiralPositions[4] + new Vector3(0f, 2.5f, 0f),
            new Vector2(5f, 5f), new Vector2(1f, 0f), 5.5f);

        // Mob platform mid-spiral (between P3 and P4)
        CreateMobPlatform(zone3.transform, "Z3_MobPlatform",
            Vector3.Lerp(spiralPositions[2], spiralPositions[3], 0.5f) + new Vector3(-switchWidth * 0.25f, 0f, 0f),
            new Vector3(5f, 0.7f, 1f), platformSprite, -2.5f, 2.5f);

        // ═══════════════════════════════════════════════════════════════
        //  ZONE 4: "THE GAUNTLET FINALE" (baseY+95 → baseY+130)
        //  Moving platforms, dual firebars, mob arenas, dash+jump combos
        // ═══════════════════════════════════════════════════════════════

        Vector3 z4Start = spiralPositions[spiralPositions.Length - 1];

        // Moving platform 1 — horizontal oscillation over void
        CreateMovingPlatform(zone4.transform, "Z4_MovPlat_1",
            new Vector3(z4Start.x + 8f, z4Start.y + 3f, 0f),
            new Vector3(3f, 0.5f, 1f), platformSprite,
            Vector2.right, 3f, 3.5f, 0f);

        // Moving platform 2 — offset phase (anti-synced with #1)
        CreateMovingPlatform(zone4.transform, "Z4_MovPlat_2",
            new Vector3(z4Start.x + 18f, z4Start.y + 6f, 0f),
            new Vector3(2.8f, 0.5f, 1f), platformSprite,
            Vector2.right, 2.5f, 3f, 0.5f);

        // Dual counter-rotating firebars — creates narrow safe window
        CreateRotatingHazard(zone4.transform, "Z4_DualFire_CW",
            new Vector3(z4Start.x + 26f, z4Start.y + 9f, 0f), 65f, true, 4, 1.2f);
        CreateRotatingHazard(zone4.transform, "Z4_DualFire_CCW",
            new Vector3(z4Start.x + 26f, z4Start.y + 9f, 0f), 55f, false, 4, 1.2f);

        // Dash+jump combo platform — requires dash across 3u gap then immediately teleport jump 4u up
        CreateStaticPlatform(zone4.transform, "Z4_DashLedge",
            new Vector3(z4Start.x + 32f, z4Start.y + 9f, 0f),
            new Vector3(2f, 0.5f, 1f), platformSprite,
            new Color(0.6f, 0.3f, 0.3f));

        CreateStaticPlatform(zone4.transform, "Z4_JumpTarget",
            new Vector3(z4Start.x + 34f, z4Start.y + 13f, 0f),
            new Vector3(2.5f, 0.5f, 1f), platformSprite,
            new Color(0.6f, 0.3f, 0.3f));

        // Mob arena platform 1 — fight while nearby hazards rotate
        CreateMobPlatform(zone4.transform, "Z4_MobArena_1",
            new Vector3(z4Start.x + 42f, z4Start.y + 15f, 0f),
            new Vector3(6f, 0.7f, 1f), platformSprite, -3f, 3f);

        // Small connector ledge
        CreateStaticPlatform(zone4.transform, "Z4_Connector",
            new Vector3(z4Start.x + 52f, z4Start.y + 18f, 0f),
            new Vector3(2.5f, 0.5f, 1f), platformSprite,
            new Color(0.5f, 0.5f, 0.4f));

        // Moving platform 3 — vertical, leads to final mob arena
        CreateMovingPlatform(zone4.transform, "Z4_MovPlat_3",
            new Vector3(z4Start.x + 58f, z4Start.y + 22f, 0f),
            new Vector3(3f, 0.5f, 1f), platformSprite,
            Vector2.up, 2f, 2.5f, 0.25f);

        // Mob arena platform 2 — final fight before Dojo 2
        CreateMobPlatform(zone4.transform, "Z4_MobArena_2",
            new Vector3(z4Start.x + 66f, z4Start.y + 25f, 0f),
            new Vector3(5f, 0.7f, 1f), platformSprite, -2.5f, 2.5f);

        // ═══════════════════════════════════════════════════════════════
        //  DOJO 2 GATEWAY — Final bridge to entrance
        // ═══════════════════════════════════════════════════════════════

        CreateStaticPlatform(gateway.transform, "Dojo2_Gateway_Bridge",
            new Vector3(DOJO2_POS.x - 5f, DOJO2_POS.y - 1f, 0f),
            new Vector3(8f, 1f, 1f), platformSprite,
            new Color(0.3f, 0.65f, 0.4f));

        // Final connecting jump platform from Z4 to gateway
        CreateStaticPlatform(gateway.transform, "Dojo2_Approach_Ledge",
            new Vector3(z4Start.x + 74f, z4Start.y + 28f, 0f),
            new Vector3(3.5f, 0.5f, 1f), platformSprite,
            new Color(0.4f, 0.6f, 0.4f));

        Debug.Log("[Dojo2ParkourBuilder V2] Complex parkour section built! " +
            $"Base ({BASE_POS.x}, {BASE_POS.y}) → Dojo 2 ({DOJO2_POS.x}, {DOJO2_POS.y}). " +
            "4 Zones, ~30 platforms, 9+ hazards.");
        Selection.activeGameObject = root;
    }

    // ══════════════════════════════════════════════════════════════════
    //  FACTORY METHODS
    // ══════════════════════════════════════════════════════════════════

    private static GameObject CreateContainer(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        return go;
    }

    private static GameObject CreateStaticPlatform(Transform parent, string name, Vector3 pos, Vector3 scale, Sprite sprite, Color tint)
    {
        return CreateBasePlatformGO(parent, name, pos, scale, sprite, tint);
    }

    private static GameObject CreateMobPlatform(Transform parent, string name, Vector3 pos, Vector3 scale, Sprite sprite, float leftOff, float rightOff)
    {
        GameObject go = CreateBasePlatformGO(parent, name, pos, scale, sprite, new Color(0.2f, 0.5f, 0.8f, 1f));
        FloatingMobPlatform fmp = go.AddComponent<FloatingMobPlatform>();
        fmp.leftPatrolOffset = leftOff;
        fmp.rightPatrolOffset = rightOff;
        return go;
    }

    private static GameObject CreateCrumblingPlatform(Transform parent, string name, Vector3 pos, Vector3 scale, Sprite sprite)
    {
        GameObject go = CreateBasePlatformGO(parent, name, pos, scale, sprite, new Color(0.9f, 0.45f, 0.2f, 1f));

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        FallingCrumblingPlatform fcp = go.AddComponent<FallingCrumblingPlatform>();
        fcp.shakeDuration = 0.35f;
        fcp.fallDelay = 0.5f;
        fcp.respawnDelay = 4f;

        return go;
    }

    private static GameObject CreateRotatingHazard(Transform parent, string name, Vector3 pos, float speed, bool cw, int count, float spacing)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;

        RotatingHazardChain rhc = go.AddComponent<RotatingHazardChain>();
        rhc.rotationSpeed = speed;
        rhc.clockwise = cw;
        rhc.hazardCount = count;
        rhc.spacing = spacing;
        rhc.GenerateHazardChain();

        return go;
    }

    private static GameObject CreateMovingPlatform(Transform parent, string name, Vector3 pos, Vector3 scale, Sprite sprite, Vector2 axis, float dist, float period, float phase)
    {
        GameObject go = CreateBasePlatformGO(parent, name, pos, scale, sprite, new Color(0.3f, 0.7f, 0.5f, 1f));

        ParkourMovingPlatform mp = go.AddComponent<ParkourMovingPlatform>();
        mp.moveAxis = axis;
        mp.moveDistance = dist;
        mp.period = period;
        mp.phaseOffset = phase;

        return go;
    }

    private static void CreateDamageZone(Transform parent, string name, Vector3 pos, Vector2 size, float dps, Transform respawnPoint)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;

        DamageZone dz = go.AddComponent<DamageZone>();
        dz.damagePerSecond = dps;
        dz.entryDamage = 25;
        dz.respawnPoint = respawnPoint;

        // Visual indicator
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreatePlatformSprite();
        sr.color = new Color(1f, 0.15f, 0.05f, 0.25f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        sr.sortingOrder = -1;
    }

    private static void CreateWindGustZone(Transform parent, string name, Vector3 pos, Vector2 size, Vector2 dir, float force)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;

        WindGustZone wgz = go.AddComponent<WindGustZone>();
        wgz.windDirection = dir;
        wgz.windForce = force;
        wgz.isGusting = true;
        wgz.gustPeriod = 2.5f;
    }

    private static GameObject CreateBasePlatformGO(Transform parent, string name, Vector3 pos, Vector3 scale, Sprite sprite, Color tint)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.tag = "Ground";

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        return go;
    }

    private static Sprite CreatePlatformSprite()
    {
        int w = 32, h = 32;
        Texture2D tex = new Texture2D(w, h);
        Color border = new Color(0.85f, 0.85f, 0.85f, 1f);
        Color fill = Color.white;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    tex.SetPixel(x, y, border);
                else
                    tex.SetPixel(x, y, fill);
            }
        }
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32);
    }
}
#endif
