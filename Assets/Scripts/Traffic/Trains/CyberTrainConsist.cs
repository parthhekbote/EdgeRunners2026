using System.Collections.Generic;
using UnityEngine;

namespace Cyberpunk.Traffic.Trains
{
    public enum TrainState
    {
        Accelerating,
        Cruising,
        Decelerating,
        Dwelling
    }

    /// <summary>
    /// Articulated multi-carriage monorail train consist simulator.
    /// Manages kinematics, realistic acceleration/braking curves, carriage coupling,
    /// cornering banking, station dwell reversals, directional headlights, and 3D audio.
    /// </summary>
    [DisallowMultipleComponent]
    public class CyberTrainConsist : MonoBehaviour
    {
        [Header("Train Carriages")]
        [Tooltip("The lead cockpit carriage.")]
        public Transform cockpit;

        [Tooltip("First middle passenger carriage.")]
        public Transform middleCar1;

        [Tooltip("Second middle passenger carriage.")]
        public Transform middleCar2;

        [Tooltip("Optional rear cockpit carriage for double-ended bidirectional shuttles.")]
        public Transform rearCockpit;

        [Header("Coupling & Alignment")]
        [Tooltip("Distance between adjacent carriage pivots (meters).")]
        public float carSpacing = 12.75f;

        [Tooltip("Vertical offset from rail beam pivot to train car pivot.")]
        public float verticalOffset = -4.39f;

        [Tooltip("Invert Cockpit forward orientation if model nose points in -Z.")]
        public bool invertCockpitForward = true;

        [Tooltip("Invert Middle cars forward orientation if model forward differs.")]
        public bool invertMiddleForward = false;

        [Tooltip("Subtle banking tilt into high-speed curves.")]
        [Range(0f, 10f)]
        public float maxBankAngle = 3.5f;

        [Header("Kinematics & Speeds")]
        [Tooltip("Top cruising speed in meters/second (~80 km/h).")]
        public float cruiseSpeed = 22.0f;

        [Tooltip("Linear acceleration in m/s².")]
        public float acceleration = 3.5f;

        [Tooltip("Braking deceleration in m/s².")]
        public float deceleration = 4.5f;

        [Tooltip("Station dwell duration in seconds before reversing direction.")]
        public float stationDwellDuration = 5.0f;

        [Tooltip("Safety margin from the physical track ends to keep all cars on rails.")]
        public float terminusBuffer = 28.0f;

        [Header("Audio")]
        public AudioClip engineHumClip;
        [Range(0f, 1f)] public float maxAudioVolume = 0.65f;
        public float audioMinDistance = 5.0f;
        public float audioMaxDistance = 50.0f;

        [Header("Lighting")]
        public bool enableHeadlights = true;
        public Color headlightColor = new Color(0.3f, 0.85f, 1.0f);
        public float headlightIntensity = 3.5f;
        public float headlightRange = 35.0f;

        // Path & Runtime State
        private CyberTrainPath _path;
        private float _currentDistance = 0f;
        private float _currentSpeed = 0f;
        private int _travelDirection = 1; // +1 = forward along path, -1 = reverse
        private TrainState _state = TrainState.Accelerating;
        private float _dwellTimer = 0f;

        private AudioSource _audioSource;
        private Light _headlightFront;
        private Light _headlightRear;

        public float CurrentSpeed => _currentSpeed;
        public TrainState State => _state;
        public int TravelDirection => _travelDirection;
        public CyberTrainPath Path => _path;

        public void Initialize(CyberTrainPath path, float startDistance = 0f, float yOffset = -4.39f)
        {
            _path = path;
            verticalOffset = yOffset;

            if (_path != null && _path.IsValid)
            {
                // Ensure starting distance is clamped within valid bounds
                float minD = terminusBuffer;
                float maxD = Mathf.Max(terminusBuffer + 1f, _path.TotalLength - terminusBuffer);
                _currentDistance = Mathf.Clamp(startDistance, minD, maxD);
            }

            SetupAudio();
            SetupHeadlights();
            UpdateCarriagePositions(true);
        }

