using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpawnOfChaos.Entities
{
    /// <summary>
    /// PlayerMageVoiceController - Centralized voice audio manager for both BasePlayer and Mage.
    /// Guarantees that both character models share the EXACT SAME voice assets:
    /// - Jump exertions (Voice/mage/jump)
    /// - Attack vocalizations (Voice/mage/attack)
    /// - Hurt grunts (Voice/mage/hurt)
    /// - Dialogue chatter (Voice/mage/talk)
    /// - Confused murmurs (Voice/mage/confused)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerMageVoiceController : MonoBehaviour
    {
        public static PlayerMageVoiceController Instance { get; private set; }

        [Header("Voice Clips")]
        public AudioClip[] jumpVoiceClips;
        public AudioClip[] attackVoiceClips;
        public AudioClip[] hurtVoiceClips;
        public AudioClip[] talkVoiceClips;
        public AudioClip[] confusedVoiceClips;

        [Header("Confused Animation")]
        [Tooltip("Animation state name to play alongside the confused mage voice.")]
        public string confusedAnimationState = "MageConfused";

        [Header("Voice Volume")]
        [Range(0f, 1f)] public float voiceVolume = 1.0f;

        private AudioSource voiceSource;
        private float lastAttackVoiceTime = -10f;
        private float lastJumpVoiceTime = -10f;
        private float lastHurtVoiceTime = -10f;

        private const float ATTACK_VOICE_COOLDOWN = 0.22f;
        private const float JUMP_VOICE_COOLDOWN = 0.25f;
        private const float HURT_VOICE_COOLDOWN = 0.35f;

        void Awake()
        {
            Instance = this;
            voiceSource = GetComponent<AudioSource>();
            if (voiceSource == null)
            {
                voiceSource = gameObject.AddComponent<AudioSource>();
            }

            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            voiceSource.spatialBlend = 0f; // Pure 2D stereo sound for crisp player vocals

            LoadAllVoiceClips();
        }

        public void LoadAllVoiceClips()
        {
            if (jumpVoiceClips == null || jumpVoiceClips.Length == 0)
                jumpVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/jump");

            if (attackVoiceClips == null || attackVoiceClips.Length == 0)
                attackVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/attack");

            if (hurtVoiceClips == null || hurtVoiceClips.Length == 0)
                hurtVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/hurt");

            if (talkVoiceClips == null || talkVoiceClips.Length == 0)
                talkVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/talk");

            if (confusedVoiceClips == null || confusedVoiceClips.Length == 0)
                confusedVoiceClips = Resources.LoadAll<AudioClip>("Voice/mage/confused");
        }

        private float GetSFXVolume()
        {
            float sfxVol = 1.0f;
            if (AudioManager.Instance != null)
            {
                sfxVol = AudioManager.Instance.GetRealSFXVolume();
            }
            return sfxVol * voiceVolume;
        }

        public void PlayJumpVoice()
        {
            if (Time.time - lastJumpVoiceTime < JUMP_VOICE_COOLDOWN) return;
            lastJumpVoiceTime = Time.time;

            if (jumpVoiceClips == null || jumpVoiceClips.Length == 0) LoadAllVoiceClips();
            if (jumpVoiceClips == null || jumpVoiceClips.Length == 0) return;

            AudioClip clip = jumpVoiceClips[UnityEngine.Random.Range(0, jumpVoiceClips.Length)];
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, GetSFXVolume());
            }
        }

        public void PlayAttackVoice()
        {
            if (Time.time - lastAttackVoiceTime < ATTACK_VOICE_COOLDOWN) return;
            lastAttackVoiceTime = Time.time;

            if (attackVoiceClips == null || attackVoiceClips.Length == 0) LoadAllVoiceClips();
            if (attackVoiceClips == null || attackVoiceClips.Length == 0) return;

            AudioClip clip = attackVoiceClips[UnityEngine.Random.Range(0, attackVoiceClips.Length)];
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, GetSFXVolume());
            }
        }

        public void PlayHurtVoice()
        {
            if (Time.time - lastHurtVoiceTime < HURT_VOICE_COOLDOWN) return;
            lastHurtVoiceTime = Time.time;

            if (hurtVoiceClips == null || hurtVoiceClips.Length == 0) LoadAllVoiceClips();
            if (hurtVoiceClips == null || hurtVoiceClips.Length == 0) return;

            AudioClip clip = hurtVoiceClips[UnityEngine.Random.Range(0, hurtVoiceClips.Length)];
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, GetSFXVolume());
            }
        }

        public void PlayTalkVoice()
        {
            if (talkVoiceClips == null || talkVoiceClips.Length == 0) LoadAllVoiceClips();
            if (talkVoiceClips == null || talkVoiceClips.Length == 0) return;

            AudioClip clip = talkVoiceClips[UnityEngine.Random.Range(0, talkVoiceClips.Length)];
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, GetSFXVolume());
            }
        }

        public void PlayConfusedVoice()
        {
            if (confusedVoiceClips == null || confusedVoiceClips.Length == 0) LoadAllVoiceClips();
            if (confusedVoiceClips == null || confusedVoiceClips.Length == 0) return;

            AudioClip clip = confusedVoiceClips[UnityEngine.Random.Range(0, confusedVoiceClips.Length)];
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, GetSFXVolume());
            }

            TryPlayConfusedAnimation();
        }

        private void TryPlayConfusedAnimation()
        {
            Animator animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            string[] candidateStates =
            {
                confusedAnimationState,
                "Confused",
                "Mage_Confused",
                "mageConfused"
            };

            foreach (string stateName in candidateStates)
            {
                if (string.IsNullOrWhiteSpace(stateName)) continue;
                if (!HasAnimationState(animator, stateName)) continue;

                animator.Play(stateName, 0, 0f);
                StartCoroutine(RestoreIdleAfterConfusedAnimation(animator, stateName));
                return;
            }
        }

        private static bool HasAnimationState(Animator animator, string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name.Equals(stateName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator RestoreIdleAfterConfusedAnimation(Animator animator, string activeStateName)
        {
            if (animator == null)
            {
                yield break;
            }

            float waitTime = GetAnimationLength(animator, activeStateName);
            yield return new WaitForSeconds(waitTime);

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                yield break;
            }

            if (HasAnimationState(animator, "Idle"))
            {
                animator.Play("Idle", 0, 0f);
            }
            else if (HasAnimationState(animator, "idle"))
            {
                animator.Play("idle", 0, 0f);
            }
        }

        private static float GetAnimationLength(Animator animator, string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            {
                return 0.6f;
            }

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name.Equals(stateName, StringComparison.OrdinalIgnoreCase))
                {
                    return Mathf.Max(0.2f, clip.length);
                }
            }

            return 0.6f;
        }

        /// <summary>
        /// Ensures the voice controller is attached to any given player object.
        /// </summary>
        public static PlayerMageVoiceController EnsureAttached(GameObject player)
        {
            if (player == null) return null;
            var vc = player.GetComponent<PlayerMageVoiceController>();
            if (vc == null)
            {
                vc = player.AddComponent<PlayerMageVoiceController>();
            }
            return vc;
        }
    }
}
