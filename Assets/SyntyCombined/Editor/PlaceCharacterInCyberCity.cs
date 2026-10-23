#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SyntyCombined.Editor
{
    public static class PlaceCharacterInCyberCity
    {
        private const string CyberCityScenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";
        private const string PlayerPrefabPath = "Assets/Synty/AnimationBaseLocomotion/Samples/Prefabs/PF_PolygonPlayer.prefab";

        [MenuItem("Tools/SyntyCombined/Place Character in Cyber City (First Person)")]
        public static void PlaceCharacter()
        {
            var scene = EditorSceneManager.OpenScene(CyberCityScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[SyntyCombined] Failed to open scene at {CyberCityScenePath}");
                return;
            }

            // Find ground position near default city camera view (12.55, -16.29, 57.12)
            Vector3 testOrigin = new Vector3(12.55f, 5f, 57.12f);
            Vector3 spawnPos = new Vector3(12.55f, -17.94f, 57.12f);

            if (Physics.Raycast(testOrigin, Vector3.down, out RaycastHit hit, 100f))
            {
                spawnPos = hit.point + Vector3.up * 0.05f;
            }

            // Check if player exists
            GameObject playerInstance = GameObject.Find("PF_PolygonPlayer");
            if (playerInstance == null)
            {
                playerInstance = GameObject.FindWithTag("Player");
            }

            if (playerInstance == null)
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (playerPrefab == null)
                {
                    Debug.LogError($"[SyntyCombined] Player prefab not found at {PlayerPrefabPath}");
                    return;
                }

                playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerInstance.name = "PF_PolygonPlayer";
                playerInstance.tag = "Player";
                Undo.RegisterCreatedObjectUndo(playerInstance, "Spawn Player in Cyber City");
            }

            Undo.RecordObject(playerInstance.transform, "Position Player");
            playerInstance.transform.position = spawnPos;
            playerInstance.transform.rotation = Quaternion.Euler(0f, 130f, 0f);

            // Controller
            var ctrl = playerInstance.GetComponent<SyntySwordLocomotionController>();
            if (ctrl == null)
            {
                ctrl = Undo.AddComponent<SyntySwordLocomotionController>(playerInstance);
            }

            Undo.RecordObject(ctrl, "Setup Player Controller");
            ctrl.firstPersonView = false;
            ctrl.isInvincible = false;

            // Camera setup
            Camera mainCam = null;
            var playerCams = playerInstance.GetComponentsInChildren<Camera>(true);
            if (playerCams.Length > 0)
            {
                mainCam = playerCams[0];
            }
            else
            {
                mainCam = Camera.main;
            }

            if (mainCam == null)
            {
                GameObject camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                Undo.RegisterCreatedObjectUndo(camGo, "Create MainCamera");
            }

            Undo.RecordObject(mainCam.gameObject, "Setup MainCamera");
            mainCam.tag = "MainCamera";
            mainCam.enabled = true;
            mainCam.allowHDR = true;
            mainCam.nearClipPlane = 0.03f;
            mainCam.fieldOfView = 75f;

            // Configure URP Post-Processing on MainCamera
            var uac = mainCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (uac == null)
            {
                uac = Undo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(mainCam.gameObject);
            }
            Undo.RecordObject(uac, "Setup Camera Post Processing");
            uac.renderPostProcessing = true;
            uac.volumeLayerMask = ~0; // Everything
            uac.volumeTrigger = mainCam.transform;
            uac.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            uac.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
            uac.renderShadows = true;

            ctrl.playerCamera = mainCam;

            // Disable duplicate static scene Camera if present
            GameObject oldCamGo = GameObject.Find("Camera");
            if (oldCamGo != null && oldCamGo != mainCam.gameObject && oldCamGo.transform.parent == null)
            {
                Undo.RecordObject(oldCamGo, "Disable Duplicate Scene Camera");
                oldCamGo.SetActive(false);
            }

            // CameraTarget and eye height alignment
            Transform camTarget = playerInstance.transform.Find("CameraTarget");
            if (camTarget == null)
            {
                GameObject targetGo = new GameObject("CameraTarget");
                targetGo.transform.SetParent(playerInstance.transform);
                targetGo.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                camTarget = targetGo.transform;
                Undo.RegisterCreatedObjectUndo(targetGo, "Create CameraTarget");
            }
            ctrl.cameraTarget = camTarget;

            // Position camera at exact eye level
            var anim = playerInstance.GetComponentInChildren<Animator>();
            Transform headBone = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 eyePos = headBone != null 
                ? headBone.position + Vector3.up * 0.07f + playerInstance.transform.forward * 0.08f
                : playerInstance.transform.position + Vector3.up * 1.63f + playerInstance.transform.forward * 0.08f;
            mainCam.transform.position = eyePos;
            mainCam.transform.rotation = playerInstance.transform.rotation;

            // Ambient lighting & Directional Light brightness
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.12f, 0.25f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.45f, 0.55f);
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.35f, 0.65f);

            var dirLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (var l in dirLights)
            {
                if (l.type == LightType.Directional)
                {
                    Undo.RecordObject(l, "Boost Directional Light");
                    l.intensity = 1.45f;
                }
            }

            Selection.activeGameObject = playerInstance;
            EditorGUIUtility.PingObject(playerInstance);

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SyntyCombined] Successfully placed {playerInstance.name} at {spawnPos} in First-Person View mode in {CyberCityScenePath}!");
        }
    }
}
#endif
