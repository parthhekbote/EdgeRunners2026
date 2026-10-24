using System.Collections;
using UnityEngine;
using Alita.UI;

namespace Cyberpunk.Mars
{
    /// <summary>
    /// The plot twist before the final duel: Switchblade has been piloting Mechatron the whole time, and the only reason
    /// Alita lived through the Pit and the bridge is that he needed her to find the Antimatter core for him.
    /// </summary>
    public partial class MarsArrivalCutsceneDirector
    {
        private struct RevealLine
        {
            public bool alita;
            public string text;
            public int shot; // 0 wide, 1 Mechatron visor, 2 Alita close-up
            public RevealLine(bool alita, string text, int shot) { this.alita = alita; this.text = text; this.shot = shot; }
        }

        private static readonly RevealLine[] RevealScript =
        {
            new RevealLine(false, "Did you think I let you live out of mercy, little ghost?", 1),
            new RevealLine(true,  "That voice. The bridge. You're inside that machine.", 2),
            new RevealLine(false, "Mechatron is my hand, Alita. My eyes. I have worn this body since before your city had a name.", 1),
            new RevealLine(true,  "You let me walk out of the Pit. You let me run. You never wanted me dead. You wanted me to lead you here.", 2),
            new RevealLine(false, "Your Berserker core is the rarest power source in the universe, and the old URM buried it in a grave on Mars. I did not need to hunt you. I needed you to find it.", 1),
            new RevealLine(true,  "Ido died so I could keep it away from you.", 2),
            new RevealLine(false, "Ido died because he stood between me and a throne. Every core I have taken made me stronger. Yours will make me a god.", 1),
            new RevealLine(true,  "You walked across the galaxy to dig up a grave. The core is not in the ground anymore. It is in me.", 2),
            new RevealLine(false, "Then I will cut it out of you and wear it. Mechatron. Engage.", 1),
            new RevealLine(true,  "Come on, then. Let's finish what you started on that bridge.", 0),
        };

