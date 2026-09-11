using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized Scriptable Dialogue & Expression Configuration for Nyxaris in the Tutorial Level.
/// Edit lines, button prompts, and animated expression keys directly here or override them
/// individually on each NyxarisTutorialWaypoint component in the Unity Inspector!
/// </summary>
public static class TutorialDialogueConfig
{
    [Serializable]
    public struct WaypointDialogueEntry
    {
        public string waypointKey;
        [TextArea(2, 5)]
        public string dialogueText;
        public string promptBadgeText;
        public string expressionKey; // Matches any of the 31 Nyxaris animation folders (e.g. "explaining", "happy", "excited", "cutely_annoyed", "neutral", "thinking", "confidently")
    }

    /// <summary>
    /// Master dialogue script for every waypoint in TutorialScene.
    /// You can freely tweak and expand these entries!
    /// </summary>
    public static readonly Dictionary<string, WaypointDialogueEntry> Entries = new Dictionary<string, WaypointDialogueEntry>(StringComparer.OrdinalIgnoreCase)
    {
        {
            "startscene", new WaypointDialogueEntry
            {
                waypointKey = "startscene",
                dialogueText = "Do I have to teach you how to walk, mortal? Let us see if your legs work.",
                promptBadgeText = "Press [A] / [D] to move",
                expressionKey = "explaining"
            }
        },
        {
            "startjump", new WaypointDialogueEntry
            {
                waypointKey = "startjump",
                dialogueText = "Mind the gap! Channel your weight and leap across.",
                promptBadgeText = "Press [Space] to Jump",
                expressionKey = "neutral"
            }
        },
        {
            "starttrustfall", new WaypointDialogueEntry
            {
                waypointKey = "starttrustfall",
                dialogueText = "A sheer drop ahead into the depths. Take a leap of faith—I will catch your spirit if you falter.",
                promptBadgeText = "Drop down the chasm",
                expressionKey = "confidently"
            }
        },
        {
            "loredump", new WaypointDialogueEntry
            {
                waypointKey = "loredump",
                dialogueText = "This timeline was fractured when the chaos broke free. Stay alert; reality is brittle here.",
                promptBadgeText = "Proceed forward",
                expressionKey = "explaining"
            }
        },
        {
            "startplatform", new WaypointDialogueEntry
            {
                waypointKey = "startplatform",
                dialogueText = "Time your leaps across these floating stones carefully. Precision is everything.",
                promptBadgeText = "Jump across platforms",
                expressionKey = "thinking"
            }
        },
        {
            "avoidtheliquid", new WaypointDialogueEntry
            {
                waypointKey = "avoidtheliquid",
                dialogueText = "Avoid that purple corruption below! It will dissolve your mortal flesh on contact.",
                promptBadgeText = "Jump over the toxic liquid",
                expressionKey = "cutely_upset"
            }
        },
        {
            "howyourun", new WaypointDialogueEntry
            {
                waypointKey = "howyourun",
                dialogueText = "This runway is unstable and crumbling! You'll have to sprint to clear the distance!",
                promptBadgeText = "Double-tap [A] / [D] to Run",
                expressionKey = "excited"
            }
        },
        {
            "howyoublob", new WaypointDialogueEntry
            {
                waypointKey = "howyoublob",
                dialogueText = "This ancient rock tunnel is too low for human form. Disperse into a shadow blob to slip through!",
                promptBadgeText = "Hold [M] for Blob Form",
                expressionKey = "explaining"
            }
        },
        {
            "fight", new WaypointDialogueEntry
            {
                waypointKey = "fight",
                dialogueText = "Corrupted Chaos Shades ahead! Draw your blade and strike them down!",
                promptBadgeText = "Press [J] to Attack (Defeat Chaos Shades)",
                expressionKey = "pissed"
            }
        },
        {
            "postfight", new WaypointDialogueEntry
            {
                waypointKey = "postfight",
                dialogueText = "Splendid blade work, mortal! You wield that steel with surprising grace.",
                promptBadgeText = "Proceed onward to the ferry",
                expressionKey = "happy"
            }
        },
        {
            "meetgenbu", new WaypointDialogueEntry
            {
                waypointKey = "meetgenbu",
                dialogueText = "That ancient spirit ahead is Genbu the Ferryman. Approach him to cross the celestial rift.",
                promptBadgeText = "Approach Genbu & press [E]",
                expressionKey = "neutral"
            }
        },
        {
            "levelcomplete", new WaypointDialogueEntry
            {
                waypointKey = "levelcomplete",
                dialogueText = "Impressive, mortal! You've mastered the fundamentals and survived the fractured rift. Beyond lies the Cherry Blossom Forest—where the true trial begins!",
                promptBadgeText = "Proceed to Cherry Blossom Forest",
                expressionKey = "excited"
            }
        }
    };

    /// <summary>
    /// Finds the dialogue entry matching the object or waypoint name.
    /// </summary>
    public static bool TryGetEntry(string rawName, out WaypointDialogueEntry entry)
    {
        string n = rawName.ToLower().Replace(" ", "").Replace("_", "").Replace("'", "");

        foreach (var kvp in Entries)
        {
            if (n.Contains(kvp.Key) || kvp.Key.Contains(n))
            {
                entry = kvp.Value;
                return true;
            }
        }

        // Fallback default
        entry = new WaypointDialogueEntry
        {
            waypointKey = rawName,
            dialogueText = "Keep moving forward, mortal. The path lies ahead.",
            promptBadgeText = "Proceed forward",
            expressionKey = "neutral"
        };
        return false;
    }
}
