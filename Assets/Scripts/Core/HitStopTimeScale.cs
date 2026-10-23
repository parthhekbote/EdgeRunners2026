using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cyberpunk.Core
{
    /// <summary>
    /// Shared bookkeeping for short time-scale dips (hit-stop, kill freezes).
    ///
    /// Several systems used to do "prev = Time.timeScale; Time.timeScale = slow; wait; Time.timeScale = prev".
    /// When two dips overlap, or one is restarted mid-way, the second one saves the already slowed value as its
    /// "previous" scale and restores that, which leaves the game stuck in slow motion. Here the real scale is
    /// remembered once, when the first dip starts, and put back only when the last one ends.
    /// </summary>
    public static class HitStopTimeScale
    {
        private const float StaleSeconds = 4f;

        private static int _depth;
        private static float _baseline = 1f;
        private static float _lastBeginRealtime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _depth = 0;
            _baseline = 1f;

            // The editor keeps Time.timeScale between play sessions, so a freeze that was cut short last time
            // (stop pressed mid hit-stop) would start the next session in slow motion. Every session begins at full speed.
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // A coroutine that dies with its scene never calls End(); drop the dip and restore the real scale
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_depth > 0) ForceRestore();
        }

        public static void Begin(float scale)
        {
            // A dip that never ended (its owner was destroyed) must not poison the next one
            if (_depth > 0 && Time.realtimeSinceStartup - _lastBeginRealtime > StaleSeconds) ForceRestore();

            if (_depth == 0) _baseline = Mathf.Clamp(Time.timeScale, 0f, 1f) < 0.0001f ? 1f : Time.timeScale;
            _depth++;
            _lastBeginRealtime = Time.realtimeSinceStartup;
            Time.timeScale = Mathf.Min(Time.timeScale, scale);
        }

        public static void End()
        {
            if (_depth <= 0) return;
            _depth--;
            if (_depth == 0) Time.timeScale = _baseline;
        }

        public static void ForceRestore()
        {
            if (_depth > 0) Time.timeScale = _baseline;
            _depth = 0;
        }
    }
}
