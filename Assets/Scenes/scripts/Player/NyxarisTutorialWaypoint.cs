using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tutorial Waypoint component attached to each child object in "Nyxaris Guide Locations".
/// Accurately recognizes child names and provides tailored dialogues, prompt badges, and action triggers.
/// </summary>
[AddComponentMenu("Tutorial/Nyxaris Tutorial Waypoint")]
public class NyxarisTutorialWaypoint : MonoBehaviour
{
    public enum TutorialActionType
    {
        MoveHorizontal,
        Run,
        Jump,
        BlobForm,
        Dash,
        Attack,
        DialogueOnly
    }

    [Header("Tutorial Dialogue & Prompt")]
    [Tooltip("Sassy/lore dialogue spoken by Nyxaris when entering this location.")]
    [TextArea(2, 5)]
    public string dialogueText = "Do I have to teach you how to walk?";

    [Tooltip("Button prompt badge displayed below dialogue.")]
    public string promptBadgeText = "Press [A] / [D] to move";

    [Header("Action Requirement")]
    public TutorialActionType requiredAction = TutorialActionType.MoveHorizontal;

    [Header("Proximity Range & Lead Target")]
    [Tooltip("Proximity radius around this location that triggers the tutorial.")]
    public float triggerRadius = 5.5f;

    [Tooltip("Offset where Nyxaris will hover to lead and demonstrate.")]
    public Vector3 leadHoverOffset = new Vector3(1.4f, 2.0f, 0f);

    [Header("State")]
    public bool isCompleted = false;
    public bool hasTriggered = false;

    [Header("Events")]
    public UnityEvent onWaypointActivated;
    public UnityEvent onWaypointCompleted;

    public Vector3 TargetLeadPosition => transform.position + leadHoverOffset;

    void Reset()
    {
        InferDefaultsFromName();
    }

    void Awake()
    {
        InferDefaultsFromName();
    }

    public void InferDefaultsFromName()
    {
        string n = gameObject.name.ToLower().Replace(" ", "").Replace("_", "").Replace("'", "");

        // 1. Walking / Start
        if (n.Contains("startscene") || n.Contains("startwalk") || n.Contains("startmove"))
        {
            dialogueText = "Do I have to teach you how to walk?";
            promptBadgeText = "Press [A] / [D] to move";
            requiredAction = TutorialActionType.MoveHorizontal;
        }
        // 2. Jumping
        else if (n.Contains("startjump") || n == "jump")
        {
            dialogueText = "Mind the gap. Try not to fall on your face, mortal.";
            promptBadgeText = "Press [Space] to jump";
            requiredAction = TutorialActionType.Jump;
        }
        // 3. Trust fall
        else if (n.Contains("trustfall") || n.Contains("fall"))
        {
            dialogueText = "A sheer drop ahead. Take a leap of faith into the depths.";
            promptBadgeText = "Drop down to proceed";
            requiredAction = TutorialActionType.DialogueOnly;
        }
        // 4. Lore Dump
        else if (n.Contains("loredump") || n.Contains("lore"))
        {
            dialogueText = "This timeline was fractured when the chaos broke free. Stay alert.";
            promptBadgeText = "Proceed forward";
            requiredAction = TutorialActionType.DialogueOnly;
        }
        // 5. Platforming
        else if (n.Contains("startplatform") || n.Contains("platform"))
        {
            dialogueText = "Time your leaps across these floating stones carefully.";
            promptBadgeText = "Jump across platforms";
            requiredAction = TutorialActionType.Jump;
        }
        // 6. Liquid Hazard Avoidance
        else if (n.Contains("avoidtheliquid") || n.Contains("liquid") || n.Contains("hazard"))
        {
            dialogueText = "Avoid that purple corruption below! It will dissolve your flesh on contact.";
            promptBadgeText = "Jump over the toxic liquid";
            requiredAction = TutorialActionType.DialogueOnly;
        }
        // 7. Running
        else if (n.Contains("howyourun") || n.Contains("run") || n.Contains("sprint"))
        {
            dialogueText = "This runway is unstable and crumbling! You'll have to run!";
            promptBadgeText = "Double-tap [A] / [D] to run";
            requiredAction = TutorialActionType.Run;
        }
        // 8. Blob Form
        else if (n.Contains("howyoublob") || n.Contains("blob") || n.Contains("crawl"))
        {
            dialogueText = "This rock tunnel is too low for human form. Hold [M] to slip through as a blob!";
            promptBadgeText = "Hold [M] for Blob Form";
            requiredAction = TutorialActionType.BlobForm;
        }
        // 9. Meet Genbu
        else if (n.Contains("meetgenbu") || n.Contains("genbu") || n.Contains("turtle"))
        {
            dialogueText = "That ancient spirit ahead is Genbu the Ferryman. Speak with him.";
            promptBadgeText = "Approach Genbu & press [E]";
            requiredAction = TutorialActionType.DialogueOnly;
        }
        // 10. Fight / Combat
        else if (n.Contains("fight") || n.Contains("combat") || n.Contains("attack") || n.Contains("strike"))
        {
            dialogueText = "Corrupted shadows ahead! Draw your weapon and strike them down!";
            promptBadgeText = "Press [J] to attack (Defeat Chaos Shades)";
            requiredAction = TutorialActionType.Attack;
            if (GetComponent<TutorialCombatArea>() == null)
            {
                gameObject.AddComponent<TutorialCombatArea>();
            }
        }
        // 11. Dash
        else if (n.Contains("dash") || n.Contains("dodge"))
        {
            dialogueText = "Phase through danger. Quick on your feet!";
            promptBadgeText = "Press [Shift] to dash";
            requiredAction = TutorialActionType.Dash;
        }
    }

