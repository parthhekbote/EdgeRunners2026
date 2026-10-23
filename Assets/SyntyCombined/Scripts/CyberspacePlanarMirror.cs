using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SyntyCombined
{
    /// <summary>
    /// Cyberpunk 2077 Cyberspace Real-Time Planar Mirror Reflection Controller (URP).
    /// Uses the battle-tested Scriptable Render Pipeline reflection architecture:
    /// - Dynamic aspect ratio matching the source camera (no horizontal/vertical squeeze).
    /// - True reflection matrix applied to worldToCameraMatrix (no 180-degree roll/swap).
    /// - Oblique near clip plane to clip everything below the mirror plane.
    /// - GL.invertCulling = true to correct triangle winding.
    /// - Real-time HDR reflections of the Blackwall, characters, and VFX onto the water floor.
    /// </summary>
    [ExecuteAlways]
    public class CyberspacePlanarMirror : MonoBehaviour
    {
        [Header("Mirror Plane Settings")]
        public float planeHeight = 0.0f;
        public float clipPlaneOffset = 0.05f;
        public Renderer floorRenderer;
        public Material mirrorMaterial;

        [Header("Reflection Quality")]
        [Range(256, 2048)]
        public int textureResolution = 1024;
        [Range(0.25f, 1.0f)]
        public float renderScale = 1.0f;
        public LayerMask reflectionLayers = ~0;
        public bool renderInEditor = true;

        private Camera _reflectionCamera;
        private RenderTexture _reflectionTexture;
        private static bool _isRendering = false;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_reflectionCamera != null)
            {
                _reflectionCamera.targetTexture = null;
                SafeDestroy(_reflectionCamera.gameObject);
                _reflectionCamera = null;
            }
            if (_reflectionTexture != null)
            {
                _reflectionTexture.Release();
                SafeDestroy(_reflectionTexture);
                _reflectionTexture = null;
            }
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isEditor)
                DestroyImmediate(obj);
            else
                Destroy(obj);
        }

        private void EnsureResources(Camera srcCam)
        {
            int targetW = Mathf.Max(256, Mathf.RoundToInt(srcCam.pixelWidth * renderScale));
            int targetH = Mathf.Max(256, Mathf.RoundToInt(srcCam.pixelHeight * renderScale));

            if (_reflectionTexture == null || _reflectionTexture.width != targetW || _reflectionTexture.height != targetH)
            {
                if (_reflectionTexture != null)
                {
                    _reflectionTexture.Release();
                    SafeDestroy(_reflectionTexture);
                }

                _reflectionTexture = new RenderTexture(targetW, targetH, 24, RenderTextureFormat.ARGBHalf)
                {
                    name = "Cyberspace_PlanarReflection_RT",
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                _reflectionTexture.Create();
            }

            if (_reflectionCamera == null)
            {
                GameObject camGO = new GameObject("Cyberspace_Reflection_Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _reflectionCamera = camGO.AddComponent<Camera>();
                _reflectionCamera.enabled = false;
                _reflectionCamera.cameraType = CameraType.Reflection;
                _reflectionCamera.allowHDR = true;
                _reflectionCamera.allowMSAA = false;

                var urpData = _reflectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                urpData.renderPostProcessing = false;
                urpData.renderShadows = false;
                urpData.requiresColorOption = CameraOverrideOption.Off;
                urpData.requiresDepthOption = CameraOverrideOption.Off;
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!enabled || cam == null || _isRendering) return;
            if (cam.cameraType == CameraType.Reflection || cam.cameraType == CameraType.Preview) return;
            if (!Application.isPlaying && !renderInEditor) return;

            // Only reflect for the main game camera
            if (cam.cameraType != CameraType.Game && cam != Camera.main) return;

            EnsureResources(cam);
            UpdateReflection(context, cam);
        }

        private void UpdateReflection(ScriptableRenderContext context, Camera srcCam)
        {
            _isRendering = true;

            try
            {
                Vector3 pos = new Vector3(0, planeHeight, 0);
                Vector3 normal = Vector3.up;

                // 1. Synchronize camera settings
                _reflectionCamera.CopyFrom(srcCam);
                _reflectionCamera.useOcclusionCulling = false;
                _reflectionCamera.cullingMask = reflectionLayers;
                _reflectionCamera.targetTexture = _reflectionTexture;

                // 2. Reflection plane & matrix calculation (Standard URP Boat Attack / Eric Lengyel formulation)
                float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
                Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

                Matrix4x4 reflection = Matrix4x4.identity;
                CalculateReflectionMatrix(ref reflection, reflectionPlane);

                Vector3 oldPosition = srcCam.transform.position - new Vector3(0, pos.y * 2f, 0);
                Vector3 newPosition = new Vector3(oldPosition.x, -oldPosition.y, oldPosition.z);
                _reflectionCamera.transform.forward = Vector3.Scale(srcCam.transform.forward, new Vector3(1, -1, 1));
                _reflectionCamera.worldToCameraMatrix = srcCam.worldToCameraMatrix * reflection;

                if (srcCam.transform.position.y <= planeHeight + 0.05f) return;

                // 3. Oblique Near Clip Plane (clips out geometry below mirror surface)
                Vector4 clipPlane = CameraSpacePlane(_reflectionCamera, pos - Vector3.up * 0.05f, normal, 1.0f);
                if (Mathf.Abs(clipPlane.w) > 0.0001f)
                {
                    try
                    {
                        Matrix4x4 projection = srcCam.CalculateObliqueMatrix(clipPlane);
                        _reflectionCamera.projectionMatrix = projection;
                    }
                    catch { _reflectionCamera.ResetProjectionMatrix(); }
                }
                else
                {
                    _reflectionCamera.ResetProjectionMatrix();
                }
                _reflectionCamera.transform.position = newPosition;

                // 4. Invert culling order for reflected triangles and render
                bool oldInvertCulling = GL.invertCulling;
                GL.invertCulling = true;
#pragma warning disable CS0618
                UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera);
#pragma warning restore CS0618
                GL.invertCulling = oldInvertCulling;

                // 5. Apply reflection texture to floor material
                if (mirrorMaterial != null)
                {
                    mirrorMaterial.SetTexture("_ReflectionTex", _reflectionTexture);
                }
                if (floorRenderer != null && floorRenderer.sharedMaterial != null)
                {
                    floorRenderer.sharedMaterial.SetTexture("_ReflectionTex", _reflectionTexture);
                }
                Shader.SetGlobalTexture("_ReflectionTex", _reflectionTexture);
                Shader.SetGlobalTexture("_PlanarReflectionTexture", _reflectionTexture);
            }
            finally
            {
                _isRendering = false;
            }
        }

        private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMat.m01 = (-2F * plane[0] * plane[1]);
            reflectionMat.m02 = (-2F * plane[0] * plane[2]);
            reflectionMat.m03 = (-2F * plane[3] * plane[0]);

            reflectionMat.m10 = (-2F * plane[1] * plane[0]);
            reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMat.m12 = (-2F * plane[1] * plane[2]);
            reflectionMat.m13 = (-2F * plane[3] * plane[1]);

            reflectionMat.m20 = (-2F * plane[2] * plane[0]);
            reflectionMat.m21 = (-2F * plane[2] * plane[1]);
            reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMat.m23 = (-2F * plane[3] * plane[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }

        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            Vector3 offsetPos = pos + normal * clipPlaneOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cameraPosition = m.MultiplyPoint(offsetPos);
            Vector3 cameraNormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
        }
    }
}
