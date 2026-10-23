using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Master Visual Effect System for the TRON: Ares Continuous Single-Camera Awakening Oner.
    /// Manages the exact sequence of visual effects constructed right on the character's eye and body:
    /// - 10.2s - 26.0s: Macro Eye Pupil Core, spinning Ares Gimbal Rings, Geometric Retinal Mandala, and 3D Wireframe Eyeball.
    /// - 22.0s - 42.0s: Floral Rosette Iris Blossom, Almond Eyelid Arcs, Full Facial Holographic Wireframe Grid (brow, cheek, nose, jaw).
    /// - 34.0s - 54.0s: Cranial Helmet Chassis, Glowing Temple Conduits, Sagittal Crest Arch, and Coronal Helmet Arches.
    /// - 48.5s - 51.0s: Searing Eye Explosion with Laser Filaments firing directly towards the camera lens.
    /// - 50.0s - 68.0s: Molten Nanite Crystallization spreading across Clavicles, Shoulders, and Cyber Arm.
    /// </summary>
    public class AlitaTronAwakeningReconstructionVFXSystem : MonoBehaviour
    {
        [Header("Anchor Transforms")]
        public Transform eyeAnchor;
        public Transform headAnchor;

        [Header("VFX Hierarchy Roots")]
        public Transform pupilMandalaRoot;
        public Transform cyberEyeRoot;
        public Transform eyelidArcsRoot;
        public Transform irisBlossomRoot;
        public Transform facialTopologyRoot;
        public Transform cranialChassisRoot;
        public Transform templeConduitsRoot;
        public Transform eyeExplosionRoot;
        public Transform naniteCrystallizationRoot;

        [Header("Cinematic Lights")]
        public Light pupilCoreLight;
        public Light eyeKeyLight;
        public Light eyeExplosionFlashLight;

        [Header("Materials")]
        public Material whiteCoreMat;
        public Material amberMat;
        public Material redOrangeMat;
        public Material deepRedMat;
        public Material blackSlabMat;
        public Material mirrorMat;

        private Transform _retinalRingsRoot;
        private Transform _aresGimbalRoot;
        private Transform _radialRaysRoot;
        private Transform _dataCubesRoot;
        private Transform _wireframeEyeballRoot;
        private Transform _explosionShockwaveRing;

        private void Awake()
        {
            if (_nanoCubeNodes == null || _nanoCubeNodes.Count == 0)
            {
                RebuildNanoCubes();
            }
        }

        private void OnEnable()
        {
            if (_nanoCubeNodes == null || _nanoCubeNodes.Count == 0)
            {
                RebuildNanoCubes();
            }
        }

        public void InitializeVFX(Transform characterRoot, Material whiteMat, Material ambMat, Material redOrgMat, Material deepRMat, Material blkMat, Material mirMat)
        {
            whiteCoreMat = whiteMat;
            amberMat = ambMat;
            redOrangeMat = redOrgMat;
            deepRedMat = deepRMat;
            blackSlabMat = blkMat;
            mirrorMat = mirMat;

            // 1. Create Eye Anchor at right eye position
            GameObject eyeAnchorGO = new GameObject("VFX_Anchor_Right_Eye");
            eyeAnchorGO.transform.SetParent(characterRoot != null ? characterRoot : transform, false);
            // Right eye position on face mesh surface when lying on floating slab
            eyeAnchorGO.transform.position = new Vector3(0.038f, 0.865f, 0.735f);
            eyeAnchorGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            eyeAnchor = eyeAnchorGO.transform;

            // 2. Create Head Anchor aligned with world coordinates
            GameObject headAnchorGO = new GameObject("VFX_Anchor_Head");
            headAnchorGO.transform.SetParent(characterRoot != null ? characterRoot : transform, false);
            headAnchorGO.transform.position = new Vector3(0.000f, 0.840f, 0.740f);
            headAnchorGO.transform.rotation = Quaternion.identity;
            headAnchor = headAnchorGO.transform;

            BuildPupilMandalaSystem();
            BuildCyberEyeAndFacialSystem();
            BuildCranialChassisSystem();
            BuildEyeExplosionSystem();
            BuildFullBodyNanoCubeConstructionSystem();
        }

        private void BuildPupilMandalaSystem()
        {
            GameObject mRoot = new GameObject("Eye_Pupil_Mandala_VFX");
            mRoot.transform.SetParent(eyeAnchor, false);
            pupilMandalaRoot = mRoot.transform;

            // 1. Core Nucleus (White-hot singularity)
            GameObject nucleus = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nucleus.name = "Pupil_Core_Nucleus";
            nucleus.transform.SetParent(mRoot.transform, false);
            nucleus.transform.localPosition = Vector3.zero;
            nucleus.transform.localScale = Vector3.one * 0.008f;
            nucleus.GetComponent<MeshRenderer>().sharedMaterial = whiteCoreMat;
            DestroyImmediate(nucleus.GetComponent<Collider>());

            // Pupil Point Light
            GameObject pLightGO = new GameObject("Light_Pupil_Core");
            pLightGO.transform.SetParent(mRoot.transform, false);
            pLightGO.transform.localPosition = new Vector3(0f, 0f, -0.010f);
            pupilCoreLight = pLightGO.AddComponent<Light>();
            pupilCoreLight.type = LightType.Point;
            pupilCoreLight.color = new Color(1.0f, 0.65f, 0.35f);
            pupilCoreLight.intensity = 0.9f;
            pupilCoreLight.range = 0.35f;

            // 2. Ares Core Gimbal Rings (3 concentric rings spinning on different axes)
            GameObject gimbalRoot = new GameObject("Ares_Gimbal_Rings");
            gimbalRoot.transform.SetParent(mRoot.transform, false);
            _aresGimbalRoot = gimbalRoot.transform;

            float[] gimbalRadii = new float[] { 0.009f, 0.016f, 0.024f };
            for (int g = 0; g < gimbalRadii.Length; g++)
            {
                GameObject gRing = new GameObject($"GimbalRing_{g}");
                gRing.transform.SetParent(gimbalRoot.transform, false);
                gRing.transform.localRotation = Quaternion.Euler(g * 25f, g * 45f, g * 15f);

                int ticks = 24 + g * 8;
                float r = gimbalRadii[g];
                for (int t = 0; t < ticks; t++)
                {
                    float angle = (t / (float)ticks) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
                    GameObject tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tick.name = $"Tick_{t}";
                    tick.transform.SetParent(gRing.transform, false);
                    tick.transform.localPosition = pos;
                    tick.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                    tick.transform.localScale = new Vector3(0.0006f, 0.0016f, 0.0005f);
                    tick.GetComponent<MeshRenderer>().sharedMaterial = (g == 0) ? whiteCoreMat : (g == 1 ? amberMat : redOrangeMat);
                    DestroyImmediate(tick.GetComponent<Collider>());
                }
            }

            // 3. Concentric Retinal Code Rings (5 fine laser rings)
            GameObject ringsRoot = new GameObject("Retinal_Iris_Rings");
            ringsRoot.transform.SetParent(mRoot.transform, false);
            _retinalRingsRoot = ringsRoot.transform;

            float[] rRadii = new float[] { 0.014f, 0.022f, 0.032f, 0.045f, 0.060f };
            for (int r = 0; r < rRadii.Length; r++)
            {
                GameObject ringGO = new GameObject($"RetinalRing_{r}");
                ringGO.transform.SetParent(ringsRoot.transform, false);
                int segs = 24 + r * 8;
                float radius = rRadii[r];
                for (int s = 0; s < segs; s++)
                {
                    float angle = (s / (float)segs) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                    GameObject tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tick.name = $"Seg_{s}";
                    tick.transform.SetParent(ringGO.transform, false);
                    tick.transform.localPosition = pos;
                    tick.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                    tick.transform.localScale = new Vector3(0.0007f, 0.0015f, 0.0004f);
                    tick.GetComponent<MeshRenderer>().sharedMaterial = (r % 2 == 0) ? amberMat : redOrangeMat;
                    DestroyImmediate(tick.GetComponent<Collider>());
                }
            }

            // 4. Symmetrical Geometric Laser Rays (48 radial beams in cardinal tiers)
            GameObject raysRoot = new GameObject("Radial_Circuit_Rays");
            raysRoot.transform.SetParent(mRoot.transform, false);
            _radialRaysRoot = raysRoot.transform;

            int rayCount = 48;
            for (int i = 0; i < rayCount; i++)
            {
                float angle = (i / (float)rayCount) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                // Cardinal tiered lengths: 12 long rays, 12 medium rays, 24 fine rays
                float rayLen = 0.035f;
                Material rayMat = deepRedMat;
                if (i % 4 == 0) // 12 cardinal beams
                {
                    rayLen = 0.080f;
                    rayMat = amberMat;
                }
                else if (i % 2 == 0) // 12 intermediate beams
                {
                    rayLen = 0.055f;
                    rayMat = redOrangeMat;
                }

                GameObject ray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ray.name = $"Ray_{i}";
                ray.transform.SetParent(raysRoot.transform, false);
                ray.transform.localPosition = dir * (0.012f + rayLen * 0.5f);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                ray.transform.localScale = new Vector3(0.0006f, rayLen, 0.0004f);
                ray.GetComponent<MeshRenderer>().sharedMaterial = rayMat;
                DestroyImmediate(ray.GetComponent<Collider>());
            }

            // 5. 3D Wireframe Eyeball (Latitude & Longitude laser grid)
            GameObject wireEyeRoot = new GameObject("3D_Wireframe_Eyeball");
            wireEyeRoot.transform.SetParent(mRoot.transform, false);
            _wireframeEyeballRoot = wireEyeRoot.transform;

            float eyeRadius = 0.013f;
            // 6 Latitude rings
            for (int lat = -2; lat <= 2; lat++)
            {
                float latAng = (lat / 3.0f) * (Mathf.PI * 0.4f);
                float ringR = Mathf.Cos(latAng) * eyeRadius;
                float ringZ = Mathf.Sin(latAng) * eyeRadius;
                int latSegs = 20;

                for (int ls = 0; ls < latSegs; ls++)
                {
                    float a = (ls / (float)latSegs) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(a) * ringR, Mathf.Sin(a) * ringR, -ringZ);
                    GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = $"LatSeg_{lat}_{ls}";
                    seg.transform.SetParent(wireEyeRoot.transform, false);
                    seg.transform.localPosition = pos;
                    seg.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                    seg.transform.localScale = new Vector3(0.0005f, 0.0018f, 0.0004f);
                    seg.GetComponent<MeshRenderer>().sharedMaterial = (lat == 0) ? amberMat : redOrangeMat;
                    DestroyImmediate(seg.GetComponent<Collider>());
                }
            }

            // 6. Floating Holographic Data Bits
            GameObject cubesRoot = new GameObject("Floating_Data_Cubes");
            cubesRoot.transform.SetParent(mRoot.transform, false);
            _dataCubesRoot = cubesRoot.transform;

            int cubeCount = 24;
            for (int c = 0; c < cubeCount; c++)
            {
                float a = (c / (float)cubeCount) * Mathf.PI * 2f;
                float dist = 0.025f + (c % 4) * 0.012f;
                Vector3 pos = new Vector3(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist, ((c % 3) - 1) * 0.005f);

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"DataCube_{c}";
                cube.transform.SetParent(cubesRoot.transform, false);
                cube.transform.localPosition = pos;
                cube.transform.localScale = Vector3.one * 0.0014f;
                cube.GetComponent<MeshRenderer>().sharedMaterial = (c % 2 == 0) ? amberMat : redOrangeMat;
                DestroyImmediate(cube.GetComponent<Collider>());
            }
        }

        private void BuildCyberEyeAndFacialSystem()
        {
            GameObject eyeRoot = new GameObject("Cyber_Eye_And_Face_VFX");
            eyeRoot.transform.SetParent(eyeAnchor, false);
            cyberEyeRoot = eyeRoot.transform;

            // Dedicated Key Light for face
            GameObject eLightGO = new GameObject("Light_Eye_Key");
            eLightGO.transform.SetParent(eyeRoot.transform, false);
            eLightGO.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            eyeKeyLight = eLightGO.AddComponent<Light>();
            eyeKeyLight.type = LightType.Point;
            eyeKeyLight.color = new Color(1.0f, 0.65f, 0.40f);
            eyeKeyLight.intensity = 0.8f;
            eyeKeyLight.range = 0.35f;

            // 1. Floral Rosette Iris Blossom (3-tier blooming mechanical flower)
            GameObject irisRoot = new GameObject("Floral_Rosette_Iris_Blossom");
            irisRoot.transform.SetParent(eyeRoot.transform, false);
            irisBlossomRoot = irisRoot.transform;

            // Tier 1: Inner White Core Petals (12 petals)
            for (int t = 0; t < 12; t++)
            {
                float a = (t / 12f) * Mathf.PI * 2f;
                Vector3 pPos = new Vector3(Mathf.Cos(a) * 0.007f, Mathf.Sin(a) * 0.007f, -0.001f);
                GameObject pet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pet.name = $"T1Petal_{t}";
                pet.transform.SetParent(irisRoot.transform, false);
                pet.transform.localPosition = pPos;
                pet.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                pet.transform.localScale = new Vector3(0.0012f, 0.0035f, 0.0005f);
                pet.GetComponent<MeshRenderer>().sharedMaterial = whiteCoreMat;
                DestroyImmediate(pet.GetComponent<Collider>());
            }

            // Tier 2: Amber Radiant Petals (16 petals)
            for (int t = 0; t < 16; t++)
            {
                float a = (t / 16f) * Mathf.PI * 2f;
                Vector3 pPos = new Vector3(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f, -0.001f);
                GameObject pet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pet.name = $"T2Petal_{t}";
                pet.transform.SetParent(irisRoot.transform, false);
                pet.transform.localPosition = pPos;
                pet.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 8f);
                pet.transform.localScale = new Vector3(0.0015f, 0.0048f, 0.0005f);
                pet.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                DestroyImmediate(pet.GetComponent<Collider>());
            }

            // Tier 3: Crimson Coronal Petals (20 petals)
            for (int t = 0; t < 20; t++)
            {
                float a = (t / 20f) * Mathf.PI * 2f;
                Vector3 pPos = new Vector3(Mathf.Cos(a) * 0.017f, Mathf.Sin(a) * 0.017f, -0.001f);
                GameObject pet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pet.name = $"T3Petal_{t}";
                pet.transform.SetParent(irisRoot.transform, false);
                pet.transform.localPosition = pPos;
                pet.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                pet.transform.localScale = new Vector3(0.0016f, 0.0055f, 0.0005f);
                pet.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
                DestroyImmediate(pet.GetComponent<Collider>());
            }

            // 2. Almond Eyelid Arcs (defining the eye contours on face)
            GameObject lidsRoot = new GameObject("Almond_Eyelid_Arcs");
            lidsRoot.transform.SetParent(eyeRoot.transform, false);
            eyelidArcsRoot = lidsRoot.transform;

            int lidSegs = 24;
            float eyeHalfW = 0.022f;
            float upperArchH = 0.010f;
            float lowerArchH = -0.007f;

            // Upper Eyelid: 2 contoured amber laser strands
            for (int sIdx = 0; sIdx < 2; sIdx++)
            {
                float uOff = sIdx * 0.0014f;
                Material uMat = (sIdx == 0) ? amberMat : redOrangeMat;
                for (int ls = 0; ls < lidSegs; ls++)
                {
                    float t = ls / (float)(lidSegs - 1);
                    float x = Mathf.Lerp(-eyeHalfW, eyeHalfW, t);
                    float curve = Mathf.Sin(t * Mathf.PI);
                    Vector3 pos = new Vector3(x, curve * (upperArchH + uOff), -curve * 0.003f);

                    GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = $"UpperStrand_{sIdx}_{ls}";
                    seg.transform.SetParent(lidsRoot.transform, false);
                    seg.transform.localPosition = pos;
                    seg.transform.localScale = new Vector3(eyeHalfW / lidSegs * 1.4f, 0.0008f, 0.0006f);
                    seg.GetComponent<MeshRenderer>().sharedMaterial = uMat;
                    DestroyImmediate(seg.GetComponent<Collider>());
                }
            }

            // Lower Eyelid: 2 contoured crimson laser strands
            for (int sIdx = 0; sIdx < 2; sIdx++)
            {
                float lOff = -sIdx * 0.0014f;
                for (int ls = 0; ls < lidSegs; ls++)
                {
                    float t = ls / (float)(lidSegs - 1);
                    float x = Mathf.Lerp(-eyeHalfW, eyeHalfW, t);
                    float curve = Mathf.Sin(t * Mathf.PI);
                    Vector3 pos = new Vector3(x, curve * (lowerArchH + lOff), -curve * 0.003f);

                    GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = $"LowerStrand_{sIdx}_{ls}";
                    seg.transform.SetParent(lidsRoot.transform, false);
                    seg.transform.localPosition = pos;
                    seg.transform.localScale = new Vector3(eyeHalfW / lidSegs * 1.4f, 0.0008f, 0.0006f);
                    seg.GetComponent<MeshRenderer>().sharedMaterial = deepRedMat;
                    DestroyImmediate(seg.GetComponent<Collider>());
                }
            }

            // 3. Full Facial Holographic Wireframe Grid
            GameObject topoRoot = new GameObject("Facial_Holographic_Wireframe");
            topoRoot.transform.SetParent(eyeRoot.transform, false);
            facialTopologyRoot = topoRoot.transform;

            // (A) Zygomatic Cheekbone Laser Arc
            int cheekCount = 18;
            for (int c = 0; c < cheekCount; c++)
            {
                float ct = c / (float)(cheekCount - 1);
                float cx = Mathf.Lerp(-0.020f, 0.038f, ct);
                float cy = -Mathf.Sin(ct * Mathf.PI) * 0.018f - 0.012f;
                GameObject cVox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cVox.name = $"Cheek_{c}";
                cVox.transform.SetParent(topoRoot.transform, false);
                cVox.transform.localPosition = new Vector3(cx, cy, -0.002f);
                cVox.transform.localScale = new Vector3(0.0018f, 0.0008f, 0.0006f);
                cVox.GetComponent<MeshRenderer>().sharedMaterial = deepRedMat;
                DestroyImmediate(cVox.GetComponent<Collider>());
            }

            // (D) Left Eye Dormant Blueprint Contour (symmetrical balance on face)
            for (int ls = 0; ls < 16; ls++)
            {
                float t = ls / 15f;
                float x = -0.076f + Mathf.Lerp(-0.018f, 0.018f, t);
                float curve = Mathf.Sin(t * Mathf.PI);
                Vector3 pos = new Vector3(x, curve * 0.008f, -curve * 0.002f);
                GameObject lSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lSeg.name = $"LeftEyeBluePrint_{ls}";
                lSeg.transform.SetParent(topoRoot.transform, false);
                lSeg.transform.localPosition = pos;
                lSeg.transform.localScale = new Vector3(0.0020f, 0.0006f, 0.0005f);
                lSeg.GetComponent<MeshRenderer>().sharedMaterial = deepRedMat;
                DestroyImmediate(lSeg.GetComponent<Collider>());
            }
        }

        private void BuildCranialChassisSystem()
        {
            GameObject cRoot = new GameObject("Cranial_Helmet_Chassis_VFX");
            cRoot.transform.SetParent(headAnchor, false);
            cranialChassisRoot = cRoot.transform;

            // 1. Dual Glowing Temple Conduits (Right & Left sides of skull)
            GameObject condRoot = new GameObject("Temple_Conduits");
            condRoot.transform.SetParent(cRoot.transform, false);
            templeConduitsRoot = condRoot.transform;

            for (int side = -1; side <= 1; side += 2)
            {
                float xSide = side * 0.118f;
                // Upper amber temple conduit
                GameObject upCond = GameObject.CreatePrimitive(PrimitiveType.Cube);
                upCond.name = $"TempleConduit_Up_{side}";
                upCond.transform.SetParent(condRoot.transform, false);
                upCond.transform.localPosition = new Vector3(xSide, 0.010f, 0.020f);
                upCond.transform.localScale = new Vector3(0.0025f, 0.0025f, 0.18f);
                upCond.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                DestroyImmediate(upCond.GetComponent<Collider>());

                // Lower red-orange temple conduit
                GameObject lowCond = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lowCond.name = $"TempleConduit_Low_{side}";
                lowCond.transform.SetParent(condRoot.transform, false);
                lowCond.transform.localPosition = new Vector3(xSide, -0.015f, 0.020f);
                lowCond.transform.localScale = new Vector3(0.0025f, 0.0025f, 0.18f);
                lowCond.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
                DestroyImmediate(lowCond.GetComponent<Collider>());

                // Neural connector ribs branching along temple
                for (int b = 0; b < 6; b++)
                {
                    float bz = -0.05f + b * 0.025f;
                    GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    branch.name = $"TempleRib_{side}_{b}";
                    branch.transform.SetParent(condRoot.transform, false);
                    branch.transform.localPosition = new Vector3(xSide, -0.002f, bz);
                    branch.transform.localScale = new Vector3(0.002f, 0.025f, 0.002f);
                    branch.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                    DestroyImmediate(branch.GetComponent<Collider>());
                }
            }

            // 2. Sagittal Crest Arch (Forehead over cranial dome to occiput)
            // Head center is at Y=0.94, crown at Y=1.04 (+0.10m local Y)
            int sagCount = 28;
            for (int s = 0; s < sagCount; s++)
            {
                float t = s / (float)(sagCount - 1);
                float z = Mathf.Lerp(-0.07f, 0.13f, t);
                // Parabolic arch curving up over crown
                float curve = Mathf.Sin(t * Mathf.PI);
                float y = Mathf.Lerp(0.07f, 0.02f, t) + curve * 0.040f;

                GameObject sagSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sagSeg.name = $"SagittalSeg_{s}";
                sagSeg.transform.SetParent(cRoot.transform, false);
                sagSeg.transform.localPosition = new Vector3(0f, y, z);
                sagSeg.transform.localScale = new Vector3(0.0045f, 0.0025f, 0.007f);
                sagSeg.GetComponent<MeshRenderer>().sharedMaterial = (s % 3 == 0) ? whiteCoreMat : (s % 2 == 0 ? amberMat : redOrangeMat);
                DestroyImmediate(sagSeg.GetComponent<Collider>());
            }

            // 3. Coronal Helmet Wireframe Ribs (wrapping ear-to-ear over crown)
            float[] coronalZ = new float[] { -0.02f, 0.03f, 0.08f };
            for (int ci = 0; ci < coronalZ.Length; ci++)
            {
                float cz = coronalZ[ci];
                int corCount = 24;
                for (int cs = 0; cs < corCount; cs++)
                {
                    float ct = cs / (float)(corCount - 1);
                    float cx = Mathf.Lerp(-0.118f, 0.118f, ct);
                    float cy = Mathf.Sin(ct * Mathf.PI) * 0.105f;

                    GameObject cRib = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cRib.name = $"CoronalRib_{ci}_{cs}";
                    cRib.transform.SetParent(cRoot.transform, false);
                    cRib.transform.localPosition = new Vector3(cx, cy, cz);
                    cRib.transform.localScale = new Vector3(0.008f, 0.0020f, 0.0020f);
                    cRib.GetComponent<MeshRenderer>().sharedMaterial = (ci == 0) ? amberMat : redOrangeMat;
                    DestroyImmediate(cRib.GetComponent<Collider>());
                }
            }
        }

        private void BuildEyeExplosionSystem()
        {
            GameObject expRoot = new GameObject("Eye_Explosion_Lines_VFX");
            expRoot.transform.SetParent(eyeAnchor, false);
            eyeExplosionRoot = expRoot.transform;

            // Explosion Searing Flash Light
            GameObject fLightGO = new GameObject("Light_Eye_Explosion_Flash");
            fLightGO.transform.SetParent(expRoot.transform, false);
            fLightGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            eyeExplosionFlashLight = fLightGO.AddComponent<Light>();
            eyeExplosionFlashLight.type = LightType.Point;
            eyeExplosionFlashLight.color = new Color(1.0f, 0.40f, 0.20f);
            eyeExplosionFlashLight.intensity = 0f;
            eyeExplosionFlashLight.range = 3.5f;

            // Expanding Shockwave Ring (Planar ring expanding across the face)
            GameObject ringGO = new GameObject("Explosion_Shockwave_Ring");
            ringGO.transform.SetParent(expRoot.transform, false);
            _explosionShockwaveRing = ringGO.transform;
            int ringSegs = 32;
            for (int s = 0; s < ringSegs; s++)
            {
                float ang = (s / (float)ringSegs) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                GameObject rSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rSeg.name = $"ShockSeg_{s}";
                rSeg.transform.SetParent(ringGO.transform, false);
                rSeg.transform.localPosition = pos;
                rSeg.transform.localRotation = Quaternion.Euler(0f, 0f, -ang * Mathf.Rad2Deg);
                rSeg.transform.localScale = new Vector3(0.012f, 0.003f, 0.002f);
                rSeg.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                DestroyImmediate(rSeg.GetComponent<Collider>());
            }

            // 48 Laser Filaments firing directly towards the camera lens
            // Vector from eye to camera at 49.5s in eyeAnchor local space is roughly:
            // World dir: (0.58, 0.81, 0.01). EyeAnchor rot = (90, 0, 0).
            // In eye local space: local X = world X = +0.58. local Y = world -Z = -0.01. local Z = world -Y = -0.81.
            Vector3 camLocalDir = new Vector3(0.55f, 0.0f, -0.82f).normalized;

            int expCount = 48;
            for (int e = 0; e < expCount; e++)
            {
                float ang = (e / (float)expCount) * Mathf.PI * 2f;
                // Conical spread of 35 degrees around camera direction
                Vector3 perp1 = Vector3.Cross(camLocalDir, Vector3.up).normalized;
                Vector3 perp2 = Vector3.Cross(camLocalDir, perp1).normalized;
                float spread = Random.Range(0.08f, 0.38f);
                Vector3 dir = (camLocalDir + (Mathf.Cos(ang) * perp1 + Mathf.Sin(ang) * perp2) * spread).normalized;

                float len = Random.Range(0.08f, 0.26f);

                GameObject expLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                expLine.name = $"ExpLine_{e}";
                expLine.transform.SetParent(expRoot.transform, false);
                expLine.transform.localPosition = dir * (len * 0.5f);
                expLine.transform.localRotation = Quaternion.LookRotation(dir);
                expLine.transform.localScale = new Vector3(0.0010f, 0.0010f, len);
                expLine.GetComponent<MeshRenderer>().sharedMaterial = (e % 3 == 0) ? whiteCoreMat : (e % 2 == 0 ? amberMat : redOrangeMat);
                DestroyImmediate(expLine.GetComponent<Collider>());
            }

            expRoot.SetActive(false);
        }

        [System.Serializable]
        public struct NanoCubeNode
        {
            public Transform transform;
            public Vector3 targetLocalPos;
            public Vector3 initialOffset;
            public Quaternion targetRotation;
            public float bodyHeightRatio; // 0.0 (boots) to 1.0 (head)
            public float baseScale;
        }

        public Transform fullBodyNanoCubesRoot;
        public Light nanoScanlineLight;

        [SerializeField]
        private System.Collections.Generic.List<NanoCubeNode> _nanoCubeNodes = new System.Collections.Generic.List<NanoCubeNode>();

        public void RebuildNanoCubes()
        {
            if (fullBodyNanoCubesRoot != null)
            {
                // Destroy existing children to prevent duplicates
                for (int i = fullBodyNanoCubesRoot.childCount - 1; i >= 0; i--)
                {
                    var child = fullBodyNanoCubesRoot.GetChild(i);
                    if (child.name != "Light_Nano_Laser_Scanline")
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
            else
            {
                GameObject nRoot = new GameObject("Full_Body_Nano_Cubes_VFX");
                nRoot.transform.SetParent(this.transform, false);
                fullBodyNanoCubesRoot = nRoot.transform;
            }

            _nanoCubeNodes.Clear();

            // Moving Laser Scanline Light
            Transform sLightTr = fullBodyNanoCubesRoot.Find("Light_Nano_Laser_Scanline");
            if (sLightTr == null)
            {
                GameObject sLightGO = new GameObject("Light_Nano_Laser_Scanline");
                sLightGO.transform.SetParent(fullBodyNanoCubesRoot, false);
                sLightGO.transform.localPosition = new Vector3(0f, 0.95f, 0.86f);
                nanoScanlineLight = sLightGO.AddComponent<Light>();
                nanoScanlineLight.type = LightType.Point;
                nanoScanlineLight.color = new Color(1.0f, 0.65f, 0.25f);
                nanoScanlineLight.intensity = 1.5f;
                nanoScanlineLight.range = 1.2f;
            }
            else
            {
                nanoScanlineLight = sLightTr.GetComponent<Light>();
            }

            // Generate ~260 nano cubes placed with millimeter accuracy along Alita's body lying on slab
            // Strictly ordered from TOP (Head/Cranium = 1.0) to BOTTOM (Combat Boots = 0.0):
            // 1. Cranium & Top of Skull (Z: 0.82 - 0.94)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0f, 0.76f, 0.86f), new Vector3(0.09f, 0.04f, 0.08f), 28, 0.88f, 1.00f, 0.005f, 0.010f);

            // 2. Face, Forehead & Jaw (Z: 0.68 - 0.80)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0f, 0.77f, 0.74f), new Vector3(0.08f, 0.04f, 0.06f), 24, 0.80f, 0.89f, 0.004f, 0.008f);

            // 3. Neck & Cervical Spine (Z: 0.58 - 0.68)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0f, 0.74f, 0.63f), new Vector3(0.06f, 0.03f, 0.05f), 16, 0.74f, 0.82f, 0.004f, 0.008f);

            // 4. Clavicles & Pectorals (Z: 0.40 - 0.55)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0f, 0.75f, 0.47f), new Vector3(0.14f, 0.04f, 0.08f), 28, 0.65f, 0.76f, 0.006f, 0.012f);

            // 5. Left & Right Shoulders / Deltoids
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(-0.18f, 0.72f, 0.52f), new Vector3(0.05f, 0.04f, 0.06f), 12, 0.65f, 0.75f, 0.006f, 0.012f);
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0.18f, 0.72f, 0.52f), new Vector3(0.05f, 0.04f, 0.06f), 12, 0.65f, 0.75f, 0.006f, 0.012f);

            // 6. Left & Right Upper Arms
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(-0.20f, 0.71f, 0.38f), new Vector3(0.04f, 0.03f, 0.07f), 12, 0.55f, 0.67f, 0.005f, 0.010f);
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0.20f, 0.71f, 0.38f), new Vector3(0.04f, 0.03f, 0.07f), 12, 0.55f, 0.67f, 0.005f, 0.010f);

            // 7. Left & Right Forearms & Wrists
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(-0.24f, 0.71f, 0.20f), new Vector3(0.04f, 0.03f, 0.07f), 12, 0.48f, 0.58f, 0.005f, 0.010f);
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0.24f, 0.71f, 0.20f), new Vector3(0.04f, 0.03f, 0.07f), 12, 0.48f, 0.58f, 0.005f, 0.010f);

            // 8. Hands & Flexing Cyber Fingers
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(-0.27f, 0.72f, 0.03f), new Vector3(0.04f, 0.03f, 0.05f), 10, 0.42f, 0.50f, 0.004f, 0.008f);
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0.27f, 0.72f, 0.03f), new Vector3(0.04f, 0.03f, 0.05f), 10, 0.42f, 0.50f, 0.004f, 0.008f);

            // 9. Ribcage, Spine & Core (Z: 0.18 - 0.35)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0f, 0.74f, 0.26f), new Vector3(0.14f, 0.04f, 0.08f), 28, 0.52f, 0.65f, 0.006f, 0.012f);

            // 10. Pelvis & Hips (Z: -0.05 - 0.12)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0f, 0.73f, 0.04f), new Vector3(0.14f, 0.04f, 0.07f), 22, 0.38f, 0.52f, 0.006f, 0.012f);

            // 11. Left & Right Thighs (Z: -0.32 - -0.08)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(-0.10f, 0.73f, -0.20f), new Vector3(0.05f, 0.04f, 0.12f), 16, 0.24f, 0.40f, 0.006f, 0.012f);
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0.10f, 0.73f, -0.20f), new Vector3(0.05f, 0.04f, 0.12f), 16, 0.24f, 0.40f, 0.006f, 0.012f);

            // 12. Left & Right Knees & Calves (Z: -0.58 - -0.34)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(-0.10f, 0.72f, -0.46f), new Vector3(0.05f, 0.04f, 0.11f), 14, 0.10f, 0.26f, 0.005f, 0.010f);
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0.10f, 0.72f, -0.46f), new Vector3(0.05f, 0.04f, 0.11f), 14, 0.10f, 0.26f, 0.005f, 0.010f);

            // 13. Left & Right Combat Boots & Soles (Z: -0.80 - -0.62)
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(-0.10f, 0.72f, -0.71f), new Vector3(0.05f, 0.04f, 0.07f), 12, 0.00f, 0.12f, 0.005f, 0.010f);
            AddCubeSegment(fullBodyNanoCubesRoot, new Vector3(0.10f, 0.72f, -0.71f), new Vector3(0.05f, 0.04f, 0.07f), 12, 0.00f, 0.12f, 0.005f, 0.010f);

            fullBodyNanoCubesRoot.gameObject.SetActive(false);
        }

        private void BuildFullBodyNanoCubeConstructionSystem()
        {
            RebuildNanoCubes();
        }

        private void AddCubeSegment(Transform parent, Vector3 center, Vector3 extents, int count, float minHeightRatio, float maxHeightRatio, float scaleMin, float scaleMax)
        {
            for (int i = 0; i < count; i++)
            {
                float rx = Random.Range(-extents.x, extents.x);
                float ry = Random.Range(-extents.y, extents.y);
                float rz = Random.Range(-extents.z, extents.z);
                Vector3 targetPos = center + new Vector3(rx, ry, rz);

                // Subtle quantum floating initial offset (1-4cm above anatomy)
                Vector3 offset = new Vector3(
                    Random.Range(-0.025f, 0.025f),
                    Random.Range(0.010f, 0.040f),
                    Random.Range(-0.025f, 0.025f)
                );

                float tZ = (rz + extents.z) / (extents.z * 2.0f + 0.0001f);
                float hRatio = Mathf.Lerp(minHeightRatio, maxHeightRatio, tZ);

                GameObject cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeGO.name = $"NanoCube_{_nanoCubeNodes.Count}";
                cubeGO.transform.SetParent(parent, false);
                cubeGO.transform.localPosition = targetPos;
                cubeGO.transform.localRotation = Random.rotation;

                float bScale = Random.Range(scaleMin, scaleMax);
                cubeGO.transform.localScale = Vector3.one * bScale;

                // Color tiering: white core, amber, red-orange, deep red
                Material mat = (i % 4 == 0) ? whiteCoreMat : (i % 3 == 0 ? amberMat : (i % 2 == 0 ? redOrangeMat : deepRedMat));
                cubeGO.GetComponent<MeshRenderer>().sharedMaterial = mat;
                DestroyImmediate(cubeGO.GetComponent<Collider>());

                NanoCubeNode node = new NanoCubeNode
                {
                    transform = cubeGO.transform,
                    targetLocalPos = targetPos,
                    initialOffset = offset,
                    targetRotation = Random.rotation,
                    bodyHeightRatio = hRatio,
                    baseScale = bScale
                };
                _nanoCubeNodes.Add(node);
            }
        }

        /// <summary>
        /// Evaluates all reconstruction VFX at cutscene time.
        /// </summary>
        public void EvaluateAtTime(float time)
        {
            // 1. Pupil Mandala System (10.2s - 26.0s)
            bool isPupilActive = (time >= 10.2f && time < 26.0f);
            if (pupilMandalaRoot != null)
            {
                pupilMandalaRoot.gameObject.SetActive(isPupilActive);
                if (isPupilActive)
                {
                    float pulse = 1.0f + Mathf.Sin(time * 6f) * 0.04f;
                    pupilMandalaRoot.localScale = Vector3.one * pulse;

                    if (_aresGimbalRoot != null)
                        _aresGimbalRoot.localRotation = Quaternion.Euler((time - 10.2f) * 22f, (time - 10.2f) * 35f, (time - 10.2f) * 16f);

                    if (_retinalRingsRoot != null)
                        _retinalRingsRoot.localRotation = Quaternion.Euler(0f, 0f, (time - 10.2f) * -20f);

                    if (_radialRaysRoot != null)
                        _radialRaysRoot.localRotation = Quaternion.Euler(0f, 0f, (time - 10.2f) * 12f);

                    if (_wireframeEyeballRoot != null)
                        _wireframeEyeballRoot.localRotation = Quaternion.Euler((time - 10.2f) * 15f, (time - 10.2f) * 25f, 0f);

                    if (_dataCubesRoot != null)
                        _dataCubesRoot.localRotation = Quaternion.Euler((time - 10.2f) * 8f, (time - 10.2f) * 12f, 0f);

                    if (pupilCoreLight != null)
                    {
                        pupilCoreLight.intensity = 0.9f + Mathf.Sin(time * 6f) * 0.15f;
                    }
                }
            }

            // 2. Cyber Eye & Facial Holographic System (22.0s - 44.0s)
            bool isFaceActive = (time >= 22.0f && time < 44.0f);
            if (cyberEyeRoot != null)
            {
                cyberEyeRoot.gameObject.SetActive(isFaceActive);
                if (isFaceActive)
                {
                    float tFace = Mathf.Clamp01((time - 22.0f) / 18.0f);
                    float faceScale = Mathf.Lerp(0.90f, 1.0f, Mathf.SmoothStep(0f, 1f, tFace));
                    cyberEyeRoot.localScale = Vector3.one * faceScale;

                    if (irisBlossomRoot != null)
                        irisBlossomRoot.localRotation = Quaternion.Euler(0f, 0f, (time - 22.0f) * -8f);

                    if (eyeKeyLight != null)
                    {
                        eyeKeyLight.intensity = 0.8f + Mathf.Sin(time * 5f) * 0.15f;
                    }
                }
            }

            // 3. Cranial Chassis & Temple Conduits (34.0s - 46.0s: Early head compilation)
            bool isCranialActive = (time >= 34.0f && time < 46.0f);
            if (cranialChassisRoot != null)
            {
                cranialChassisRoot.gameObject.SetActive(isCranialActive);
                if (isCranialActive)
                {
                    float tCran = Mathf.Clamp01((time - 34.0f) / 12.0f);
                    float cranScale = Mathf.Lerp(0.92f, 1.0f, Mathf.SmoothStep(0f, 1f, tCran));
                    cranialChassisRoot.localScale = Vector3.one * cranScale;
                }
            }

            // 4. Searing Eye Detonation Transition Flash (48.0s - 49.2s)
            bool isExploding = (time >= 48.0f && time < 49.2f);
            if (eyeExplosionRoot != null)
            {
                eyeExplosionRoot.gameObject.SetActive(isExploding);
                if (isExploding)
                {
                    float tExp = Mathf.Clamp01((time - 48.0f) / 1.2f);
                    float expScale = Mathf.Lerp(0.2f, 1.8f, tExp);
                    eyeExplosionRoot.localScale = Vector3.one * expScale;

                    if (_explosionShockwaveRing != null)
                    {
                        float rScale = Mathf.Lerp(0.02f, 0.35f, Mathf.Pow(tExp, 0.5f));
                        _explosionShockwaveRing.localScale = new Vector3(rScale, rScale, 1f);
                    }

                    if (eyeExplosionFlashLight != null)
                    {
                        eyeExplosionFlashLight.intensity = (1f - tExp) * 35.0f;
                    }
                }
            }

            // 5. Full-Body Nano-Cube Assembly Construction System (10.2s - 72.5s)
            // STRICT TOP-TO-BOTTOM CONSTRUCTION:
            // Sweeps from Head (Top, h=1.0, Z=+0.86m) down to Combat Boots (Bottom, h=0.0, Z=-0.76m)
            bool isNanoConstructionActive = (time >= 10.2f && time < 72.5f);
            if (fullBodyNanoCubesRoot != null)
            {
                fullBodyNanoCubesRoot.gameObject.SetActive(isNanoConstructionActive);
                if (isNanoConstructionActive)
                {
                    if (_nanoCubeNodes == null || _nanoCubeNodes.Count == 0)
                    {
                        RebuildNanoCubes();
                    }

                    // Global compilation progress: 0.0 at top of head (10.2s), 1.0 at soles of boots (72.5s)
                    float progress = Mathf.Clamp01((time - 10.2f) / 62.3f);
                    // Scan threshold sweeps strictly from 1.0 (Head) down to 0.0 (Boots)
                    float scanThreshold = Mathf.Lerp(1.0f, 0.0f, progress);

                    // Moving laser scanline light along the slab from +Z (Head) to -Z (Boots)
                    if (nanoScanlineLight != null)
                    {
                        float currentZ = Mathf.Lerp(0.86f, -0.76f, progress);
                        nanoScanlineLight.transform.localPosition = new Vector3(0f, 0.95f, currentZ);
                        nanoScanlineLight.intensity = 1.5f + Mathf.Sin(time * 8f) * 0.3f;
                    }

                    // Animate every single nano cube in her body strictly from TOP TO BOTTOM
                    for (int k = 0; k < _nanoCubeNodes.Count; k++)
                    {
                        NanoCubeNode node = _nanoCubeNodes[k];
                        if (node.transform == null) continue;

                        float h = node.bodyHeightRatio;
                        // delta = h - scanThreshold
                        // Since scanline moves from 1.0 -> 0.0:
                        // delta > 0 means h > scanThreshold (Scanline has ALREADY passed this point!)
                        // delta == 0 means h == scanThreshold (Scanline is RIGHT AT this point!)
                        // delta < 0 means h < scanThreshold (Scanline has NOT reached this point yet!)
                        float delta = h - scanThreshold;

                        if (delta > 0.04f)
                        {
                            // CONSTRUCTED REGION (Above scanline, across upper body & completed anatomy):
                            // Active glowing crystalline nano cubes forming the anatomical matrix across upper body!
                            node.transform.gameObject.SetActive(true);
                            node.transform.localPosition = node.targetLocalPos;
                            node.transform.localRotation = node.targetRotation;
                            float upperPulse = node.baseScale * (0.90f + 0.15f * Mathf.Sin(time * 8f + k * 1.7f));
                            node.transform.localScale = Vector3.one * upperPulse;
                        }
                        else if (Mathf.Abs(delta) <= 0.04f)
                        {
                            // AT THE SCANLINE:
                            // Searing incandescent quantum crystallization and locking into anatomical position!
                            node.transform.gameObject.SetActive(true);
                            float heat = 1.0f - (Mathf.Abs(delta) / 0.04f);
                            node.transform.localPosition = node.targetLocalPos;
                            float pulse = node.baseScale * (1.18f + heat * 0.55f + Mathf.Sin(time * 26f + k * 3f) * 0.16f);
                            node.transform.localScale = Vector3.one * pulse;
                            node.transform.localRotation = node.targetRotation;
                        }
                        else if (delta >= -0.20f)
                        {
                            // AHEAD OF SCANLINE (Below, towards Boots):
                            // Scanline is approaching from above! Floating quantum cubes swarm and converge into anatomy!
                            node.transform.gameObject.SetActive(true);
                            float tAssemble = (delta - (-0.20f)) / 0.16f; // 0 to 1
                            Vector3 curPos = Vector3.Lerp(node.targetLocalPos + node.initialOffset, node.targetLocalPos, tAssemble);
                            node.transform.localPosition = curPos;
                            node.transform.localScale = Vector3.one * Mathf.Lerp(0.001f, node.baseScale, tAssemble);
                            node.transform.localRotation = Quaternion.Euler(time * 50f + k * 18f, time * 65f + k * 22f, 0f);
                        }
                        else
                        {
                            // FAR BELOW SCANLINE (Waiting):
                            // Scanline has not reached this anatomy yet. Dormant in digital void.
                            node.transform.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }
    }
}

