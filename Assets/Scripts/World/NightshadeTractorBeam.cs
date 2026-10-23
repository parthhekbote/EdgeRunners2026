using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;

namespace SyntyCombined.World
{
    /// <summary>
    /// SS-04 Nightshade Striker Ventral Magnetic Tractor Beam & Cargo Latch System.
    /// Provides:
    /// - Dual-layer volumetric energy tractor beam with animated helical resonance
    /// - 60-meter conical magnetic lock-on targeting system
    /// - Zero-G gravitational pull physics with distance damping
    /// - Physical ventral magnetic clamp array holding up to 4 Core Matrices
    /// - Tactical HUD reticle with distance telemetry and cargo capacity readout
    /// - Procedural magnetic hum, clamp lock audio & electrical discharge VFX
    /// </summary>
    public class NightshadeTractorBeam : MonoBehaviour
    {
        public static NightshadeTractorBeam ActiveTractor { get; private set; }

        [Header("Tractor Beam Settings")]
        public Transform emitterOrigin;
        public float maxRange = 65f;
        public float coneAngle = 80f;
        public float pullForce = 28f;
        public float latchDistance = 3.6f;
        public KeyCode toggleKey = KeyCode.T;
        public bool isBeamActive = true;

        [Header("Ventral Cargo Sockets (4 Cells)")]
        public Transform cargoBayRoot;
        public Vector3[] localSocketOffsets = new Vector3[]
        {
            new Vector3(-1.35f, -0.92f, 0.90f),  // Front Left
            new Vector3( 1.35f, -0.92f, 0.90f),  // Front Right
            new Vector3(-1.35f, -0.92f, -1.90f), // Rear Left
            new Vector3( 1.35f, -0.92f, -1.90f)  // Rear Right
        };

        private List<AsteroidCoreMatrix> _heldCores = new List<AsteroidCoreMatrix>();
        public List<AsteroidCoreMatrix> HeldCores => _heldCores;
        public int HeldCoreCount => _heldCores.Count;

        // Active Target & Beams
        private AsteroidCoreMatrix _currentTarget;
        private LineRenderer _innerBeam;
        private LineRenderer _outerBeam;
        private Light _emitterLight;

        // Audio
        private AudioSource _tractorAudio;
        private AudioSource _clampAudio;
        private AudioClip _tractorHumClip;
        private AudioClip _clampLockClip;

        // Spaceship Flight Controller
        private SpaceshipFlightController _flightCtrl;
        private Camera _mainCam;

        public event System.Action<AsteroidCoreMatrix> OnCoreSecured;

        private void Awake()
        {
            ActiveTractor = this;
            _flightCtrl = GetComponent<SpaceshipFlightController>();
            SetupEmitterAndCargoBay();
            SetupBeams();
            SetupAudio();
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

        private void SetupEmitterAndCargoBay()
        {
            if (cargoBayRoot == null)
            {
                var bay = transform.Find("Ventral_Cargo_Clamp_Bay");
                if (bay == null)
                {
                    var go = new GameObject("Ventral_Cargo_Clamp_Bay");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = Vector3.zero;
                    cargoBayRoot = go.transform;
                }
                else
                {
                    cargoBayRoot = bay;
                }
            }

            if (emitterOrigin == null)
            {
                var emitter = transform.Find("Tractor_Beam_Emitter");
                if (emitter == null)
                {
                    var go = new GameObject("Tractor_Beam_Emitter");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, -0.95f, 0.45f);
                    emitterOrigin = go.transform;
                }
                else
                {
                    emitterOrigin = emitter;
                }
            }

            // Emitter Glow Light
            var lightObj = emitterOrigin.Find("Tractor_Emitter_Light");
            if (lightObj == null)
            {
                var go = new GameObject("Tractor_Emitter_Light");
                go.transform.SetParent(emitterOrigin, false);
                _emitterLight = go.AddComponent<Light>();
                _emitterLight.type = LightType.Point;
                _emitterLight.color = new Color(0f, 0.95f, 1f);
                _emitterLight.range = 9.0f;
                _emitterLight.intensity = 2.0f;
            }
            else
            {
                _emitterLight = lightObj.GetComponent<Light>();
            }
        }

