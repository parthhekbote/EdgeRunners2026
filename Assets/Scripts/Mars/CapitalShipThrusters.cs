using System.Collections.Generic;
using UnityEngine;
using SyntyCombined.VFX;

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Engine flames for the two capital-ship prefabs, using the project's AAAStarshipThrusterSystem (the tapered
    /// white-cyan plasma flame, needle sparks and ribbon the player's starship burns with) placed in the real bell
    /// nozzles of the hulls. Positions were measured from the prefabs' rear view (prefab-local units, so they hold at any
    /// ship scale).
    /// </summary>
    public static class CapitalShipThrusters
    {
        public class Rig
        {
            public readonly List<AAAStarshipThrusterSystem> pods = new List<AAAStarshipThrusterSystem>();
            public readonly List<Light> lights = new List<Light>();
            public float lightIntensity = 16f;
        }

        // SM_Ship_Colossal_01: four bells (two upper, two lower), exit plane near the stern
        private static readonly Vector3[] ColossalNozzles =
        {
            new Vector3(-37f, -13.7f, -298f), new Vector3(37f, -13.7f, -298f),
            new Vector3(-20.7f, -51.9f, -298f), new Vector3(20.7f, -51.9f, -298f),
        };

        // SM_Ship_Galactic_Carrier_01: three bells (left, right, lower centre)
        private static readonly Vector3[] CarrierNozzles =
        {
            new Vector3(-20.2f, -3.1f, -131f), new Vector3(20f, -3.1f, -131f),
            new Vector3(0f, -15.7f, -131f),
        };

        public static Rig BuildColossal(GameObject ship) => Build(ship, ColossalNozzles, 4.0f, 150f, 18f);
        public static Rig BuildCarrier(GameObject ship) => Build(ship, CarrierNozzles, 1.8f, 90f, 14f);

        private static Rig Build(GameObject ship, Vector3[] nozzles, float anchorScale, float lightRange, float lightIntensity)
        {
            var rig = new Rig { lightIntensity = lightIntensity };
            if (ship == null) return rig;

            // Remove the old cylinder-and-light glows an earlier version saved into scenes
            for (int i = ship.transform.childCount - 1; i >= 0; i--)
            {
                var child = ship.transform.GetChild(i).gameObject;
                string n = child.name;
                if (n.StartsWith("Thruster_Glow_Mesh_") || n.StartsWith("Thruster_Light_") || n.StartsWith("Engines_Pod_"))
                {
                    if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
                }
            }

            // Two nozzles per pod: the system looks for a left and a right anchor under its own transform
            for (int start = 0, podIndex = 0; start < nozzles.Length; start += 2, podIndex++)
            {
                var pod = new GameObject("Engines_Pod_" + (podIndex + 1));
                pod.transform.SetParent(ship.transform, false);

                AddAnchor(pod.transform, "Booster_L_Anchor", nozzles[start], anchorScale);
                if (start + 1 < nozzles.Length) AddAnchor(pod.transform, "Booster_R_Anchor", nozzles[start + 1], anchorScale);

                var sys = pod.AddComponent<AAAStarshipThrusterSystem>(); // builds the nozzle rigs from the anchors
                sys.currentThrottleFactor = 1f;
                sys.isBoosting = false;
                rig.pods.Add(sys);

                // A wide soft glow so the flames light the hull and the planet below
                Vector3 mid = nozzles[start];
                if (start + 1 < nozzles.Length) mid = (nozzles[start] + nozzles[start + 1]) * 0.5f;
                var lightGo = new GameObject("Engine_Glow");
                lightGo.transform.SetParent(pod.transform, false);
                lightGo.transform.localPosition = mid + Vector3.back * (anchorScale * 4f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.35f, 0.9f, 1f);
                l.range = lightRange;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                rig.lights.Add(l);
            }
            return rig;
        }

        private static void AddAnchor(Transform pod, string name, Vector3 localPos, float scale)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(pod, false);
            anchor.localPosition = localPos;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one * scale;
        }

        /// <summary>0 = engines dark, 1 = full cruise burn, above 0.85 the flames switch to boost length.</summary>
        public static void SetPower(Rig rig, float factor)
        {
            if (rig == null) return;
            factor = Mathf.Clamp01(factor);
            foreach (var sys in rig.pods)
            {
                if (sys == null) continue;
                sys.gameObject.SetActive(factor > 0.03f);
                sys.currentThrottleFactor = Mathf.Max(0.2f, factor);
                sys.isBoosting = false; // cruise-length flames: boost would make them a quarter of the hull long
            }
            foreach (var l in rig.lights)
            {
                if (l != null) l.intensity = rig.lightIntensity * factor;
            }
        }
    }
}
