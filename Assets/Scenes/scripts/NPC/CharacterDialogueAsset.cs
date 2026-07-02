using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterDialogue", menuName = "Dialogue/Character Dialogue Asset")]
public class CharacterDialogueAsset : ScriptableObject
{
    [Header("Character Identity")]
    [Tooltip("The display name of the NPC.")]
    public string characterName = "Villager";
    
    [Tooltip("The portrait of the NPC displayed in the dialogue panel.")]
    public Sprite portrait;

    [Header("Dialogue Content")]
    [TextArea(3, 8)]
    [Tooltip("The dialogue lines for the character.")]
    public string[] dialogueLines;

    [Header("Custom Styling")]
    [Tooltip("Text color for the character's name.")]
    public Color nameColor = new Color(1f, 0.78f, 0f, 1f); // default gold/yellow
    
    [Tooltip("Text color for the dialogue body text.")]
    public Color textColor = Color.white;
    
    [Tooltip("Typewriter speed interval in seconds per character (lower is faster).")]
    public float typingSpeed = 0.02f;

    [Header("Audio (Optional)")]
    [Tooltip("Sound played when each letter is typed.")]
    public AudioClip textBeepSound;
    
    [Range(0.5f, 2.0f)]
    [Tooltip("Audio pitch modifier for the dialogue text beeps.")]
    public float voicePitch = 1.0f;
}
