using UnityEngine;

/// <summary>
/// PlayerNinjaVoice (Deprecated / Neutralized) - Redirects all voice calls to PlayerMageVoiceController
/// to guarantee all characters share the Mage voice.
/// </summary>
public class PlayerNinjaVoice : MonoBehaviour
{
    private SpawnOfChaos.Entities.PlayerMageVoiceController voiceController;

    void Awake()
    {
        voiceController = SpawnOfChaos.Entities.PlayerMageVoiceController.EnsureAttached(gameObject);
    }

    public void PlayAttackVoice()
    {
        if (voiceController != null) voiceController.PlayAttackVoice();
    }

    public void PlayJumpVoice()
    {
        if (voiceController != null) voiceController.PlayJumpVoice();
    }

    public void PlayHurtVoice()
    {
        if (voiceController != null) voiceController.PlayHurtVoice();
    }

    public void PlayDashVoice() { }
    public void PlayDeathVoice()
    {
        if (voiceController != null) voiceController.PlayHurtVoice();
    }
}
