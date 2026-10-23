using System.Collections;
using UnityEngine;
using InsaneGun.Combat;

namespace PolygonMech
{
    /// <summary>
    /// AAA Melee Blade Combat VFX Controller for Goliath Mech Boss.
    /// Manages:
    /// 1. Continuous Luminous Ribbon Trail on the 4.14m Greatsword (tapered width, HDR emission).
    /// 2. Titanic Procedural 3D Slash Arcs (5.0m outer radius) synchronized with attack swings.
    /// 3. Dynamic Real-time Point Light Flares at blade tip.
    /// 4. Directional Impact Needle Sparks & Plasma Shockwaves on hits.
    /// 5. Camera Shake and Hitstop micro-delays for visceral combat impact.
    /// 6. Dual-Phase Color Shifting (Cyber Gold in Phase 1 -> Overdrive Crimson/Plasma in Phase 2).
    /// </summary>
    public class MechBladeCombatVFX : MonoBehaviour
    {
        [Header("Weapon Transforms")]
        [SerializeField] private Transform swordRoot;
        [SerializeField] private Transform swordTip;

        [Header("Trail Settings")]
        [SerializeField] private float trailTime = 0.26f;
        [SerializeField] private float trailBaseWidth = 0.65f;
        [SerializeField] private Color phase1TrailColor = new Color(4.8f, 1.5f, 0.2f, 1.0f);   // Cyber Gold HDR
        [SerializeField] private Color phase2TrailColor = new Color(5.5f, 0.4f, 1.4f, 1.0f);   // Overdrive Crimson/Plasma HDR

        [Header("Dynamic Point Light")]
        [SerializeField] private float normalLightIntensity = 1.5f;
        [SerializeField] private float swingLightIntensity  = 6.5f;
        [SerializeField] private float lightRange           = 9.0f;

        [Header("Titan Slash Arc Settings")]
        [SerializeField] private float slashScaleMultiplier = 2.4f;
        [SerializeField] private float slashHeightOffset    = 1.8f;

        private TrailRenderer bladeTrail;
        private Light bladeLight;
        private Material trailMaterial;
        private bool isPhase2 = false;
        private bool isSwinging = false;

        private void Awake()
        {
            FindWeaponBones();
            SetupTrailRenderer();
            SetupBladeLight();
        }

        private void FindWeaponBones()
        {
            if (swordRoot == null)
            {
                swordRoot = transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R/Hand_R/Prop_R/Prop_R_Socket/SM_Wep_Sword_01");
            }

            if (swordRoot != null && swordTip == null)
            {
                swordTip = swordRoot.Find("SwordTip");
                if (swordTip == null)
                {
                    // Create tip marker at sword extent (3.5m along Y)
                    GameObject tipObj = new GameObject("SwordTip");
                    tipObj.transform.SetParent(swordRoot, false);
                    tipObj.transform.localPosition = new Vector3(0f, 3.5f, 0f);
                    swordTip = tipObj.transform;
                }
            }
        }

        private void SetupTrailRenderer()
        {
            if (swordTip == null) return;

            bladeTrail = swordTip.GetComponent<TrailRenderer>();
            if (bladeTrail == null)
                bladeTrail = swordTip.gameObject.AddComponent<TrailRenderer>();

            bladeTrail.time = trailTime;
            bladeTrail.minVertexDistance = 0.08f;
            bladeTrail.autodestruct = false;
            bladeTrail.emitting = false;

            // Tapered width curve: razor tip (0.0) -> blade belly (0.65m) -> hilt (0.1m)
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.0f);
            curve.AddKey(0.2f, trailBaseWidth);
            curve.AddKey(0.8f, trailBaseWidth * 0.7f);
            curve.AddKey(1.0f, 0.05f);
            bladeTrail.widthCurve = curve;

            // Shader & Material
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                              ?? Shader.Find("Particles/Standard Unlit")
                              ?? Shader.Find("Sprites/Default");

