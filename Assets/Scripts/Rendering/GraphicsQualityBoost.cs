using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Cyberpunk.Rendering
{
    /// <summary>
    /// Turns on high-quality SMAA for every camera in every scene (gameplay, cinematic and cutscene cameras).
    /// The renderer is Deferred, which cannot use MSAA, so post-process anti-aliasing is what removes jagged edges
    /// on neon lines and building outlines. Cameras created later (cutscene rigs) are picked up automatically.
    /// </summary>
    public class GraphicsQualityBoost : MonoBehaviour
    {
        private static GraphicsQualityBoost _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[GraphicsQualityBoost]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GraphicsQualityBoost>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Start() => StartCoroutine(Sweep());

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        private IEnumerator Sweep()
        {
            var wait = new WaitForSecondsRealtime(1.5f);
            while (true)
            {
                Apply();
                yield return wait;
            }
        }

        private static void Apply()
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.cameraType != CameraType.Game) continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;

                if (data.antialiasing != AntialiasingMode.SubpixelMorphologicalAntiAliasing)
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                if (data.antialiasingQuality != AntialiasingQuality.High)
                    data.antialiasingQuality = AntialiasingQuality.High;
            }
        }
    }
}
