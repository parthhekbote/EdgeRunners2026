using System;
using System.Collections.Generic;
using UnityEngine;

namespace SyntyCombined.Cutscene
{
    [Serializable]
    public class FlightWaypoint
    {
        public string label = "Waypoint";
        public Vector3 position;
        public Vector3 handleIn = new Vector3(0f, 0f, -5f);
        public Vector3 handleOut = new Vector3(0f, 0f, 5f);
        [Range(-65f, 65f)] public float rollBankAngle = 0f;
        public float targetSpeed = 45f;
        [Range(0.1f, 2.5f)] public float thrusterPower = 1.0f;
        [Range(40f, 95f)] public float cameraFov = 68f;

        public FlightWaypoint(string lbl, Vector3 pos, Vector3 hIn, Vector3 hOut, float roll, float speed, float thruster, float fov)
        {
            label = lbl;
            position = pos;
            handleIn = hIn;
            handleOut = hOut;
            rollBankAngle = roll;
            targetSpeed = speed;
            thrusterPower = thruster;
            cameraFov = fov;
        }
    }

    /// <summary>
    /// Guardians of the Galaxy Themed Curvy Flight Path ScriptableObject.
    /// Completely tweakable in the Unity Inspector and Scene View:
    /// - Interactive 3D Bézier waypoints with handles
    /// - Roll banking angles into turns
    /// - Speed and thruster intensity profiles
    /// </summary>
    [CreateAssetMenu(fileName = "BlackMarket_Arrival_FlightPath", menuName = "Cyberpunk/Black Market Flight Path SO")]
    public class BlackMarketFlightPathSO : ScriptableObject
    {
        [Header("Flight Waypoint Curve")]
        public List<FlightWaypoint> waypoints = new List<FlightWaypoint>();

        [Header("Gizmo Settings")]
        public Color curveColor = new Color(0.0f, 0.95f, 1.0f, 0.95f);
        public Color handleColor = new Color(1.0f, 0.85f, 0.05f, 0.85f);
        public Color waypointColor = new Color(1.0f, 0.25f, 0.45f, 1.0f);
        public int curveResolution = 80;

        /// <summary>
        /// Evaluates the complete flight curve at normalized progress t (0.0 to 1.0)
        /// </summary>
        public void Evaluate(float t, out Vector3 position, out Quaternion rotation, out float speed, out float thruster, out float fov)
        {
            t = Mathf.Clamp01(t);

            if (waypoints == null || waypoints.Count == 0)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                speed = 30f;
                thruster = 1f;
                fov = 68f;
                return;
            }

            if (waypoints.Count == 1)
            {
                position = waypoints[0].position;
                rotation = Quaternion.Euler(0f, 0f, waypoints[0].rollBankAngle);
                speed = waypoints[0].targetSpeed;
                thruster = waypoints[0].thrusterPower;
                fov = waypoints[0].cameraFov;
                return;
            }

            int numSegments = waypoints.Count - 1;
            float scaledT = t * numSegments;
            int segIdx = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, numSegments - 1);
            float segT = scaledT - segIdx;

            var p0 = waypoints[segIdx];
            var p1 = waypoints[segIdx + 1];

            Vector3 cp0 = p0.position;
            Vector3 cp1 = p0.position + p0.handleOut;
            Vector3 cp2 = p1.position + p1.handleIn;
            Vector3 cp3 = p1.position;

            // Cubic Bezier position
            position = CalculateCubicBezier(cp0, cp1, cp2, cp3, segT);