            trailMaterial = new Material(unlitShader) { name = "M_MechBlade_Trail" };
            trailMaterial.SetFloat("_Surface", 1); // Transparent
            trailMaterial.SetFloat("_Blend", 1);   // Additive
            trailMaterial.SetColor("_BaseColor", phase1TrailColor);
            bladeTrail.material = trailMaterial;
        }

        private void SetupBladeLight()
        {
            if (swordTip == null) return;

            bladeLight = swordTip.GetComponent<Light>();
            if (bladeLight == null)
                bladeLight = swordTip.gameObject.AddComponent<Light>();

            bladeLight.type = LightType.Point;
            bladeLight.range = lightRange;
            bladeLight.intensity = normalLightIntensity;
            bladeLight.color = new Color(1.0f, 0.65f, 0.2f);
            bladeLight.shadows = LightShadows.None;
            bladeLight.enabled = true;
        }

        public void SetPhase2(bool active)
        {
            isPhase2 = active;
            if (trailMaterial != null)
            {
                trailMaterial.SetColor("_BaseColor", isPhase2 ? phase2TrailColor : phase1TrailColor);
            }
            if (bladeLight != null)
            {
                bladeLight.color = isPhase2 ? new Color(1.0f, 0.2f, 0.5f) : new Color(1.0f, 0.65f, 0.2f);
            }
        }

        /// <summary>
        /// Activates continuous ribbon emission during weapon swing windows.
        /// </summary>
        public void StartSwingTrail()
        {
            isSwinging = true;
            if (bladeTrail != null)
            {
                bladeTrail.Clear();
                bladeTrail.emitting = true;
            }
            if (bladeLight != null)
            {
                bladeLight.intensity = swingLightIntensity * (isPhase2 ? 1.4f : 1.0f);
            }
        }

        /// <summary>
        /// Ceases ribbon emission at swing completion.
        /// </summary>
        public void StopSwingTrail()
        {
            isSwinging = false;
            if (bladeTrail != null)
            {
                bladeTrail.emitting = false;
            }
            if (bladeLight != null)
            {
                bladeLight.intensity = normalLightIntensity;
            }
        }

        /// <summary>
        /// Spawns a colossal 3D Tron Melee Slash Arc scaled specifically for the Goliath Mech.
        /// Attack Type mapping:
        /// 1 = Cleave (Horizontal wide)
        /// 2 = Cross Cut (Rising diagonal)
        /// 3 = Upward Launcher (Vertical uppercut)
        /// 4 = Piston Thrust (Focused piercing tunnel)
        /// 5 = Overhead Split (Vertical execution)
        /// 6 = Cyclone Whirlwind (360° perimeter wall)
        /// 7 = Decapitation Finisher (Colossal hyper-arc)
        /// </summary>
        /// <summary>
        /// Spawns a colossal 3D Tron Melee Slash Arc scaled specifically for the Goliath Mech.
        /// Attack Type mapping:
        /// 1 = Cleave (Horizontal wide)
        /// 2 = Cross Cut (Rising diagonal)
        /// 3 = Upward Launcher (Vertical uppercut)
        /// 4 = Piston Thrust (Focused piercing tunnel)
        /// 5 = Overhead Split (Vertical execution)
        /// 6 = Cyclone Whirlwind (360° perimeter wall)
        /// 7 = Decapitation Finisher (Colossal hyper-arc)
        /// </summary>
        public void EmitMechSlashArc(int attackType, Vector3 customOffset = default, float pitchDown = 0f)
        {
            Transform attacker = transform;
            float scale = slashScaleMultiplier * (isPhase2 ? 1.15f : 1.0f);
            float hOffset = slashHeightOffset + customOffset.y;
            TronSlashArcGenerator.SpawnTronSlash(attacker, attackType, isPhase2, scale, hOffset, pitchDown);
            FlashBladeLight();

            // Visceral Impact Juice: hitstop + camera shake
            float hitstopDuration = (attackType == 7 || attackType == 5) ? 0.08f : 0.045f;
            TriggerHitstop(hitstopDuration);

            var master = GetComponent<TitanMechProceduralMaster>();
            if (master != null)
            {
                float shakeIntensity = (attackType == 7 || attackType == 5) ? 0.75f : 0.35f;
                master.TriggerProximityCameraShake(shakeIntensity);
            }
        }