        /// <summary>Alita and Mechatron both carry leftover locomotion state from the fight setup, so they would pace in place while talking.</summary>
        private void HoldStandingPoses()
        {
            if (_alitaAnimator != null)
            {
                _alitaAnimator.SetFloat("MoveSpeed", 0f);
                _alitaAnimator.speed = 1f;
            }
            if (_bossAnimator != null)
            {
                // The boss controller drops back to its walking locomotion blend after Idle, so pin it every frame
                _bossAnimator.SetFloat("Speed", 0f);
                _bossAnimator.SetFloat("MoveX", 0f);
                _bossAnimator.SetFloat("MoveZ", 0f);
                if (!_bossAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle") || _bossAnimator.IsInTransition(0))
                {
                    var next = _bossAnimator.GetNextAnimatorStateInfo(0);
                    if (!(_bossAnimator.IsInTransition(0) && next.IsName("Idle")))
                        _bossAnimator.CrossFadeInFixedTime("Idle", 0.15f);
                }
            }
        }

        private const float RevealVoiceSpeed = 1.1f;

        private IEnumerator SwitchbladeRevealDialogue()
        {
            Cyberpunk.Audio.DialogueVoice.Speed = RevealVoiceSpeed;
            if (_bossHealthBar != null)
            {
                _bossHealthBar.bossName = "MECHATRON // SWITCHBLADE";
                _bossHealthBar.bossSub = "TITAN WAR MECH PILOTED BY THE CYBORG OVERLORD";
            }
            SetArrivalHudVisible(false); // minimap, quest text and waypoints would sit on top of the conversation
            var card = SyntyCombined.Cutscene.GotGTitleCardUI.GetOrCreate();
            if (card != null) card.SetLetterboxOnly(true, 0.5f);
            float baseMusic = _musicSource != null ? _musicSource.volume : 0f;
            Vector3 aPos = AlitaCombatStartPos;
            Vector3 bPos = BossCombatStartPos;
            Vector3 mid = (aPos + bPos) * 0.5f;

            foreach (var line in RevealScript)
            {
                string speaker0 = line.alita ? "ALITA" : "SWITCHBLADE";
                var voice = Cyberpunk.Audio.DialogueVoice.Find(speaker0, line.text);
                float typing = line.text.Length * CyberpunkDialogueHUD.GetOrCreate().typeSpeed;
                // Wait for the spoken line itself (checked live below) so it is never cut off; this is only the no-clip fallback
                float seconds = voice != null ? Mathf.Max(typing, voice.length / RevealVoiceSpeed) + 8f : typing + 0.8f + line.text.Length * 0.012f;
                HoldStandingPoses();
                string speaker = line.alita ? "ALITA" : "SWITCHBLADE";
                string title = line.alita ? "MARTIAN CYBORG" : "CYBORG OVERLORD // VIA MECHATRON";
                CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(speaker, line.text, null, title, null, 0.1f); // the HUD keeps the box up until the voice ends, and the next line replaces it
                if (!line.alita && _voiceSource != null && commsChirpAudio != null) _voiceSource.PlayOneShot(commsChirpAudio, 0.25f);

                // Music sits low under the conversation
                float t = 0f;
                float fromMusic = _musicSource != null ? _musicSource.volume : 0f;
                bool voiceStarted = false;
                float tail = 0f;
                while (t < seconds + 0.1f)
                {
                    if (voice != null)
                    {
                        if (Cyberpunk.Audio.DialogueVoice.IsPlaying) voiceStarted = true;
                        else if (voiceStarted || t > 1.5f) { tail += Time.deltaTime; if (tail > 0.2f) break; }
                    }
                    t += Time.deltaTime;
                    HoldStandingPoses();
                    float k = Mathf.Clamp01(t / (voice != null ? voice.length / RevealVoiceSpeed + 0.3f : seconds + 0.1f));
                    if (_musicSource != null) _musicSource.volume = Mathf.Lerp(fromMusic, baseMusic * 0.4f, Mathf.Clamp01(t / 0.6f));
                    if (bossEyeVisorLight != null && !line.alita)
                        bossEyeVisorLight.intensity = 8.5f + Mathf.Sin(Time.time * 18f) * 2.5f; // the voice rides the visor
                    else if (bossEyeVisorLight != null)
                        bossEyeVisorLight.intensity = Mathf.Lerp(bossEyeVisorLight.intensity, 5f, Time.deltaTime * 4f);

                    switch (line.shot)
                    {
                        case 1: // Mechatron's visor, slow push in
                            CamAt(bPos + new Vector3(Mathf.Lerp(3.2f, 2.4f, k), Mathf.Lerp(4.4f, 4.9f, k), -9f + k * 1.5f), bPos + Vector3.up * 5.3f, Mathf.Lerp(34f, 28f, k));
                            break;
                        case 2: // Alita, facing the machine
                            CamAt(aPos + new Vector3(Mathf.Lerp(1.1f, 0.8f, k), 1.55f, Mathf.Lerp(2.3f, 1.8f, k)), aPos + Vector3.up * 1.5f, Mathf.Lerp(30f, 24f, k));
                            break;
                        default: // wide: the two of them across the dust
                            CamAt(mid + new Vector3(-9f, 2.0f, -2f), mid + Vector3.up * 2.4f, 40f);
                            break;
                    }
                    yield return null;
                }
            }

            // He commits: the Titan powers up as the music comes back for the fight
            if (_bossAnimator != null) _bossAnimator.CrossFadeInFixedTime("ReactorPowerUp", 0.2f);
            if (_sfxSource != null) _sfxSource.PlayOneShot(PolygonMech.MechAudioSynth.GetOverdriveAlarm(), 0.5f);
            if (bossEyeVisorLight != null) bossEyeVisorLight.intensity = 8.5f;
            AddCameraTrauma(0.6f);
            float t2 = 0f;
            float lowMusic = _musicSource != null ? _musicSource.volume : 0f;
            while (t2 < 1.2f)
            {
                t2 += Time.deltaTime;
                if (_musicSource != null) _musicSource.volume = Mathf.Lerp(lowMusic, baseMusic, t2 / 1.2f);
                CamAt(bPos + new Vector3(4.5f, 2.6f, -12f), bPos + Vector3.up * 4.6f, Mathf.Lerp(40f, 52f, t2 / 1.2f));
                yield return null;
            }

            Cyberpunk.Audio.DialogueVoice.Speed = 1f;
            CyberpunkDialogueHUD.GetOrCreate().CloseDialogue(0.15f);
            SetArrivalHudVisible(true);
            if (card != null) card.SetLetterboxOnly(false, 0.5f);
        }
    }
}
