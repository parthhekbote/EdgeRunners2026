using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Procedural AAA Audio Synthesizer for Tron: Ares & Tron: Legacy inspired
    /// cybernetic awakening sequence. Synthesizes:
    /// 1. Deep Sub-Bass Drone (55Hz Daft Punk style filtered electronic hum)
    /// 2. Power Surge Swell (Exponential harmonic sweep from 80Hz to 880Hz)
    /// 3. Pneumatic Canopy Steam Hiss (Filtered white noise burst)
    /// 4. Optic Activation Boot Chime (C-Major 9th digital crystal arpeggio)
    /// 5. Ambient Cyber Chamber Pad (Slow evolving atmospheric chord)
    /// </summary>
    public static class AlitaAwakeningAudioEngine
    {
        private const int SampleRate = 44100;

        /// <summary>
        /// Deep resonant sub-bass drone with slow low-frequency oscillation (LFO).
        /// </summary>
        public static AudioClip CreateTronSubBassDrone(float duration = 12f)
        {
            int totalSamples = (int)(SampleRate * duration);
            AudioClip clip = AudioClip.Create("Tron_SubBass_Drone", totalSamples, 1, SampleRate, false);
            float[] data = new float[totalSamples];

            float baseFreq = 55f; // A1 note
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float lfo = 1f + 0.12f * Mathf.Sin(t * 0.8f * Mathf.PI * 2f);
                float freq = baseFreq * lfo;

                // Rich analog sawtooth approximation with filtered harmonics
                float saw = Mathf.Sin(t * freq * Mathf.PI * 2f) * 0.60f
                          + Mathf.Sin(t * freq * 2f * Mathf.PI * 2f) * 0.25f
                          + Mathf.Sin(t * freq * 3f * Mathf.PI * 2f) * 0.10f
                          + Mathf.Sin(t * (freq * 0.5f) * Mathf.PI * 2f) * 0.40f; // Sub-octave

                // Soft saturation limiter
                data[i] = (float)System.Math.Tanh(saw * 1.2f) * 0.75f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Exponential rising power surge as energy floods into the quantum core.
        /// </summary>
        public static AudioClip CreatePowerSurgeSwell(float duration = 4.5f)
        {
            int totalSamples = (int)(SampleRate * duration);
            AudioClip clip = AudioClip.Create("Tron_Power_Surge", totalSamples, 1, SampleRate, false);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples; // 0 to 1
                float currentFreq = Mathf.Lerp(65f, 650f, Mathf.Pow(t, 2.2f));
                float amp = Mathf.SmoothStep(0f, 1f, t * 1.5f) * Mathf.Clamp01((1f - t) * 4f);

                float wave = Mathf.Sin((float)i / SampleRate * currentFreq * Mathf.PI * 2f) * 0.7f
                           + Mathf.Sin((float)i / SampleRate * currentFreq * 2.01f * Mathf.PI * 2f) * 0.3f;

                // Subtle resonant flanging
                float flange = Mathf.Sin(t * 40f * Mathf.PI);
                data[i] = Mathf.Clamp(wave * amp * (0.85f + 0.15f * flange), -1f, 1f) * 0.85f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Crystalline high-tech boot chime when optic sensors power on.
        /// </summary>
        public static AudioClip CreateOpticBootChime(float duration = 2.8f)
        {
            int totalSamples = (int)(SampleRate * duration);
            AudioClip clip = AudioClip.Create("Tron_Optic_Boot", totalSamples, 1, SampleRate, false);
            float[] data = new float[totalSamples];

            // Crystal frequencies (C6, E6, G6, B6, D7)
            float[] freqs = new float[] { 1046.50f, 1318.51f, 1567.98f, 1975.53f, 2349.32f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int f = 0; f < freqs.Length; f++)
                {
                    float noteDelay = f * 0.045f;
                    if (t >= noteDelay)
                    {
                        float noteT = t - noteDelay;
                        float decay = Mathf.Exp(-noteT * 3.8f);
                        sample += Mathf.Sin(noteT * freqs[f] * Mathf.PI * 2f) * decay * 0.22f;
                    }
                }

                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Pneumatic pressure decompression hiss as the pod canopy unseals.
        /// </summary>
        public static AudioClip CreatePneumaticSteamHiss(float duration = 3.2f)
        {
            int totalSamples = (int)(SampleRate * duration);
            AudioClip clip = AudioClip.Create("Tron_Pneumatic_Hiss", totalSamples, 1, SampleRate, false);
            float[] data = new float[totalSamples];

            System.Random rand = new System.Random(42);
            float filterState = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float env = Mathf.Pow(1f - t, 1.4f) * (t < 0.05f ? t / 0.05f : 1f);

                // Filtered pink/white noise
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                filterState = Mathf.Lerp(filterState, white, 0.35f); // Low-pass filter

                data[i] = filterState * env * 0.65f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Crisp tactile mechanical keyboard typing sequence with authentic micro-clicks.
        /// </summary>
        public static AudioClip CreateTypingSequence(float duration = 3.0f, int keyCount = 28)
        {
            int totalSamples = (int)(SampleRate * duration);
            AudioClip clip = AudioClip.Create("Tron_Terminal_Typing", totalSamples, 1, SampleRate, false);
            float[] data = new float[totalSamples];

            System.Random rand = new System.Random(1337);

            for (int k = 0; k < keyCount; k++)
            {
                // Staggered typing timing with realistic human jitter
                float keyTime = (float)k / keyCount * (duration * 0.85f) + (float)(rand.NextDouble() * 0.04 - 0.02);
                int startSample = Mathf.Clamp((int)(keyTime * SampleRate), 0, totalSamples - 1);
                int clickLen = (int)(SampleRate * 0.035f); // 35ms mechanical click
                float pitch = 1800f + (float)rand.NextDouble() * 800f; // High transient snap
                float thudFreq = 160f + (float)rand.NextDouble() * 50f; // Plastic bottom-out thud

                for (int s = 0; s < clickLen && (startSample + s) < totalSamples; s++)
                {
                    float clickT = (float)s / SampleRate;
                    float clickEnv = Mathf.Exp(-clickT * 140f);
                    float thudEnv = Mathf.Exp(-clickT * 50f);

                    float clickWave = Mathf.Sin(clickT * pitch * Mathf.PI * 2f) * clickEnv * 0.55f;
                    float thudWave = Mathf.Sin(clickT * thudFreq * Mathf.PI * 2f) * thudEnv * 0.40f;
                    float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * clickEnv * 0.25f;

                    data[startSample + s] += (clickWave + thudWave + noise) * 0.65f;
                }
            }

            // Normalize slightly to prevent clipping
            for (int i = 0; i < totalSamples; i++)
            {
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Retro-cyberpunk system confirmation beep.
        /// </summary>
        public static AudioClip CreateTerminalBeep(float freq = 920f, float duration = 0.12f)
        {
            int totalSamples = (int)(SampleRate * duration);
            AudioClip clip = AudioClip.Create("Tron_Terminal_Beep", totalSamples, 1, SampleRate, false);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 22f);
                float wave = Mathf.Sin(t * freq * Mathf.PI * 2f) * 0.7f
                           + Mathf.Sin(t * freq * 2f * Mathf.PI * 2f) * 0.3f;
                data[i] = wave * env * 0.55f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Massive Nine Inch Nails style distorted industrial sub-bass drop on program launch.
        /// </summary>
        public static AudioClip CreateIndustrialSubDrop(float duration = 4.2f)
        {
            int totalSamples = (int)(SampleRate * duration);
            AudioClip clip = AudioClip.Create("Tron_Industrial_SubDrop", totalSamples, 1, SampleRate, false);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float timeSec = (float)i / SampleRate;

                // Pitch envelope dropping from 140Hz punch down to 42Hz heavy rumble
                float pitch = Mathf.Lerp(140f, 42f, Mathf.Pow(t, 0.45f));
                float env = Mathf.Pow(1f - t, 0.8f);

                // Initial punch transient
                float punch = (timeSec < 0.08f) ? Mathf.Sin(timeSec * 220f * Mathf.PI * 2f) * Mathf.Exp(-timeSec * 45f) * 0.6f : 0f;

                // Analog clipped square/sine hybrid
                float fundamental = Mathf.Sin(timeSec * pitch * Mathf.PI * 2f);
                float sub = Mathf.Sin(timeSec * (pitch * 0.5f) * Mathf.PI * 2f) * 0.5f;
                float saturated = (float)System.Math.Tanh((fundamental + sub + punch) * 2.2f) * 0.85f;

                data[i] = saturated * env;
            }

            clip.SetData(data, 0);
            return clip;
        }
    }
}
