using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.VFX
{
    /// <summary>
    /// AAA-Grade Sci-Fi Starship Plasma Thruster VFX System.
    /// Replaces low-poly blocky particle meshes with:
    /// 1. Continuous high-intensity HDR plasma jet core (smooth mesh, attached to nozzle, zero dropped chunks).
    /// 2. High-speed directional plasma needle sparks (stretched billboards, ultra-short 0.1s lifetime).
    /// 3. Twin aerodynamic plasma ribbon trails (TrailRenderer with width taper).
    /// 4. Dynamic HDR nozzle throat point lights with engine vibration.
    /// 5. Real-time throttle and afterburner boost modulation.
    /// </summary>
    public class AAAStarshipThrusterSystem : MonoBehaviour
    {
        [System.Serializable]
        public class NozzleRig
        {
            public string nozzleName;
            public Transform nozzleTransform;
            public Transform flameMeshTransform;
            public MeshRenderer flameMeshRenderer;
            public ParticleSystem needleSparksPS;
            public TrailRenderer ribbonTrail;
            public Light nozzleLight;
            public Vector3 baseScale = new Vector3(8f, 12f, 8f);
        }

        [Header("Nozzle Rigs")]
        public List<NozzleRig> nozzles = new List<NozzleRig>();

        [Header("Materials & Assets")]
        public Material plasmaCoreMaterial;
        public Material needleSparkMaterial;
        public Material ribbonTrailMaterial;
        public UnityEngine.Mesh flameMesh;

        [Header("Colors (HDR)")]
        [ColorUsage(true, true)] public Color plasmaCruiseColor = new Color(0.1f, 2.2f, 3.5f, 1.0f);
        [ColorUsage(true, true)] public Color plasmaBoostColor = new Color(0.6f, 3.5f, 5.0f, 1.0f);
        [ColorUsage(true, true)] public Color lightCruiseColor = new Color(0f, 0.85f, 1f, 1f);
        [ColorUsage(true, true)] public Color lightBoostColor = new Color(0.4f, 0.95f, 1f, 1f);

        [Header("Dynamic Tuning")]
        [Range(0f, 2f)] public float currentThrottleFactor = 0f;
        public bool isBoosting = false;
        public float idleLength = 0.45f;
        public float cruiseLength = 1.85f;
        public float boostLength = 3.60f;

        private SpaceshipFlightController _flightController;

        private void Awake()
        {
            _flightController = GetComponent<SpaceshipFlightController>();
            LoadDefaultAssets();
            SetupNozzles();
        }

        private void OnEnable()
        {
            if (nozzles == null || nozzles.Count == 0)
            {
                SetupNozzles();
            }
        }

        public void LoadDefaultAssets()
        {
#if UNITY_EDITOR
            if (flameMesh == null)
            {
                string path = "Assets/PolygonSciFiSpace/Models/FX_Meshes/SM_Flame_Mesh_New.fbx";
                var objs = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var obj in objs)
                {
                    if (obj is UnityEngine.Mesh m && m.name.Contains("Flame"))
                    {
                        flameMesh = m;
                        break;
                    }
                }
            }

            if (plasmaCoreMaterial == null)
            {
                plasmaCoreMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/FX/FX_Addivitve_01.mat");
                if (plasmaCoreMaterial == null)
                {
                    plasmaCoreMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonSciFiSpace/Materials/FX/FX_Lines.mat");
                }
            }

            if (needleSparkMaterial == null)
            {
                needleSparkMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Circle.mat");
            }

            if (ribbonTrailMaterial == null)
            {
                ribbonTrailMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonSciFiSpace/Materials/FX/FX_Trail_Streaks.mat");
                if (ribbonTrailMaterial == null)
                {
                    ribbonTrailMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Additive_Soft.mat");
                }
            }
#endif
        }

        [ContextMenu("Build/Rebuild Thruster Rigs")]
        public void SetupNozzles()
        {
            LoadDefaultAssets();

            // Clear old legacy booster objects that dropped mesh particles
            var oldLegacyL = transform.Find("Booster_Flame_L") ?? transform.Find("Booster_L_Anchor/Booster_Flame_L");
            if (oldLegacyL != null) DestroyImmediate(oldLegacyL.gameObject);
            var oldLegacyR = transform.Find("Booster_Flame_R") ?? transform.Find("Booster_R_Anchor/Booster_Flame_R");
            if (oldLegacyR != null) DestroyImmediate(oldLegacyR.gameObject);

            // Clean existing rigs
            foreach (var n in nozzles)
            {
                if (n.flameMeshTransform != null) DestroyImmediate(n.flameMeshTransform.gameObject);
                if (n.needleSparksPS != null) DestroyImmediate(n.needleSparksPS.gameObject);
                if (n.ribbonTrail != null) DestroyImmediate(n.ribbonTrail.gameObject);
                if (n.nozzleLight != null) DestroyImmediate(n.nozzleLight.gameObject);
            }
            nozzles.Clear();

            // Find booster anchor transforms
            Transform bL = transform.Find("Booster_L_Anchor") ?? transform.Find("SM_Veh_HoverCraft_01_Booster_l");
            Transform bR = transform.Find("Booster_R_Anchor") ?? transform.Find("SM_Veh_HoverCraft_01_Booster_r");

            if (bL == null || bR == null)
            {
                // Recursive search
                var allT = GetComponentsInChildren<Transform>(true);
                foreach (var t in allT)
                {
                    string low = t.name.ToLower();
                    if (bL == null && (low.Contains("booster_l") || low.Contains("booster_left") || low.Contains("thruster_l")))
                        bL = t;
                    else if (bR == null && (low.Contains("booster_r") || low.Contains("booster_right") || low.Contains("thruster_r")))
                        bR = t;
                }
            }

            if (bL != null) nozzles.Add(CreateNozzleRig(bL, "Left_Thruster"));
            if (bR != null) nozzles.Add(CreateNozzleRig(bR, "Right_Thruster"));

            // If none found, fallback to ship rear
            if (nozzles.Count == 0)
            {
                GameObject anchorL = new GameObject("Booster_L_Anchor");
                anchorL.transform.SetParent(transform, false);
                anchorL.transform.localPosition = new Vector3(-0.85f, 0.45f, -2.2f);
                nozzles.Add(CreateNozzleRig(anchorL.transform, "Left_Thruster"));

                GameObject anchorR = new GameObject("Booster_R_Anchor");
                anchorR.transform.SetParent(transform, false);
                anchorR.transform.localPosition = new Vector3(0.85f, 0.45f, -2.2f);
                nozzles.Add(CreateNozzleRig(anchorR.transform, "Right_Thruster"));
            }
        }

        private NozzleRig CreateNozzleRig(Transform parent, string name)
        {
            var rig = new NozzleRig();
            rig.nozzleName = name;
            rig.nozzleTransform = parent;

            // 1. Attached Core Flame Mesh (SM_Flame_Mesh_New)
            GameObject flameObj = new GameObject($"AAA_FlameCore_{name}");
            flameObj.transform.SetParent(parent, false);
            flameObj.transform.localPosition = new Vector3(0f, 0f, -0.15f);
            flameObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Point -Z backwards
            flameObj.transform.localScale = new Vector3(5.5f, 10.0f, 5.5f);

            var mf = flameObj.AddComponent<MeshFilter>();
            if (flameMesh != null) mf.sharedMesh = flameMesh;
            else mf.sharedMesh = CreateFallbackJetMesh();

            var mr = flameObj.AddComponent<MeshRenderer>();
            if (plasmaCoreMaterial != null) mr.sharedMaterial = plasmaCoreMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            rig.flameMeshTransform = flameObj.transform;
            rig.flameMeshRenderer = mr;
            rig.baseScale = new Vector3(5.5f, 10.0f, 5.5f);

            // 2. High-Speed Plasma Needle Sparks (Stretched billboard, 0.1s lifetime)
            GameObject sparksObj = new GameObject($"AAA_PlasmaSparks_{name}");
            sparksObj.transform.SetParent(parent, false);
            sparksObj.transform.localPosition = new Vector3(0f, 0f, -0.35f);
            sparksObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Emit backward

            var ps = sparksObj.AddComponent<ParticleSystem>();
            var psr = sparksObj.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 2.8f;
            psr.velocityScale = 0.018f;
            if (needleSparkMaterial != null) psr.sharedMaterial = needleSparkMaterial;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // Naturally trail in the ship's wake
            main.startLifetime = 0.11f; // Ultra-short, never leaves frozen chunks
            main.startSpeed = 38f; // Blazing velocity
            main.startSize = 0.22f;
            main.startColor = new Color(0.2f, 0.95f, 1.0f, 0.9f);
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6.0f;
            shape.radius = 0.15f;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(new Color(0f, 0.85f, 1f), 0.35f),
                    new GradientColorKey(new Color(0.1f, 0.2f, 0.8f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.4f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLife.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1.0f);
            sizeCurve.AddKey(0.6f, 0.8f);
            sizeCurve.AddKey(1.0f, 0.0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            rig.needleSparksPS = ps;

            // 3. Aerodynamic Plasma Ribbon Trail (TrailRenderer)
            GameObject trailObj = new GameObject($"AAA_RibbonTrail_{name}");
            trailObj.transform.SetParent(parent, false);
            trailObj.transform.localPosition = new Vector3(0f, 0f, -0.4f);

            var tr = trailObj.AddComponent<TrailRenderer>();
            tr.time = 0.24f;
            tr.minVertexDistance = 0.15f;
            if (ribbonTrailMaterial != null) tr.sharedMaterial = ribbonTrailMaterial;

            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 0.40f);
            widthCurve.AddKey(0.4f, 0.25f);
            widthCurve.AddKey(1f, 0.0f);
            tr.widthCurve = widthCurve;

            Gradient trailGrad = new Gradient();
            trailGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0f, 0.95f, 1f), 0.0f),
                    new GradientColorKey(new Color(0.3f, 0.2f, 0.9f), 0.7f),
                    new GradientColorKey(new Color(0.1f, 0.1f, 0.4f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.85f, 0.0f),
                    new GradientAlphaKey(0.45f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            tr.colorGradient = trailGrad;
            tr.emitting = false;

            rig.ribbonTrail = tr;

            // 4. Dynamic Point Light
            GameObject lightObj = new GameObject($"AAA_GlowLight_{name}");
            lightObj.transform.SetParent(parent, false);
            lightObj.transform.localPosition = new Vector3(0f, 0f, -0.6f);

            var l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = lightCruiseColor;
            l.range = 6.0f;
            l.intensity = 1.6f;

            rig.nozzleLight = l;

            return rig;
        }

        private UnityEngine.Mesh CreateFallbackJetMesh()
        {
            // Simple procedural cone mesh along +Y
            UnityEngine.Mesh m = new UnityEngine.Mesh();
            m.name = "Procedural_Jet_Cone";
            int segments = 12;
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            Vector3 tip = new Vector3(0f, -0.25f, 0f);
            Vector3 baseCenter = Vector3.zero;
            verts.Add(tip); // 0
            verts.Add(baseCenter); // 1

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(Mathf.Cos(angle) * 0.08f, 0f, Mathf.Sin(angle) * 0.08f));
            }

            for (int i = 0; i < segments; i++)
            {
                int curr = 2 + i;
                int next = 2 + ((i + 1) % segments);
                // Tip to ring
                tris.Add(0); tris.Add(next); tris.Add(curr);
                // Base to ring
                tris.Add(1); tris.Add(curr); tris.Add(next);
            }

            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private void Update()
        {
            // Sync with flight controller state if present
            if (_flightController != null)
            {
                if (_flightController.IsPiloted)
                {
                    float throttle = Mathf.Max(0.18f, Mathf.Abs(Input.GetAxis("Vertical")));
                    isBoosting = Input.GetKey(KeyCode.LeftShift);
                    currentThrottleFactor = isBoosting ? 1.65f : throttle;
                }
                else
                {
                    // Unpiloted hover
                    currentThrottleFactor = 0.20f;
                    isBoosting = false;
                }
            }

            UpdateThrusterVisuals(currentThrottleFactor, isBoosting);
        }

        public void UpdateThrusterVisuals(float factor, bool boost)
        {
            float targetLength = Mathf.Lerp(idleLength, boost ? boostLength : cruiseLength, Mathf.Clamp01(factor));
            float targetWidth = Mathf.Lerp(0.85f, boost ? 1.35f : 1.05f, Mathf.Clamp01(factor));
            float jitter = Random.Range(-0.04f, 0.04f);

            for (int i = 0; i < nozzles.Count; i++)
            {
                var n = nozzles[i];
                if (n == null) continue;

                // 1. Scale continuous core flame
                if (n.flameMeshTransform != null)
                {
                    Vector3 s = new Vector3(
                        n.baseScale.x * targetWidth,
                        n.baseScale.y * (targetLength + jitter),
                        n.baseScale.z * targetWidth
                    );
                    n.flameMeshTransform.localScale = Vector3.Lerp(n.flameMeshTransform.localScale, s, Time.deltaTime * 24f);
                }

                // 2. Modulate high-speed needle sparks
                if (n.needleSparksPS != null)
                {
                    var em = n.needleSparksPS.emission;
                    var m = n.needleSparksPS.main;

                    if (factor > 0.12f)
                    {
                        em.enabled = true;
                        em.rateOverTime = boost ? 95f : Mathf.Lerp(30f, 65f, factor);
                        m.startSpeed = boost ? 65f : Mathf.Lerp(25f, 45f, factor);
                        m.startLifetime = boost ? 0.13f : 0.09f;
                    }
                    else
                    {
                        em.enabled = false;
                    }
                }

                // 3. Ribbon trail (active during cruise & boost)
                if (n.ribbonTrail != null)
                {
                    n.ribbonTrail.emitting = factor > 0.45f;
                }

                // 4. Glow light
                if (n.nozzleLight != null)
                {
                    n.nozzleLight.color = boost ? lightBoostColor : lightCruiseColor;
                    n.nozzleLight.intensity = Mathf.Lerp(1.2f, boost ? 5.2f : 2.8f, factor) + Random.Range(-0.1f, 0.1f);
                    n.nozzleLight.range = boost ? 11f : Mathf.Lerp(5f, 8f, factor);
                }
            }
        }
    }
}