        private void SetupBeams()
        {
            // Inner core laser beam
            var innerObj = new GameObject("Tractor_Inner_Beam");
            innerObj.transform.SetParent(emitterOrigin, false);
            _innerBeam = innerObj.AddComponent<LineRenderer>();
            _innerBeam.useWorldSpace = true;
            _innerBeam.positionCount = 2;
            _innerBeam.startWidth = 0.22f;
            _innerBeam.endWidth = 0.45f;
            _innerBeam.material = CreateBeamMaterial(new Color(0.7f, 0.95f, 1f, 0.95f));
            _innerBeam.enabled = false;

            // Outer energy helix beam
            var outerObj = new GameObject("Tractor_Outer_Helix_Beam");
            outerObj.transform.SetParent(emitterOrigin, false);
            _outerBeam = outerObj.AddComponent<LineRenderer>();
            _outerBeam.useWorldSpace = true;
            _outerBeam.positionCount = 20;
            _outerBeam.startWidth = 0.55f;
            _outerBeam.endWidth = 0.95f;
            _outerBeam.material = CreateBeamMaterial(new Color(0f, 0.75f, 1f, 0.55f));
            _outerBeam.enabled = false;
        }

        private Material CreateBeamMaterial(Color col)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = col;
            mat.SetColor("_BaseColor", col);
            return mat;
        }

        private void SetupAudio()
        {
            _tractorHumClip = GenerateTractorHum();
            _clampLockClip = GenerateClampLockSound();

            _tractorAudio = gameObject.AddComponent<AudioSource>();
            _tractorAudio.clip = _tractorHumClip;
            _tractorAudio.loop = true;
            _tractorAudio.playOnAwake = false;
            _tractorAudio.spatialBlend = 0.4f;
            _tractorAudio.volume = 0f;

            _clampAudio = gameObject.AddComponent<AudioSource>();
            _clampAudio.playOnAwake = false;
            _clampAudio.spatialBlend = 0.3f;
            _clampAudio.volume = 0.85f;
        }

