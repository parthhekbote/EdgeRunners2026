using UnityEngine;

namespace CyberCombat.Audio
{
    /// <summary>
    /// Real-time Procedural Audio Synthesizer for Cyberpunk Combat.
    /// Provides synthesized, punchy SFX for weapon swings, metallic blade clashes,
    /// gun shots, air dashes, parry deflects, and boss impact slams when no external audio assets exist.
    /// Zero external file dependencies; clips are cached in memory.
    /// </summary>
    public static class CyberCombatAudioSynth
    {
        private static AudioClip[] _swordLightSlashes;
        private static AudioClip[] _swordHeavyCleaves;
        private static AudioClip[] _swordPiercingThrusts;
        private static AudioClip[] _swordImpacts;
        private static AudioClip[] _heavyImpacts;
        private static AudioClip[] _gunShots;
        private static AudioClip[] _dodgeWhooshes;
        private static AudioClip[] _parryClinks;
        private static AudioClip[] _swordDrawClips;
        private static AudioClip[] _swordSheatheClips;
        private static AudioClip[] _jumpTakeoffs;
        private static AudioClip[] _jumpLandings;
        private static AudioClip[] _crowdCheers;
        private static AudioClip[] _crowdClaps;
        private static AudioClip[] _crowdBoos;
        private static AudioClip[] _crowdMurmurBeds;
        private static AudioClip[] _crowdGasps;

        private const int SampleRate = 44100;

        private static AudioClip TryLoadClip(string path)
        {
#if UNITY_EDITOR
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;
#endif
            return null;
        }

        public static AudioClip GetSwordSlash(int comboStep = 1, bool isHeavy = false)
        {
            if (isHeavy || comboStep >= 7)
            {
                if (_swordHeavyCleaves == null) GenerateHeavyCleaves();
                return _swordHeavyCleaves[Random.Range(0, _swordHeavyCleaves.Length)];
            }
            if (_swordLightSlashes == null) GenerateLightSlashes();
            int idx = Mathf.Clamp(comboStep - 1, 0, _swordLightSlashes.Length - 1);
            return _swordLightSlashes[idx % _swordLightSlashes.Length];
        }

        public static AudioClip GetSwordSwing()
        {
            return GetSwordSlash(1, false);
        }

        public static AudioClip GetHeavySlash()
        {
            return GetSwordSlash(1, true);
        }

        public static AudioClip GetSwordImpact()
        {
            if (_swordImpacts == null) GenerateSwordImpacts();
            return _swordImpacts[Random.Range(0, _swordImpacts.Length)];
        }

        public static AudioClip GetHeavyImpact()
        {
            if (_heavyImpacts == null) GenerateHeavyImpacts();
            return _heavyImpacts[Random.Range(0, _heavyImpacts.Length)];
        }

        public static AudioClip GetGunShot()
        {
            if (_gunShots == null) GenerateGunShots();
            return _gunShots[Random.Range(0, _gunShots.Length)];
        }

        public static AudioClip GetDodgeWhoosh()
        {
            if (_dodgeWhooshes == null) GenerateDodgeWhooshes();
            return _dodgeWhooshes[Random.Range(0, _dodgeWhooshes.Length)];
        }

        public static AudioClip GetParryClink()
        {
            if (_parryClinks == null) GenerateParryClinks();
            return _parryClinks[Random.Range(0, _parryClinks.Length)];
        }

        public static AudioClip GetSwordDraw()
        {
            if (_swordDrawClips == null) GenerateSwordDraws();
            return _swordDrawClips[Random.Range(0, _swordDrawClips.Length)];
        }

        public static AudioClip GetSwordSheathe()
        {
            if (_swordSheatheClips == null) GenerateSwordSheathes();
            return _swordSheatheClips[Random.Range(0, _swordSheatheClips.Length)];
        }

        public static AudioClip GetJumpWhoosh()
        {
            if (_jumpTakeoffs == null) GenerateJumpTakeoffs();
            return _jumpTakeoffs[Random.Range(0, _jumpTakeoffs.Length)];
        }

        public static AudioClip GetJumpTakeoff()
        {
            return GetJumpWhoosh();
        }

        public static AudioClip GetJumpLanding()
        {
            if (_jumpLandings == null) GenerateJumpLandings();
            return _jumpLandings[Random.Range(0, _jumpLandings.Length)];
        }

        public static AudioClip GetCrowdCheer(int variant = 0)
        {
            if (_crowdCheers == null) GenerateCrowdCheers();
            int idx = Mathf.Clamp(variant, 0, _crowdCheers.Length - 1);
            return _crowdCheers[idx];
        }

        public static AudioClip GetCrowdClap()
        {
            if (_crowdClaps == null) GenerateCrowdClaps();
            return _crowdClaps[Random.Range(0, _crowdClaps.Length)];
        }

