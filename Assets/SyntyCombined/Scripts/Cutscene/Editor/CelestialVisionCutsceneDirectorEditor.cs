#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

namespace SyntyCombined.Cutscene
{
    [CustomEditor(typeof(CelestialVisionCutsceneDirector))]
    public class CelestialVisionCutsceneDirectorEditor : UnityEditor.Editor
    {
        private CelestialVisionCutsceneDirector _director;
        private double _lastEditorTime;
        private bool _isEditorPreviewing = false;

        private void OnEnable()
        {
            _director = (CelestialVisionCutsceneDirector)target;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _isEditorPreviewing = false;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("🎬 Corporation Head (Credits Scene) Controls (3:23 - 3:28)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float newT = EditorGUILayout.Slider("Scrub Timeline", _director.normalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_director, "Scrub Astropulse Cutscene");
                _director.normalizedTime = newT;
                _director.Evaluate(newT);
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();

            if (!_isEditorPreviewing && !_director.isPlaying)
            {
                GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
                if (GUILayout.Button("▶ Play Realtime Preview", GUILayout.Height(32)))
                {
                    if (Application.isPlaying)
                    {
                        _director.Play();
                    }
                    else
                    {
                        _isEditorPreviewing = true;
                        _director.normalizedTime = 0f;
                        _lastEditorTime = EditorApplication.timeSinceStartup;
                    }
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.95f, 0.4f, 0.4f);
                if (GUILayout.Button("⏹ Stop Preview", GUILayout.Height(32)))
                {
                    _isEditorPreviewing = false;
                    _director.Stop();
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("⚡ Auto-Assign Drone Audio Clip", GUILayout.Height(24)))
            {
                string audioPath = "Assets/Audio/Cutscenes/Astropulse_Celestial_Drone.mp3";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                if (clip != null)
                {
                    Undo.RecordObject(_director, "Assign Astropulse Drone Audio");
                    _director.celestialDroneClip = clip;
                    EditorUtility.SetDirty(_director);
                    Debug.Log("<color=#A855F7>[Astropulse Cutscene]</color> Successfully assigned celestial drone audio clip!");
                }
                else
                {
                    Debug.LogWarning("[Astropulse Cutscene] Audio clip not found at: " + audioPath);
                }
            }
        }

        private void OnEditorUpdate()
        {
            if (!_isEditorPreviewing || Application.isPlaying || _director == null) return;

            double currentTime = EditorApplication.timeSinceStartup;
            float delta = (float)(currentTime - _lastEditorTime);
            _lastEditorTime = currentTime;

            _director.normalizedTime += delta / Mathf.Max(0.1f, _director.duration);
            if (_director.normalizedTime > 1f)
            {
                if (_director.loopInPreview)
                {
                    _director.normalizedTime = 0f;
                }
                else
                {
                    _director.normalizedTime = 1f;
                    _isEditorPreviewing = false;
                }
            }

            _director.Evaluate(_director.normalizedTime);
            SceneView.RepaintAll();
            Repaint();
        }

        [MenuItem("Game/Build Corporation Head Credits Scene")]
        public static void BuildCorporationHeadCreditsScene()
        {
            string sceneDir = "Assets/SyntyCombined/Scenes";
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }

            string scenePath = sceneDir + "/GameCredits_CorporationHead.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Skybox & Lighting
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/Misc/SkyBoxSpace_01.mat");
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 0.65f;
            }

            // Directional Backlight (Nebula Rim)
            GameObject rimLightGo = new GameObject("Nebula_RimBacklight");
            Light rimLight = rimLightGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.22f, 0.74f, 0.97f); // Radiant cyan-blue
            rimLight.intensity = 3.5f;
            rimLightGo.transform.rotation = Quaternion.Euler(15f, 180f, 0f);

            // Ambient Fill Light (Soft Deep Violet)
            GameObject fillLightGo = new GameObject("Cosmic_FillLight");
            Light fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.18f, 0.12f, 0.35f);
            fillLight.intensity = 0.5f;
            fillLightGo.transform.rotation = Quaternion.Euler(45f, 25f, 0f);

            // Materials
            string matDir = "Assets/SyntyCombined/Materials/CorporationHead";
            if (!Directory.Exists(matDir))
            {
                Directory.CreateDirectory(matDir);
            }

            string goldMatPath = matDir + "/M_CorporationHead_GoldMask.mat";
            Material goldMat = AssetDatabase.LoadAssetAtPath<Material>(goldMatPath);
            if (goldMat == null)
            {
                goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                goldMat.color = new Color(0.96f, 0.65f, 0.08f);
                goldMat.SetFloat("_Metallic", 0.95f);
                goldMat.SetFloat("_Smoothness", 0.88f);
                AssetDatabase.CreateAsset(goldMat, goldMatPath);
            }

            string cowlMatPath = matDir + "/M_CorporationHead_Cowl.mat";
            Material cowlMat = AssetDatabase.LoadAssetAtPath<Material>(cowlMatPath);
            if (cowlMat == null)
            {
                cowlMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                cowlMat.color = new Color(0.06f, 0.06f, 0.08f);
                cowlMat.SetFloat("_Metallic", 0.12f);
                cowlMat.SetFloat("_Smoothness", 0.22f);
                AssetDatabase.CreateAsset(cowlMat, cowlMatPath);
            }