        private AudioClip GenerateTractorHum()
        {
            int sampleRate = 44100;
            int samples = sampleRate * 2;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.45f;
                float s2 = Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.25f;
                float mod = (1f + Mathf.Sin(2f * Mathf.PI * 12f * t) * 0.35f);
                data[i] = Mathf.Clamp((s1 + s2) * mod, -1f, 1f);
            }
            var clip = AudioClip.Create("Tractor_Hum_Procedural", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateClampLockSound()
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * 0.38f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(480f, 95f, t * t);
                float env = Mathf.Pow(1f - t, 1.8f);
                float noise = (Random.value * 2f - 1f) * 0.25f * env;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * (float)i / sampleRate) * 0.75f + noise) * env;
            }
            var clip = AudioClip.Create("Clamp_Lock_Procedural", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void Update()
        {
            if (_flightCtrl == null) _flightCtrl = GetComponent<SpaceshipFlightController>();
            if (_mainCam == null) FindCamera();

            // Toggle Tractor Beam on [T] when piloted
            if (_flightCtrl != null && _flightCtrl.IsPiloted)
            {
                if (Input.GetKeyDown(toggleKey))
                {
                    isBeamActive = !isBeamActive;
                }
            }

            if (isBeamActive && _heldCores.Count < localSocketOffsets.Length)
            {
                UpdateTargetScanning();
                UpdateTractorPull();
            }
            else
            {
                ClearTarget();
            }

            UpdateBeamVisuals();
        }

        private void UpdateTargetScanning()
        {
            // Find closest unlatched AsteroidCoreMatrix within range and cone
            AsteroidCoreMatrix closest = null;
            float closestDist = maxRange;

            Vector3 emitterPos = emitterOrigin != null ? emitterOrigin.position : transform.position;
            Vector3 scanDir = -transform.up * 0.5f + transform.forward * 0.5f;

            foreach (var core in AsteroidCoreMatrix.AllMatrices)
            {
                if (core == null || core.currentState != CoreMatrixState.DriftingInSpace && core.currentState != CoreMatrixState.TractorPull)
                {
                    continue;
                }

                Vector3 toCore = core.transform.position - emitterPos;
                float dist = toCore.magnitude;
                if (dist > maxRange) continue;

                float angle = Vector3.Angle(scanDir, toCore);
                if (angle <= coneAngle * 0.5f)
                {
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = core;
                    }
                }
            }

            if (closest != _currentTarget)
            {
                if (_currentTarget != null && _currentTarget.currentState == CoreMatrixState.TractorPull)
                {
                    _currentTarget.currentState = CoreMatrixState.DriftingInSpace;
                }

                _currentTarget = closest;
                if (_currentTarget != null)
                {
                    _currentTarget.currentState = CoreMatrixState.TractorPull;
                }
            }
        }

        private void UpdateTractorPull()
        {
            if (_currentTarget == null) return;

            Vector3 emitterPos = emitterOrigin != null ? emitterOrigin.position : transform.position;
            Vector3 toEmitter = emitterPos - _currentTarget.transform.position;
            float dist = toEmitter.magnitude;

            // Gravitational pull towards ship's ventral docking bay
            if (dist > latchDistance)
            {
                float speedFactor = Mathf.Clamp01(dist / 20f);
                float currentSpeed = Mathf.Lerp(pullForce * 1.5f, pullForce, speedFactor);
                Vector3 pullStep = toEmitter.normalized * currentSpeed * Time.deltaTime;
                _currentTarget.transform.position += pullStep;
            }
            else
            {
                // Magnetically latch the core to the underbelly rack!
                LatchCore(_currentTarget);
            }
        }

        private void LatchCore(AsteroidCoreMatrix core)
        {
            if (core == null || _heldCores.Contains(core)) return;
            if (_heldCores.Count >= localSocketOffsets.Length) return;

            int socketIndex = _heldCores.Count;
            Vector3 socketPos = localSocketOffsets[socketIndex];

            _heldCores.Add(core);
            core.AttachToShipClamp(cargoBayRoot, socketPos);

            // Clamp lock SFX
            if (_clampAudio != null && _clampLockClip != null)
            {
                _clampAudio.PlayOneShot(_clampLockClip, 0.90f);
            }

            // Spawn electric docking spark flash
            SpawnLatchSparks(core.transform.position);

            OnCoreSecured?.Invoke(core);

            // Clear current target so scanner immediately seeks next core
            _currentTarget = null;
        }

        private void SpawnLatchSparks(Vector3 pos)
        {
            var sparkObj = new GameObject("Core_Latch_Sparks");
            sparkObj.transform.position = pos;

            var ps = sparkObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 8f;
            main.startSize = 0.25f;
            main.startColor = new Color(0f, 0.95f, 1f, 0.9f);
            main.stopAction = ParticleSystemStopAction.Destroy;

            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            ps.Play();
            Destroy(sparkObj, 0.8f);
        }

        private void ClearTarget()
        {
            if (_currentTarget != null)
            {
                if (_currentTarget.currentState == CoreMatrixState.TractorPull)
                {
                    _currentTarget.currentState = CoreMatrixState.DriftingInSpace;
                }
                _currentTarget = null;
            }
        }

        private void UpdateBeamVisuals()
        {
            bool hasTarget = _currentTarget != null && isBeamActive;

            if (_innerBeam != null) _innerBeam.enabled = hasTarget;
            if (_outerBeam != null) _outerBeam.enabled = hasTarget;

            if (hasTarget)
            {
                Vector3 startPos = emitterOrigin.position;
                Vector3 endPos = _currentTarget.transform.position;

                // Inner core beam
                _innerBeam.SetPosition(0, startPos);
                _innerBeam.SetPosition(1, endPos);

                // Outer animated helix beam
                int count = 20;
                float dist = Vector3.Distance(startPos, endPos);
                Vector3 dir = (endPos - startPos).normalized;
                Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                Vector3 up = Vector3.Cross(dir, right).normalized;

                float helixFreq = 6.0f;
                float helixRadius = 0.65f;
                float waveOffset = Time.time * 16f;

                for (int i = 0; i < count; i++)
                {
                    float p = (float)i / (count - 1);
                    Vector3 linearPos = Vector3.Lerp(startPos, endPos, p);
                    float sin = Mathf.Sin(p * dist * helixFreq + waveOffset) * helixRadius * (1f - p * 0.4f);
                    float cos = Mathf.Cos(p * dist * helixFreq + waveOffset) * helixRadius * (1f - p * 0.4f);
                    Vector3 helixPos = linearPos + right * sin + up * cos;
                    _outerBeam.SetPosition(i, helixPos);
                }

                // Audio modulation
                if (_tractorAudio != null)
                {
                    if (!_tractorAudio.isPlaying) _tractorAudio.Play();
                    _tractorAudio.volume = Mathf.MoveTowards(_tractorAudio.volume, 0.45f, Time.deltaTime * 4f);
                    _tractorAudio.pitch = Mathf.Lerp(1.15f, 0.95f, dist / maxRange);
                }

                if (_emitterLight != null)
                {
                    _emitterLight.intensity = 3.5f + Mathf.Sin(Time.time * 24f) * 0.8f;
                }
            }
            else
            {
                if (_tractorAudio != null && _tractorAudio.isPlaying)
                {
                    _tractorAudio.volume = Mathf.MoveTowards(_tractorAudio.volume, 0f, Time.deltaTime * 6f);
                    if (_tractorAudio.volume <= 0.01f) _tractorAudio.Stop();
                }

                if (_emitterLight != null)
                {
                    _emitterLight.intensity = Mathf.MoveTowards(_emitterLight.intensity, 1.2f, Time.deltaTime * 2f);
                }
            }
        }

        public void ReleaseAllCoresToReceptor(Vector3 receptorBayPos, System.Action onComplete)
        {
            if (_heldCores.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(ReleaseRoutine(receptorBayPos, onComplete));
        }

        private IEnumerator ReleaseRoutine(Vector3 receptorBayPos, System.Action onComplete)
        {
            List<AsteroidCoreMatrix> coresToRelease = new List<AsteroidCoreMatrix>(_heldCores);
            _heldCores.Clear();

            int completed = 0;
            for (int i = 0; i < coresToRelease.Count; i++)
            {
                var core = coresToRelease[i];
                float angle = (i * 90f) * Mathf.Deg2Rad;
                Vector3 slotOffset = new Vector3(Mathf.Sin(angle) * 3.2f, 0f, Mathf.Cos(angle) * 3.2f);
                Vector3 targetPos = receptorBayPos + slotOffset;
                Quaternion targetRot = Quaternion.Euler(0f, i * 90f, 0f);

                core.SlotIntoEngine(targetPos, targetRot, () =>
                {
                    completed++;
                });

                yield return new WaitForSeconds(0.45f);
            }

            while (completed < coresToRelease.Count)
            {
                yield return null;
            }

            onComplete?.Invoke();
        }

        #region HUD Telemetry
        private void OnGUI()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;
            if (_flightCtrl == null || !_flightCtrl.IsPiloted) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // Ventral Cargo Clamp Status (Bottom Right)
            float bayW = 240f;
            float bayH = 75f;
            float bayX = sw - bayW - 24f;
            float bayY = sh - bayH - 45f;

            GUI.color = new Color(0.02f, 0.06f, 0.12f, 0.90f);
            GUI.DrawTexture(new Rect(bayX, bayY, bayW, bayH), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0.94f, 1f, 0.95f);
            DrawBorder(new Rect(bayX, bayY, bayW, bayH), 1);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0f, 0.95f, 1f);
            GUI.Label(new Rect(bayX + 12f, bayY + 8f, bayW - 24f, 18f), $"VENTRAL CARGO: {_heldCores.Count} / 4 MATRICES", titleStyle);

            // 4 Slot Indicators
            float boxSize = 20f;
            float spacing = 12f;
            float totalBoxW = (boxSize * 4) + (spacing * 3);
            float startBoxX = bayX + (bayW - totalBoxW) * 0.5f;
            float boxY = bayY + 34f;

            for (int i = 0; i < 4; i++)
            {
                bool isFilled = i < _heldCores.Count;
                Rect boxRect = new Rect(startBoxX + i * (boxSize + spacing), boxY, boxSize, boxSize);

                GUI.color = isFilled ? new Color(0f, 0.95f, 1f, 0.95f) : new Color(0.12f, 0.22f, 0.35f, 0.65f);
                GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
                GUI.color = new Color(0f, 0.95f, 1f, 0.95f);
                DrawBorder(boxRect, 1);
            }

            // Tractor Lock Reticle on Screen
            if (_currentTarget != null && _mainCam != null)
            {
                Vector3 screenPos = _mainCam.WorldToScreenPoint(_currentTarget.transform.position);
                if (screenPos.z > 0.5f)
                {
                    float cx = screenPos.x;
                    float cy = sh - screenPos.y;
                    float dist = screenPos.z;

                    GUI.color = new Color(0f, 0.95f, 1f, 0.95f);
                    DrawCircleReticle(cx, cy, 32f);

                    var lockStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        fontStyle = FontStyle.Bold
                    };
                    lockStyle.normal.textColor = new Color(0.2f, 1f, 0.5f);
                    GUI.Label(new Rect(cx - 120f, cy + 34f, 240f, 20f), $">> TRACTOR PULLING // {dist:F0}m <<", lockStyle);
                }
            }

            GUI.color = Color.white;
        }

        private void DrawBorder(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private void DrawCircleReticle(float cx, float cy, float radius)
        {
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a1 = (i / (float)segments) * Mathf.PI * 2f;
                float a2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector2 p1 = new Vector2(cx + Mathf.Cos(a1) * radius, cy + Mathf.Sin(a1) * radius);
                Vector2 p2 = new Vector2(cx + Mathf.Cos(a2) * radius, cy + Mathf.Sin(a2) * radius);
                DrawGuiLine(p1, p2, 2);
            }
        }

        private void DrawGuiLine(Vector2 p1, Vector2 p2, int thickness)
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
