using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;
using InsaneGun.Combat;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Handles the cinematic materialization of Rogue AI holographic enemies emerging
    /// directly from the 200m Cyberpunk Blackwall barrier plane into the cyberspace arena.
    /// Can also be attached to any enemy to make them permanently holographic.
    /// </summary>
    public class HolographicBlackwallEmergence : MonoBehaviour
    {
        [Header("Emergence Parameters")]
        [Tooltip("Time in seconds to complete materialization from Blackwall.")]
        public float emergenceDuration = 2.0f;
        [Tooltip("Forward step speed as enemy walks out of the Blackwall plane.")]
        public float forwardStepSpeed = 2.0f;
        [Tooltip("Shader used for holographic glitch materialization.")]
        public Shader holographicShader;

        [Header("Permanent Holographic Setting")]
        [Tooltip("If true, enemy remains an ethereal, glowing holographic Rogue AI entity permanently throughout combat.")]
        public bool keepPermanentlyHolographic = true;

        [Header("Hologram Color Palette")]
        public Color holographicColor = new Color(1.0f, 0.08f, 0.35f, 1.0f);
        public Color rimGlowColor = new Color(3.5f, 0.15f, 1.2f, 1.0f);
        public float coreTranslucency = 0.35f;

        [Header("VFX & Audio")]
        [Tooltip("Optional ghost trail emitter during emergence.")]
        public HolographicGhostEmitter ghostEmitter;
        [Tooltip("Audio clip for rogue AI emergence screech.")]
        public AudioClip emergenceAudioClip;

        private Renderer[] _renderers;
        private Material[] _holographicMats;
        private Material[][] _originalMats;
        private bool _isEmerging = true;
        private float _emergenceTimer = 0f;
        private SwordEnemyAI _swordAI;
        private InsaneGunSwordController _insaneAI;
        private CharacterController _characterController;

        public bool IsEmerging => _isEmerging;

        private void Awake()
        {
            _swordAI = GetComponent<SwordEnemyAI>();
            _insaneAI = GetComponent<InsaneGunSwordController>();
            _characterController = GetComponent<CharacterController>();

            // Temporarily disable AI movement during materialization
            if (_swordAI != null) _swordAI.enabled = false;
            if (_insaneAI != null) _insaneAI.enabled = false;
            if (_characterController != null) _characterController.enabled = false;
        }

        public void BeginEmergence(Vector3 spawnPoint, Quaternion spawnRotation)
        {
            transform.position = spawnPoint;
            transform.rotation = spawnRotation;

            ApplyHolographicMaterials(initialDissolve: 0.0f);

            // Optional Audio
            if (emergenceAudioClip != null)
            {
                AudioSource.PlayClipAtPoint(emergenceAudioClip, spawnPoint, 0.85f);
            }

            // Setup ghost trail emitter
            if (ghostEmitter == null)
            {
                ghostEmitter = GetComponent<HolographicGhostEmitter>();
                if (ghostEmitter == null) ghostEmitter = gameObject.AddComponent<HolographicGhostEmitter>();
            }

            if (ghostEmitter != null)
            {
                ghostEmitter.EmitBurst(0.6f);
            }

            StartCoroutine(EmergenceRoutine());
        }

        /// <summary>
        /// Converts all renderers on this entity into permanent glowing Cyberpunk holograms.
        /// </summary>
        public void ApplyHolographicMaterials(float initialDissolve = 1.0f)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (_renderers == null || _renderers.Length == 0) return;

            if (holographicShader == null)
            {
                holographicShader = Shader.Find("Cyberpunk/Holographic_RogueAI");
            }

            if (holographicShader == null)
            {
                Debug.LogWarning("[HolographicEmergence] Cyberpunk/Holographic_RogueAI shader not found!");
                return;
            }

            _originalMats = new Material[_renderers.Length][];
            List<Material> allHoloMats = new List<Material>();

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _originalMats[i] = _renderers[i].sharedMaterials;

                if (_originalMats[i] != null && _originalMats[i].Length > 0)
                {
                    Material[] subMats = new Material[_originalMats[i].Length];
                    for (int m = 0; m < subMats.Length; m++)
                    {
                        Material holoMat = new Material(holographicShader);
                        holoMat.name = $"Holo_{_renderers[i].name}_{m}";
                        holoMat.SetColor("_HoloColor", holographicColor);
                        holoMat.SetColor("_RimColor", rimGlowColor);
                        holoMat.SetFloat("_CoreAlpha", coreTranslucency);
                        holoMat.SetFloat("_Dissolve", initialDissolve);
                        holoMat.SetFloat("_GlitchIntensity", 0.25f);

                        if (_originalMats[i][m] != null && _originalMats[i][m].mainTexture != null)
                        {
                            holoMat.SetTexture("_BaseMap", _originalMats[i][m].mainTexture);
                        }

                        subMats[m] = holoMat;
                        allHoloMats.Add(holoMat);
                    }
                    _renderers[i].materials = subMats;
                }
            }

            _holographicMats = allHoloMats.ToArray();
        }

        private IEnumerator EmergenceRoutine()
        {
            _isEmerging = true;
            _emergenceTimer = 0f;

            // Trigger locomotion animation if animator exists anywhere in hierarchy
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Play("Walk_Forward", 0, 0f);
            }

            while (_emergenceTimer < emergenceDuration)
            {
                _emergenceTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_emergenceTimer / emergenceDuration);

                // Step forward out of Blackwall (-Z direction)
                transform.position += transform.forward * (forwardStepSpeed * Time.deltaTime);

                // Update shader _Dissolve progress (0.0 -> 1.0)
                if (_holographicMats != null)
                {
                    for (int i = 0; i < _holographicMats.Length; i++)
                    {
                        if (_holographicMats[i] != null)
                        {
                            _holographicMats[i].SetFloat("_Dissolve", progress);
                        }
                    }
                }

                // Periodically burst ghost trails
                if (_emergenceTimer % 0.35f < Time.deltaTime && ghostEmitter != null)
                {
                    ghostEmitter.EmitSnapshot();
                }

                yield return null;
            }

            // Materialization Complete!
            CompleteMaterialization();
        }

        private void CompleteMaterialization()
        {
            _isEmerging = false;

            if (!keepPermanentlyHolographic && _renderers != null && _originalMats != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null && _originalMats[i] != null)
                    {
                        _renderers[i].materials = _originalMats[i];
                    }
                }
            }
            else if (_holographicMats != null)
            {
                // Lock in permanent combat holographic state
                for (int i = 0; i < _holographicMats.Length; i++)
                {
                    if (_holographicMats[i] != null)
                    {
                        _holographicMats[i].SetFloat("_Dissolve", 1.0f);
                        _holographicMats[i].SetFloat("_GlitchIntensity", 0.25f);
                    }
                }
            }

            if (_characterController != null) _characterController.enabled = true;

            // Enable AI Combat
            if (_swordAI != null)
            {
                _swordAI.enabled = true;
                var player = GameObject.FindWithTag("Player");
                if (player != null) _swordAI.playerTarget = player.transform;
            }

            if (_insaneAI != null)
            {
                _insaneAI.enabled = true;
                _insaneAI.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
            }

            Debug.Log($"<color=#FF0055><b>[Blackwall Emergence]</b></color> Permanent Holographic Rogue AI <b>{gameObject.name}</b> ready for combat!");
        }
    }
}