        /// <summary>
        /// Anti-Small Target Cleave: sweeps low along the ground plane (ankle/waist height)
        /// pitched down toward the floor so the colossal blade cuts right through small agile fighters like Alita.
        /// </summary>
        public void EmitAntiSmallGroundCleave(float heightOffset = -0.55f, float pitchDown = 22f)
        {
            Transform attacker = transform;
            float scale = slashScaleMultiplier * (isPhase2 ? 1.15f : 1.0f);
            float hOffset = slashHeightOffset + heightOffset; // Sweeps at ~0.5m-0.8m above asphalt

            TronSlashArcGenerator.SpawnTronSlash(attacker, 1, isPhase2, scale, hOffset, pitchDown);
            FlashBladeLight();

            // High-density pavement friction sparks as blade sweeps along the asphalt
            Vector3 scrapePoint = attacker.position + attacker.forward * (scale * 1.5f) + Vector3.up * 0.15f;
            Color sparkColor = isPhase2 ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
            TronSlashArcGenerator.SpawnNeedleSparks(scrapePoint, attacker.right + attacker.forward * 0.4f, sparkColor, 24);

            TriggerHitstop(0.045f);
            GetComponent<TitanMechProceduralMaster>()?.TriggerProximityCameraShake(0.35f);
        }

        /// <summary>
        /// Anti-Small Overhead Guillotine Split: drives colossal blade directly into the ground,
        /// detonating an expanding planar floor shockwave grid ring and seismic dust/debris.
        /// </summary>
        public void EmitAntiSmallOverheadSplit(Vector3 groundImpactPos, float radius = 7.5f)
        {
            Transform attacker = transform;
            float scale = slashScaleMultiplier * (isPhase2 ? 1.15f : 1.0f);

            // Vertical downward chop cutting from top to bottom
            TronSlashArcGenerator.SpawnTronSlash(attacker, 5, isPhase2, scale, -0.2f, 15f);
            FlashBladeLight();

            // Radial floor shockwave grid ring expanding along pavement
            Color waveColor = isPhase2 ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
            TronSlashArcGenerator.SpawnFloorGridRing(groundImpactPos, waveColor, radius);

            // Kinetic needle sparks spraying upward from the ground impact crater
            Vector3 sparkBurstDir = Vector3.up + attacker.forward * 0.5f;
            TronSlashArcGenerator.SpawnNeedleSparks(groundImpactPos + Vector3.up * 0.2f, sparkBurstDir.normalized, waveColor, 32);
            TronSlashArcGenerator.SpawnPointLightFlash(groundImpactPos, waveColor, 10f, 14f, 0.12f);

            TriggerHitstop(0.085f);
            GetComponent<TitanMechProceduralMaster>()?.TriggerProximityCameraShake(0.85f);
        }

        /// <summary>
        /// Anti-Small Impale Thrust: drives the 3.5m blade tip diagonally down into the asphalt at a 28° angle.
        /// </summary>
        public void EmitAntiSmallImpaleThrust(Vector3 targetGroundPos)
        {
            Transform attacker = transform;
            float scale = slashScaleMultiplier * (isPhase2 ? 1.15f : 1.0f);

            // Piercing laser tunnel angled downwards toward the floor
            TronSlashArcGenerator.SpawnTronSlash(attacker, 4, isPhase2, scale, -0.3f, 28f);
            FlashBladeLight();

            Color waveColor = isPhase2 ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
            TronSlashArcGenerator.SpawnNeedleSparks(targetGroundPos + Vector3.up * 0.2f, attacker.forward, waveColor, 20);

            TriggerHitstop(0.06f);
            GetComponent<TitanMechProceduralMaster>()?.TriggerProximityCameraShake(0.50f);
        }

