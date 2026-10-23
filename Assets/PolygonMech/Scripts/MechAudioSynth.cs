using UnityEngine;

namespace PolygonMech
{
    /// <summary>
    /// Procedural Audio Synthesizer specifically crafted for Giant Titan Mechs.
    /// Synthesizes heavy industrial footsteps, pneumatic air hiss releases, mechanical servo whines,
    /// thruster afterburner roars, seismic ground slams, and Gatling fire.
    /// Zero external file dependencies; clips are cached in memory.
    /// </summary>
    public static class MechAudioSynth
    {
        private static AudioClip[] _footstepStomps;
        private static AudioClip[] _pneumaticReleases;
        private static AudioClip[] _servoWhines;
        private static AudioClip _movementServoLoop;
        private static AudioClip[] _jointStrains;
        private static AudioClip _thrusterIgnition;
        private static AudioClip _thrusterLoop;
        private static AudioClip[] _missileLaunches;
        private static AudioClip[] _seismicSlams;
        private static AudioClip[] _titanBladeCleaves;
        private static AudioClip[] _gatlingShots;
        private static AudioClip _overdriveAlarm;

        private const int SampleRate = 44100;

        public static AudioClip GetFootstepStomp()
        {
#if UNITY_EDITOR
            if (_footstepStomps == null)
            {
                var c1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_01.wav");
                var c2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_02.wav");
                var c3 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_03.wav");
                if (c1 != null && c2 != null && c3 != null)
                {
                    _footstepStomps = new AudioClip[] { c1, c2, c3 };
                }
            }
#endif
            if (_footstepStomps == null) GenerateFootstepStomps();
            return _footstepStomps[Random.Range(0, _footstepStomps.Length)];
        }

        public static AudioClip GetPneumaticRelease()
        {
            if (_pneumaticReleases == null) GeneratePneumaticReleases();
            return _pneumaticReleases[Random.Range(0, _pneumaticReleases.Length)];
        }

        public static AudioClip GetServoWhine()
        {
#if UNITY_EDITOR
            if (_servoWhines == null)
            {
                var a1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_01.wav");
                var a2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_02.wav");
                var a3 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_03.wav");
                if (a1 != null && a2 != null && a3 != null)
                {
                    _servoWhines = new AudioClip[] { a1, a2, a3 };
                }
            }
#endif
            if (_servoWhines == null) GenerateServoWhines();
            return _servoWhines[Random.Range(0, _servoWhines.Length)];
        }

        public static AudioClip GetMovementServoLoop()
        {
#if UNITY_EDITOR
            if (_movementServoLoop == null)
            {
                var loop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Chassis_Reactor_Loop.wav");
                if (loop != null) _movementServoLoop = loop;
            }
#endif
            if (_movementServoLoop == null) GenerateMovementServoLoop();
            return _movementServoLoop;
        }

        public static AudioClip GetJointStrain()
        {
            if (_jointStrains == null) GenerateJointStrains();
            return _jointStrains[Random.Range(0, _jointStrains.Length)];
        }

        public static AudioClip GetThrusterIgnition()
        {
            if (_thrusterIgnition == null) GenerateThrusterClips();
            return _thrusterIgnition;
        }

        public static AudioClip GetThrusterLoop()
        {
            if (_thrusterLoop == null) GenerateThrusterClips();
            return _thrusterLoop;
        }

        public static AudioClip GetMissileLaunch()
        {
            if (_missileLaunches == null) GenerateMissileLaunches();
            return _missileLaunches[Random.Range(0, _missileLaunches.Length)];
        }

        public static AudioClip GetSeismicSlam()
        {
            if (_seismicSlams == null) GenerateSeismicSlams();
            return _seismicSlams[Random.Range(0, _seismicSlams.Length)];
        }

        public static AudioClip GetTitanBladeCleave()
        {
            if (_titanBladeCleaves == null) GenerateTitanBladeCleaves();
            return _titanBladeCleaves[Random.Range(0, _titanBladeCleaves.Length)];
        }

