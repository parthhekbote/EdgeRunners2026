using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace PolygonMech
{
    /// <summary>
    /// Component attached to each of the 32 Mech Exhibits in the Showcase Scene.
    /// Manages continuous animation looping, category lighting, 3D hologram labels, and inspection focus.
    /// </summary>
    [SelectionBase]
    public class MechShowcaseExhibit : MonoBehaviour
    {
        [Header("Animation Metadata")]
        public int animationIndex = 1;
        public string animationId = "01_Idle_TitanGuard";
        public string category = "Idle";
        public string displayName = "Titan Guard Stance";
        public AnimationClip clip;
        public float baseDuration = 1.0f;
        public string description = "";

        [Header("Visual Elements")]
        public Light spotlight;
        public Renderer pedestalRingRenderer;
        public Color themeColor = Color.cyan;
        public Transform billboardAnchor;

        [Header("Playback State")]
        [Range(0.05f, 3.0f)]
        public float speedMultiplier = 1.0f;
        public bool isPlaying = true;
        public bool isHighlighted = false;

        private Animator animator;
        private PlayableGraph playableGraph;
        private AnimationClipPlayable clipPlayable;
        private Material ringMaterialInstance;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (pedestalRingRenderer != null)
            {
                ringMaterialInstance = pedestalRingRenderer.material;
                ApplyColorTheme(themeColor);
            }
        }

        private void Start()
        {
            InitializePlayable();
        }

        private void InitializePlayable()
        {
            if (animator == null || clip == null) return;

            // Ensure clean start
            if (playableGraph.IsValid()) playableGraph.Destroy();

            playableGraph = PlayableGraph.Create($"{gameObject.name}_ShowcaseGraph");
            clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetSpeed(speedMultiplier);

            var animOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
            animOutput.SetSourcePlayable(clipPlayable);

            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playableGraph.Play();
        }

        private void Update()
        {
            if (!playableGraph.IsValid() || clip == null || clip.length <= 0f) return;

            if (isPlaying)
            {
                // Smooth loop wrap
                double currentTime = clipPlayable.GetTime();
                if (currentTime >= clip.length)
                {
                    clipPlayable.SetTime(currentTime % clip.length);
                }
            }
        }

        public void SetSpeed(float speed)
        {
            speedMultiplier = speed;
            if (clipPlayable.IsValid())
            {
                clipPlayable.SetSpeed(isPlaying ? speedMultiplier : 0f);
            }
            if (animator != null)
            {
                animator.speed = isPlaying ? speedMultiplier : 0f;
            }
        }

        public void SetPlaying(bool playing)
        {
            isPlaying = playing;
            if (clipPlayable.IsValid())
            {
                clipPlayable.SetSpeed(isPlaying ? speedMultiplier : 0f);
            }
            if (animator != null)
            {
                animator.speed = isPlaying ? speedMultiplier : 0f;
            }
        }

        public void SetHighlight(bool highlighted)
        {
            isHighlighted = highlighted;
            if (spotlight != null)
            {
                spotlight.intensity = highlighted ? 5.5f : 2.2f;
                spotlight.color = highlighted ? (themeColor * 1.5f + Color.white * 0.4f) : themeColor;
            }
            if (ringMaterialInstance != null)
            {
                ringMaterialInstance.SetColor("_BaseColor", highlighted ? themeColor * 2.5f : themeColor);
                if (ringMaterialInstance.HasProperty("_EmissionColor"))
                {
                    ringMaterialInstance.SetColor("_EmissionColor", highlighted ? themeColor * 3.0f : themeColor * 1.2f);
                }
            }
        }

        public void ApplyColorTheme(Color color)
        {
            themeColor = color;
            if (spotlight != null)
            {
                spotlight.color = themeColor;
            }
            if (ringMaterialInstance != null)
            {
                ringMaterialInstance.color = themeColor;
                if (ringMaterialInstance.HasProperty("_EmissionColor"))
                {
                    ringMaterialInstance.SetColor("_EmissionColor", themeColor * 1.5f);
                }
            }
        }

        private void OnDestroy()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = themeColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, new Vector3(3f, 5.5f, 3f));
        }
    }
}