            // Tangent direction for flight orientation
            Vector3 tangent = CalculateCubicBezierTangent(cp0, cp1, cp2, cp3, segT);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = (cp3 - cp0).normalized;
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;
            }
            else
            {
                tangent.Normalize();
            }

            // Interpolate banking roll angle smoothly
            float roll = Mathf.Lerp(p0.rollBankAngle, p1.rollBankAngle, Mathf.SmoothStep(0f, 1f, segT));

            // Aerodynamic roll banking around tangent axis
            Vector3 worldUp = Vector3.up;
            Vector3 right = Vector3.Cross(worldUp, tangent).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 bankedUp = Quaternion.AngleAxis(-roll, tangent) * Vector3.up;

            Quaternion flightRot = Quaternion.LookRotation(tangent, bankedUp);
            Vector3 flightEuler = flightRot.eulerAngles;
            float rawPitchX = flightEuler.x > 180f ? flightEuler.x - 360f : flightEuler.x;

            // Dynamic Landing Flare Sequence (t from 0.65 to 1.00):
            // Real sci-fi starships flare their nose UP (+4.5°) using ventral repulsors to cushion the descent,
            // and NEVER dive nose-down into the concrete pad!
            if (t < 0.65f)
            {
                rotation = flightRot;
            }
            else
            {
                // Phase 1: Dive Pullout (t = 0.65 to 0.75) - smoothly pull nose out of downward dive to level
                float pullout = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.65f) / 0.10f));

                // Phase 2: Landing Flare & Touchdown (t = 0.75 to 1.00) - flare nose up +4.5°, then gently level to 0.0°
                float flareProgress = Mathf.Clamp01((t - 0.75f) / 0.25f);
                float flareUp = Mathf.Sin(flareProgress * Mathf.PI) * 4.5f;

                // Effective pitch: pull out of dive, then flare nose UP (negative EulerX in Unity is nose UP)
                float pitch = Mathf.Lerp(rawPitchX, 0f, pullout) - flareUp;

                // Yaw: smooth arc into pad heading (180.0° South into Sector 4)
                Vector3 horizTangent = new Vector3(tangent.x, 0f, tangent.z);
                float pathYaw = horizTangent.sqrMagnitude > 0.001f 
                    ? Mathf.Atan2(horizTangent.x, horizTangent.z) * Mathf.Rad2Deg 
                    : 180f;
                float targetYaw = Mathf.LerpAngle(pathYaw, 180f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.70f) / 0.22f)));

                // Roll: level wings (0.0° roll)
                float targetRoll = Mathf.Lerp(roll, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.65f) / 0.25f)));

                rotation = Quaternion.Euler(pitch, targetYaw, targetRoll);
            }

            // Interpolate dynamic parameters
            speed = Mathf.Lerp(p0.targetSpeed, p1.targetSpeed, segT);
            thruster = Mathf.Lerp(p0.thrusterPower, p1.thrusterPower, segT);
            fov = Mathf.Lerp(p0.cameraFov, p1.cameraFov, segT);
        }

        public static Vector3 CalculateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }

        public static Vector3 CalculateCubicBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        /// <summary>
        /// Populates an organic curvy Guardians of the Galaxy arrival flight path into Black Market
        /// </summary>
        public void ResetToDefaultCurvyPath()
        {
            waypoints.Clear();

            // Waypoint 0: High-speed approach starting BEHIND the camera
            waypoints.Add(new FlightWaypoint(
                "01_BehindCamera_Flyby",
                new Vector3(-126.5f, 212.8f, 1005.0f),
                new Vector3(0f, 0f, 15f),
                new Vector3(-4.5f, -9.5f, -48f),
                8f,   // roll
                135f, // roaring flyby speed
                1.8f, // afterburners blazing
                60f   // fov
            ));

            // Waypoint 1: Screaming overhead pass directly over the camera lens
            waypoints.Add(new FlightWaypoint(
                "02_CameraPass_Overhead",
                new Vector3(-138f, 188f, 890f),
                new Vector3(6f, 12f, 55f),
                new Vector3(-14f, -10f, -75f),
                -18f, // initial left bank
                120f,
                1.6f,
                60f
            ));

            // Waypoint 2: Sweeping cosmic S-turn towards fractured sphere opening
            waypoints.Add(new FlightWaypoint(
                "03_Cosmic_S_Turn",
                new Vector3(-175f, 160f, 620f),
                new Vector3(35f, 22f, 80f),
                new Vector3(-35f, -25f, -85f),
                -42f, // sharp left bank
                90f,
                1.4f,
                60f
            ));

            // Waypoint 3: TITLE DROP SLAM - Threading through the fractured cage shell
            waypoints.Add(new FlightWaypoint(
                "04_TitleDrop_CageEntry",
                new Vector3(-95f, 95f, 340f),
                new Vector3(-35f, 30f, 85f),
                new Vector3(35f, -25f, -80f),
                38f,  // counter-bank right
                72f,
                1.2f,
                65f
            ));

            // Waypoint 4: Neon industrial alleyway dive & crane weave
            waypoints.Add(new FlightWaypoint(
                "05_StationCore_CraneWeave",
                new Vector3(12f, 42f, 125f),
                new Vector3(-35f, 25f, 80f),
                new Vector3(10f, -14f, -42f),
                -24f, // left bank
                48f,
                0.95f,
                65f
            ));

            // Waypoint 5: Landing pad flare & repulsor cushion setup over SM_Bld_Corp_LandingPad_02
            waypoints.Add(new FlightWaypoint(
                "06_LandingPad_Approach",
                new Vector3(27.48f, 7.20f, 47.0f),
                new Vector3(-5.0f, 7.0f, 21.0f),
                new Vector3(0f, -1.8f, -6.0f),
                0f,   // level wings
                18f,  // deceleration flare
                0.65f,// hover thruster
                62f
            ));

            // Waypoint 6: Touchdown directly on SM_Bld_Corp_LandingPad_02
            waypoints.Add(new FlightWaypoint(
                "07_LandingPad_Touchdown",
                new Vector3(27.48f, 1.86f, 34.00f),
                new Vector3(0f, 0.0f, 5.0f), // Horizontal tangent at pad touchdown
                Vector3.zero,
                0f,   // perfectly level
                0f,   // stopped
                0.2f, // idle standby pulse
                60f
            ));

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