        public static AudioClip GetGatlingShot()
        {
            if (_gatlingShots == null) GenerateGatlingShots();
            return _gatlingShots[Random.Range(0, _gatlingShots.Length)];
        }

        public static AudioClip GetOverdriveAlarm()
        {
            if (_overdriveAlarm == null) GenerateOverdriveAlarm();
            return _overdriveAlarm;
        }

        // ══════════════════════════════════════════════════════════════════════
        // SOUND GENERATORS
        // ══════════════════════════════════════════════════════════════════════

        private static void GenerateFootstepStomps()
        {
            _footstepStomps = new AudioClip[3];
            for (int i = 0; i < 3; i++)
            {
                float duration = 0.55f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];
                float basePitch = 55f + i * 8f;

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    float progress = (float)s / count;

                    // Deep exponential sub-bass pitch drop (55Hz down to 25Hz)
                    float freq = Mathf.Lerp(basePitch * 2.2f, 28f, Mathf.Pow(progress, 0.4f));
                    float sine = Mathf.Sin(2f * Mathf.PI * freq * t);

                    // Concrete gravel punch (short noise burst in the first 0.08s)
                    float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 35f) * 0.6f;

                    // Metallic chassis clank resonance
                    float metalClang = Mathf.Sin(2f * Mathf.PI * 340f * t) * Mathf.Exp(-t * 22f) * 0.35f;

                    // Combined with heavy punch envelope
                    float env = Mathf.Exp(-t * 8f);
                    float sample = (sine * 0.8f + noise + metalClang) * env;

                    // Soft saturation for heavy industrial weight
                    data[s] = Mathf.Clamp(sample * 1.5f, -0.98f, 0.98f);
                }

