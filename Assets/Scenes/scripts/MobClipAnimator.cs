using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Animator-controller-free animation player for 2D or 3D mobs.
/// Assign clips in the Inspector and call the named methods from any AI script.
/// It supports looping locomotion clips, one-shot actions, blending, and any
/// number of custom named slots without requiring an Animator Controller.
/// </summary>
[DisallowMultipleComponent]
public class MobClipAnimator : MonoBehaviour
{
    [Serializable]
    public class ClipSlot
    {
        [Tooltip("The name used by Play(). For example: Fly, Crawl, Charge, Roar.")]
        public string name;
        public AnimationClip clip;
        public bool loop;
        [Min(0f)] public float blendDuration = 0.12f;
    }

    [Header("Core Slots")]
    public ClipSlot idle = new ClipSlot { name = "Idle", loop = true };
    public ClipSlot move = new ClipSlot { name = "Move", loop = true };
    public ClipSlot attack = new ClipSlot { name = "Attack", loop = false };
    public ClipSlot hit = new ClipSlot { name = "Hit", loop = false };
    public ClipSlot death = new ClipSlot { name = "Death", loop = false };

    [Header("Optional Mob-Specific Slots")]
    [Tooltip("Add as many actions as needed: Fly, Crawl, Dash, Burrow, Cast, Roar, etc.")]
    public ClipSlot[] customSlots;

    [Header("Playback")]
    [Tooltip("If true, Idle plays automatically when this object enables.")]
    public bool playIdleOnEnable = true;
    public bool useUnscaledTime;

    public string CurrentState { get; private set; }
    public bool IsPlayingOneShot => currentSlot != null && !currentSlot.loop && !HasFinished;
    public bool HasFinished => currentSlot == null || currentSlot.loop || currentClip == null || currentPlayable.GetTime() >= currentClip.length;

    private Animator animator;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable currentPlayable;
    private AnimationClipPlayable previousPlayable;
    private AnimationClip currentClip;
    private ClipSlot currentSlot;
    private int activeInput;
    private float blendElapsed;
    private float blendDuration;
    private bool isBlending;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = gameObject.AddComponent<Animator>();

        graph = PlayableGraph.Create(name + "_MobClipGraph");
        graph.SetTimeUpdateMode(useUnscaledTime ? DirectorUpdateMode.Manual : DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 2, true);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "MobAnimation", animator);
        output.SetSourcePlayable(mixer);
        graph.Play();
    }

    private void OnEnable()
    {
        if (graph.IsValid() && playIdleOnEnable && currentSlot == null)
            PlayIdle();
    }

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (useUnscaledTime && graph.IsValid())
            graph.Evaluate(deltaTime);

        if (!graph.IsValid() || currentSlot == null || currentClip == null)
            return;

        UpdateBlend(deltaTime);

        // Explicit looping keeps behavior consistent even when a source clip was imported as non-looping.
        if (currentSlot.loop && currentClip.length > 0f && currentPlayable.GetTime() >= currentClip.length)
            currentPlayable.SetTime(currentPlayable.GetTime() % currentClip.length);
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }

    public bool PlayIdle() => Play(idle);
    public bool PlayMove() => Play(move);
    public bool PlayAttack() => Play(attack);
    public bool PlayHit() => Play(hit);
    public bool PlayDeath() => Play(death, true);

    /// <summary>Plays an Inspector slot by name. Names are case-insensitive.</summary>
    public bool Play(string slotName, bool restartIfAlreadyPlaying = false)
    {
        return Play(FindSlot(slotName), restartIfAlreadyPlaying);
    }

    /// <summary>Convenience for AI movement: true plays Move, false plays Idle.</summary>
    public void SetMoving(bool isMoving)
    {
        Play(isMoving ? move : idle);
    }

    public bool Play(ClipSlot slot, bool restartIfAlreadyPlaying = false)
    {
        if (slot == null || slot.clip == null || !graph.IsValid())
            return false;

        if (currentSlot == slot && !restartIfAlreadyPlaying)
            return true;

        int nextInput = 1 - activeInput;
        if (mixer.GetInput(nextInput).IsValid())
            graph.DestroySubgraph(mixer.GetInput(nextInput));

        AnimationClipPlayable nextPlayable = AnimationClipPlayable.Create(graph, slot.clip);
        nextPlayable.SetTime(0d);
        nextPlayable.SetSpeed(1d);
        graph.Connect(nextPlayable, 0, mixer, nextInput);
        mixer.SetInputWeight(nextInput, currentSlot == null ? 1f : 0f);

        previousPlayable = currentPlayable;
        currentPlayable = nextPlayable;
        currentClip = slot.clip;
        currentSlot = slot;
        CurrentState = slot.name;
        blendElapsed = 0f;
        blendDuration = Mathf.Max(0.001f, slot.blendDuration);
        isBlending = previousPlayable.IsValid();

        if (!isBlending)
        {
            mixer.SetInputWeight(activeInput, 0f);
            mixer.SetInputWeight(nextInput, 1f);
        }

        activeInput = nextInput;
        return true;
    }

    private void UpdateBlend(float deltaTime)
    {
        if (!isBlending)
            return;

        blendElapsed += deltaTime;
        float weight = Mathf.Clamp01(blendElapsed / blendDuration);
        mixer.SetInputWeight(activeInput, weight);
        mixer.SetInputWeight(1 - activeInput, 1f - weight);

        if (weight >= 1f)
        {
            int previousInput = 1 - activeInput;
            if (mixer.GetInput(previousInput).IsValid())
                graph.DestroySubgraph(mixer.GetInput(previousInput));
            mixer.SetInputWeight(previousInput, 0f);
            isBlending = false;
        }
    }

    private ClipSlot FindSlot(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return null;

        ClipSlot[] coreSlots = { idle, move, attack, hit, death };
        foreach (ClipSlot slot in coreSlots)
        {
            if (slot != null && string.Equals(slot.name, slotName, StringComparison.OrdinalIgnoreCase))
                return slot;
        }

        if (customSlots != null)
        {
            foreach (ClipSlot slot in customSlots)
            {
                if (slot != null && string.Equals(slot.name, slotName, StringComparison.OrdinalIgnoreCase))
                    return slot;
            }
        }
        return null;
    }
}
