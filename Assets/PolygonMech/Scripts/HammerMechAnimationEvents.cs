using System;
using UnityEngine;
using UnityEngine.Events;

namespace PolygonMech
{
    /// <summary>
    /// Handles Animation Events fired by the Hammer Warrior animations
    /// (FootL, FootR, Hit, Land, Shoot, WeaponSwitch) on the Hammer Mech.
    /// Prevents 'has no receiver' errors and provides audio/FX hooks.
    /// </summary>
    public class HammerMechAnimationEvents : MonoBehaviour
    {
        [Header("Event Callbacks")]
        public UnityEvent onFootL = new UnityEvent();
        public UnityEvent onFootR = new UnityEvent();
        public UnityEvent onHit = new UnityEvent();
        public UnityEvent onLand = new UnityEvent();
        public UnityEvent onShoot = new UnityEvent();
        public UnityEvent onWeaponSwitch = new UnityEvent();

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] footstepSounds;
        [SerializeField] private AudioClip[] hammerImpactSounds;
        [SerializeField] private AudioClip[] landSounds;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        // Animation Event: FootL
        public void FootL()
        {
            onFootL.Invoke();
            if (footstepSounds != null && footstepSounds.Length > 0)
            {
                PlayRandomSound(footstepSounds, 0.5f);
            }
            else
            {
                PlaySynthesizedMechFootstep(0.75f);
            }
        }

        // Animation Event: FootR
        public void FootR()
        {
            onFootR.Invoke();
            if (footstepSounds != null && footstepSounds.Length > 0)
            {
                PlayRandomSound(footstepSounds, 0.5f);
            }
            else
            {
                PlaySynthesizedMechFootstep(0.75f);
            }
        }

        private void PlaySynthesizedMechFootstep(float volume)
        {
            var hammerAI = GetComponent<HammerMechAI>() ?? GetComponentInParent<HammerMechAI>();
            if (hammerAI != null)
            {
                hammerAI.PlayMechFootstep(volume);
                return;
            }

            if (audioSource == null) return;
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(MechAudioSynth.GetFootstepStomp(), volume);
        }

        // Animation Event: Hit
        public void Hit()
        {
            onHit.Invoke();
            PlayRandomSound(hammerImpactSounds, 0.8f);
        }

        // Animation Event: Land
        public void Land()
        {
            onLand.Invoke();
            PlayRandomSound(landSounds, 0.6f);
        }

        // Animation Event: Shoot
        public void Shoot()
        {
            onShoot.Invoke();
        }

        // Animation Event: WeaponSwitch
        public void WeaponSwitch()
        {
            onWeaponSwitch.Invoke();
        }

        private void PlayRandomSound(AudioClip[] clips, float volume)
        {
            if (audioSource == null || clips == null || clips.Length == 0) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