        private void SetupAudio()
        {
            if (engineHumClip == null) return;

            Transform audioTarget = cockpit != null ? cockpit : transform;
            _audioSource = audioTarget.GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = audioTarget.gameObject.AddComponent<AudioSource>();
            }

            _audioSource.clip = engineHumClip;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 1.0f; // Full 3D spatialization
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = audioMinDistance;
            _audioSource.maxDistance = audioMaxDistance;
            _audioSource.volume = 0.1f;
            _audioSource.pitch = 0.8f;
            _audioSource.playOnAwake = false;

            if (!_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
        }

        private void SetupHeadlights()
        {
            if (!enableHeadlights) return;

            if (cockpit != null && _headlightFront == null)
            {
                GameObject lightObj = new GameObject("Train_Headlight_Front");
                lightObj.transform.SetParent(cockpit, false);
                lightObj.transform.localPosition = new Vector3(0f, 1.2f, invertCockpitForward ? -5.5f : 5.5f);
                lightObj.transform.localRotation = Quaternion.Euler(10f, invertCockpitForward ? 180f : 0f, 0f);

                _headlightFront = lightObj.AddComponent<Light>();
                _headlightFront.type = LightType.Spot;
                _headlightFront.color = headlightColor;
                _headlightFront.intensity = headlightIntensity;
                _headlightFront.range = headlightRange;
                _headlightFront.spotAngle = 65f;
            }

            Transform rear = rearCockpit != null ? rearCockpit : middleCar2;
            if (rear != null && _headlightRear == null)
            {
                GameObject rearLightObj = new GameObject("Train_Headlight_Rear");
                rearLightObj.transform.SetParent(rear, false);
                rearLightObj.transform.localPosition = new Vector3(0f, 1.2f, invertMiddleForward ? -5.5f : 5.5f);
                rearLightObj.transform.localRotation = Quaternion.Euler(10f, invertMiddleForward ? 180f : 0f, 0f);

                _headlightRear = rearLightObj.AddComponent<Light>();
                _headlightRear.type = LightType.Spot;
                _headlightRear.color = Color.red; // Red taillight
                _headlightRear.intensity = headlightIntensity * 0.6f;
                _headlightRear.range = 15f;
                _headlightRear.spotAngle = 70f;
            }
        }

        private void Update()
        {
            if (_path == null || !_path.IsValid) return;

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            UpdateKinematics(dt);
            UpdateCarriagePositions(false);
            UpdateAudio();
            UpdateLights();
        }

        private void UpdateKinematics(float dt)
        {
            float minD = terminusBuffer;
            float maxD = Mathf.Max(terminusBuffer + 1f, _path.TotalLength - terminusBuffer);

            switch (_state)
            {
                case TrainState.Dwelling:
                    _currentSpeed = 0f;
                    _dwellTimer += dt;
                    if (_dwellTimer >= stationDwellDuration)
                    {
                        // Reverse direction and start accelerating
                        _travelDirection = -_travelDirection;
                        _state = TrainState.Accelerating;
                        _dwellTimer = 0f;
                    }
                    break;

                case TrainState.Accelerating:
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, cruiseSpeed, acceleration * dt);
                    if (Mathf.Abs(_currentSpeed - cruiseSpeed) < 0.05f)
                    {
                        _state = TrainState.Cruising;
                    }
                    CheckBrakingDistance(minD, maxD);
                    AdvanceDistance(dt);
                    break;

                case TrainState.Cruising:
                    _currentSpeed = cruiseSpeed;
                    CheckBrakingDistance(minD, maxD);
                    AdvanceDistance(dt);
                    break;

                case TrainState.Decelerating:
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, 1.0f, deceleration * dt);

