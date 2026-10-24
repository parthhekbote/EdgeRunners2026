using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InsaneGun.Combat
{
    /// <summary>
    /// High-Fidelity Cyberpunk Melee Combat Visual Effects Engine.
    /// Powered by the official Synty POLYGON - Particle FX package:
    /// - Multi-layered sword slash arcs & thrust cones (FX_SwordSlash_01, FX_SwordStab_01, FX_Slash_Large_01)
    /// - Visceral hit sparks & metal impact ricochets (FX_Sparks_01, FX_Impact_Metal_01)
    /// - Heavy finisher detonations & shockwaves (FX_Explosion_01, FX_Impact_Large_01)
    /// - Agile dodge rush & ground dust kickup (FX_Swirl_Fast_01, FX_Trail_Dust_01)
    /// - Dynamic real-time point-light flashes & smooth alpha blending with zero visual blowout
    /// </summary>
    public class AAACyberCombatVFX : MonoBehaviour
    {
        public static AAACyberCombatVFX Instance { get; private set; }

        // Prefab cache
        private static GameObject _prefabSwordSlash;
        private static GameObject _prefabSwordStab;
        private static GameObject _prefabSlashSmall;
        private static GameObject _prefabSlashLarge;
        private static GameObject _prefabSparks;
        private static GameObject _prefabImpactMetal;
        private static GameObject _prefabImpactLarge;
        private static GameObject _prefabExplosion;
        private static GameObject _prefabDodgeSwirl;
        private static GameObject _prefabSparklesTrail;
        private static GameObject _prefabDustTrail;
        private static bool _prefabsLoaded = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            LoadPrefabs();
        }

        public static void LoadPrefabs()
        {
            if (_prefabsLoaded && _prefabSlashSmall != null) return;

#if UNITY_EDITOR
            string basePath = "Assets/Synty/PolygonParticles/Prefabs/";
            _prefabSwordSlash   = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_SwordSlash_01.prefab");
            _prefabSwordStab    = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_SwordStab_01.prefab");
            _prefabSlashSmall   = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Slash_01.prefab");
            _prefabSlashLarge   = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Slash_Large_01.prefab");
            _prefabSparklesTrail = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Sparkles_Trail_01.prefab");
            _prefabSparks       = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Sparks_01.prefab");
            _prefabImpactMetal  = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Impact_Metal_01.prefab");
            _prefabImpactLarge  = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Impact_Large_01.prefab");
            _prefabExplosion    = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Explosion_01.prefab");
            _prefabDodgeSwirl   = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Swirl_Fast_01.prefab");
            _prefabDustTrail    = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(basePath + "FX_Trail_Dust_01.prefab");
#endif
            _prefabsLoaded = true;
        }

        // ── 0. WEAPON RIBBON TRAIL CONFIGURATION ─────────────────────────────
        /// <summary>
        /// Configures weapon TrailRenderer with luminous Tron Light Cycle wall geometry,
        /// Additive HDR blending, and tapered katana silhouette curve for high-velocity swings.
        /// </summary>
        public static void ConfigureBladeTrail(TrailRenderer trail, bool isPlayer)
        {
            if (trail == null) return;
            trail.time = 0.24f; // Responsive, kinetic 0.24s Tron ribbon wall
            trail.minVertexDistance = 0.008f;
            trail.numCornerVertices = 6;
            trail.numCapVertices = 6;
            trail.autodestruct = false;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;

            // Broad, razor-sharp Tron Light Cycle wall curve
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 0.42f);   // 0.42m broad Tron laser wall leading edge
            widthCurve.AddKey(0.18f, 0.35f);
            widthCurve.AddKey(0.50f, 0.22f);
            widthCurve.AddKey(0.80f, 0.08f);
            widthCurve.AddKey(1f, 0.01f);
            trail.widthCurve = widthCurve;

            // Tron Electric Cyan (Player in CyberCity) vs Tron Martian Crimson (Player on Mars) vs Tron Laser Amber/Orange (Enemy)
            bool isMarsScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Mars", System.StringComparison.OrdinalIgnoreCase) >= 0;
            Color playerNeon = isMarsScene ? new Color(1.0f, 0.22f, 0.12f) : new Color(0f, 0.95f, 1.0f);
            Color neonColor = isPlayer ? playerNeon : new Color(1.0f, 0.45f, 0.05f);
            Color coreColor = Color.white;

            // Translucent glowing laser ribbon with soft falloff
            Gradient colorGrad = new Gradient();
            colorGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(coreColor, 0f),
                    new GradientColorKey(neonColor, 0.20f),
                    new GradientColorKey(neonColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.70f, 0.45f),
                    new GradientAlphaKey(0.0f, 1f)
                }
            );
            trail.colorGradient = colorGrad;

            Shader shader = Shader.Find("Cyberpunk/Tron_Blade_LightWall") 
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                         ?? Shader.Find("Sprites/Default");
            
            Material m = trail.sharedMaterial;
            if (m == null || m.shader != shader)
            {
                m = new Material(shader);
                m.name = isPlayer ? "M_Player_TronBladeLightWall" : "M_Enemy_TronBladeLightWall";
            }
            m.SetColor("_NeonColor", neonColor * 1.8f);
            m.SetColor("_CoreColor", Color.white * 2.0f);
            m.SetFloat("_RailWidth", 0.12f);
            m.SetFloat("_RailGlow", 1.8f);
            m.SetFloat("_ScanlineFreq", 22.0f);
            m.SetFloat("_ScanlineIntensity", 0.65f);
            m.SetFloat("_CoreFilamentWidth", 0.32f);

            trail.sharedMaterial = m;
        }

        // ── 1. SWORD SLASH ARCS & COMBOS ─────────────────────────────────────
        /// <summary>
        /// Spawns procedural Tron Light Cycle Wall crescent slash arcs on EVERY melee swing (Combo 1-7).
        /// </summary>
        public static void SpawnSlashArc(Transform attacker, int comboIndex, bool isFinisher)
        {
            if (attacker == null) return;
            float scale = attacker.localScale.x;
            float heightOffset = scale > 1.15f ? (scale - 1f) * 0.65f : 0f;
            TronSlashArcGenerator.SpawnTronSlash(attacker, comboIndex, isFinisher, scale, heightOffset);
        }

        // ── 2. VISCERAL HIT IMPACTS & SPARKS ──────────────────────────────────
        /// <summary>
        /// Spawns clean, punchy metal sparks at point of sword impact.
        /// Zero explosions, zero visual blowout.
        /// </summary>
        public static void SpawnHitImpact(Vector3 pos, Vector3 cutDir, int comboIndex, bool isFinisher)
        {
            LoadPrefabs();

            GameObject container = new GameObject("Combat_Hit_Sparks_FX");
            container.transform.position = pos;

            Color themeColor = isFinisher ? new Color(1.0f, 0.72f, 0.15f) : (comboIndex % 2 == 0 ? new Color(0.95f, 0.12f, 0.65f) : new Color(0f, 0.88f, 1.0f));
            Quaternion impactRot = cutDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(cutDir) : Quaternion.identity;

            // Crisp needle impact sparks - no square/box particles
            GameObject chosenSparks = _prefabImpactMetal != null ? _prefabImpactMetal : _prefabSparks;
            if (chosenSparks != null)
            {
                GameObject sparks = Instantiate(chosenSparks, pos, impactRot, container.transform);
                sparks.name = "Hit_Sparks";
                sparks.transform.localScale = Vector3.one * (isFinisher ? 0.75f : 0.50f);
                TintParticleSystems(sparks, themeColor);
                var ps = sparks.GetComponentsInChildren<ParticleSystem>();
                foreach (var p in ps) p.Play();
            }

            // Subtle micro point light flash
            SpawnPointLightFlash(pos, themeColor, 1.1f, 0.22f, 0.045f, container.transform);

            SafeDestroy(container, 0.8f);
        }

        // ── 3. DODGE / DASH VFX ──────────────────────────────────────────────
        /// <summary>
        /// Spawns agile air swirl and dust rush along the dodge vector.
        /// </summary>
        public static void SpawnDodgeFX(Vector3 origin, Vector3 moveDir)
        {
            LoadPrefabs();

            Quaternion rot = moveDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(moveDir) : Quaternion.identity;

            if (_prefabDodgeSwirl != null)
            {
                GameObject swirl = Instantiate(_prefabDodgeSwirl, origin + Vector3.up * 0.85f, rot);
                swirl.name = "Dodge_Swirl_FX";
                swirl.transform.localScale = Vector3.one * 0.85f;
                TintParticleSystems(swirl, new Color(0f, 0.88f, 1.0f, 0.75f));
                var ps = swirl.GetComponentsInChildren<ParticleSystem>();
                foreach (var p in ps) p.Play();
                SafeDestroy(swirl, 0.9f);
            }

            if (_prefabDustTrail != null)
            {
                GameObject dust = Instantiate(_prefabDustTrail, origin + Vector3.up * 0.05f, rot);
                dust.name = "Dodge_Dust_FX";
                dust.transform.localScale = Vector3.one * 0.75f;
                var ps = dust.GetComponentsInChildren<ParticleSystem>();
                foreach (var p in ps) p.Play();
                SafeDestroy(dust, 0.8f);
            }
        }

        // ── 4. PERFECT PARRY VFX ─────────────────────────────────────────────
        public static void SpawnParryFX(Vector3 pos)
        {
            LoadPrefabs();
            GameObject container = new GameObject("Parry_Sparks_FX");
            container.transform.position = pos;

            Color gold = new Color(1f, 0.85f, 0.2f);
            if (_prefabImpactLarge != null)
            {
                GameObject l = Instantiate(_prefabImpactLarge, pos, Quaternion.identity, container.transform);
                TintParticleSystems(l, gold);
            }
            if (_prefabImpactMetal != null)
            {
                GameObject s = Instantiate(_prefabImpactMetal, pos, Quaternion.identity, container.transform);
                TintParticleSystems(s, gold);
            }

            SpawnPointLightFlash(pos, gold, 3.2f, 0.85f, 0.10f, container.transform);
            SafeDestroy(container, 1.2f);
        }

        private static void SafeDestroy(GameObject go, float delay = 0f)
        {
            if (go == null) return;
            if (Application.isPlaying)
            {
                if (delay > 0f) Destroy(go, delay);
                else Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        // ── HELPERS ──────────────────────────────────────────────────────────
        private static void TintParticleSystems(GameObject root, Color color)
        {
            if (root == null) return;
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                Color current = main.startColor.color;
                // Preserve original alpha if transparent
                main.startColor = new Color(color.r, color.g, color.b, current.a > 0.01f ? current.a : color.a);
            }
        }

        private static void SpawnPointLightFlash(Vector3 pos, Color color, float range, float intensity, float duration, Transform parent = null)
        {
            GameObject lObj = new GameObject("VFX_Point_Flash");
            lObj.transform.position = pos + Vector3.up * 0.15f;
            if (parent != null) lObj.transform.SetParent(parent);

            var light = lObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;

            lObj.AddComponent<LightFlashFader>().Initialize(duration);
        }
    }

    /// <summary>
    /// Subtle auto-fading light component for instantaneous dynamic combat flashes.
    /// </summary>
    public class LightFlashFader : MonoBehaviour
    {
        private Light _light;
        private float _initialIntensity;
        private float _duration;
        private float _timer;

        public void Initialize(float duration)
        {
            _light = GetComponent<Light>();
            _initialIntensity = _light != null ? _light.intensity : 1f;
            _duration = Mathf.Max(0.01f, duration);
            _timer = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = _timer / _duration;
            if (_light != null)
            {
                // Quadratic falloff: punchy flash then rapid fade
                _light.intensity = Mathf.Lerp(_initialIntensity, 0f, t * t);
            }
            if (_timer >= _duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
