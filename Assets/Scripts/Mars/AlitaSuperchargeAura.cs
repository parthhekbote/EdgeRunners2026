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

        private void Awake()
        {
            InitializeComponents();
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

            // Find or setup blade trail
            bladeTrail = GetComponentInChildren<TrailRenderer>(true);
            if (bladeTrail == null && _bladeRenderers.Length > 0)
            {
                var trailGO = new GameObject("Supercharge_Blade_Trail");
                trailGO.transform.SetParent(_bladeRenderers[0].transform, false);
                trailGO.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                bladeTrail = trailGO.AddComponent<TrailRenderer>();
                bladeTrail.time = 0.28f;
                bladeTrail.startWidth = 0.22f;
                bladeTrail.endWidth = 0.02f;

                var trailMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                trailMat.color = new Color(0.15f, 0.95f, 1.0f, 0.85f);
                if (trailMat.HasProperty("_EmissionColor"))
                {
                    trailMat.EnableKeyword("_EMISSION");
                    trailMat.SetColor("_EmissionColor", new Color(0.5f, 3.5f, 5.0f));
                }
                bladeTrail.sharedMaterial = trailMat;
                bladeTrail.enabled = false;
            }

            // Setup Chest Reactor Point Light
            if (chestReactorLight == null)
            {
                GameObject lGO = new GameObject("Alita_Chest_Reactor_Light");
                lGO.transform.SetParent(transform, false);
                lGO.transform.localPosition = new Vector3(0f, 1.25f, 0.18f);
                chestReactorLight = lGO.AddComponent<Light>();
                chestReactorLight.type = LightType.Point;
                chestReactorLight.color = new Color(0.2f, 0.92f, 1.0f);
                chestReactorLight.range = 8.0f;
                chestReactorLight.intensity = 0f; // Off until supercharged
            }

            // Setup Electric Lightning Aura Particle System
            if (electricAuraParticles == null)
            {
                GameObject psGO = new GameObject("Alita_Electric_Lightning_Aura");
                psGO.transform.SetParent(transform, false);
                psGO.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                electricAuraParticles = psGO.AddComponent<ParticleSystem>();

                var main = electricAuraParticles.main;
                main.startLifetime = 0.18f;
                main.startSpeed = 2.4f;
                main.startSize = 0.14f;
                main.startColor = new Color(0.2f, 0.95f, 1.0f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.loop = true;
                main.playOnAwake = false;

                var shape = electricAuraParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.65f;

                var emission = electricAuraParticles.emission;
                emission.rateOverTime = 28f;

                var rend = psGO.GetComponent<ParticleSystemRenderer>();
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                rend.material.color = new Color(0.15f, 0.95f, 1.0f);
            }
        }

        /// <summary>
        /// Activates Alita's supercharged URM state.
        /// </summary>
        public void ActivateSupercharge()
        {
            InitializeComponents();
            isSupercharged = true;

            // 1. Ignite Damascus Blade Emission
            foreach (var r in _bladeRenderers)
            {
                if (r != null && r.material != null)
                {
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", new Color(0.25f, 2.8f, 4.2f) * 2.5f);
                }
            }

            // 2. Enable Blade Trail
            if (bladeTrail != null) bladeTrail.enabled = true;

            // 3. Ignite Chest Light
            if (chestReactorLight != null) chestReactorLight.intensity = 3.2f;

            // 4. Play Electric Aura
            if (electricAuraParticles != null) electricAuraParticles.Play();

            Debug.Log("[AlitaSuperchargeAura] URM Combat Heart Synchronized! Alita is SUPERCHARGED.");
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
