using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SyntyCombined.World
{
    public enum CoreMatrixState
    {
        DriftingInSpace,
        TractorPull,
        LatchedToShip,
        SlottingIntoEngine,
        StabilizedInEngine
    }

    /// <summary>
    /// Scattered Asteroid Engine Core Matrix component.
    /// Blew out from SM_Env_Core_01_Engine_01 during station decompression.
    /// Features:
    /// - Zero-G physics drift & tumbling rotation in orbit
    /// - High-energy pulsating plasma core lighting & particle aura
    /// - Tactical 3D HUD waypoint beacon with distance telemetry
    /// - Smooth magnetic pull & physical latching to SS-04 Nightshade Striker
    /// - Reactor bay slotting sequence with energy surge
    /// </summary>
    public class AsteroidCoreMatrix : MonoBehaviour
    {
        public static List<AsteroidCoreMatrix> AllMatrices = new List<AsteroidCoreMatrix>();

        [Header("Matrix Telemetry")]
        public string matrixId = "ALPHA";
        public string matrixName = "ASTEROID CORE MATRIX // ALPHA";
        public CoreMatrixState currentState = CoreMatrixState.DriftingInSpace;
        public Color energyColor = new Color(0f, 0.95f, 1f); // Radiant cyan

        [Header("Zero-G Drift Dynamics")]
        public float tumbleSpeed = 18f;
        public Vector3 tumbleAxis;
        public float floatingBobAmplitude = 0.65f;
        public float floatingBobFrequency = 0.75f;
        private Vector3 _baseDriftPos;
        private float _timeOffset;

        [Header("Lighting & VFX")]
        public Light coreLight;
        public ParticleSystem coreParticles;
        private float _baseIntensity = 3.5f;

        [Header("Audio")]
        private AudioSource _audioSource;
        private static AudioClip _proceduralHumClip;

        // HUD beacon
        private Camera _mainCam;
        private bool _isLatched = false;
        private Transform _latchParent;
        private Vector3 _latchOffset;

        private void OnEnable()
        {
            if (!AllMatrices.Contains(this))
            {
                AllMatrices.Add(this);
            }
        }

        private void OnDisable()
        {
            AllMatrices.Remove(this);
        }

        private void Awake()
        {
            _baseDriftPos = transform.position;
            _timeOffset = Random.Range(0f, 100f);
            if (tumbleAxis == Vector3.zero)
            {
                tumbleAxis = Random.onUnitSphere;
            }

            SetupVisualsAndAudio();
        }

        private void Start()
        {
            FindCamera();
        }

        private void FindCamera()
        {
            if (_mainCam == null)
            {
                _mainCam = Camera.main;
                if (_mainCam == null)
                {
                    var camObj = GameObject.Find("Main Camera");
                    if (camObj != null) _mainCam = camObj.GetComponent<Camera>();
                }
            }
        }

        private void SetupVisualsAndAudio()
        {
            // Point light
            if (coreLight == null)
            {
                coreLight = GetComponentInChildren<Light>();
                if (coreLight == null)
                {
                    var lightObj = new GameObject("Core_Glow_Light");
                    lightObj.transform.SetParent(transform, false);
                    coreLight = lightObj.AddComponent<Light>();
                    coreLight.type = LightType.Point;
                    coreLight.range = 14f;
                    coreLight.color = energyColor;
                    coreLight.intensity = _baseIntensity;
                }
            }

            // Audio hum
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1.0f;
                _audioSource.minDistance = 4f;
                _audioSource.maxDistance = 60f;
                _audioSource.loop = true;
                _audioSource.playOnAwake = false;
                _audioSource.volume = 0.35f;

                if (_proceduralHumClip == null)
                {
                    _proceduralHumClip = GenerateProceduralCoreHum();
                }
                _audioSource.clip = _proceduralHumClip;
                _audioSource.Play();
            }

            // Setup emissive material tint if possible
            var rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", energyColor * 2.2f);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        private AudioClip GenerateProceduralCoreHum()
        {
            int sampleRate = 44100;
            int samples = sampleRate * 2;
            float[] data = new float[samples];
            float freq1 = 92f;
            float freq2 = 184f;
            float freq3 = 368f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float w1 = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.40f;
                float w2 = Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.25f;
                float w3 = Mathf.Sin(2f * Mathf.PI * freq3 * t) * 0.15f;
                float sub = Mathf.Sin(2f * Mathf.PI * 46f * t) * 0.20f;
                data[i] = Mathf.Clamp(w1 + w2 + w3 + sub, -1f, 1f);
            }

            var clip = AudioClip.Create("Asteroid_Core_Hum", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void Update()
        {
            // Core light pulsation
            if (coreLight != null)
            {
                float pulse = Mathf.Sin(Time.time * 4f + _timeOffset) * 0.35f;
                coreLight.intensity = _baseIntensity + pulse;
            }

            switch (currentState)
            {
                case CoreMatrixState.DriftingInSpace:
                    UpdateDriftPhysics();
                    break;

                case CoreMatrixState.TractorPull:
                    // Rotation speeds up as magnetic induction pulls it
                    transform.Rotate(tumbleAxis, tumbleSpeed * 2.2f * Time.deltaTime, Space.World);
                    break;

                case CoreMatrixState.LatchedToShip:
                    // Stabilized in ship ventral clamp
                    if (_latchParent != null)
                    {
                        transform.position = _latchParent.TransformPoint(_latchOffset);
                        transform.rotation = _latchParent.rotation;
                    }
                    break;

                case CoreMatrixState.SlottingIntoEngine:
                    // Handled via Coroutine
                    break;

                case CoreMatrixState.StabilizedInEngine:
                    transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.World);
                    break;
            }
        }

        private void UpdateDriftPhysics()
        {
            // Smooth zero-g tumble
            transform.Rotate(tumbleAxis, tumbleSpeed * Time.deltaTime, Space.World);

            // Subtle zero-g orbital float bobbing
            float bobY = Mathf.Sin((Time.time + _timeOffset) * floatingBobFrequency) * floatingBobAmplitude;
            float bobX = Mathf.Cos((Time.time + _timeOffset) * floatingBobFrequency * 0.6f) * (floatingBobAmplitude * 0.5f);
            transform.position = _baseDriftPos + new Vector3(bobX, bobY, 0f);
        }

        public void AttachToShipClamp(Transform shipClampTransform, Vector3 localSocketOffset)
        {
            currentState = CoreMatrixState.LatchedToShip;
            _isLatched = true;
            _latchParent = shipClampTransform;
            _latchOffset = localSocketOffset;
            transform.SetParent(shipClampTransform, true);
            transform.localPosition = localSocketOffset;
            transform.localRotation = Quaternion.identity;

            // Reduce collider to prevent physics snag while carried
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (_audioSource != null)
            {
                _audioSource.pitch = 1.35f;
                _audioSource.volume = 0.20f;
            }
        }

        public void DetachFromShip()
        {
            _isLatched = false;
            _latchParent = null;
            transform.SetParent(null, true);
        }

        public void SlotIntoEngine(Vector3 targetPosition, Quaternion targetRotation, System.Action onSlotComplete)
        {
            currentState = CoreMatrixState.SlottingIntoEngine;
            DetachFromShip();
            StartCoroutine(SlottingRoutine(targetPosition, targetRotation, onSlotComplete));
        }

        private IEnumerator SlottingRoutine(Vector3 targetPos, Quaternion targetRot, System.Action onComplete)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float duration = 2.2f;
            float elapsed = 0f;

            // Arcing trajectory into engine core
            Vector3 midPoint = (startPos + targetPos) * 0.5f + Vector3.up * 4.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                // Quadratic Bezier arc
                Vector3 m1 = Vector3.Lerp(startPos, midPoint, ease);
                Vector3 m2 = Vector3.Lerp(midPoint, targetPos, ease);
                transform.position = Vector3.Lerp(m1, m2, ease);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, ease);

                if (coreLight != null)
                {
                    coreLight.intensity = Mathf.Lerp(_baseIntensity, _baseIntensity * 3.5f, ease);
                }

                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
            currentState = CoreMatrixState.StabilizedInEngine;

            onComplete?.Invoke();
        }

        #region Tactical HUD Waypoint Beacon
        private void OnGUI()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;

            if (currentState != CoreMatrixState.DriftingInSpace && currentState != CoreMatrixState.TractorPull)
            {
                return;
            }

            if (_mainCam == null) FindCamera();
            if (_mainCam == null) return;

            Vector3 screenPos = _mainCam.WorldToScreenPoint(transform.position);
            // Must be in front of camera
            if (screenPos.z <= 0.5f) return;

            float dist = screenPos.z;
            float screenX = screenPos.x;
            float screenY = Screen.height - screenPos.y;

            // Waypoint diamond bracket
            float size = Mathf.Clamp(44f - (dist * 0.08f), 22f, 38f);
            float half = size * 0.5f;

            GUI.color = energyColor;
            DrawDiamond(new Rect(screenX - half, screenY - half, size, size), 2);

            // Distance & Identifier Label
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = energyColor;

            string labelText = $"{matrixName}\n[{dist:F0}m]";
            if (currentState == CoreMatrixState.TractorPull)
            {
                labelText = $"TRACTOR LOCKED // {matrixId}\n[{dist:F0}m]";
                style.normal.textColor = new Color(0.1f, 1f, 0.4f);
            }

            GUI.Label(new Rect(screenX - 100f, screenY + half + 2f, 200f, 32f), labelText, style);
            GUI.color = Color.white;
        }

        private void DrawDiamond(Rect rect, int thickness)
        {
            Vector2 top = new Vector2(rect.center.x, rect.yMin);
            Vector2 right = new Vector2(rect.xMax, rect.center.y);
            Vector2 bottom = new Vector2(rect.center.x, rect.yMax);
            Vector2 left = new Vector2(rect.xMin, rect.center.y);

            DrawLine(top, right, thickness);
            DrawLine(right, bottom, thickness);
            DrawLine(bottom, left, thickness);
            DrawLine(left, top, thickness);
        }

        private void DrawLine(Vector2 p1, Vector2 p2, int thickness)
        {
            var matrix = GUI.matrix;
            float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(p1, p2);

            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
        #endregion
    }
}