            string oculusMatPath = matDir + "/M_CorporationHead_Oculus.mat";
            Material oculusMat = AssetDatabase.LoadAssetAtPath<Material>(oculusMatPath);
            if (oculusMat == null)
            {
                oculusMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                oculusMat.color = new Color(0.66f, 0.33f, 0.98f);
                oculusMat.EnableKeyword("_EMISSION");
                oculusMat.SetColor("_EmissionColor", new Color(0.75f, 0.45f, 1.0f) * 4.5f);
                AssetDatabase.CreateAsset(oculusMat, oculusMatPath);
            }

            // Character Root
            GameObject corpHeadRoot = new GameObject("CorporationHead_Overseer");
            corpHeadRoot.transform.position = Vector3.zero;
            corpHeadRoot.transform.rotation = Quaternion.identity;

            // Robed Body
            GameObject scavengerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab");
            if (scavengerPrefab != null)
            {
                GameObject bodyInstance = (GameObject)PrefabUtility.InstantiatePrefab(scavengerPrefab, corpHeadRoot.transform);
                bodyInstance.name = "RobedBody";
                bodyInstance.transform.localPosition = Vector3.zero;
                bodyInstance.transform.localRotation = Quaternion.identity;

                var smrs = bodyInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrs)
                {
                    if (smr.gameObject.name.Contains("Scavenger_01") || smr.gameObject.name.Contains("Strider"))
                    {
                        smr.gameObject.SetActive(true);
                        smr.sharedMaterial = cowlMat;
                    }
                    else
                    {
                        smr.gameObject.SetActive(false);
                    }
                }
            }

            // Golden Mask
            GameObject maskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/Attachments/SM_Chr_Attach_Head_Mask_01.prefab");
            if (maskPrefab != null)
            {
                GameObject maskInstance = (GameObject)PrefabUtility.InstantiatePrefab(maskPrefab, corpHeadRoot.transform);
                maskInstance.name = "GoldenMask_Visage";
                maskInstance.transform.localPosition = new Vector3(0f, 1.58f, 0.04f);
                maskInstance.transform.localRotation = Quaternion.identity;
                maskInstance.transform.localScale = Vector3.one * 1.05f;

                var r = maskInstance.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = goldMat;
                }
            }

            // Cowl Hood
            GameObject hatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/Attachments/SM_Chr_Attach_Hat_01.prefab");
            if (hatPrefab != null)
            {
                GameObject hatInstance = (GameObject)PrefabUtility.InstantiatePrefab(hatPrefab, corpHeadRoot.transform);
                hatInstance.name = "Cowl_Hood";
                hatInstance.transform.localPosition = new Vector3(0f, 1.62f, -0.02f);
                hatInstance.transform.localRotation = Quaternion.identity;
                hatInstance.transform.localScale = Vector3.one * 1.12f;

                var r = hatInstance.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = cowlMat;
                }
            }

            // Look Target
            GameObject lookTargetGo = new GameObject("LookTarget_Visage");
            lookTargetGo.transform.parent = corpHeadRoot.transform;
            lookTargetGo.transform.localPosition = new Vector3(0f, 1.58f, 0.05f);

            // Violet Oculus Core
            GameObject oculusEyeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            oculusEyeGo.name = "VioletOculus_Core";
            oculusEyeGo.transform.parent = corpHeadRoot.transform;
            oculusEyeGo.transform.localPosition = new Vector3(0.04f, 1.62f, 0.12f);
            oculusEyeGo.transform.localScale = Vector3.one * 0.045f;
            Collider col = oculusEyeGo.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            oculusEyeGo.GetComponent<Renderer>().sharedMaterial = oculusMat;

            // Oculus Point Light
            GameObject oculusLightGo = new GameObject("Oculus_PointLight");
            oculusLightGo.transform.parent = oculusEyeGo.transform;
            oculusLightGo.transform.localPosition = Vector3.zero;
            Light oculusLight = oculusLightGo.AddComponent<Light>();
            oculusLight.type = LightType.Point;
            oculusLight.color = new Color(0.66f, 0.33f, 0.98f);
            oculusLight.intensity = 8.5f;
            oculusLight.range = 1.6f;

            // Anamorphic horizontal streak
            GameObject flareQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flareQuad.name = "AnamorphicStreak_Flare";
            flareQuad.transform.parent = oculusEyeGo.transform;
            flareQuad.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            flareQuad.transform.localRotation = Quaternion.identity;
            flareQuad.transform.localScale = new Vector3(0.45f, 0.025f, 1f);
            Collider fcol = flareQuad.GetComponent<Collider>();
            if (fcol != null) Object.DestroyImmediate(fcol);
            flareQuad.GetComponent<Renderer>().sharedMaterial = oculusMat;

            // Cinematic Camera
            GameObject camGo = new GameObject("Cinematic_CreditsCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 42f;
            camGo.AddComponent<AudioListener>();

            // Cutscene Director Manager
            GameObject cutsceneMgrGo = new GameObject("CelestialVision_CutsceneManager");
            CelestialVisionCutsceneDirector director = cutsceneMgrGo.AddComponent<CelestialVisionCutsceneDirector>();
            director.cinematicCamera = cam;
            director.overseerEntity = corpHeadRoot.transform;
            director.lookTarget = lookTargetGo.transform;
            director.oculusPointLight = oculusLight;
            director.anamorphicFlareTransform = flareQuad.transform;
            director.duration = 4.8f;
            director.blackoutDuration = 0.25f;
            director.playOnStart = true;
            director.loopInPreview = true;

            string audioPath = "Assets/Audio/Cutscenes/Astropulse_Celestial_Drone.mp3";
            AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
            director.celestialDroneClip = audioClip;

            director.Evaluate(0f);

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#10B981>[Astropulse]</color> Game Credits Scene built successfully at: " + scenePath);
        }
    }
}
#endif
