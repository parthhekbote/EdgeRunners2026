using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SyntyCombined
{
    /// <summary>
    /// Real-time Sci-Fi Minimap & Radar system for Demo_City.
    /// Creates a top-down orthographic render camera, projects terrain & entities onto
    /// a Synty HUD RawImage frame, and updates player/enemy blip positions dynamically.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public static MinimapController Instance { get; private set; }

        [Header("Target & Tracking")]
        public Transform targetPlayer;
        public float cameraHeight = 120f;
        public float orthographicSize = 40f;
        public bool rotateWithPlayer = false;

        [Header("Optimization")]
        [Tooltip("Throttled render rate for minimap camera (18 FPS avoids running redundant 60-144 FPS passes across 10,000+ objects)")]
        public float targetRenderFps = 18f;
        private float _renderTimer = 0f;

        [Header("UI Render Target")]
        public RawImage minimapRawImage;
        public RectTransform playerIconRect;
        public RectTransform compassNorthIcon;
        public Transform enemyBlipsContainer;

        [Header("Blip Prefabs / Visuals")]
        public Color enemyBlipColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        public float blipSize = 12f;

        private Camera _minimapCam;
        private RenderTexture _renderTexture;
        private List<SwordEnemyAI> _activeEnemies = new List<SwordEnemyAI>();
        private Dictionary<SwordEnemyAI, RectTransform> _blipMap = new Dictionary<SwordEnemyAI, RectTransform>();

        private void Awake()
        {
            Instance = this;
            FindPlayerReference();
            SetupMinimapCamera();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        private void Start()
        {
            FindPlayerReference();
            SetupMinimapCamera();
            InvokeRepeating(nameof(RefreshEnemyList), 0.5f, 2.0f);
        }

        private void FindPlayerReference()
        {
            if (targetPlayer == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null)
                {
                    SyntySwordLocomotionController controller = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
                    if (controller != null) p = controller.gameObject;
                }
                if (p != null) targetPlayer = p.transform;
            }
        }

        public void SetupMinimapCamera()
        {
            GameObject existingCam = GameObject.Find("MinimapRenderCamera");
            if (existingCam != null)
            {
                _minimapCam = existingCam.GetComponent<Camera>();
            }

            if (_minimapCam == null)
            {
                GameObject camObj = new GameObject("MinimapRenderCamera");
                _minimapCam = camObj.AddComponent<Camera>();
            }

            _minimapCam.orthographic = true;
            _minimapCam.orthographicSize = orthographicSize;
            _minimapCam.farClipPlane = cameraHeight + 45f;
            _minimapCam.nearClipPlane = 0.5f;
            _minimapCam.clearFlags = CameraClearFlags.SolidColor;
            _minimapCam.backgroundColor = new Color(0.02f, 0.05f, 0.1f, 1f);

            // Exclude UI, TransparentFX, Ignore Raycast
            int ignoreMask = (1 << 1) | (1 << 2) | (1 << 4); // TransparentFX, Ignore Raycast, Water
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) ignoreMask |= (1 << uiLayer);
            _minimapCam.cullingMask = ~ignoreMask;

            // Universal Additional Camera Data for URP forward rendering
            var camData = _minimapCam.gameObject.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = _minimapCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
            if (camData != null)
            {
                camData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
                camData.renderPostProcessing = false;
                camData.renderShadows = false;
                camData.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
                camData.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            }

            // Create 256x256 Render Texture (1:1 match for 226x226 UI square, 4x lower pixel fillrate than 512x512)
            if (_renderTexture == null || !_renderTexture.IsCreated())
            {
                if (_renderTexture != null) _renderTexture.Release();
                _renderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
                _renderTexture.name = "RT_Minimap_Live3D";
                _renderTexture.wrapMode = TextureWrapMode.Clamp;
                _renderTexture.filterMode = FilterMode.Bilinear;
                _renderTexture.Create();
            }

            _minimapCam.targetTexture = _renderTexture;
            // Disable automatic per-frame rendering; rendered on throttled interval in LateUpdate
            _minimapCam.enabled = false;

            if (minimapRawImage != null)
            {
                minimapRawImage.texture = _renderTexture;
            }
        }

        public void SetMinimapTarget(RawImage rawImg, RectTransform playerIcon, Transform blipsContainer)
        {
            minimapRawImage = rawImg;
            playerIconRect = playerIcon;
            enemyBlipsContainer = blipsContainer;

            if (_renderTexture == null || !_renderTexture.IsCreated())
            {
                SetupMinimapCamera();
            }

            if (minimapRawImage != null && _renderTexture != null)
            {
                minimapRawImage.texture = _renderTexture;
            }
        }

        private void LateUpdate()
        {
            bool isInterior = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Interior", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isInterior)
            {
                // In Dr. Ido's lab (interior), disable minimap camera rendering and hide live map blips
                if (_minimapCam != null && _minimapCam.enabled) _minimapCam.enabled = false;
                if (minimapRawImage != null && minimapRawImage.enabled) minimapRawImage.enabled = false;
                if (playerIconRect != null && playerIconRect.gameObject.activeSelf) playerIconRect.gameObject.SetActive(false);
                if (enemyBlipsContainer != null && enemyBlipsContainer.gameObject.activeSelf) enemyBlipsContainer.gameObject.SetActive(false);
                return;
            }
            else
            {
                if (minimapRawImage != null && !minimapRawImage.enabled) minimapRawImage.enabled = true;
                if (playerIconRect != null && !playerIconRect.gameObject.activeSelf) playerIconRect.gameObject.SetActive(true);
                if (enemyBlipsContainer != null && !enemyBlipsContainer.gameObject.activeSelf) enemyBlipsContainer.gameObject.SetActive(true);
            }

            if (minimapRawImage != null && (minimapRawImage.texture == null || minimapRawImage.texture != _renderTexture) && _renderTexture != null)
            {
                minimapRawImage.texture = _renderTexture;
            }

            if (targetPlayer == null)
            {
                FindPlayerReference();
                if (targetPlayer == null) return;
            }

            if (_minimapCam == null || _renderTexture == null || !_renderTexture.IsCreated())
            {
                SetupMinimapCamera();
                if (_minimapCam == null) return;
            }

            // Position Camera directly above player
            Vector3 camPos = targetPlayer.position;
            camPos.y += cameraHeight;
            _minimapCam.transform.position = camPos;

            if (rotateWithPlayer)
            {
                _minimapCam.transform.rotation = Quaternion.Euler(90f, targetPlayer.eulerAngles.y, 0f);
            }
            else
            {
                _minimapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Throttled minimap camera rendering pass (18 FPS saves massive GPU/CPU draw calls)
            _renderTimer += Time.unscaledDeltaTime;
            float interval = targetRenderFps > 0f ? (1f / targetRenderFps) : 0.055f;
            if (_renderTimer >= interval)
            {
                _renderTimer = 0f;
                _minimapCam.Render();
            }

            UpdatePlayerIcon();
            UpdateEnemyBlips();
        }

        private void UpdatePlayerIcon()
        {
            if (playerIconRect == null || targetPlayer == null) return;

            if (rotateWithPlayer)
            {
                playerIconRect.localRotation = Quaternion.identity;
            }
            else
            {
                float zRot = -targetPlayer.eulerAngles.y;
                playerIconRect.localRotation = Quaternion.Euler(0f, 0f, zRot);
            }
        }

        private void RefreshEnemyList()
        {
            _activeEnemies.Clear();
            SwordEnemyAI[] enemies = Object.FindObjectsByType<SwordEnemyAI>();
            foreach (var e in enemies)
            {
                if (e != null && e.gameObject.activeInHierarchy && e.currentHealth > 0)
                {
                    _activeEnemies.Add(e);
                }
            }

            // Remove destroyed blips
            List<SwordEnemyAI> keysToRemove = new List<SwordEnemyAI>();
            foreach (var kvp in _blipMap)
            {
                if (kvp.Key == null || !_activeEnemies.Contains(kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var k in keysToRemove)
            {
                _blipMap.Remove(k);
            }

            // Create blips for new enemies
            if (enemyBlipsContainer != null)
            {
                foreach (var e in _activeEnemies)
                {
                    if (!_blipMap.ContainsKey(e))
                    {
                        GameObject blipObj = new GameObject($"Blip_{e.name}");
                        blipObj.transform.SetParent(enemyBlipsContainer, false);

                        RectTransform rect = blipObj.AddComponent<RectTransform>();
                        rect.sizeDelta = new Vector2(blipSize, blipSize);

                        Image img = blipObj.AddComponent<Image>();
                        img.color = enemyBlipColor;

                        _blipMap[e] = rect;
                    }
                }
            }
        }

        private void UpdateEnemyBlips()
        {
            if (targetPlayer == null || minimapRawImage == null) return;

            float minimapWidth = minimapRawImage.rectTransform.rect.width;
            float minimapHeight = minimapRawImage.rectTransform.rect.height;
            float scaleRatio = (minimapWidth / 2f) / orthographicSize;

            foreach (var kvp in _blipMap)
            {
                if (kvp.Key == null || kvp.Value == null) continue;

                Vector3 diff = kvp.Key.transform.position - targetPlayer.position;

                Vector2 blipPos;
                if (rotateWithPlayer)
                {
                    float angle = targetPlayer.eulerAngles.y * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    float x = diff.x * cos - diff.z * sin;
                    float y = diff.x * sin + diff.z * cos;
                    blipPos = new Vector2(x, y) * scaleRatio;
                }
                else
                {
                    blipPos = new Vector2(diff.x, diff.z) * scaleRatio;
                }

                // Clamp to minimap bounds so blips stay visible on border
                float maxOffset = (minimapWidth / 2f) - 10f;
                blipPos.x = Mathf.Clamp(blipPos.x, -maxOffset, maxOffset);
                blipPos.y = Mathf.Clamp(blipPos.y, -maxOffset, maxOffset);

                kvp.Value.anchoredPosition = blipPos;
            }
        }
    }
}
