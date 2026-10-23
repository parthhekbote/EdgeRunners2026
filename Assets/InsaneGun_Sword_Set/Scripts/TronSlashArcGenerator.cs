using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InsaneGun.Combat
{
    /// <summary>
    /// AAA Tron Light Cycle Inspired Procedural Sword Slash Engine.
    /// 
    /// Architecture:
    /// 1. Procedural 3D Polar Coordinate Fan Mesh (Tron Light Wall Ribbon).
    ///    - Dual luminous border rails (outer blade-tip rail & inner hilt rail).
    ///    - Incandescent white-hot laser core filament at the leading cutting edge.
    ///    - Embedded holographic digital scanlines across the ribbon face.
    ///    - Digital de-rezzing dissolve threshold from trailing tail to leading edge.
    /// 2. De-Rez Digital Data Voxels:
    ///    - Floating 3D holographic cubes tumbling along the blade slipstream.
    /// 3. High-Velocity Directional Laser Needle Sparks:
    ///    - Stretched needle beams spraying along the cutting plane.
    /// 4. Planar Expanding Tron Floor Shockwave Ring:
    ///    - Concentric digital grid rings expanding outward on heavy hits and finishers.
    /// 5. Dynamic Real-Time Tron Point Light Flash:
    ///    - Real-time cybernetic illumination with quadratic falloff.
    /// </summary>
    public static class TronSlashArcGenerator
    {
        private static Material _tronArcMaterialPlayer;
        private static Material _tronArcMaterialEnemy;
        private static Material _tronArcMaterialFinisher;
        private static Material _needleSparkMaterial;

        private static void EnsureMaterials()
        {
            if (_tronArcMaterialPlayer == null)
            {
                Shader arcShader = Shader.Find("Cyberpunk/Tron_Slash_Arc") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                _tronArcMaterialPlayer = new Material(arcShader) { name = "M_TronSlashArc_Player" };
                _tronArcMaterialPlayer.SetColor("_MainColor", new Color(0.0f, 1.6f, 2.4f, 1.0f));
                _tronArcMaterialPlayer.SetColor("_CoreColor", new Color(1.5f, 1.5f, 1.5f, 1.0f));
                _tronArcMaterialPlayer.SetFloat("_TopRailWidth", 0.08f);
                _tronArcMaterialPlayer.SetFloat("_BottomRailWidth", 0.06f);
                _tronArcMaterialPlayer.SetFloat("_RailIntensity", 1.8f);
                _tronArcMaterialPlayer.SetFloat("_GridFrequency", 18.0f);
                _tronArcMaterialPlayer.SetFloat("_GridIntensity", 0.70f);
                _tronArcMaterialPlayer.SetFloat("_LeadingEdgeBoost", 1.8f);

                _tronArcMaterialEnemy = new Material(arcShader) { name = "M_TronSlashArc_Enemy" };
                _tronArcMaterialEnemy.SetColor("_MainColor", new Color(2.4f, 0.85f, 0.1f, 1.0f));
                _tronArcMaterialEnemy.SetColor("_CoreColor", new Color(1.5f, 1.5f, 1.5f, 1.0f));
                _tronArcMaterialEnemy.SetFloat("_TopRailWidth", 0.08f);
                _tronArcMaterialEnemy.SetFloat("_BottomRailWidth", 0.06f);
                _tronArcMaterialEnemy.SetFloat("_RailIntensity", 1.8f);
                _tronArcMaterialEnemy.SetFloat("_GridFrequency", 18.0f);
                _tronArcMaterialEnemy.SetFloat("_GridIntensity", 0.70f);
                _tronArcMaterialEnemy.SetFloat("_LeadingEdgeBoost", 1.8f);

                _tronArcMaterialFinisher = new Material(arcShader) { name = "M_TronSlashArc_Finisher" };
                _tronArcMaterialFinisher.SetColor("_MainColor", new Color(2.4f, 0.35f, 1.8f, 1.0f));
                _tronArcMaterialFinisher.SetColor("_CoreColor", new Color(1.8f, 1.8f, 1.8f, 1.0f));
                _tronArcMaterialFinisher.SetFloat("_TopRailWidth", 0.10f);
                _tronArcMaterialFinisher.SetFloat("_BottomRailWidth", 0.08f);
                _tronArcMaterialFinisher.SetFloat("_RailIntensity", 2.2f);
                _tronArcMaterialFinisher.SetFloat("_GridFrequency", 22.0f);
                _tronArcMaterialFinisher.SetFloat("_GridIntensity", 0.75f);
                _tronArcMaterialFinisher.SetFloat("_LeadingEdgeBoost", 2.4f);
            }

            if (_needleSparkMaterial == null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                _needleSparkMaterial = new Material(unlit) { name = "M_TronNeedleSparks" };
                _needleSparkMaterial.SetFloat("_Surface", 1);
                _needleSparkMaterial.SetFloat("_Blend", 1);
                _needleSparkMaterial.SetColor("_BaseColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));
            }
        }

        /// <summary>
        /// Spawns a high-fidelity Tron Bike Trail inspired 3D slash arc.
        /// </summary>
        /// <summary>
        /// Spawns a high-fidelity Tron Bike Trail inspired 3D slash arc.
        /// Supports custom scaleMultiplier, heightOffset, and pitchDownAngle for titan mechs or anti-infantry combat.
        /// </summary>
        public static void SpawnTronSlash(Transform attacker, int comboIndex, bool isFinisher, float scaleMultiplier = 1.0f, float heightOffset = 0.0f, float pitchDownAngle = 0.0f)
        {
            if (attacker == null) return;
            EnsureMaterials();

            bool isPlayer = attacker.CompareTag("Player") || attacker.name.Contains("Hero") || attacker.name.Contains("Alita");

            // Pick Material & Color
            Material baseMat = isFinisher ? _tronArcMaterialFinisher : (isPlayer ? _tronArcMaterialPlayer : _tronArcMaterialEnemy);
            Color themeColor = isFinisher 
                ? new Color(4.8f, 0.5f, 3.8f) 
                : (isPlayer ? new Color(0.0f, 3.2f, 4.8f) : new Color(4.5f, 1.3f, 0.15f));

            // Radius, sweep angle, and orientation per combo choreography
            float innerRadius = 0.55f * scaleMultiplier;
            float outerRadius = 2.05f * scaleMultiplier;
            float sweepAngle = 145f;
            float duration = 0.16f;

            Vector3 origin = attacker.position + Vector3.up * (1.10f + heightOffset) + attacker.forward * (0.40f * scaleMultiplier);
            Quaternion basePitch = (pitchDownAngle != 0f) ? Quaternion.Euler(pitchDownAngle, 0f, 0f) : Quaternion.identity;
            Quaternion orientation = attacker.rotation * basePitch;

            switch (comboIndex)
            {
                case 1: // Right-to-Left horizontal slice
                    orientation = attacker.rotation * Quaternion.Euler(0f, -10f, 10f);
                    sweepAngle = 140f;
                    duration = 0.15f;
                    break;
                case 2: // Backhand rising diagonal
                    orientation = attacker.rotation * Quaternion.Euler(-15f, 25f, -35f);
                    sweepAngle = 145f;
                    duration = 0.15f;
                    break;
                case 3: // High rising uppercut diagonal
                    orientation = attacker.rotation * Quaternion.Euler(-30f, 0f, 60f);
                    outerRadius = 2.25f * scaleMultiplier;
                    sweepAngle = 155f;
                    duration = 0.17f;
                    break;
                case 4: // Focused forward piercing thrust
                    orientation = attacker.rotation * Quaternion.Euler(0f, 0f, 45f);
                    origin += attacker.forward * (0.45f * scaleMultiplier);
                    innerRadius = 0.20f * scaleMultiplier;
                    outerRadius = 2.45f * scaleMultiplier;
                    sweepAngle = 60f; // Narrow concentrated laser tunnel
                    duration = 0.15f;
                    break;
                case 5: // Overhead vertical cleave
                    orientation = attacker.rotation * Quaternion.Euler(90f, 0f, 0f);
                    origin += Vector3.up * (0.25f * scaleMultiplier);
                    outerRadius = 2.30f * scaleMultiplier;
                    sweepAngle = 160f;
                    duration = 0.18f;
                    break;
                case 6: // 360-degree Whirlwind Light Cycle Wall
                    orientation = attacker.rotation * Quaternion.Euler(0f, 180f, 0f);
                    outerRadius = 2.45f * scaleMultiplier;
                    sweepAngle = 360f; // Full perimeter circular wall!
                    duration = 0.22f;
                    break;
                case 7: // Finisher: Colossal Tron Chromatic Rupture
                default:
                    orientation = attacker.rotation * Quaternion.Euler(0f, 15f, 75f);
                    origin += attacker.forward * (0.35f * scaleMultiplier) + Vector3.up * (0.15f * scaleMultiplier);
                    outerRadius = 2.85f * scaleMultiplier;
                    innerRadius = 0.65f * scaleMultiplier;
                    sweepAngle = 180f;
                    duration = 0.24f;
                    break;
            }

            // 1. Build and instantiate 3D Polar Fan Mesh
            GameObject arcObj = new GameObject($"Tron_LightWall_Arc_{comboIndex}");
            arcObj.transform.position = origin;
            arcObj.transform.rotation = orientation;

            Mesh arcMesh = CreatePolarFanMesh(innerRadius, outerRadius, sweepAngle, segments: sweepAngle > 200f ? 48 : 36);
            var mf = arcObj.AddComponent<MeshFilter>();
            mf.sharedMesh = arcMesh;

            var mr = arcObj.AddComponent<MeshRenderer>();
            Material instancedMat = new Material(baseMat);
            mr.material = instancedMat;

            // Drive animation (scale expansion, de-rez dissolve, cleanup)
            var animator = arcObj.AddComponent<TronArcMeshAnimator>();
            animator.Initialize(instancedMat, duration, isFinisher ? 1.15f : 1.08f);

            // 2. Spawn High-Velocity Directional Needle Sparks (clean AAA laser sparks, zero blocky/mesh voxels)
            Vector3 cutDirection = orientation * Vector3.forward;
            SpawnNeedleSparks(origin + attacker.forward * (1.1f * scaleMultiplier), cutDirection, themeColor, isFinisher ? 28 : 14);

            // 4. Expanding Floor Shockwave Grid Ring (for Finishers, Overhead, and Whirlwind)
            if (isFinisher || comboIndex == 6 || comboIndex == 5)
            {
                SpawnFloorGridRing(attacker.position, themeColor, (isFinisher ? 2.8f : 2.0f) * scaleMultiplier);
            }

            // 5. Dynamic Tron Point Light Flash (subtle, clean cyber glow)
            SpawnPointLightFlash(origin, themeColor, (isFinisher ? 3.2f : 2.0f) * scaleMultiplier, isFinisher ? 0.85f : 0.45f, 0.05f);
        }

        /// <summary>
        /// Generates a smooth 3D polar fan ribbon mesh for the Tron slash wall.
        /// </summary>
        private static Mesh CreatePolarFanMesh(float innerRadius, float outerRadius, float sweepAngle, int segments)
        {
            Mesh mesh = new Mesh { name = "TronSlashArc_Mesh" };

            int vertCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Color[] colors = new Color[vertCount];
            int[] triangles = new int[segments * 6];

            float halfSweep = sweepAngle * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                // Angle sweeping from trailing start (-halfSweep) to leading blade edge (+halfSweep)
                float angleDeg = Mathf.Lerp(-halfSweep, halfSweep, t);
                float rad = angleDeg * Mathf.Deg2Rad;

                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                // Local arc plane (X = forward/right spread, Z = forward sweep)
                Vector3 dir = new Vector3(sin, 0f, cos);

                int idxIn = i * 2;
                int idxOut = i * 2 + 1;

                vertices[idxIn] = dir * innerRadius;
                vertices[idxOut] = dir * outerRadius;

                // U = sweep progress (0 = trailing fade, 1 = leading cutting edge)
                // V = radius progress (0 = inner hilt rail, 1 = outer tip rail)
                uvs[idxIn] = new Vector2(t, 0f);
                uvs[idxOut] = new Vector2(t, 1f);

                // Vertex alpha: quadratic falloff so the tail dissolves smoothly while the leading edge is solid
                float alpha = Mathf.Pow(t, 1.8f);
                colors[idxIn] = new Color(1f, 1f, 1f, alpha);
                colors[idxOut] = new Color(1f, 1f, 1f, alpha);

                if (i < segments)
                {
                    int triIdx = i * 6;
                    // Triangle 1
                    triangles[triIdx + 0] = idxIn;
                    triangles[triIdx + 1] = idxOut;
                    triangles[triIdx + 2] = idxIn + 2;

                    // Triangle 2
                    triangles[triIdx + 3] = idxOut;
                    triangles[triIdx + 4] = idxOut + 2;
                    triangles[triIdx + 5] = idxIn + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// (Deprecated) Previously spawned 3D digital data cubes. Disabled to eliminate blocky/purple box artifacts during sword swings.
        /// </summary>
        private static void SpawnDeRezDataCubes(Vector3 center, Quaternion orientation, float sweepAngle, float radius, Color color, int count)
        {
            // Fully disabled: 3D cube particles appeared as solid glitch/blocky purple boxes along sword swing arcs.
            // Combat now relies on high-fidelity procedural crescent arc meshes and directional needle sparks.
        }

        /// <summary>
        /// Spawns high-speed directional laser needle sparks spraying along the blade cut tangent.
        /// </summary>
        public static void SpawnNeedleSparks(Vector3 origin, Vector3 cutDir, Color color, int count)
        {
            GameObject container = new GameObject("Tron_Laser_Needle_Sparks");
            container.transform.position = origin;
            if (cutDir.sqrMagnitude > 0.001f) container.transform.rotation = Quaternion.LookRotation(cutDir);

            var ps = container.AddComponent<ParticleSystem>();
            var psRenderer = container.GetComponent<ParticleSystemRenderer>();

            psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            psRenderer.lengthScale = 2.5f;
            psRenderer.velocityScale = 0.018f;
            psRenderer.material = _needleSparkMaterial;
            if (_needleSparkMaterial != null)
            {
                _needleSparkMaterial.SetColor("_BaseColor", color * 3.5f);
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.020f, 0.038f);
            main.startColor = color * 3.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.15f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, count) });

            ps.Play();
            SafeDestroy(container, 0.35f);
        }

        /// <summary>
        /// Spawns an expanding planar Tron digital grid ring on the ground.
        /// </summary>
        public static void SpawnFloorGridRing(Vector3 origin, Color color, float targetRadius)
        {
            GameObject ring = new GameObject("Tron_Floor_Shockwave_Grid");
            ring.transform.position = origin + Vector3.up * 0.04f;
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Planar with floor

            Mesh ringMesh = CreatePolarFanMesh(0.15f, 1.0f, 360f, segments: 40);
            var mf = ring.AddComponent<MeshFilter>();
            mf.sharedMesh = ringMesh;

            var mr = ring.AddComponent<MeshRenderer>();
            Material mat = new Material(_tronArcMaterialPlayer);
            mat.SetColor("_MainColor", color * 2.2f);
            mat.SetColor("_CoreColor", Color.white * 3.0f);
            mat.SetFloat("_GridFrequency", 12.0f);
            mr.material = mat;

            var anim = ring.AddComponent<TronFloorRingAnimator>();
            anim.Initialize(mat, targetRadius, 0.22f);
        }

        /// <summary>
        /// Spawns a projected holographic ground danger telegraph indicator warning small targets like Alita of impending impact.
        /// </summary>
        public static void SpawnGroundDangerTelegraph(Vector3 groundPos, float radius, float duration, Color warningColor = default)
        {
            EnsureMaterials();
            if (warningColor == default) warningColor = new Color(5.5f, 0.4f, 0.2f); // Warning fiery crimson/amber

            GameObject telegraph = new GameObject("Tron_Ground_Danger_Telegraph");
            telegraph.transform.position = groundPos + Vector3.up * 0.03f;
            telegraph.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Mesh circleMesh = CreatePolarFanMesh(0.08f, 1.0f, 360f, segments: 44);
            var mf = telegraph.AddComponent<MeshFilter>();
            mf.sharedMesh = circleMesh;

            var mr = telegraph.AddComponent<MeshRenderer>();
            Material mat = new Material(_tronArcMaterialEnemy ?? _tronArcMaterialPlayer);
            mat.SetColor("_MainColor", warningColor * 1.8f);
            mat.SetColor("_CoreColor", Color.white * 2.2f);
            mat.SetFloat("_GridFrequency", 16.0f);
            mat.SetFloat("_GridIntensity", 0.85f);
            mr.material = mat;

            var anim = telegraph.AddComponent<TronGroundTelegraphAnimator>();
            anim.Initialize(mat, radius, duration);
        }

        public static void SpawnPointLightFlash(Vector3 pos, Color color, float range, float intensity, float duration)
        {
            GameObject lObj = new GameObject("Tron_VFX_Point_Flash");
            lObj.transform.position = pos + Vector3.up * 0.10f;

            var light = lObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;

            lObj.AddComponent<LightFlashFader>().Initialize(duration);
        }

        private static void SafeDestroy(GameObject go, float delay)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go, delay);
            else Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// Animates the procedural Tron Light Wall arc mesh: expanding scale and de-rez dissolve.
    /// </summary>
    public class TronArcMeshAnimator : MonoBehaviour
    {
        private Material _material;
        private float _duration;
        private float _maxScale;
        private float _timer;
        private Vector3 _startScale;

        public void Initialize(Material mat, float duration, float maxScale)
        {
            _material = mat;
            _duration = Mathf.Max(0.05f, duration);
            _maxScale = maxScale;
            _timer = 0f;
            _startScale = transform.localScale;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float progress = Mathf.Clamp01(_timer / _duration);

            // Centrifugal slight expansion
            float scaleMul = Mathf.Lerp(1.0f, _maxScale, Mathf.Sqrt(progress));
            transform.localScale = _startScale * scaleMul;

            // De-rez dissolve sweeps across the arc
            if (_material != null)
            {
                // Quadratic sweep progression
                float dissolve = Mathf.Pow(progress, 1.4f);
                _material.SetFloat("_Dissolve", dissolve);
            }

            if (_timer >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }

    /// <summary>
    /// Animates the planar Tron floor shockwave ring expanding outward and dissolving.
    /// </summary>
    public class TronFloorRingAnimator : MonoBehaviour
    {
        private Material _material;
        private float _targetRadius;
        private float _duration;
        private float _timer;

        public void Initialize(Material mat, float targetRadius, float duration)
        {
            _material = mat;
            _targetRadius = targetRadius;
            _duration = Mathf.Max(0.05f, duration);
            _timer = 0f;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float progress = Mathf.Clamp01(_timer / _duration);

            // Fast explosive expansion with slight deceleration
            float r = Mathf.Lerp(0.2f, _targetRadius, Mathf.Pow(progress, 0.65f));
            transform.localScale = new Vector3(r, r, 1f);

            if (_material != null)
            {
                _material.SetFloat("_Dissolve", progress);
            }

            if (_timer >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }

    /// <summary>
    /// Animates the holographic ground danger warning telegraph indicator for small targets.
    /// </summary>
    public class TronGroundTelegraphAnimator : MonoBehaviour
    {
        private Material _material;
        private float _targetRadius;
        private float _duration;
        private float _timer;

        public void Initialize(Material mat, float targetRadius, float duration)
        {
            _material = mat;
            _targetRadius = targetRadius;
            _duration = Mathf.Max(0.05f, duration);
            _timer = 0f;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float progress = Mathf.Clamp01(_timer / _duration);

            // Fast expansion then intense warning strobe
            float scale = Mathf.Lerp(0.3f, _targetRadius, Mathf.Pow(progress, 0.5f));
            transform.localScale = new Vector3(scale, scale, 1f);

            if (_material != null)
            {
                float pulse = 1.0f + 0.35f * Mathf.Sin(progress * Mathf.PI * 8f);
                _material.SetFloat("_RailIntensity", pulse * 2.4f);
                _material.SetFloat("_Dissolve", 1f - progress * 0.85f);
            }

            if (_timer >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
