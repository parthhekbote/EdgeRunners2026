#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PolygonCyberCity.NPC.Editor
{
    /// <summary>
    /// Procedural Cyberpunk Audio Generator.
    /// Synthesizes high-fidelity 16-bit 44.1kHz PCM WAV files for:
    /// - Civilian phone chatter & radio babble
    /// - Ambient pedestrian murmurs & conversational vocalizations
    /// - HelperBot synthetic servo chirps, telemetry blips, and digital sweeps
    /// - Startled vehicle dodge barks and near-miss gasps
    /// - Mercenary comms static and tactical grunts
    /// </summary>
    public static class CyberNPCAudioGenerator
    {
        private const string OutputDir = "Assets/Audio/NPC_Chatter";
        private const int SampleRate = 44100;

        [MenuItem("CyberCity/Audio/Generate NPC Cyber Chatter Audio", false, 20)]
        [MenuItem("Tools/Cyber City/Audio/Generate NPC Cyber Chatter Audio", false, 20)]
        public static void GenerateAllChatterAudio()
        {
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
                AssetDatabase.Refresh();
            }

            // 1. Phone chatter (Formant modulated vocal stream with subtle cyber radio filter)
            CreatePhoneChatter("npc_phone_chatter_01.wav", duration: 3.2f, pitchBase: 180f, cadence: 4.5f, seed: 101);
            CreatePhoneChatter("npc_phone_chatter_02.wav", duration: 2.8f, pitchBase: 240f, cadence: 5.2f, seed: 202);
            CreatePhoneChatter("npc_phone_chatter_03.wav", duration: 3.5f, pitchBase: 150f, cadence: 3.8f, seed: 303);

            // 2. Civilian murmurs & conversational phrases
            CreateVocalMurmur("npc_civilian_murmur_01.wav", duration: 2.2f, pitchBase: 165f, seed: 404);
            CreateVocalMurmur("npc_civilian_murmur_02.wav", duration: 1.9f, pitchBase: 210f, seed: 505);
            CreateVocalMurmur("npc_civilian_murmur_03.wav", duration: 2.5f, pitchBase: 140f, seed: 606);

            // 3. HelperBot / Robot synthetic telemetry blips & servo chirps
            CreateRobotChirp("npc_helperbot_chirp_01.wav", duration: 1.2f, freqStart: 880f, freqEnd: 1760f, beeps: 3, seed: 707);
            CreateRobotChirp("npc_helperbot_chirp_02.wav", duration: 0.9f, freqStart: 1200f, freqEnd: 600f, beeps: 2, seed: 808);
            CreateRobotChirp("npc_helperbot_chirp_03.wav", duration: 1.5f, freqStart: 650f, freqEnd: 1450f, beeps: 4, seed: 909);

            // 4. Startled pedestrian reactions (vehicle near-miss / horn evasion)
            CreateStartleBark("npc_reaction_startle_01.wav", duration: 0.75f, pitch: 260f, seed: 111);
            CreateStartleBark("npc_reaction_startle_02.wav", duration: 0.65f, pitch: 340f, seed: 222);

            // 5. Mercenary / Thug tactical grunts & radio squelches
            CreateMercGrunt("npc_merc_grunt_01.wav", duration: 1.1f, pitch: 110f, seed: 333);
            CreateMercGrunt("npc_merc_grunt_02.wav", duration: 1.3f, pitch: 95f, seed: 444);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CyberNPCAudioGenerator] Generated 12 pristine Cyberpunk NPC chatter audio clips in '{OutputDir}'");
        }

        private static void CreatePhoneChatter(string filename, float duration, float pitchBase, float cadence, int seed)
        {
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(seed);

            float phase1 = 0f, phase2 = 0f, phase3 = 0f;
            float formantF1 = 500f, formantF2 = 1500f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // Speech cadence envelope (syllable bursts and micro pauses)
                float syllableEnvelope = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * cadence * 2f) * 1.5f);
                if (Mathf.PerlinNoise(t * 3f, seed) < 0.35f) syllableEnvelope *= 0.1f; // natural phrase pause

                // Subtle intonation glide
                float currentPitch = pitchBase + Mathf.Sin(t * 7f) * 25f + (float)(rand.NextDouble() - 0.5) * 8f;

                // Pitch oscillator
                phase1 += (2f * Mathf.PI * currentPitch) / SampleRate;
                float voice = Mathf.Sin(phase1) * 0.5f + Mathf.Sin(phase1 * 2f) * 0.25f + Mathf.Sin(phase1 * 3f) * 0.12f;

                // Formant resonance filters
                phase2 += (2f * Mathf.PI * formantF1) / SampleRate;
                phase3 += (2f * Mathf.PI * formantF2) / SampleRate;
                float resonantVocal = voice * (0.6f + 0.3f * Mathf.Sin(phase2) + 0.2f * Mathf.Sin(phase3));

                // Bandpass radio/phone distortion (cyber comms effect)
                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.04f;
                float filtered = Mathf.Clamp((resonantVocal + noise) * 1.3f, -0.8f, 0.8f);

                // Smooth fade-in & fade-out
                float edgeFade = Mathf.Clamp01(Mathf.Min(t * 15f, (duration - t) * 15f));
                samples[i] = filtered * syllableEnvelope * edgeFade * 0.75f;
            }

            WriteWavFile(Path.Combine(OutputDir, filename), samples, SampleRate);
        }

        private static void CreateVocalMurmur(string filename, float duration, float pitchBase, int seed)
        {
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(seed);

            float phase = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Clamp01(Mathf.Sin(t / duration * Mathf.PI));
                float inflection = Mathf.Sin(t * 6f) * 18f;

                phase += (2f * Mathf.PI * (pitchBase + inflection)) / SampleRate;
                float raw = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.28f + Mathf.Sin(phase * 3f) * 0.12f;

                // Gentle throat resonance & breath
                float breath = ((float)rand.NextDouble() * 2f - 1f) * 0.03f;
                samples[i] = (raw + breath) * envelope * 0.7f;
            }

            WriteWavFile(Path.Combine(OutputDir, filename), samples, SampleRate);
        }

        private static void CreateRobotChirp(string filename, float duration, float freqStart, float freqEnd, int beeps, int seed)
        {
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(seed);

            float phase = 0f;
            float beepDuration = duration / beeps;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                int currentBeep = Mathf.Clamp((int)(t / beepDuration), 0, beeps - 1);
                float beepLocalT = (t - (currentBeep * beepDuration)) / beepDuration;

                // Beep pulse envelope (clean rectangular blip with micro-envelope)
                float pulse = (beepLocalT < 0.75f) ? Mathf.Sin(beepLocalT / 0.75f * Mathf.PI) : 0f;

                // Frequency step + sweep
                float targetFreq = Mathf.Lerp(freqStart, freqEnd, (float)currentBeep / (beeps > 1 ? (beeps - 1) : 1));
                targetFreq += Mathf.Sin(beepLocalT * 30f) * 80f;

                phase += (2f * Mathf.PI * targetFreq) / SampleRate;
                float squarePulse = Mathf.Sign(Mathf.Sin(phase)) * 0.35f + Mathf.Sin(phase) * 0.45f;

                // Subtle digital bit-crush grit
                float crushed = Mathf.Round(squarePulse * 16f) / 16f;
                samples[i] = crushed * pulse * 0.7f;
            }

            WriteWavFile(Path.Combine(OutputDir, filename), samples, SampleRate);
        }

        private static void CreateStartleBark(string filename, float duration, float pitch, int seed)
        {
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(seed);

            float phase = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                // Fast attack, exponential decay
                float envelope = Mathf.Exp(-t * 5.5f) * Mathf.Clamp01(t * 30f);
                float curPitch = pitch * (1f + Mathf.Exp(-t * 8f) * 0.4f);

                phase += (2f * Mathf.PI * curPitch) / SampleRate;
                float vocal = Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2f) * 0.3f;
                float gasp = ((float)rand.NextDouble() * 2f - 1f) * 0.15f * Mathf.Exp(-t * 3f);

                samples[i] = (vocal + gasp) * envelope * 0.85f;
            }

            WriteWavFile(Path.Combine(OutputDir, filename), samples, SampleRate);
        }

        private static void CreateMercGrunt(string filename, float duration, float pitch, int seed)
        {
            int totalSamples = (int)(SampleRate * duration);
            float[] samples = new float[totalSamples];
            System.Random rand = new System.Random(seed);

            float phase = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Clamp01(Mathf.Sin(t / duration * Mathf.PI));

                // Radio key squelch at beginning & end
                float squelch = 0f;
                if (t < 0.08f || t > duration - 0.08f)
                {
                    squelch = ((float)rand.NextDouble() * 2f - 1f) * 0.25f;
                }

                phase += (2f * Mathf.PI * pitch) / SampleRate;
                float grunt = Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.3f;

                samples[i] = (grunt * envelope * 0.65f) + squelch;
            }

            WriteWavFile(Path.Combine(OutputDir, filename), samples, SampleRate);
        }

        private static void WriteWavFile(string filepath, float[] samples, int sampleRate)
        {
            byte[] wavBytes = EncodeToWav(samples, sampleRate);
            File.WriteAllBytes(filepath, wavBytes);
        }

        private static byte[] EncodeToWav(float[] samples, int sampleRate)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int sampleCount = samples.Length;
                int byteRate = sampleRate * 1 * 2; // 1 channel, 16 bits (2 bytes)

                // RIFF Header
                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + sampleCount * 2);
                writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

                // fmt Subchunk
                writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1Size for PCM
                writer.Write((short)1); // AudioFormat 1 = PCM
                writer.Write((short)1); // NumChannels = 1 (Mono)
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)2); // BlockAlign
                writer.Write((short)16); // BitsPerSample

                // data Subchunk
                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(sampleCount * 2);

                // Samples
                for (int i = 0; i < sampleCount; i++)
                {
                    short val = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                    writer.Write(val);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
#endif
