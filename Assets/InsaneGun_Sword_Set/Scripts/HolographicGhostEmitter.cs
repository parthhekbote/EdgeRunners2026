using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Spawns holographic ghost projections / afterimages of the character's skinned mesh
    /// during high-speed lunges, combat dash steps, and evasive dodges.
    /// Inspired by Dead as Disco, Devil May Cry (Trickster/Mirage), and Cyberpunk Sandevistan.
    /// </summary>
    [AddComponentMenu("InsaneGun/Holographic Ghost Emitter")]
    public class HolographicGhostEmitter : MonoBehaviour
    {
        [Header("Ghost Visuals")]
        [Tooltip("Default color of the holographic projection")]
        public Color defaultGhostColor = new Color(0.0f, 0.95f, 1.0f, 0.85f); // Neon Cyan

        [Tooltip("Color for heavy combo lunges & finishers")]
        public Color heavyComboColor = new Color(1.0f, 0.10f, 0.45f, 0.90f);  // Neon Magenta

        [Tooltip("Color for evasive dodges")]
        public Color dodgeGhostColor = new Color(0.3f, 1.0f, 0.85f, 0.75f);  // Electric Mint

        [Tooltip("Duration each ghost projection remains visible before fading completely")]
        public float ghostLifetime = 0.28f;

        [Tooltip("Time between each snapshot during a continuous burst")]
        public float burstInterval = 0.04f;

        private SkinnedMeshRenderer[] _skinnedMeshes;
        private Material _ghostMaterial;
        private static readonly int PropColor = Shader.PropertyToID("_GhostColor");
        private static readonly int PropAlpha = Shader.PropertyToID("_Alpha");

        private Coroutine _activeBurstCoroutine;

        private void Awake()
        {
            RefreshRenderers();
            InitMaterial();
        }

        public void RefreshRenderers()
        {
            _skinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        private Material GhostMaterial
        {
            get
            {
                if (_ghostMaterial == null) InitMaterial();
                return _ghostMaterial;
            }
        }

        private void InitMaterial()
        {
            Shader shader = Shader.Find("InsaneGun/HolographicGhostTrail");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _ghostMaterial = new Material(shader);
            _ghostMaterial.name = "Mat_HolographicGhost_Instance";
            if (_ghostMaterial.HasProperty(PropColor))
                _ghostMaterial.SetColor(PropColor, defaultGhostColor);
            if (_ghostMaterial.HasProperty("_BaseColor"))
                _ghostMaterial.SetColor("_BaseColor", defaultGhostColor);
        }

        /// <summary>
        /// Emits a single holographic projection snapshot at the character's current pose.
        /// </summary>
        public void EmitSnapshot(Color? overrideColor = null, float? overrideLifetime = null)
        {
            if (_skinnedMeshes == null || _skinnedMeshes.Length == 0)
            {
                RefreshRenderers();
                if (_skinnedMeshes == null || _skinnedMeshes.Length == 0) return;
            }

            Color color = overrideColor ?? defaultGhostColor;
            float lifetime = overrideLifetime ?? ghostLifetime;

            for (int i = 0; i < _skinnedMeshes.Length; i++)
            {
                var smr = _skinnedMeshes[i];
                if (smr == null || !smr.gameObject.activeInHierarchy || !smr.enabled) continue;

                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);

                GameObject ghostGo = new GameObject("Ghost_Projection");
                ghostGo.hideFlags = HideFlags.DontSave;
                ghostGo.transform.position = smr.transform.position;
                ghostGo.transform.rotation = smr.transform.rotation;
                ghostGo.transform.localScale = smr.transform.lossyScale;

                MeshFilter mf = ghostGo.AddComponent<MeshFilter>();
                mf.sharedMesh = bakedMesh;

                MeshRenderer mr = ghostGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GhostMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(PropColor, color);
                mpb.SetFloat(PropAlpha, 1.0f);
                mr.SetPropertyBlock(mpb);

                if (Application.isPlaying) Destroy(ghostGo, lifetime + 0.15f);
                StartCoroutine(FadeAndDestroy(ghostGo, bakedMesh, mr, mpb, lifetime));
            }
        }

        /// <summary>
        /// Emits a continuous trail of afterimage projections for the specified duration.
        /// Perfect for forward attack lunges, gap closers, and roll dodges.
        /// </summary>
        public void EmitBurst(float duration, Color? overrideColor = null, float? interval = null, float? overrideLifetime = null)
        {
            if (_activeBurstCoroutine != null) StopCoroutine(_activeBurstCoroutine);
            _activeBurstCoroutine = StartCoroutine(BurstRoutine(duration, overrideColor, interval ?? burstInterval, overrideLifetime));
        }

        private IEnumerator BurstRoutine(float duration, Color? color, float stepInterval, float? overrideLifetime)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                EmitSnapshot(color, overrideLifetime);
                yield return new WaitForSeconds(stepInterval);
                elapsed += stepInterval;
            }
            _activeBurstCoroutine = null;
        }

        private IEnumerator FadeAndDestroy(GameObject ghostGo, Mesh mesh, MeshRenderer mr, MaterialPropertyBlock mpb, float lifetime)
        {
            float elapsed = 0f;
            while (elapsed < lifetime)
            {
                if (ghostGo == null) yield break;

                elapsed += Time.deltaTime;
                float progress = elapsed / lifetime;
                // Quadratic falloff gives a sharp, neon snap fade
                float alpha = Mathf.Clamp01(1f - (progress * progress));

                mpb.SetFloat(PropAlpha, alpha);
                if (mr != null) mr.SetPropertyBlock(mpb);

                yield return null;
            }

            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            if (ghostGo != null)
            {
                if (Application.isPlaying) Destroy(ghostGo);
                else DestroyImmediate(ghostGo);
            }
        }

        private void OnDestroy()
        {
            if (_ghostMaterial != null)
            {
                if (Application.isPlaying) Destroy(_ghostMaterial);
                else DestroyImmediate(_ghostMaterial);
            }
        }
    }
}
