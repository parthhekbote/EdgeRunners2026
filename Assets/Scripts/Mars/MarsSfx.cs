using System.Collections;
using UnityEngine;

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Helpers for the Mars cutscenes. Pitch on a shared AudioSource applies to every clip it is currently playing,
    /// so one-shots that need their own pitch (and fades for looping beds) go through here instead.
    /// </summary>
    public static class MarsSfx
    {
        /// <summary>Plays a clip on its own short-lived source so pitch changes never warble other sounds.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("_MarsSfx");
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.clip = clip;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.3f);
        }

        /// <summary>Fades a source to silence and stops it, so beds and loops end without a click.</summary>
        public static IEnumerator FadeOutAndStop(AudioSource src, float duration)
        {
            if (src == null || !src.isPlaying) yield break;
            float start = src.volume;
            for (float t = 0f; t < duration && src != null; t += Time.unscaledDeltaTime)
            {
                src.volume = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            if (src != null) { src.Stop(); src.volume = start; }
        }
    }
}
