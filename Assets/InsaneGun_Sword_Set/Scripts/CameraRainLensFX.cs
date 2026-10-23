using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Cinematic Screen-Space Camera Lens Rain Droplet & Moisture FX.
    /// Emulates physical water beads, droplets, and downward gravity streaks
    /// running down the camera lens during rainy cyberpunk combat.
    /// Inspired by Arkham Knight, Driveclub, and Cyberpunk 2077.
    /// </summary>
    [AddComponentMenu("InsaneGun/Camera Rain Lens FX")]
    public class CameraRainLensFX : MonoBehaviour
    {
        public static CameraRainLensFX Instance { get; private set; }

        [Header("Rain Droplet Settings")]
        public bool enableRainDroplets = true;
        [Range(10, 80)] public int maxActiveDroplets = 45;
        [Range(0.2f, 2.5f)] public float dropletSpawnRate = 1.2f;
        [Range(0.5f, 3.0f)] public float gravityStreakSpeed = 1.6f;
        [Range(0.02f, 0.08f)] public float dropletSize = 0.045f;
        [Range(0.1f, 1.0f)] public float opacity = 0.75f;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Camera _targetCam;
        private float _spawnTimer = 0f;

        private class RainDroplet
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float life;
            public float maxLife;
            public float initialAlpha;
            public bool isStreaking;
        }

        private readonly List<RainDroplet> _activeDroplets = new List<RainDroplet>();
        private readonly Queue<RainDroplet> _pool = new Queue<RainDroplet>();
        private Sprite _dropletSprite;

        private void Awake()
        {
            Instance = this;
            _targetCam = GetComponent<Camera>();
            if (_targetCam == null) _targetCam = Camera.main;

            CreateDropletTexture();
            CreateOverlayCanvas();
        }

        private void OnDisable()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                if (Application.isPlaying) Destroy(_canvas.gameObject);
                else DestroyImmediate(_canvas.gameObject);
            }
        }

        private void CreateDropletTexture()
        {
            // Procedural high-res smooth droplet texture with specular highlight and refractive edge
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float dist = Vector2.Distance(pos, center);

                    if (dist > radius)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else
                    {
                        float normDist = dist / radius;
                        // Glass refraction ring: transparent center with bright specular ring rim
                        float ringRim = Mathf.SmoothStep(0.6f, 1.0f, normDist) * 0.85f;
                        // Upper specular highlight glint
                        Vector2 glintPos = center + new Vector2(-radius * 0.3f, radius * 0.35f);
                        float glintDist = Vector2.Distance(pos, glintPos);
                        float glint = Mathf.Clamp01(1f - (glintDist / (radius * 0.28f)));
                        glint = Mathf.Pow(glint, 2.5f) * 0.95f;

                        float alpha = Mathf.Clamp01(ringRim + glint + 0.12f);
                        Color c = new Color(0.95f, 0.98f, 1.0f, alpha);
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            _dropletSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void CreateOverlayCanvas()
        {
            var canvasGo = new GameObject("Camera_Rain_Lens_Canvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 3000; // Over game, behind pause menus

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void Update()
        {
            if (!enableRainDroplets || _canvas == null)
            {
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            _canvasGroup.alpha = opacity;

            // Spawn new droplets
            _spawnTimer += Time.deltaTime * dropletSpawnRate;
            while (_spawnTimer >= 1.0f && _activeDroplets.Count < maxActiveDroplets)
            {
                _spawnTimer -= 1.0f;
                SpawnDroplet();
            }

            // Update existing droplets
            float dt = Time.deltaTime;
            for (int i = _activeDroplets.Count - 1; i >= 0; i--)
            {
                var d = _activeDroplets[i];
                d.life -= dt;

                if (d.life <= 0f)
                {
                    DespawnDroplet(i);
                    continue;
                }

                // Downward streak movement if gravity streak active
                if (d.isStreaking)
                {
                    d.rect.anchoredPosition += d.velocity * dt;
                }

                // Smooth fade-out in final 25% of lifetime
                float fadeT = Mathf.Clamp01(d.life / (d.maxLife * 0.25f));
                Color c = d.image.color;
                c.a = d.initialAlpha * fadeT;
                d.image.color = c;
            }
        }

        private void SpawnDroplet()
        {
            RainDroplet d = GetOrCreateDroplet();

            // Random screen position
            float x = Random.Range(-900f, 900f);
            float y = Random.Range(-480f, 520f);
            d.rect.anchoredPosition = new Vector2(x, y);

            // Variable droplet scale & elongation
            float sizePx = Random.Range(24f, 54f) * (dropletSize / 0.045f);
            bool streak = Random.value < 0.45f;
            d.isStreaking = streak;
            d.rect.sizeDelta = streak ? new Vector2(sizePx * 0.75f, sizePx * Random.Range(1.3f, 2.2f)) : new Vector2(sizePx, sizePx);

            d.maxLife = Random.Range(3.0f, 7.5f);
            d.life = d.maxLife;
            d.initialAlpha = Random.Range(0.45f, 0.85f);
            d.velocity = streak ? new Vector2(Random.Range(-5f, 5f), -Random.Range(35f, 85f) * gravityStreakSpeed) : Vector2.zero;

            d.image.enabled = true;
            Color c = d.image.color;
            c.a = d.initialAlpha;
            d.image.color = c;

            _activeDroplets.Add(d);
        }

        private RainDroplet GetOrCreateDroplet()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }

            var go = new GameObject("Rain_Bead", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);

            var rect = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();
            img.sprite = _dropletSprite;
            img.raycastTarget = false;

            return new RainDroplet
            {
                rect = rect,
                image = img
            };
        }

        private void DespawnDroplet(int index)
        {
            var d = _activeDroplets[index];
            _activeDroplets.RemoveAt(index);
            d.image.enabled = false;
            _pool.Enqueue(d);
        }
    }
}
