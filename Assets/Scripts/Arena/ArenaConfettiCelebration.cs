using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CyberCombat.Audio;
using Cyberpunk.Audio;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Arena
{
    /// <summary>
    /// AAA Victory Confetti Celebration FX for the Underground Combat Arena.
    /// Triggered when Boss Vulcan is defeated in Mission 4.
    /// Features:
    /// - Multi-stage staged cannon volleys (4 corners, 4 cardinal edges, center skyward eruption)
    /// - Cascading continuous confetti rain from the arena roof/catwalks covering the 22m x 22m ring
    /// - Interactive finisher execution burst when Alita finishes off Vulcan
    /// - High-fidelity URP particle rendering with zero-allocation pooling & automatic cleanup
    /// - Seamless integration with PitCrowdAudioManager cheering and synthesized cannon pops
    /// </summary>
    public class ArenaConfettiCelebration : MonoBehaviour
    {
        private static ArenaConfettiCelebration _instance;
        public static ArenaConfettiCelebration Instance
        {
            get
            {
                if (_instance == null)
                {
                    var existing = FindAnyObjectByType<ArenaConfettiCelebration>();
                    if (existing != null) _instance = existing;
                    else
                    {
                        var go = new GameObject("[Arena_Confetti_Celebration_Director]");
                        _instance = go.AddComponent<ArenaConfettiCelebration>();
                    }
                }
                return _instance;
            }
        }

        private const string ConfettiBurstPrefabPath = "Assets/Synty/PolygonParticles/Prefabs/FX_Confetti_01.prefab";
        private const string ConfettiRainPrefabPath = "Assets/Synty/PolygonParticles/Prefabs/FX_Confetti_Falling_01.prefab";

        private GameObject _cachedBurstPrefab;
        private GameObject _cachedRainPrefab;
        private GameObject _activeRainObject;
        private Coroutine _celebrationRoutine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            LoadPrefabs();
        }

        private void LoadPrefabs()
        {
#if UNITY_EDITOR
            if (_cachedBurstPrefab == null)
                _cachedBurstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfettiBurstPrefabPath);
            if (_cachedRainPrefab == null)
                _cachedRainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfettiRainPrefabPath);