        /// <summary>
        /// Anti-Small Low 360° Ankle Sweeper: spins the blade right along the floor (ankle/knee height)
        /// creating a full perimeter hazard that forces agile targets to jump or parry.
        /// </summary>
        public void EmitAntiSmallLowWhirlwind(float radius = 7.0f)
        {
            Transform attacker = transform;
            float scale = slashScaleMultiplier * (isPhase2 ? 1.15f : 1.0f);

            // Low-altitude 360-degree light wall
            TronSlashArcGenerator.SpawnTronSlash(attacker, 6, isPhase2, scale, -0.65f, 5f);
            FlashBladeLight();

            Color waveColor = isPhase2 ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
            TronSlashArcGenerator.SpawnFloorGridRing(attacker.position, waveColor, radius);

            TriggerHitstop(0.065f);
            GetComponent<TitanMechProceduralMaster>()?.TriggerProximityCameraShake(0.60f);
        }

        /// <summary>
        /// Spawns a glowing ground danger telegraph indicator warning agile targets like Alita of impending impact.
        /// </summary>
        public void SpawnGroundTelegraph(Vector3 groundPos, float radius, float duration)
        {
            Color warningColor = isPhase2 ? new Color(5.5f, 0.2f, 0.5f) : new Color(5.0f, 0.6f, 0.1f);
            TronSlashArcGenerator.SpawnGroundDangerTelegraph(groundPos, radius, duration, warningColor);
        }

        /// <summary>
        /// Spawns high-energy needle spark bursts and kinetic flashes during blade parries or shields.
        /// </summary>
        public void EmitDefensiveParrySparks(Vector3 contactPoint = default)
        {
            if (contactPoint == default)
            {
                contactPoint = (swordTip != null) 
                    ? swordTip.position 
                    : (transform.position + transform.forward * 1.8f + Vector3.up * 2.2f);
            }

            Color sparkColor = isPhase2 ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
            Vector3 sparkDirection = -transform.forward + Random.insideUnitSphere * 0.4f;
            TronSlashArcGenerator.SpawnNeedleSparks(contactPoint, sparkDirection.normalized, sparkColor, 28);
            TronSlashArcGenerator.SpawnPointLightFlash(contactPoint, sparkColor, 8.0f, 12f, 0.10f);

            TriggerHitstop(0.06f);

            var master = GetComponent<TitanMechProceduralMaster>();
            if (master != null)
            {
                master.TriggerProximityCameraShake(0.40f);
            }
        }

        /// <summary>
        /// Flashes the blade point light for 1-2 frames of blinding kinetic brilliance.
        /// </summary>
        public void FlashBladeLight()
        {
            StartCoroutine(RoutineFlashLight());
        }

        private IEnumerator RoutineFlashLight()
        {
            if (bladeLight == null) yield break;
            float prevInt = bladeLight.intensity;
            bladeLight.intensity = isPhase2 ? 14f : 10f;
            yield return new WaitForSeconds(0.06f);
            bladeLight.intensity = isSwinging ? swingLightIntensity : normalLightIntensity;
        }

        /// <summary>
        /// Executes cinematic hitstop micro-delay (freezes animator momentarily on weapon contact).
        /// </summary>
        public void TriggerHitstop(float duration = 0.05f)
        {
            StartCoroutine(RoutineHitstop(duration));
        }

        private IEnumerator RoutineHitstop(float duration)
        {
            Animator anim = GetComponent<Animator>();
            if (anim == null) yield break;

            float originalSpeed = anim.speed;
            anim.speed = 0.02f; // Near-freeze
            yield return new WaitForSecondsRealtime(duration);
            if (anim != null) anim.speed = originalSpeed;
        }
    }
}
