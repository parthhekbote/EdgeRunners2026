using System;
using UnityEngine;
using SyntyCombined.Cutscene;
using Alita;

namespace SyntyCombined.Cutscene
{
    /// <summary>
    /// Centralized registry for cutscene state.
    /// When any cutscene is playing, all gameplay HUDs, prompts, reticles,
    /// dialogue boxes, and overlays are automatically suppressed to ensure
    /// pristine, unobstructed cinematic presentation.
    /// </summary>
    public static class GlobalCutsceneState
    {
        private static int _activeCutscenes = 0;
        private static bool _manualOverride = false;

        public static bool IsCutsceneActive
        {
            get
            {
                if (_manualOverride) return true;
                if (_activeCutscenes > 0) return true;

                // Check BlackMarketArrivalCutsceneDirector
                if (BlackMarketArrivalCutsceneDirector.Instance != null && BlackMarketArrivalCutsceneDirector.Instance.isPlaying)
                {
                    return true;
                }

                // Check CelestialVisionCutsceneDirector
                if (CelestialVisionCutsceneDirector.Instance != null && CelestialVisionCutsceneDirector.Instance.isPlaying)
                {
                    return true;
                }

                // Check AlitaCutsceneController (Scrapyard Discovery)
                if (AlitaCutsceneController.Instance != null && AlitaCutsceneController.Instance.currentPhase != CutscenePhase.Finished)
                {
                    return true;
                }

                // Check AlitaAwakeningCutsceneController
                if (AlitaAwakeningCutsceneController.Instance != null && AlitaAwakeningCutsceneController.Instance.isPlaying)
                {
                    return true;
                }

                // Check AlitaMoonBattleCutsceneDirector
                if (AlitaMoonBattleCutsceneDirector.Instance != null && AlitaMoonBattleCutsceneDirector.Instance.isPlaying)
                {
                    return true;
                }

                // Check AlitaTronAwakeningDirector
                if (AlitaTronAwakeningDirector.Instance != null && AlitaTronAwakeningDirector.Instance.isPlaying)
                {
                    return true;
                }

                return false;
            }
        }

        public static void SetManualCutsceneActive(bool active)
        {
            _manualOverride = active;
        }

        public static void RegisterCutsceneStart()
        {
            _activeCutscenes++;
        }

        public static void RegisterCutsceneEnd()
        {
            _activeCutscenes = Math.Max(0, _activeCutscenes - 1);
        }
    }
}
