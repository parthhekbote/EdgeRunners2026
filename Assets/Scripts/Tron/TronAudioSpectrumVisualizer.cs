using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tron.Terminal
{
    /// <summary>
    /// Samples audio frequencies in real-time from the Tron background music
    /// and renders both dynamic UI equalizer bars and ASCII spectrum readouts.
    /// </summary>
    public class TronAudioSpectrumVisualizer : MonoBehaviour
    {
        [Header("Audio Source")]
        public AudioSource targetSource;

        [Header("UI Equalizer Bars")]
        public RectTransform[] spectrumBars;
        public float minBarHeight = 4f;
        public float maxBarHeight = 65f;
        public float smoothSpeed = 16f;

        [Header("ASCII Equalizer (Optional)")]
        public TMP_Text asciiSpectrumText;
        public int asciiBarCount = 16;

        private float[] _samples = new float[64];
        private float[] _smoothValues;
        private static readonly string[] AsciiBlocks = { " ", " ", "▂", "▃", "▄", "▅", "▆", "▇", "█" };

        private void Awake()
        {
            _smoothValues = new float[spectrumBars != null ? Mathf.Max(spectrumBars.Length, asciiBarCount) : asciiBarCount];
        }

        private void Update()
        {
            if (targetSource == null || !targetSource.isPlaying)
            {
                DecayBars();
                return;
            }

            targetSource.GetSpectrumData(_samples, 0, FFTWindow.BlackmanHarris);

            int barCount = (spectrumBars != null && spectrumBars.Length > 0) ? spectrumBars.Length : asciiBarCount;

            for (int i = 0; i < barCount; i++)
            {
                // Quadratic frequency distribution to emphasize lower & mid frequencies
                int sampleIdx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Pow((float)i / barCount, 1.6f) * (_samples.Length - 1)), 0, _samples.Length - 1);
                float rawValue = _samples[sampleIdx] * (12f + i * 2.5f); // boost highs
                rawValue = Mathf.Clamp01(rawValue);

                _smoothValues[i] = Mathf.Lerp(_smoothValues[i], rawValue, Time.deltaTime * smoothSpeed);

                // Update UI Bars if configured
                if (spectrumBars != null && i < spectrumBars.Length && spectrumBars[i] != null)
                {
                    float targetHeight = Mathf.Lerp(minBarHeight, maxBarHeight, _smoothValues[i]);
                    Vector2 size = spectrumBars[i].sizeDelta;
                    size.y = targetHeight;
                    spectrumBars[i].sizeDelta = size;
                }
            }

            // Update ASCII Equalizer text if assigned
            if (asciiSpectrumText != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(asciiBarCount + 16);
                sb.Append("<color=#00FFFF>[EQ: ");
                for (int i = 0; i < asciiBarCount; i++)
                {
                    int blockIdx = Mathf.Clamp(Mathf.FloorToInt(_smoothValues[i] * (AsciiBlocks.Length - 1)), 0, AsciiBlocks.Length - 1);
                    sb.Append(AsciiBlocks[blockIdx]);
                }
                sb.Append("]</color>");
                asciiSpectrumText.text = sb.ToString();
            }
        }

        private void DecayBars()
        {
            if (_smoothValues == null) return;
            for (int i = 0; i < _smoothValues.Length; i++)
            {
                _smoothValues[i] = Mathf.Lerp(_smoothValues[i], 0f, Time.deltaTime * 8f);
                if (spectrumBars != null && i < spectrumBars.Length && spectrumBars[i] != null)
                {
                    Vector2 size = spectrumBars[i].sizeDelta;
                    size.y = Mathf.Lerp(size.y, minBarHeight, Time.deltaTime * 8f);
                    spectrumBars[i].sizeDelta = size;
                }
            }
        }

        public string GetCurrentAsciiBar()
        {
            if (_smoothValues == null) return "[               ]";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(asciiBarCount + 2);
            sb.Append("[");
            for (int i = 0; i < asciiBarCount; i++)
            {
                int blockIdx = Mathf.Clamp(Mathf.FloorToInt(_smoothValues[i] * (AsciiBlocks.Length - 1)), 0, AsciiBlocks.Length - 1);
                sb.Append(AsciiBlocks[blockIdx]);
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}
