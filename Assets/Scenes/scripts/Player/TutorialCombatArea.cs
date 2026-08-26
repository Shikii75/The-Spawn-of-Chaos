using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tutorial Combat Area - Manages the procedural Chaos Shade mob encounters at the "fight" tutorial location.
/// Spawns code-generated shadow mobs, tracks combat progress, updates the prompt dynamically,
/// and completes the waypoint once all mobs are defeated.
/// </summary>
[AddComponentMenu("Tutorial/Tutorial Combat Area")]
public class TutorialCombatArea : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Number of procedural Chaos Shade mobs to spawn in this area.")]
    public int mobCount = 2;

    [Tooltip("Horizontal spacing between spawned mobs.")]
    public float spawnSpacing = 2.4f;

    [Tooltip("Proximity distance from player to trigger mob awakening.")]
    public float triggerRadius = 6.5f;

    [Header("State")]
    public bool isEncounterStarted = false;
    public bool isEncounterCompleted = false;

    private readonly List<TutorialShadowMob> activeMobs = new List<TutorialShadowMob>();
    private NyxarisTutorialWaypoint waypointComp;
    private Transform playerTransform;

    void Awake()
    {
        waypointComp = GetComponent<NyxarisTutorialWaypoint>();
    }

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        if (move.Instance != null) playerTransform = move.Instance.transform;
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    void Update()
    {
        if (isEncounterCompleted) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (!isEncounterStarted && dist <= triggerRadius)
        {
            StartCombatEncounter();
        }
    }

    /// <summary>
    /// Spawns the code-generated Chaos Shade mobs.
    /// </summary>
    public void StartCombatEncounter()
    {
        if (isEncounterStarted || isEncounterCompleted) return;
        isEncounterStarted = true;

        activeMobs.Clear();

        for (int i = 0; i < mobCount; i++)
        {
            Vector3 spawnPos = transform.position + new Vector3((i + 1) * spawnSpacing, 0f, 0f);

            GameObject mobGO = new GameObject($"ChaosShade_Mob_{i + 1}");
            mobGO.transform.position = spawnPos;

            TutorialShadowMob mob = mobGO.AddComponent<TutorialShadowMob>();
            mob.Setup(this, spawnPos);
            activeMobs.Add(mob);
        }

        UpdatePromptBadge();
        Debug.Log($"<color=#D47BFF>[TutorialCombatArea] Spawned {mobCount} procedural Chaos Shade mobs at '{gameObject.name}'.</color>");
    }

    public void OnMobDefeated(TutorialShadowMob mob)
    {
        if (isEncounterCompleted) return;

        if (activeMobs.Contains(mob))
        {
            activeMobs.Remove(mob);
        }

        if (activeMobs.Count == 0)
        {
            CompleteCombatEncounter();
        }
        else
        {
            UpdatePromptBadge();
        }
    }

    private void UpdatePromptBadge()
    {
        if (isEncounterCompleted) return;

        int remaining = activeMobs.Count;
        string badge = remaining > 1 ? $"Press [J] to Attack (Defeat {remaining} Chaos Shades)" : $"Press [J] to Attack (Defeat 1 Chaos Shade)";

        if (waypointComp != null)
        {
            waypointComp.promptBadgeText = badge;
        }

        if (NyxarisOrbGuide.Instance != null && NyxarisOrbGuide.Instance.currentState == NyxarisOrbGuide.GuideState.LeadingWaypoint)
        {
            NyxarisOrbGuide.Instance.ShowDialogue("Corrupted shadows ahead! Draw your weapon and strike them down!", badge);
        }
    }

    private void CompleteCombatEncounter()
    {
        if (isEncounterCompleted) return;
        isEncounterCompleted = true;

        if (waypointComp != null)
        {
            waypointComp.MarkCompleted();
        }

        if (NyxarisOrbGuide.Instance != null)
        {
            NyxarisOrbGuide.Instance.MorphIntoFoxForm();
        }

        Debug.Log("<color=#55FF88>[TutorialCombatArea] All tutorial Chaos Shade mobs defeated! Waypoint completed.</color>");
    }

    public bool AreAllMobsDefeated()
    {
        return isEncounterCompleted || (isEncounterStarted && activeMobs.Count == 0);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        Gizmos.color = Color.red;
        for (int i = 0; i < mobCount; i++)
        {
            Vector3 pos = transform.position + new Vector3((i + 1) * spawnSpacing, 0.9f, 0f);
            Gizmos.DrawWireCube(pos, new Vector3(1.1f, 1.8f, 1f));
        }
    }
#endif
}
