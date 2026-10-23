using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Controls the central monolithic extruded datacenter core.
    /// Pulses the intense crimson red laser/plasma core light and material emission
    /// to match the concept art aesthetic.
    /// </summary>
    public class DatacenterRedCorePulsator : MonoBehaviour
    {
        [Header("Lights")]
        public Light[] coreLights;
        public float baseLightIntensity = 4.5f;
        public float pulseLightIntensity = 7.5f;

        [Header("Material")]
        public Renderer coreRenderer;
        public Color baseEmissionColor = new Color(3.5f, 0.05f, 0.1f, 1f);
        public Color peakEmissionColor = new Color(8.0f, 0.2f, 0.35f, 1f);

        [Header("Pulse Speed")]
        public float pulseSpeed = 1.8f;
        public float flickerAmount = 0.08f;

        private Material _instancedMat;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            if (coreRenderer != null)
            {
                _instancedMat = coreRenderer.material;
                _instancedMat.EnableKeyword("_EMISSION");
            }
        }

        private void Update()
        {
            float sine = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) * 0.5f;
            float noise = (Mathf.PerlinNoise(Time.time * 8.0f, 0f) - 0.5f) * flickerAmount;
            float factor = Mathf.Clamp01(sine + noise);

            // Interpolate light intensity
            if (coreLights != null)
            {
                float currentIntensity = Mathf.Lerp(baseLightIntensity, pulseLightIntensity, factor);
                for (int i = 0; i < coreLights.Length; i++)
                {
                    if (coreLights[i] != null)
                    {
                        coreLights[i].intensity = currentIntensity;
                    }
                }
            }

            // Interpolate material emission
            if (_instancedMat != null)
            {
                Color curCol = Color.Lerp(baseEmissionColor, peakEmissionColor, factor);
                _instancedMat.SetColor(EmissionColorId, curCol);
                _instancedMat.SetColor(BaseColorId, curCol);
            }
        }

        private void OnDestroy()
        {
            if (_instancedMat != null)
            {
                Destroy(_instancedMat);
            }
        }
    }
}