                _footstepStomps[i] = AudioClip.Create($"Mech_Stomp_{i}", count, 1, SampleRate, false);
                _footstepStomps[i].SetData(data, 0);
            }
        }

        private static void GeneratePneumaticReleases()
        {
            _pneumaticReleases = new AudioClip[2];
            for (int i = 0; i < 2; i++)
            {
                float duration = 0.45f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    // White noise shaped into high-pressure valve expulsion
                    float noise = (Random.value * 2f - 1f);
                    // Rapid attack, exponential decay with trailing hiss
                    float env = Mathf.Pow(t / 0.03f, 0.5f) * Mathf.Exp(-t * 7.5f);
                    if (t < 0.03f) env = t / 0.03f;

                    // High-pass resonance around 2.4kHz
                    float hissRes = Mathf.Sin(2f * Mathf.PI * (2200f + i * 300f) * t) * 0.25f;

                    data[s] = Mathf.Clamp((noise * 0.75f + hissRes) * env * 0.8f, -0.95f, 0.95f);
                }

                _pneumaticReleases[i] = AudioClip.Create($"Mech_Pneumatic_{i}", count, 1, SampleRate, false);
                _pneumaticReleases[i].SetData(data, 0);
            }
        }

        private static void GenerateServoWhines()
        {
            _servoWhines = new AudioClip[3];
            for (int i = 0; i < 3; i++)
            {
                float duration = 0.40f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    float progress = (float)s / count;

                    // Deep mechanical hydraulic pressure grunt (low 70-110Hz)
                    float freq = Mathf.Lerp(65f, 110f + i * 15f, progress);
                    float subMotor = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f;
                    float hydraulicPuff = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.45f;

                    float env = Mathf.Sin(progress * Mathf.PI);
                    data[s] = Mathf.Clamp((subMotor + hydraulicPuff) * env * 0.8f, -0.92f, 0.92f);
                }

                _servoWhines[i] = AudioClip.Create($"Mech_Hydraulic_{i}", count, 1, SampleRate, false);
                _servoWhines[i].SetData(data, 0);
            }
        }

        private static void GenerateMovementServoLoop()
        {
            float duration = 1.6f;
            int count = Mathf.FloorToInt(SampleRate * duration);
            float[] data = new float[count];

            // Heavy industrial diesel-electric / reactor sub-bass rumble
            // Strict integer cycles across 1.6s for seamless loop:
            // 1.6s * 50Hz  = 80 cycles (deep sub-bass body shake)
            // 1.6s * 100Hz = 160 cycles (chassis engine thrum)
            // 1.6s * 150Hz = 240 cycles (heavy gear mesh)
            for (int s = 0; s < count; s++)
            {
                float t = (float)s / SampleRate;
                float cyclicChug = 0.80f + 0.20f * Mathf.Sin(2f * Mathf.PI * 6.25f * t); // 10 cycles

                float subRumble = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.55f;
                float engineLow = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.35f;
                float gearMesh  = Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.18f;
                float airExhaust = (Random.value * 2f - 1f) * 0.08f;

                float sample = (subRumble + engineLow + gearMesh + airExhaust) * cyclicChug * 0.70f;
                data[s] = Mathf.Clamp(sample, -0.9f, 0.9f);
            }

            _movementServoLoop = AudioClip.Create("Mech_HeavyEngineLoop", count, 1, SampleRate, false);
            _movementServoLoop.SetData(data, 0);
        }

        private static void GenerateJointStrains()
        {
            _jointStrains = new AudioClip[2];
            for (int i = 0; i < 2; i++)
            {
                float duration = 0.45f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    float progress = (float)s / count;
                    float env = Mathf.Sin(progress * Mathf.PI);

                    // Low metallic groaning pitch bend
                    float freq = Mathf.Lerp(140f + i * 40f, 65f, progress);
                    float metalTone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f;
                    float rattle = Mathf.Sin(2f * Mathf.PI * (freq * 3.8f) * t) * 0.25f;

                    data[s] = Mathf.Clamp((metalTone + rattle) * env * 0.8f, -0.95f, 0.95f);
                }

                _jointStrains[i] = AudioClip.Create($"Mech_JointStrain_{i}", count, 1, SampleRate, false);
                _jointStrains[i].SetData(data, 0);
            }
        }

        private static void GenerateThrusterClips()
        {
            // Ignition burst
            {
                float duration = 0.85f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    float noise = (Random.value * 2f - 1f);
                    float boom = Mathf.Sin(2f * Mathf.PI * 65f * t) * Mathf.Exp(-t * 5f);
                    float roar = noise * Mathf.Exp(-t * 2.8f) * 0.85f;

                    data[s] = Mathf.Clamp((boom * 0.9f + roar) * 1.3f, -0.98f, 0.98f);
                }

                _thrusterIgnition = AudioClip.Create("Mech_ThrusterIgnition", count, 1, SampleRate, false);
                _thrusterIgnition.SetData(data, 0);
            }

            // Looping rumble
            {
                float duration = 1.2f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    float sub = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.5f;
                    float mid = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.3f;
                    float roar = (Random.value * 2f - 1f) * 0.45f;

                    data[s] = Mathf.Clamp(sub + mid + roar, -0.9f, 0.9f);
                }

                _thrusterLoop = AudioClip.Create("Mech_ThrusterLoop", count, 1, SampleRate, false);
                _thrusterLoop.SetData(data, 0);
            }
        }

        private static void GenerateMissileLaunches()
        {
            _missileLaunches = new AudioClip[3];
            for (int i = 0; i < 3; i++)
            {
                float duration = 0.55f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    // Initial pneumatic tube expulsion thump
                    float pop = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 40f);
                    // Rocket burn whoosh rising in frequency
                    float rocketFreq = Mathf.Lerp(160f, 750f, t / duration);
                    float rocket = ((Random.value * 2f - 1f) * 0.5f + Mathf.Sin(2f * Mathf.PI * rocketFreq * t) * 0.5f) * Mathf.Exp(-t * 3.5f);

                    data[s] = Mathf.Clamp((pop * 0.8f + rocket * 0.9f) * 1.2f, -0.95f, 0.95f);
                }

                _missileLaunches[i] = AudioClip.Create($"Mech_MissileLaunch_{i}", count, 1, SampleRate, false);
                _missileLaunches[i].SetData(data, 0);
            }
        }

        private static void GenerateSeismicSlams()
        {
            _seismicSlams = new AudioClip[2];
            for (int i = 0; i < 2; i++)
            {
                float duration = 1.1f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    float progress = (float)s / count;

                    // Ultra low seismic earth vibration
                    float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(80f, 22f, progress) * t) * Mathf.Exp(-t * 3.2f);
                    // Shockwave crash
                    float crash = (Random.value * 2f - 1f) * Mathf.Exp(-t * 12f) * 0.9f;
                    // Steel blade impact ping
                    float ping = Mathf.Sin(2f * Mathf.PI * 480f * t) * Mathf.Exp(-t * 18f) * 0.4f;

                    data[s] = Mathf.Clamp((sub * 1.2f + crash + ping) * 1.4f, -0.99f, 0.99f);
                }

                _seismicSlams[i] = AudioClip.Create($"Mech_SeismicSlam_{i}", count, 1, SampleRate, false);
                _seismicSlams[i].SetData(data, 0);
            }
        }

        private static void GenerateTitanBladeCleaves()
        {
            _titanBladeCleaves = new AudioClip[2];
            for (int i = 0; i < 2; i++)
            {
                float duration = 0.75f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    // Low sonic displacement whoosh
                    float whoosh = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(60f, 180f, t / duration) * t);
                    // Air shear noise
                    float shear = (Random.value * 2f - 1f) * Mathf.Sin(t / duration * Mathf.PI);

                    data[s] = Mathf.Clamp((whoosh * 0.6f + shear * 0.5f) * 1.1f, -0.95f, 0.95f);
                }

                _titanBladeCleaves[i] = AudioClip.Create($"Mech_TitanBladeCleave_{i}", count, 1, SampleRate, false);
                _titanBladeCleaves[i].SetData(data, 0);
            }
        }

        private static void GenerateGatlingShots()
        {
            _gatlingShots = new AudioClip[2];
            for (int i = 0; i < 2; i++)
            {
                float duration = 0.16f;
                int count = Mathf.FloorToInt(SampleRate * duration);
                float[] data = new float[count];

                for (int s = 0; s < count; s++)
                {
                    float t = (float)s / SampleRate;
                    float punch = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 50f);
                    float crack = (Random.value * 2f - 1f) * Mathf.Exp(-t * 35f);
                    float brass = Mathf.Sin(2f * Mathf.PI * 850f * t) * Mathf.Exp(-t * 70f) * 0.3f;

                    data[s] = Mathf.Clamp((punch * 0.8f + crack * 0.7f + brass) * 1.3f, -0.98f, 0.98f);
                }

                _gatlingShots[i] = AudioClip.Create($"Mech_Gatling_{i}", count, 1, SampleRate, false);
                _gatlingShots[i].SetData(data, 0);
            }
        }

        private static void GenerateOverdriveAlarm()
        {
            float duration = 1.4f;
            int count = Mathf.FloorToInt(SampleRate * duration);
            float[] data = new float[count];

            for (int s = 0; s < count; s++)
            {
                float t = (float)s / SampleRate;
                // Dual-tone cyberpunk war horn
                float f1 = 220f + Mathf.Sin(t * 14f) * 35f;
                float f2 = 440f + Mathf.Sin(t * 14f) * 70f;
                float tone = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.4f;
                float env = Mathf.Clamp01(Mathf.Sin((float)s / count * Mathf.PI));

                data[s] = Mathf.Clamp(tone * env * 0.9f, -0.95f, 0.95f);
            }

            _overdriveAlarm = AudioClip.Create("Mech_OverdriveAlarm", count, 1, SampleRate, false);
            _overdriveAlarm.SetData(data, 0);
        }
    }
}