        public static AudioClip GetCrowdBoo()
        {
            if (_crowdBoos == null) GenerateCrowdBoos();
            return _crowdBoos[Random.Range(0, _crowdBoos.Length)];
        }

        public static AudioClip GetCrowdMurmurBed()
        {
            if (_crowdMurmurBeds == null) GenerateCrowdMurmurBeds();
            return _crowdMurmurBeds[0];
        }

        public static AudioClip GetCrowdGasp()
        {
            if (_crowdGasps == null) GenerateCrowdGasps();
            return _crowdGasps[Random.Range(0, _crowdGasps.Length)];
        }

        public static void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            GameObject tempGo = new GameObject("TempAudio_Procedural");
            tempGo.transform.position = position;
            AudioSource source = tempGo.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 0.85f;
            source.minDistance = 3f;
            source.maxDistance = 35f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Object.Destroy(tempGo, clip.length + 0.1f);
        }

        // ── 1. Realistic Sword Slashes (Blade aerodynamic whistle & steel ring) ─────────
        private static void GenerateLightSlashes()
        {
            _swordLightSlashes = new AudioClip[3];
            _swordLightSlashes[0] = TryLoadClip("Assets/Audio/Combat/Sword_Slash_Light_01.wav");
            _swordLightSlashes[1] = TryLoadClip("Assets/Audio/Combat/Sword_Slash_Light_02.wav");
            _swordLightSlashes[2] = TryLoadClip("Assets/Audio/Combat/Sword_Slash_Light_03.wav");

            float[] pitchMods = { 1.0f, 1.12f, 0.94f };
            float[] whistleFreqs = { 1800f, 2100f, 1650f };

            for (int k = 0; k < 3; k++)
            {
                if (_swordLightSlashes[k] != null) continue;
                float duration = 0.15f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];
                float mod = pitchMods[k];
                float wFreq = whistleFreqs[k];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    float env = Mathf.Sin(progress * Mathf.PI);
                    float envSharp = Mathf.Pow(env, 1.8f);

                    float freq = Mathf.Lerp(wFreq * mod, 480f * mod, Mathf.Pow(progress, 1.4f));
                    float whistle = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;

                    float steelHarmonic = (Mathf.Sin(2f * Mathf.PI * 1280f * mod * t) * 0.22f
                                         + Mathf.Sin(2f * Mathf.PI * 1920f * mod * t) * 0.15f) * Mathf.Exp(-t * 12f);

                    float noise = (Random.value * 2f - 1f) * 0.38f;

                    float apex = Mathf.Exp(-Mathf.Pow((progress - 0.38f) * 16f, 2f));
                    float edgeBite = (Random.value * 2f - 1f) * Mathf.Sin(2f * Mathf.PI * 3400f * t) * apex * 0.40f;

                    float sample = (whistle * 0.50f + steelHarmonic + noise * 0.35f + edgeBite) * envSharp * 0.90f;
                    buffer[i] = Mathf.Clamp(sample, -1f, 1f);
                }

