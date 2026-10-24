using System;
using System.Collections;
using UnityEngine;
using InsaneGun.Combat;

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Manages Alita's Supercharged URM Berserker State after absorbing the ancient Martian Antimatter Core.
    /// Provides:
    /// - Blinding Damascus blade plasma ignition (HDR cyan-crimson emissive boost).
    /// - High-speed electric blade ribbon trail.
    /// - Cybernetic chest reactor point light & crackling plasma lightning particle aura.
    /// - 360° Supersonic Antimatter Cleave execution used to one-shot the Pilot Army.
    /// </summary>
    public class AlitaSuperchargeAura : MonoBehaviour
    {
        [Header("State")]
        public bool isSupercharged = false;

        [Header("Visual Effects")]
        public Light chestReactorLight;
        public ParticleSystem electricAuraParticles;
        public TrailRenderer bladeTrail;

        private Animator _animator;
        private Renderer[] _bladeRenderers;
        private Color _originalBladeColor;
        private bool _isInitialized = false;

        private static Texture2D s_softCircleTex;
        public static Texture2D GetOrCreateSoftCircleTexture()
        {
            if (s_softCircleTex != null) return s_softCircleTex;
            int size = 64;
            s_softCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            s_softCircleTex.name = "Aura_SoftCircle_Tex";
            s_softCircleTex.wrapMode = TextureWrapMode.Clamp;
            s_softCircleTex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = (size - 1) * 0.5f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.Pow(alpha, 1.8f); // Smooth radial glow falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            s_softCircleTex.SetPixels(pixels);
            s_softCircleTex.Apply();
            return s_softCircleTex;
        }

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            InitializeComponents();
            if (isSupercharged)
            {
                ApplyRedBladeEmission();
            }
        }

        public void InitializeComponents()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _animator = GetComponent<Animator>();

            // Find Damascus Blade renderers
            var renderers = GetComponentsInChildren<Renderer>(true);
            var bladeList = new System.Collections.Generic.List<Renderer>();
            foreach (var r in renderers)
            {
                if (r.name.IndexOf("Damascus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.name.IndexOf("Blade", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.name.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bladeList.Add(r);
                }
            }
            _bladeRenderers = bladeList.ToArray();

            // Find or setup blade trail (fiery Martian crimson red)
            bladeTrail = GetComponentInChildren<TrailRenderer>(true);
            if (bladeTrail == null && _bladeRenderers.Length > 0)
            {
                var trailGO = new GameObject("Supercharge_Blade_Trail");
                trailGO.transform.SetParent(_bladeRenderers[0].transform, false);
                trailGO.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                bladeTrail = trailGO.AddComponent<TrailRenderer>();
                bladeTrail.time = 0.28f;
                bladeTrail.startWidth = 0.24f;
                bladeTrail.endWidth = 0.02f;

                var trailMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit"));
                trailMat.color = new Color(1.0f, 0.20f, 0.14f, 0.90f);
                if (trailMat.HasProperty("_EmissionColor"))
                {
                    trailMat.EnableKeyword("_EMISSION");
                    trailMat.SetColor("_EmissionColor", new Color(5.5f, 0.65f, 0.20f));
                }
                bladeTrail.sharedMaterial = trailMat;
                bladeTrail.enabled = false;
            }
            else if (bladeTrail != null && bladeTrail.sharedMaterial != null)
            {
                bladeTrail.sharedMaterial.color = new Color(1.0f, 0.20f, 0.14f, 0.90f);
                if (bladeTrail.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    bladeTrail.sharedMaterial.EnableKeyword("_EMISSION");
                    bladeTrail.sharedMaterial.SetColor("_EmissionColor", new Color(5.5f, 0.65f, 0.20f));
                }
            }

            // Setup Chest Reactor Point Light (fiery Martian crimson red)
            if (chestReactorLight == null)
            {
                var existingLight = transform.Find("Alita_Chest_Reactor_Light");
                if (existingLight != null)
                {
                    chestReactorLight = existingLight.GetComponent<Light>();
                }
                else
                {
                    GameObject lGO = new GameObject("Alita_Chest_Reactor_Light");
                    lGO.transform.SetParent(transform, false);
                    lGO.transform.localPosition = new Vector3(0f, 1.25f, 0.18f);
                    chestReactorLight = lGO.AddComponent<Light>();
                    chestReactorLight.type = LightType.Point;
                    chestReactorLight.range = 8.0f;
                    chestReactorLight.intensity = 0f; // Off until supercharged
                }
            }
            if (chestReactorLight != null)
            {
                chestReactorLight.color = new Color(1.0f, 0.20f, 0.12f);
            }

            // Setup Electric Lightning / Plasma Ember Aura Particle System (Martian Crimson Sparks)
            if (electricAuraParticles == null)
            {
                var existingPS = transform.Find("Alita_Electric_Lightning_Aura");
                if (existingPS != null)
                {
                    electricAuraParticles = existingPS.GetComponent<ParticleSystem>();
                }
                else
                {
                    GameObject psGO = new GameObject("Alita_Electric_Lightning_Aura");
                    psGO.transform.SetParent(transform, false);
                    psGO.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                    electricAuraParticles = psGO.AddComponent<ParticleSystem>();

                    var main = electricAuraParticles.main;
                    main.startLifetime = 0.35f;
                    main.startSpeed = 1.8f;
                    main.startSize = 0.16f;
                    main.startColor = new Color(1.0f, 0.28f, 0.15f, 0.95f);
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.loop = true;
                    main.playOnAwake = false;

                    var shape = electricAuraParticles.shape;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.65f;

                    var emission = electricAuraParticles.emission;
                    emission.rateOverTime = 28f;

                    // Color over lifetime: bright core -> red plasma -> amber fade
                    var colOverLife = electricAuraParticles.colorOverLifetime;
                    colOverLife.enabled = true;
                    Gradient grad = new Gradient();
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(1.0f, 0.85f, 0.70f), 0f),
                            new GradientColorKey(new Color(1.0f, 0.20f, 0.12f), 0.45f),
                            new GradientColorKey(new Color(0.85f, 0.12f, 0.05f), 1f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.0f, 0f),
                            new GradientAlphaKey(0.95f, 0.2f),
                            new GradientAlphaKey(0.80f, 0.65f),
                            new GradientAlphaKey(0.0f, 1f)
                        }
                    );
                    colOverLife.color = grad;

                    // Size over lifetime: smooth shrink
                    var sizeOverLife = electricAuraParticles.sizeOverLifetime;
                    sizeOverLife.enabled = true;
                    AnimationCurve szCurve = new AnimationCurve();
                    szCurve.AddKey(0f, 0.6f);
                    szCurve.AddKey(0.3f, 1.0f);
                    szCurve.AddKey(1.0f, 0.15f);
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, szCurve);

                    var rend = psGO.GetComponent<ParticleSystemRenderer>();
                    Material particleMat = null;