    public bool IsPlayerInRange(Vector3 playerPosition)
    {
        return Vector2.Distance(transform.position, playerPosition) <= triggerRadius;
    }

    public bool CheckActionCompleted(Rigidbody2D playerRb, move playerMove)
    {
        if (isCompleted) return true;

        switch (requiredAction)
        {
            case TutorialActionType.MoveHorizontal:
                float hInput = Mathf.Abs(Input.GetAxisRaw("Horizontal"));
                float hVel = playerRb != null ? Mathf.Abs(playerRb.linearVelocity.x) : 0f;
                return hInput > 0.1f || hVel > 0.5f;

            case TutorialActionType.Run:
                float runVel = playerRb != null ? Mathf.Abs(playerRb.linearVelocity.x) : 0f;
                return runVel > 6.5f || (playerMove != null && playerMove.moveSpeed > 7f);

            case TutorialActionType.Jump:
                return Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W);

            case TutorialActionType.BlobForm:
                return Input.GetKey(KeyCode.M) || Input.GetKeyDown(KeyCode.M);

            case TutorialActionType.Dash:
                return (playerMove != null && playerMove.IsDashing) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.K);

            case TutorialActionType.Attack:
                var combatArea = GetComponent<TutorialCombatArea>();
                if (combatArea != null)
                {
                    return combatArea.AreAllMobsDefeated();
                }
                return Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0);

            case TutorialActionType.DialogueOnly:
            default:
                return false;
        }
    }

    public void MarkCompleted()
    {
        if (isCompleted) return;
        isCompleted = true;
        onWaypointCompleted?.Invoke();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = isCompleted ? new Color(0.2f, 1f, 0.4f, 0.35f) : new Color(0.85f, 0.2f, 1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        Vector3 leadPos = TargetLeadPosition;
        Gizmos.color = new Color(0f, 1f, 1f, 0.85f);
        Gizmos.DrawSphere(leadPos, 0.25f);
        Gizmos.DrawLine(transform.position, leadPos);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, $"[Location: {gameObject.name}]");
#endif
    }
#endif
}