                _swordLightSlashes[k] = AudioClip.Create($"Sword_Slash_Light_{k}", samples, 1, SampleRate, false);
                _swordLightSlashes[k].SetData(buffer, 0);
            }
        }

        private static void GenerateHeavyCleaves()
        {
            _swordHeavyCleaves = new AudioClip[2];
            _swordHeavyCleaves[0] = TryLoadClip("Assets/Audio/Combat/Sword_Slash_Heavy_Cleave_01.wav");
            _swordHeavyCleaves[1] = TryLoadClip("Assets/Audio/Combat/Sword_Slash_Heavy_Cleave_02.wav");

            float[] pitchMods = { 1.0f, 0.88f };

            for (int k = 0; k < 2; k++)
            {
                if (_swordHeavyCleaves[k] != null) continue;
                float duration = (k == 0) ? 0.28f : 0.32f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];
                float mod = pitchMods[k];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    float env = Mathf.Sin(progress * Mathf.PI);
                    float envHeavy = Mathf.Pow(env, 1.4f);

                    float subFreq = Mathf.Lerp(340f * mod, 68f * mod, progress);
                    float subWhoosh = Mathf.Sin(2f * Mathf.PI * subFreq * t) * 0.55f;

                    float vortexFreq = Mathf.Lerp(1450f * mod, 320f * mod, Mathf.Pow(progress, 1.2f));
                    float vortex = Mathf.Sin(2f * Mathf.PI * vortexFreq * t) * 0.35f;

                    float airRoar = (Random.value * 2f - 1f) * 0.48f;
                    float steelHum = Mathf.Sin(2f * Mathf.PI * 720f * t) * Mathf.Exp(-t * 6f) * 0.28f;

                    float apex = Mathf.Exp(-Mathf.Pow((progress - 0.40f) * 12f, 2f));
                    float crack = (Random.value * 2f - 1f) * apex * 0.50f;

                    float sample = (subWhoosh * 0.60f + vortex * 0.45f + airRoar * 0.35f + steelHum + crack) * envHeavy * 0.95f;
                    buffer[i] = Mathf.Clamp(sample, -1f, 1f);
                }

                _swordHeavyCleaves[k] = AudioClip.Create($"Sword_Slash_Heavy_{k}", samples, 1, SampleRate, false);
                _swordHeavyCleaves[k].SetData(buffer, 0);
            }
        }

        // ── 2. Sword Impacts (Metallic bite + blade spark crack) ───────────────
        private static void GenerateSwordImpacts()
        {
            _swordImpacts = new AudioClip[3];
            float[] toneFrequencies = { 1800f, 2400f, 1500f };

            for (int k = 0; k < 3; k++)
            {
                float duration = 0.22f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                float resonantTone = toneFrequencies[k];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float decay = Mathf.Exp(-t * 22f);

                    // Transient initial burst
                    float crack = (t < 0.02f) ? (Random.value * 2f - 1f) * 0.8f : 0f;

                    // Metallic resonant ringing
                    float metal = Mathf.Sin(2f * Mathf.PI * resonantTone * t) * 0.35f
                                + Mathf.Sin(2f * Mathf.PI * (resonantTone * 1.62f) * t) * 0.25f;

                    // Low impact thud
                    float thud = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 30f) * 0.5f;

                    buffer[i] = Mathf.Clamp((crack + metal + thud) * decay * 0.85f, -1f, 1f);
                }

                _swordImpacts[k] = AudioClip.Create($"Sword_Impact_{k}", samples, 1, SampleRate, false);
                _swordImpacts[k].SetData(buffer, 0);
            }
        }

        // ── 3. Heavy Impacts (Boss slam, finisher strike) ─────────────────────
        private static void GenerateHeavyImpacts()
        {
            _heavyImpacts = new AudioClip[2];

            for (int k = 0; k < 2; k++)
            {
                float duration = 0.45f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Sub-bass pitch drop (110Hz -> 30Hz)
                    float freq = Mathf.Lerp(110f, 28f, progress);
                    float sub = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 8f);

                    // Debris rumble noise
                    float rumble = (Random.value * 2f - 1f) * Mathf.Exp(-t * 12f) * 0.4f;

                    // Sharp mechanical clang
                    float clang = Mathf.Sin(2f * Mathf.PI * 850f * t) * Mathf.Exp(-t * 35f) * 0.5f;

                    buffer[i] = Mathf.Clamp((sub * 0.8f + rumble + clang) * 0.95f, -1f, 1f);
                }

                _heavyImpacts[k] = AudioClip.Create($"Heavy_Impact_{k}", samples, 1, SampleRate, false);
                _heavyImpacts[k].SetData(buffer, 0);
            }
        }

        // ── 4. Gun Shots (Gun-kata burst, heavy caliber crack) ────────────────
        private static void GenerateGunShots()
        {
            _gunShots = new AudioClip[2];

            for (int k = 0; k < 2; k++)
            {
                float duration = 0.20f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float decay = Mathf.Exp(-t * 26f);

                    // High pressure explosive initial shock
                    float shock = (Random.value * 2f - 1f) * Mathf.Exp(-t * 50f) * 0.9f;

                    // Body punch
                    float punch = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 20f) * 0.6f;

                    buffer[i] = Mathf.Clamp((shock + punch) * decay * 0.9f, -1f, 1f);
                }

                _gunShots[k] = AudioClip.Create($"GunShot_{k}", samples, 1, SampleRate, false);
                _gunShots[k].SetData(buffer, 0);
            }
        }

        // ── 5. Dodge Whooshes / Cyber Dash (Cybernetic thruster blink & sub-bass air displacement) ──
        private static void GenerateDodgeWhooshes()
        {
            _dodgeWhooshes = new AudioClip[3];
            _dodgeWhooshes[0] = TryLoadClip("Assets/Audio/Combat/Dash_Cyber_01.wav");
            _dodgeWhooshes[1] = TryLoadClip("Assets/Audio/Combat/Dash_Cyber_02.wav");
            _dodgeWhooshes[2] = TryLoadClip("Assets/Audio/Combat/Dash_Cyber_03.wav");

            for (int k = 0; k < 3; k++)
            {
                if (_dodgeWhooshes[k] != null) continue;
                float duration = 0.26f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];
                float pitchMod = (k == 1) ? 1.14f : (k == 2 ? 0.92f : 1.0f);

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float p = t / duration;

                    // 1. Sub-bass kinetic impact punch (0 to 0.06s)
                    float punch = 0f;
                    if (t < 0.06f)
                    {
                        float punchFreq = Mathf.Lerp(160f * pitchMod, 48f * pitchMod, t / 0.06f);
                        punch = Mathf.Sin(2f * Mathf.PI * punchFreq * t) * Mathf.Exp(-t * 48f) * 0.95f;
                    }

                    // 2. High-speed pneumatic thruster blast
                    float noise = (Random.value * 2f - 1f);
                    float blastEnv = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(p * 1.8f) * Mathf.PI * 0.5f), 1.5f) * Mathf.Exp(-p * 5.2f);
                    float sweepFreq = Mathf.Lerp(2800f * pitchMod, 650f * pitchMod, p);
                    float whistle = Mathf.Sin(2f * Mathf.PI * sweepFreq * t) * 0.35f;
                    float blast = (noise * 0.75f + whistle) * blastEnv;

                    // 3. Cybernetic servo capacitor surge (FM chirp)
                    float carFreq = Mathf.Lerp(1100f * pitchMod, 320f * pitchMod, Mathf.Pow(p, 0.7f));
                    float modFreq = 180f * pitchMod;
                    float mod = Mathf.Sin(2f * Mathf.PI * modFreq * t) * 3.5f;
                    float servo = Mathf.Sin(2f * Mathf.PI * carFreq * t + mod) * Mathf.Exp(-t * 22f) * 0.42f;

                    // 4. Sub pressure wave
                    float sub = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 14f) * 0.60f;

                    // 5. Phase-shifting tail
                    float tail = 0f;
                    if (p > 0.15f)
                    {
                        float tailEnv = Mathf.Sin((p - 0.15f) / 0.85f * Mathf.PI) * Mathf.Exp(-p * 3.8f);
                        float flanger = Mathf.Sin(2f * Mathf.PI * (800f + Mathf.Sin(t * 45f) * 300f) * t);
                        tail = flanger * (Random.value * 0.5f) * tailEnv * 0.30f;
                    }

                    float mixed = (punch * 0.75f) + (blast * 0.85f) + (servo * 0.65f) + (sub * 0.50f) + tail;
                    buffer[i] = Mathf.Clamp(mixed * 0.92f, -1f, 1f);
                }

                _dodgeWhooshes[k] = AudioClip.Create($"Dash_Cyber_{k}", samples, 1, SampleRate, false);
                _dodgeWhooshes[k].SetData(buffer, 0);
            }
        }

        // ── 6. Parry Clinks (High-frequency blade deflect) ────────────────────
        private static void GenerateParryClinks()
        {
            _parryClinks = new AudioClip[2];

            for (int k = 0; k < 2; k++)
            {
                float duration = 0.35f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float ring1 = Mathf.Sin(2f * Mathf.PI * 3400f * t) * Mathf.Exp(-t * 14f);
                    float ring2 = Mathf.Sin(2f * Mathf.PI * 5100f * t) * Mathf.Exp(-t * 22f);
                    float click = (t < 0.005f) ? (Random.value * 2f - 1f) : 0f;

                    buffer[i] = Mathf.Clamp((ring1 * 0.5f + ring2 * 0.3f + click * 0.6f) * 0.8f, -1f, 1f);
                }

                _parryClinks[k] = AudioClip.Create($"Parry_Clink_{k}", samples, 1, SampleRate, false);
                _parryClinks[k].SetData(buffer, 0);
            }
        }

        // ── 7. Sword Draws (Metallic unsheathe blade slide & resonance) ─────────
        private static void GenerateSwordDraws()
        {
            _swordDrawClips = new AudioClip[2];

            for (int k = 0; k < 2; k++)
            {
                float duration = 0.42f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                float baseTone = (k == 0) ? 2400f : 2700f;

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = t / duration;

                    // Envelope: fast rise, lingering resonance
                    float env = Mathf.Sin(Mathf.Clamp01(progress * 1.5f) * Mathf.PI * 0.5f) * Mathf.Exp(-progress * 2.8f);

                    // Rising metallic scrape slide
                    float slideFreq = Mathf.Lerp(900f, baseTone, progress * progress);
                    float metalSlide = Mathf.Sin(2f * Mathf.PI * slideFreq * t) * 0.45f;

                    // High harmonic ring
                    float ring = Mathf.Sin(2f * Mathf.PI * (baseTone * 1.55f) * t) * Mathf.Exp(-t * 8f) * 0.35f
                               + Mathf.Sin(2f * Mathf.PI * (baseTone * 2.15f) * t) * Mathf.Exp(-t * 14f) * 0.20f;

                    // Scabbard friction hiss
                    float hiss = (Random.value * 2f - 1f) * (1f - progress) * 0.30f;

                    buffer[i] = Mathf.Clamp((metalSlide + ring + hiss) * env * 0.85f, -1f, 1f);
                }

                _swordDrawClips[k] = AudioClip.Create($"Sword_Draw_{k}", samples, 1, SampleRate, false);
                _swordDrawClips[k].SetData(buffer, 0);
            }
        }

        // ── 8. Sword Sheathes (Blade insertion slide & latch click) ───────────
        private static void GenerateSwordSheathes()
        {
            _swordSheatheClips = new AudioClip[2];

            for (int k = 0; k < 2; k++)
            {
                float duration = 0.38f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                float latchTime = duration - 0.08f;

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;

                    if (t < latchTime)
                    {
                        // Inward blade slide (descending pitch)
                        float slideProg = t / latchTime;
                        float slideFreq = Mathf.Lerp(2200f, 1100f, slideProg);
                        float env = Mathf.Sin(slideProg * Mathf.PI) * 0.45f;
                        float slide = Mathf.Sin(2f * Mathf.PI * slideFreq * t) * 0.4f;
                        float hiss = (Random.value * 2f - 1f) * 0.25f;
                        buffer[i] = (slide + hiss) * env;
                    }
                    else
                    {
                        // Final scabbard locking latch click
                        float lt = t - latchTime;
                        float latchDecay = Mathf.Exp(-lt * 45f);
                        float click = (lt < 0.008f) ? (Random.value * 2f - 1f) * 0.75f : 0f;
                        float metalSnap = Mathf.Sin(2f * Mathf.PI * 3200f * lt) * 0.5f;
                        float thud = Mathf.Sin(2f * Mathf.PI * 130f * lt) * 0.4f;
                        buffer[i] = Mathf.Clamp((click + metalSnap + thud) * latchDecay * 0.85f, -1f, 1f);
                    }
                }

                _swordSheatheClips[k] = AudioClip.Create($"Sword_Sheathe_{k}", samples, 1, SampleRate, false);
                _swordSheatheClips[k].SetData(buffer, 0);
            }
        }

        // ── 9. Jump Takeoffs (Soft natural aerodynamic whoosh & gentle athletic lift) ────
        private static void GenerateJumpTakeoffs()
        {
            _jumpTakeoffs = new AudioClip[3];
            _jumpTakeoffs[0] = TryLoadClip("Assets/Audio/Combat/Jump_Takeoff_Cyber_01.wav");
            _jumpTakeoffs[1] = TryLoadClip("Assets/Audio/Combat/Jump_Takeoff_Cyber_02.wav");
            _jumpTakeoffs[2] = TryLoadClip("Assets/Audio/Combat/Jump_Takeoff_Cyber_03.wav");

            for (int k = 0; k < 3; k++)
            {
                if (_jumpTakeoffs[k] != null) continue;
                float duration = (k == 2) ? 0.15f : 0.18f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];
                float b0 = 0f, b1 = 0f, b2 = 0f;

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float p = t / duration;

                    float env = Mathf.Pow(Mathf.Sin(p * Mathf.PI), 1.4f);

                    float white = (Random.value * 2f - 1f);
                    b0 = 0.99765f * b0 + white * 0.0990460f;
                    b1 = 0.96300f * b1 + white * 0.2965164f;
                    b2 = 0.57000f * b2 + white * 1.0526913f;
                    float pink = (b0 + b1 + b2) * 0.12f;

                    float swooshFreq = (k == 0) ? Mathf.Lerp(180f, 420f, p) : ((k == 1) ? Mathf.Lerp(90f, 260f, p) : Mathf.Lerp(160f, 340f, p));
                    float swoosh = Mathf.Sin(2f * Mathf.PI * swooshFreq * t) * 0.35f;
                    float body = Mathf.Sin(2f * Mathf.PI * (k == 1 ? 65f : 85f) * t) * (1f - p) * 0.25f;

                    float combined = (pink * 0.45f + swoosh * 0.40f + body * 0.25f) * env;
                    buffer[i] = Mathf.Clamp(combined * 0.42f, -1f, 1f);
                }

                _jumpTakeoffs[k] = AudioClip.Create($"Jump_Takeoff_Cyber_{k}", samples, 1, SampleRate, false);
                _jumpTakeoffs[k].SetData(buffer, 0);
            }
        }

        // ── 10. Jump Landings (Physical boot impacts) ─────────────────────────
        private static void GenerateJumpLandings()
        {
            _jumpLandings = new AudioClip[2];
            _jumpLandings[0] = TryLoadClip("Assets/Audio/Combat/Jump_Landing_Boots_01.wav");
            _jumpLandings[1] = TryLoadClip("Assets/Audio/Combat/Jump_Landing_Boots_02.wav");

            for (int k = 0; k < 2; k++)
            {
                if (_jumpLandings[k] != null) continue;
                float duration = 0.22f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float decay = Mathf.Exp(-t * 22f);

                    float slap = (t < 0.018f) ? (Random.value * 2f - 1f) * 0.85f : 0f;
                    float subThump = Mathf.Sin(2f * Mathf.PI * 58f * t) * Mathf.Exp(-t * 18f) * 0.80f;
                    float bodyThump = Mathf.Sin(2f * Mathf.PI * 135f * t) * Mathf.Exp(-t * 26f) * 0.50f;
                    float armorRattle = (Random.value * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.22f;

                    buffer[i] = Mathf.Clamp((slap + subThump + bodyThump + armorRattle) * decay * 0.90f, -1f, 1f);
                }

                _jumpLandings[k] = AudioClip.Create($"Jump_Landing_{k}", samples, 1, SampleRate, false);
                _jumpLandings[k].SetData(buffer, 0);
            }
        }

        private static void NormalizeBuffer(float[] buffer, float targetPeak = 0.94f)
        {
            if (buffer == null || buffer.Length == 0) return;
            float maxVal = 0.0001f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float abs = Mathf.Abs(buffer[i]);
                if (abs > maxVal) maxVal = abs;
            }
            float scale = targetPeak / maxVal;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Mathf.Clamp(buffer[i] * scale, -1f, 1f);
            }
        }

        // ── 11. Crowd Cheers (Stadium & Gladiator Arena Roar / Hit Cheers) ────
        private static void GenerateCrowdCheers()
        {
            _crowdCheers = new AudioClip[3];
            _crowdCheers[0] = TryLoadClip("Assets/Audio/Crowd/Crowd_Cheer_01.wav");
            _crowdCheers[1] = TryLoadClip("Assets/Audio/Crowd/Crowd_Cheer_02.wav");
            _crowdCheers[2] = TryLoadClip("Assets/Audio/Crowd/Crowd_Cheer_03.wav");

            // Variant 0: Hit Cheer (1.3s, instant attack, sharp visceral roar + clap burst)
            // Variant 1: Heavy Hit / Finisher (2.6s, stadium hype surge + upward formant + whistle)
            // Variant 2: Grand Entrance / Round Victory (4.0s, massive standing ovation thunder)
            float[] durations = { 1.3f, 2.6f, 4.0f };
            float[] attackTimes = { 0.025f, 0.06f, 0.15f };

            for (int k = 0; k < 3; k++)
            {
                if (_crowdCheers[k] != null) continue;
                float duration = durations[k];
                float attack = attackTimes[k];
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                float pitchMod = (k == 0) ? 1.05f : ((k == 1) ? 1.15f : 1.0f);
                float[] formants = { 420f * pitchMod, 850f * pitchMod, 1380f * pitchMod, 2250f * pitchMod, 3100f * pitchMod };
                float[] formantWeights = { 0.35f, 0.30f, 0.22f, 0.16f, 0.10f };

                float pinkAcc = 0f;
                float phase0 = 0f, phase1 = 0f, phase2 = 0f, phase3 = 0f, phase4 = 0f;

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float p = t / duration;

                    // Envelope: very fast attack for hits, sustained excitement, smooth release
                    float env;
                    if (t < attack)
                        env = Mathf.SmoothStep(0f, 1f, t / attack);
                    else if (p < 0.45f)
                        env = 1.0f - (p - (attack / duration)) * 0.12f;
                    else
                        env = Mathf.Exp(-(p - 0.45f) * (k == 0 ? 5.5f : 3.2f));

                    // Dense noise stream (human voice turbulent airflow)
                    float white = (Random.value * 2f - 1f);
                    pinkAcc = (pinkAcc * 0.90f) + (white * 0.10f);

                    // Formant phase accumulation to prevent warbling/clicks
                    phase0 += 2f * Mathf.PI * (formants[0] + Mathf.Sin(t * 3.1f) * 12f) / SampleRate;
                    phase1 += 2f * Mathf.PI * (formants[1] + Mathf.Cos(t * 4.3f) * 20f) / SampleRate;
                    phase2 += 2f * Mathf.PI * (formants[2] + Mathf.Sin(t * 5.7f) * 28f) / SampleRate;
                    phase3 += 2f * Mathf.PI * (formants[3] + Mathf.Cos(t * 6.2f) * 35f) / SampleRate;
                    phase4 += 2f * Mathf.PI * (formants[4] + Mathf.Sin(t * 7.1f) * 45f) / SampleRate;

                    float voices = (Mathf.Sin(phase0) * formantWeights[0]
                                  + Mathf.Sin(phase1) * formantWeights[1]
                                  + Mathf.Sin(phase2) * formantWeights[2]
                                  + Mathf.Sin(phase3) * formantWeights[3]
                                  + Mathf.Sin(phase4) * formantWeights[4]) * (0.75f + 0.25f * white);

                    // Visceral chest roar rumble (70Hz - 130Hz)
                    float rumble = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.32f;

                    // Applause / clapping layer mixed inside the cheer
                    float clapNoise = (white > 0.88f) ? (white - 0.88f) * 4.5f : 0f;

                    // Stadium whistle for heavy hype
                    float whistle = (k > 0 && p > 0.12f && p < 0.65f)
                        ? Mathf.Sin(2f * Mathf.PI * (1950f + Mathf.Sin(t * 8f) * 45f) * t) * Mathf.Sin((p - 0.12f) / 0.53f * Mathf.PI) * 0.22f
                        : 0f;

                    buffer[i] = (voices * 0.52f + pinkAcc * 0.38f + rumble * 0.25f + clapNoise * 0.30f + whistle) * env;
                }

                NormalizeBuffer(buffer, 0.94f);
                _crowdCheers[k] = AudioClip.Create($"Crowd_Cheer_Procedural_{k}", samples, 1, SampleRate, false);
                _crowdCheers[k].SetData(buffer, 0);
            }
        }

        // ── 12. Crowd Claps (Breather Rhythmic Clapping Loop) ──────────────────
        private static void GenerateCrowdClaps()
        {
            _crowdClaps = new AudioClip[2];
            _crowdClaps[0] = TryLoadClip("Assets/Audio/Crowd/Crowd_Clap_01.wav");
            _crowdClaps[1] = TryLoadClip("Assets/Audio/Crowd/Crowd_Clap_02.wav");

            for (int k = 0; k < 2; k++)
            {
                if (_crowdClaps[k] != null) continue;
                float duration = 2.2f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                int clapsCount = (k == 0) ? 14 : 18;
                float interval = duration / clapsCount;

                for (int c = 0; c < clapsCount; c++)
                {
                    // Stagger multiple clappers around each beat
                    for (int clapper = 0; clapper < 4; clapper++)
                    {
                        float clapCenter = c * interval + Random.Range(-0.035f, 0.035f);
                        int startSample = Mathf.Max(0, (int)(clapCenter * SampleRate));
                        int clapLen = (int)(SampleRate * 0.055f);

                        for (int s = 0; s < clapLen; s++)
                        {
                            int targetIdx = (startSample + s) % samples;
                            float ct = (float)s / SampleRate;
                            float clapEnv = Mathf.Exp(-ct * 75f);
                            float slapNoise = (Random.value * 2f - 1f) * clapEnv * 0.65f;
                            float bodyTone = Mathf.Sin(2f * Mathf.PI * (950f + clapper * 80f) * ct) * clapEnv * 0.35f;
                            buffer[targetIdx] += (slapNoise + bodyTone) * 0.35f;
                        }
                    }
                }

                // Smooth loop crossfade
                int fadeLen = (int)(SampleRate * 0.12f);
                for (int i = 0; i < fadeLen; i++)
                {
                    float factor = (float)i / fadeLen;
                    buffer[i] = Mathf.Lerp(buffer[samples - fadeLen + i], buffer[i], factor);
                }

                NormalizeBuffer(buffer, 0.92f);
                _crowdClaps[k] = AudioClip.Create($"Crowd_Clap_Procedural_{k}", samples, 1, SampleRate, false);
                _crowdClaps[k].SetData(buffer, 0);
            }
        }

        // ── 13. Crowd Boos (Disgruntled Disapproval / Negative Formants) ──────
        private static void GenerateCrowdBoos()
        {
            _crowdBoos = new AudioClip[2];
            _crowdBoos[0] = TryLoadClip("Assets/Audio/Crowd/Crowd_Boo_01.wav");
            _crowdBoos[1] = TryLoadClip("Assets/Audio/Crowd/Crowd_Boo_02.wav");

            for (int k = 0; k < 2; k++)
            {
                if (_crowdBoos[k] != null) continue;
                float duration = 2.4f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float p = t / duration;

                    // Downward pitch slide: 165Hz -> 105Hz
                    float baseFreq = Mathf.Lerp(165f, 105f, Mathf.Pow(p, 0.7f));
                    float formantA = baseFreq * 2.15f; // "OOO"
                    float formantB = baseFreq * 4.10f;

                    float env = Mathf.Sin(p * Mathf.PI) * (1f - p * 0.25f);
                    float vocal = (Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.45f
                                 + Mathf.Sin(2f * Mathf.PI * formantA * t) * 0.35f
                                 + Mathf.Sin(2f * Mathf.PI * formantB * t) * 0.20f);

                    float rumble = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.35f;
                    float breath = (Random.value * 2f - 1f) * 0.20f;

                    buffer[i] = (vocal + rumble + breath) * env;
                }

                NormalizeBuffer(buffer, 0.92f);
                _crowdBoos[k] = AudioClip.Create($"Crowd_Boo_Procedural_{k}", samples, 1, SampleRate, false);
                _crowdBoos[k].SetData(buffer, 0);
            }
        }

        // ── 14. Crowd Murmur Bed (Gladiator Arena Ambient Room Tone) ──────────
        private static void GenerateCrowdMurmurBeds()
        {
            _crowdMurmurBeds = new AudioClip[1];
            _crowdMurmurBeds[0] = TryLoadClip("Assets/Audio/Crowd/Crowd_Murmur_Bed.wav");

            if (_crowdMurmurBeds[0] != null) return;
            float duration = 6.0f;
            int samples = (int)(SampleRate * duration);
            float[] buffer = new float[samples];

            float lowAcc1 = 0f;
            float lowAcc2 = 0f;
            float phaseB1 = 0f, phaseB2 = 0f, phaseB3 = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;

                // Deep gladiator stadium arena acoustics
                float roomTone1 = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.28f;
                float roomTone2 = Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.20f;

                // Human babble noise layers (filtered noise for crowd murmuring)
                float noise = (Random.value * 2f - 1f);
                lowAcc1 = (lowAcc1 * 0.92f) + (noise * 0.08f);
                lowAcc2 = (lowAcc2 * 0.85f) + (noise * 0.15f);

                // Multi-band vocal formants simulating distant crowd conversation swells
                float swellA = (Mathf.Sin(t * 1.8f) + 1.2f) * 0.5f;
                float swellB = (Mathf.Cos(t * 2.4f) + 1.2f) * 0.5f;

                phaseB1 += 2f * Mathf.PI * 450f / SampleRate;
                phaseB2 += 2f * Mathf.PI * 880f / SampleRate;
                phaseB3 += 2f * Mathf.PI * 1450f / SampleRate;

                float babbleFormants = (Mathf.Sin(phaseB1) * 0.35f * swellA
                                      + Mathf.Sin(phaseB2) * 0.25f * swellB
                                      + Mathf.Sin(phaseB3) * 0.15f) * noise;

                // High frequency air & stadium ambiance shimmer
                float airShimmer = noise * 0.08f * (0.8f + 0.2f * Mathf.Sin(t * 3.5f));

                buffer[i] = roomTone1 + roomTone2 + lowAcc1 * 0.55f + lowAcc2 * 0.35f + babbleFormants * 0.65f + airShimmer;
            }

            // Seamless crossfade loop
            int loopCross = (int)(SampleRate * 0.35f);
            for (int i = 0; i < loopCross; i++)
            {
                float f = (float)i / loopCross;
                buffer[i] = Mathf.Lerp(buffer[samples - loopCross + i], buffer[i], f);
            }

            NormalizeBuffer(buffer, 0.92f);
            _crowdMurmurBeds[0] = AudioClip.Create("Crowd_Murmur_Bed_Procedural", samples, 1, SampleRate, false);
            _crowdMurmurBeds[0].SetData(buffer, 0);
        }

        // ── 15. Crowd Gasps (Reaction on Perfect Dodge / Near Miss) ───────────
        private static void GenerateCrowdGasps()
        {
            _crowdGasps = new AudioClip[2];
            _crowdGasps[0] = TryLoadClip("Assets/Audio/Crowd/Crowd_Gasp_01.wav");
            _crowdGasps[1] = TryLoadClip("Assets/Audio/Crowd/Crowd_Gasp_02.wav");

            for (int k = 0; k < 2; k++)
            {
                if (_crowdGasps[k] != null) continue;
                float duration = 0.85f;
                int samples = (int)(SampleRate * duration);
                float[] buffer = new float[samples];
                float pitch = (k == 0) ? 1.0f : 1.18f;

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    float p = t / duration;

                    // Sharp inhalation formant: 280Hz -> 680Hz
                    float fFreq = Mathf.Lerp(280f * pitch, 680f * pitch, Mathf.Pow(p, 0.5f));
                    float env = Mathf.Sin(p * Mathf.PI) * Mathf.Exp(-p * 2.2f);
                    float breath = (Random.value * 2f - 1f) * 0.55f;
                    float formant = Mathf.Sin(2f * Mathf.PI * fFreq * t) * 0.45f;

                    buffer[i] = (breath + formant) * env;
                }

                NormalizeBuffer(buffer, 0.92f);
                _crowdGasps[k] = AudioClip.Create($"Crowd_Gasp_Procedural_{k}", samples, 1, SampleRate, false);
                _crowdGasps[k].SetData(buffer, 0);
            }
        }
    }
}