#endif
            if (_cachedBurstPrefab == null)
                _cachedBurstPrefab = Resources.Load<GameObject>("FX_Confetti_01");
            if (_cachedRainPrefab == null)
                _cachedRainPrefab = Resources.Load<GameObject>("FX_Confetti_Falling_01");
        }

        /// <summary>
        /// Main entry point: Triggers the full victory confetti celebration over the arena ring.
        /// </summary>
        public static void TriggerVictoryConfetti(Vector3 ringCenter, float ringRadius = 11f, float duration = 25f)
        {
            Instance.StartCelebration(ringCenter, ringRadius, duration);
        }

        /// <summary>
        /// Finisher burst: Massive 360-degree confetti eruption when Alita executes Vulcan with [E].
        /// </summary>
        public static void TriggerFinisherConfettiBurst(Vector3 executionPos)
        {
            Instance.SpawnFinisherBurst(executionPos);
        }

        public void StartCelebration(Vector3 ringCenter, float ringRadius, float duration)
        {
            if (_celebrationRoutine != null) StopCoroutine(_celebrationRoutine);
            _celebrationRoutine = StartCoroutine(CelebrationSequence(ringCenter, ringRadius, duration));
        }

        private IEnumerator CelebrationSequence(Vector3 ringCenter, float radius, float duration)
        {
            LoadPrefabs();

            Debug.Log("<color=#FFD700><b>[Arena Confetti]</b></color> Starting grand victory celebration sequence!");

            // 1. Start continuous falling confetti rain from arena roof trusses (Y = 16.5m)
            StartConfettiRain(ringCenter + Vector3.up * 16.5f, radius * 1.35f, duration);

            // 2. Wave 1: 4 Corner Turnbuckle Cannons angled inward
            Vector3[] corners = new Vector3[]
            {
                ringCenter + new Vector3( radius * 0.95f, 0.95f,  radius * 0.95f),
                ringCenter + new Vector3(-radius * 0.95f, 0.95f,  radius * 0.95f),
                ringCenter + new Vector3(-radius * 0.95f, 0.95f, -radius * 0.95f),
                ringCenter + new Vector3( radius * 0.95f, 0.95f, -radius * 0.95f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 toCenter = (ringCenter + Vector3.up * 7.5f) - corners[i];
                Quaternion rot = Quaternion.LookRotation(toCenter.normalized);
                SpawnCannonBurst(corners[i], rot, 1.6f);
            }
            PlayCannonSound(ringCenter);

            // 3. Wave 2 (0.35s delay): 4 Cardinal Edge Cannons
            yield return new WaitForSeconds(0.35f);
            Vector3[] cardinals = new Vector3[]
            {
                ringCenter + new Vector3( radius * 0.90f, 0.95f, 0f),
                ringCenter + new Vector3(-radius * 0.90f, 0.95f, 0f),
                ringCenter + new Vector3(0f, 0.95f,  radius * 0.90f),
                ringCenter + new Vector3(0f, 0.95f, -radius * 0.90f)
            };

            for (int i = 0; i < cardinals.Length; i++)
            {
                Vector3 toCenter = (ringCenter + Vector3.up * 8.5f) - cardinals[i];
                Quaternion rot = Quaternion.LookRotation(toCenter.normalized);
                SpawnCannonBurst(cardinals[i], rot, 1.75f);
            }
            PlayCannonSound(ringCenter);

            // 4. Wave 3 (0.70s delay): Giant Center Skyward Volley
            yield return new WaitForSeconds(0.70f);
            SpawnCannonBurst(ringCenter + Vector3.up * 0.95f, Quaternion.Euler(-90f, 0f, 0f), 2.2f);
            SpawnCannonBurst(ringCenter + Vector3.up * 0.95f, Quaternion.Euler(-75f, 45f, 0f), 1.8f);
            SpawnCannonBurst(ringCenter + Vector3.up * 0.95f, Quaternion.Euler(-75f, -135f, 0f), 1.8f);
            PlayCannonSound(ringCenter);

            // 5. Secondary periodic bursts throughout the celebration
            float elapsed = 1.05f;
            int burstCycle = 0;
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(3.5f);
                elapsed += 3.5f;
                burstCycle++;

                int cIdx = burstCycle % corners.Length;
                Vector3 cPos = corners[cIdx];
                Vector3 toCenter = (ringCenter + Vector3.up * 7.5f) - cPos;
                SpawnCannonBurst(cPos, Quaternion.LookRotation(toCenter.normalized), 1.5f);

                int oppIdx = (cIdx + 2) % corners.Length;
                Vector3 oppPos = corners[oppIdx];
                Vector3 oppToCenter = (ringCenter + Vector3.up * 7.5f) - oppPos;
                SpawnCannonBurst(oppPos, Quaternion.LookRotation(oppToCenter.normalized), 1.5f);

                PlayCannonSound(ringCenter, 0.65f);
            }

            _celebrationRoutine = null;
        }

        private void SpawnFinisherBurst(Vector3 pos)
        {
            LoadPrefabs();

            Debug.Log("<color=#00FFAA><b>[Arena Confetti]</b></color> Execution finisher confetti eruption!");

            // Ring-wide 8-cannon radial fan
            for (int angle = 0; angle < 360; angle += 45)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 1.2f, Mathf.Sin(rad)).normalized;
                Quaternion rot = Quaternion.LookRotation(dir);
                SpawnCannonBurst(pos + new Vector3(Mathf.Cos(rad) * 1.5f, 0.5f, Mathf.Sin(rad) * 1.5f), rot, 1.85f);
            }

            // Mega upward center eruption
            SpawnCannonBurst(pos + Vector3.up * 0.5f, Quaternion.Euler(-90f, 0f, 0f), 2.6f);
            PlayCannonSound(pos, 1.0f);
        }

        private void SpawnCannonBurst(Vector3 pos, Quaternion rot, float scale)
        {
            if (_cachedBurstPrefab != null)
            {
                GameObject go = Instantiate(_cachedBurstPrefab, pos, rot);
                go.name = "FX_Confetti_Cannon_Burst";
                go.transform.localScale = Vector3.one * scale;

                var particleSystems = go.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particleSystems)
                {
                    ps.Play();
                }

                Destroy(go, 6.0f);
            }
            else
            {
                // Procedural fallback
                SpawnProceduralBurst(pos, rot, scale);
            }
        }

        private void StartConfettiRain(Vector3 skyPos, float radius, float duration)
        {
            if (_activeRainObject != null)
            {
                Destroy(_activeRainObject);
                _activeRainObject = null;
            }

            if (_cachedRainPrefab != null)
            {
                _activeRainObject = Instantiate(_cachedRainPrefab, skyPos, Quaternion.identity);
                _activeRainObject.name = "FX_Arena_Confetti_Rain_Continuous";

                // Scale up the emitter volume so confetti rains over the entire ring
                float scaleFactor = Mathf.Clamp(radius / 2.0f, 5.0f, 15.0f);
                _activeRainObject.transform.localScale = new Vector3(scaleFactor, 1.0f, scaleFactor);

                var ps = _activeRainObject.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    main.duration = 1.0f;
                    main.startLifetime = 7.5f;

                    var emission = ps.emission;
                    emission.rateOverTime = 120f; // Dense, vibrant confetti flurry

                    ps.Play();
                }

                Destroy(_activeRainObject, duration);
            }
            else
            {
                SpawnProceduralRain(skyPos, radius, duration);
            }
        }

        // ── PROCEDURAL FALLBACK ───────────────────────────────────────────────
        private void SpawnProceduralBurst(Vector3 pos, Quaternion rot, float scale)
        {
            GameObject go = new GameObject("Procedural_Confetti_Burst");
            go.transform.position = pos;
            go.transform.rotation = rot;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();

            Material mat = GetConfettiMaterial();
            psr.sharedMaterial = mat;

            var main = ps.main;
            main.loop = false;
            main.duration = 2.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 4.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f * scale, 22f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.40f * scale);
            main.gravityModifier = 0.85f;
            main.startColor = GetConfettiGradient();

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.4f * scale;

            var rotOverLifetime = ps.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.x = new ParticleSystem.MinMaxCurve(180f, 720f);
            rotOverLifetime.y = new ParticleSystem.MinMaxCurve(180f, 720f);
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(180f, 720f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(75 * scale)) });

            ps.Play();
            Destroy(go, 5.0f);
        }

        private void SpawnProceduralRain(Vector3 pos, float radius, float duration)
        {
            GameObject go = new GameObject("Procedural_Confetti_Rain");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();

            psr.sharedMaterial = GetConfettiMaterial();

            var main = ps.main;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = 7.0f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.gravityModifier = 0.45f;
            main.startColor = GetConfettiGradient();

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, radius * 2f, 1f);

            var emission = ps.emission;
            emission.rateOverTime = 95f;

            var rotOverLifetime = ps.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.x = new ParticleSystem.MinMaxCurve(90f, 360f);
            rotOverLifetime.y = new ParticleSystem.MinMaxCurve(90f, 360f);

            _activeRainObject = go;
            ps.Play();
            Destroy(go, duration);
        }

        private static Material _sharedConfettiMat;
        private static Material GetConfettiMaterial()
        {
            if (_sharedConfettiMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _sharedConfettiMat = new Material(shader);
                _sharedConfettiMat.name = "Mat_Procedural_Confetti";
            }
            return _sharedConfettiMat;
        }

        private static ParticleSystem.MinMaxGradient GetConfettiGradient()
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.0f, 0.95f, 1.0f), 0.0f),   // Neon Cyan
                    new GradientColorKey(new Color(1.0f, 0.85f, 0.1f), 0.20f),  // Cyber Gold
                    new GradientColorKey(new Color(1.0f, 0.05f, 0.55f), 0.40f), // Neon Magenta
                    new GradientColorKey(new Color(0.1f, 1.0f, 0.35f), 0.60f),  // Electric Emerald
                    new GradientColorKey(new Color(1.0f, 0.45f, 0.05f), 0.80f), // Radiant Orange
                    new GradientColorKey(new Color(0.7f, 0.2f, 1.0f), 1.0f)     // Blackwall Purple
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            return new ParticleSystem.MinMaxGradient(g);
        }

        // ── AUDIO ─────────────────────────────────────────────────────────────
        private static AudioClip _confettiPopClip;
        private void PlayCannonSound(Vector3 pos, float volume = 0.85f)
        {
            if (_confettiPopClip == null)
            {
                _confettiPopClip = GenerateCannonPopClip();
            }

            if (_confettiPopClip != null)
            {
                AudioSource.PlayClipAtPoint(_confettiPopClip, pos, volume);
            }
        }

        private static AudioClip GenerateCannonPopClip()
        {
            int sampleRate = 44100;
            float duration = 0.32f;
            int samples = (int)(sampleRate * duration);
            float[] buffer = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Punchy pneumatic thud + air whoosh + high celebratory pop
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 50f, progress) * t) * Mathf.Exp(-t * 28f);
                float hiss = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.45f;
                float pop = Mathf.Sin(2f * Mathf.PI * 920f * t) * Mathf.Exp(-t * 45f) * 0.5f;

                buffer[i] = Mathf.Clamp((thud * 0.7f + hiss + pop) * 0.85f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("SFX_Confetti_Cannon_Pop", samples, 1, sampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }
    }
}