#if UNITY_EDITOR
                    particleMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Cutout_Round.mat")
                               ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Sparkle.mat");
#endif
                    if (particleMat == null)
                    {
                        var pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                        particleMat = new Material(pShader);
                        particleMat.mainTexture = GetOrCreateSoftCircleTexture();
                    }
                    else
                    {
                        particleMat = new Material(particleMat);
                    }
                    particleMat.color = new Color(1.0f, 0.22f, 0.12f, 1.0f);
                    if (particleMat.HasProperty("_BaseColor")) particleMat.SetColor("_BaseColor", new Color(1.0f, 0.22f, 0.12f, 1.0f));
                    if (particleMat.HasProperty("_EmissionColor"))
                    {
                        particleMat.EnableKeyword("_EMISSION");
                        particleMat.SetColor("_EmissionColor", new Color(5.5f, 0.70f, 0.25f));
                    }
                    rend.sharedMaterial = particleMat;
                }
            }

            // Sync Holographic Ghost Emitter dodge colors to Martian Crimson
            var ghostEmitter = GetComponent<HolographicGhostEmitter>();
            if (ghostEmitter != null)
            {
                ghostEmitter.defaultGhostColor = new Color(1.0f, 0.20f, 0.12f, 0.65f);
                ghostEmitter.dodgeGhostColor = new Color(1.0f, 0.25f, 0.14f, 0.60f);
                ghostEmitter.heavyComboColor = new Color(1.3f, 0.25f, 0.10f, 0.75f);
            }
        }

        private void ApplyRedBladeEmission()
        {
            if (_bladeRenderers == null || _bladeRenderers.Length == 0) return;
            foreach (var r in _bladeRenderers)
            {
                if (r != null && r.material != null)
                {
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", new Color(4.8f, 0.35f, 0.15f) * 2.8f);
                }
            }
        }

        /// <summary>
        /// Activates Alita's supercharged URM state.
        /// </summary>
        public void ActivateSupercharge()
        {
            InitializeComponents();
            isSupercharged = true;

            // 1. Ignite Damascus Blade Emission with incandescent Martian crimson red
            ApplyRedBladeEmission();

            // 2. Enable Blade Trail
            if (bladeTrail != null) bladeTrail.enabled = true;

            // 3. Ignite Chest Light
            if (chestReactorLight != null)
            {
                chestReactorLight.color = new Color(1.0f, 0.20f, 0.12f);
                chestReactorLight.intensity = 3.2f;
            }

            // 4. Play Electric Aura
            if (electricAuraParticles != null) electricAuraParticles.Play();

            // 5. Also configure main sword controller trail if present
            var swordCtrl = GetComponent<InsaneGunSwordController>();
            if (swordCtrl != null && swordCtrl.swordTrail != null)
            {
                var trail = swordCtrl.swordTrail;
                Gradient redGrad = new Gradient();
                redGrad.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(1.0f, 0.22f, 0.12f), 0.25f),
                        new GradientColorKey(new Color(0.95f, 0.12f, 0.05f), 1f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.95f, 0f),
                        new GradientAlphaKey(0.70f, 0.45f),
                        new GradientAlphaKey(0.0f, 1f)
                    }
                );
                trail.colorGradient = redGrad;
                if (trail.sharedMaterial != null)
                {
                    if (trail.sharedMaterial.HasProperty("_NeonColor"))
                        trail.sharedMaterial.SetColor("_NeonColor", new Color(5.2f, 0.40f, 0.16f));
                    if (trail.sharedMaterial.HasProperty("_CoreColor"))
                        trail.sharedMaterial.SetColor("_CoreColor", Color.white * 2.2f);
                }
            }

            Debug.Log("[AlitaSuperchargeAura] URM Combat Heart Synchronized! Alita is SUPERCHARGED with Martian Crimson plasma.");
        }

        /// <summary>
        /// Performs the 360° Antimatter Berserker Cataclysm Cleave animation,
        /// invoking the obliteration callback at the apex of the spin.
        /// </summary>
        public IEnumerator PerformCataclysmCleaveRoutine(Action onCleaveApex)
        {
            InitializeComponents();

            // Play fast spinning cleave animation
            if (_animator != null)
            {
                _animator.CrossFadeInFixedTime("Draw_Sword", 0.08f);
            }

            // Windup pause
            yield return new WaitForSeconds(0.22f);

            // Trigger the apex obliteration!
            onCleaveApex?.Invoke();

            // Dynamic rotation flare
            float spinTimer = 0f;
            float spinDuration = 0.45f;
            Quaternion startRot = transform.rotation;

            while (spinTimer < spinDuration)
            {
                spinTimer += Time.deltaTime;
                float norm = spinTimer / spinDuration;
                transform.rotation = startRot * Quaternion.Euler(0f, norm * 360f, 0f);
                yield return null;
            }

            transform.rotation = startRot;
        }

        private void Update()
        {
            if (!isSupercharged) return;

            // Subtle pulsing glow on chest reactor
            if (chestReactorLight != null)
            {
                chestReactorLight.intensity = 2.8f + Mathf.Sin(Time.time * 6f) * 0.65f;
            }
        }
    }
}