                    float distRemaining = _travelDirection > 0 ? (maxD - _currentDistance) : (_currentDistance - minD);
                    if (distRemaining <= 0.5f || _currentSpeed <= 1.2f)
                    {
                        _currentDistance = _travelDirection > 0 ? maxD : minD;
                        _currentSpeed = 0f;
                        _state = TrainState.Dwelling;
                        _dwellTimer = 0f;
                    }
                    else
                    {
                        AdvanceDistance(dt);
                    }
                    break;
            }
        }

        private void CheckBrakingDistance(float minD, float maxD)
        {
            // v^2 = 2 * a * d => stoppingDistance = v^2 / (2 * deceleration)
            float stoppingDist = (_currentSpeed * _currentSpeed) / (2f * Mathf.Max(0.5f, deceleration)) + 2.0f;
            float distToEnd = _travelDirection > 0 ? (maxD - _currentDistance) : (_currentDistance - minD);

            if (distToEnd <= stoppingDist)
            {
                _state = TrainState.Decelerating;
            }
        }

        private void AdvanceDistance(float dt)
        {
            _currentDistance += _currentSpeed * _travelDirection * dt;
        }

        private void UpdateCarriagePositions(bool snap)
        {
            // Lead car (Cockpit) is positioned at _currentDistance
            PositionCarriage(cockpit, _currentDistance, invertCockpitForward);

            // Carriages trail behind lead car based on direction
            float spacing1 = _currentDistance - carSpacing * _travelDirection;
            PositionCarriage(middleCar1, spacing1, invertMiddleForward);

            float spacing2 = _currentDistance - (carSpacing * 2f) * _travelDirection;
            PositionCarriage(middleCar2, spacing2, invertMiddleForward);

            if (rearCockpit != null)
            {
                float spacing3 = _currentDistance - (carSpacing * 3f) * _travelDirection;
                PositionCarriage(rearCockpit, spacing3, !invertCockpitForward);
            }
        }

        private void PositionCarriage(Transform car, float distOnPath, bool invertForward)
        {
            if (car == null) return;

            float clampedD = Mathf.Clamp(distOnPath, 0f, _path.TotalLength);
            Vector3 railPoint = _path.Evaluate(clampedD, out Vector3 tangent);

            // Apply vertical offset so train hangs below (or rides above) rail
            Vector3 targetPos = railPoint + Vector3.up * verticalOffset;
            car.position = targetPos;

            // Compute direction along travel
            Vector3 moveDir = tangent * _travelDirection;
            if (moveDir.sqrMagnitude < 0.001f) moveDir = Vector3.forward;

            // Orient model
            Vector3 lookVector = invertForward ? -moveDir : moveDir;
            Quaternion targetRot = Quaternion.LookRotation(lookVector, Vector3.up);

            // Subtle corner banking
            if (maxBankAngle > 0.1f && _currentSpeed > 2.0f)
            {
                // Measure tangent difference slightly ahead to get curvature
                _path.Evaluate(Mathf.Clamp(clampedD + 4f * _travelDirection, 0f, _path.TotalLength), out Vector3 aheadTangent);
                float turnAngle = Vector3.SignedAngle(tangent, aheadTangent, Vector3.up);
                float bankRoll = Mathf.Clamp(-turnAngle * 0.4f * (_currentSpeed / cruiseSpeed), -maxBankAngle, maxBankAngle);
                targetRot *= Quaternion.Euler(0f, 0f, bankRoll);
            }

            car.rotation = targetRot;
        }

        private void UpdateAudio()
        {
            if (_audioSource == null || !_audioSource.isPlaying) return;

            float speedRatio = Mathf.Clamp01(_currentSpeed / Mathf.Max(1f, cruiseSpeed));
            _audioSource.pitch = Mathf.Lerp(0.75f, 1.25f, speedRatio);
            _audioSource.volume = Mathf.Lerp(0.15f, maxAudioVolume, speedRatio);
        }

        private void UpdateLights()
        {
            if (!enableHeadlights) return;

            // Front headlight shines in travel direction
            if (_headlightFront != null)
            {
                _headlightFront.color = _travelDirection > 0 ? headlightColor : Color.red;
                _headlightFront.intensity = _travelDirection > 0 ? headlightIntensity : headlightIntensity * 0.5f;
            }

            if (_headlightRear != null)
            {
                _headlightRear.color = _travelDirection > 0 ? Color.red : headlightColor;
                _headlightRear.intensity = _travelDirection > 0 ? headlightIntensity * 0.5f : headlightIntensity;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_path != null)
            {
                _path.DrawGizmos(new Color(0f, 0.9f, 1f, 0.7f));
            }
        }
    }
}
