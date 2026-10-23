using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;
using SyntyCombined.BlackMarket;
using InsaneGun.Combat;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.World
{
    public enum TankCombatState
    {
        Dormant,
        PoweringUp,
        CombatActive,
        Destroyed
    }

    /// <summary>
    /// Syndicate Heavy Hover Tank (MK-IV Siege Platform) stationed inside SM_Bld_Scav_Garage_02.
    /// Powers up and assaults Alita as soon as she flees The Collector's sanctuary pod with the stolen Mars coordinates.
    /// Features:
    /// - Heavy rotating dual-barrel turret & aiming barrel with target tracking
    /// - Red telegraph targeting laser that pulses bright white before cannon discharge
    /// - High-velocity plasma cannon shells with muzzle flash, screen shake, and explosive impact shockwaves
    /// - Rapid pulse-laser suppression bursts
    /// - Hover thruster glow & ground dust particles
    /// - Dynamic hover floating bobbing motion
    /// - Full destructibility with chained multi-stage explosions and AAA boss health bar
    /// </summary>
    [SelectionBase]
    public class SyndicateHoverTankAI : MonoBehaviour, SyntyCombined.ISyntyDamageable
    {
        private static SyndicateHoverTankAI _instance;
        public static SyndicateHoverTankAI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SyndicateHoverTankAI>() ?? FindObjectOfType<SyndicateHoverTankAI>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("State & Target")]
        public TankCombatState currentState = TankCombatState.Dormant;
        public Transform targetPlayer;

        [Header("Chassis & Turret Transforms")]
        public Transform turretTransform;
        public Transform barrelTransform;
        public Transform muzzlePoint;
        public List<Transform> hoverPads = new List<Transform>();

        [Header("Combat Settings")]
        public float maxHealth = 600f;
        public float currentHealth = 600f;
        public float turretTurnSpeed = 75f;
        public float cannonCooldown = 3.2f;
        public float cannonDamage = 28f;
        public float laserBurstDamage = 8f;

        [Header("Aim")]
        [Tooltip("Every shot is aimed beside Alita, never at her: the tank is a threat, not a damage source")]
        public bool intentionallyMissPlayer = true;
        public float cannonMissDistance = 5.5f;
        public float laserMissDistance = 2.8f;
        public float attackRange = 55f;

        [Header("Hover Motion Settings")]
        public Vector3 garageParkedPos;
        public Vector3 combatDeployPos = new Vector3(-4.87f, -0.58f, -22.50f);
        public float deployMoveSpeed = 4.5f;
        public float hoverBobFrequency = 2.4f;
        public float hoverBobAmplitude = 0.18f;

        [Header("Visual & FX")]
        public LineRenderer targetingLaser;
        public GameObject muzzleFlashPrefab;
        public GameObject explosionPrefab;
        public GameObject laserBulletPrefab;
        public Color targetingLaserColor = new Color(1.0f, 0.05f, 0.05f, 0.85f);
        public Color chargingLaserColor = new Color(1.0f, 0.95f, 0.70f, 1.0f);

        // Internal State
        private float _lastCannonFireTime = 0f;
        private float _lastPulseBurstTime = 0f;
        private bool _isChargingCannon = false;
        private const float CANNON_CHARGE_DURATION = 0.85f;
        private AudioSource _audioSource;
        private List<Light> _hoverThrusterLights = new List<Light>();
        private bool _hasDeployedToStreet = false;
        private float _baseY = 0f;

        private void Awake()
        {
            Instance = this;
            EnsureComponents();
        }

        private void Start()
        {
            FindReferences();
            garageParkedPos = transform.position;
            _baseY = transform.position.y;
            currentHealth = maxHealth;
            SetThrustersActive(false);
            if (targetingLaser != null) targetingLaser.enabled = false;
        }

        private void EnsureComponents()
        {
            // Set Tank and all children to Enemy layer so Alita can target and damage it
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                gameObject.layer = enemyLayer;
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = enemyLayer;
                }
            }
            gameObject.tag = "Enemy";

            // Ensure Kinematic Rigidbody for clean physics and hit detection
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0.75f;
                _audioSource.minDistance = 8f;
                _audioSource.maxDistance = 75f;
                _audioSource.volume = 0.9f;
            }

            // Ensure Targeting Laser LineRenderer
            if (targetingLaser == null)
            {
                var laserObj = new GameObject("Tank_Targeting_Laser");
                laserObj.transform.SetParent(transform);
                targetingLaser = laserObj.AddComponent<LineRenderer>();
                targetingLaser.startWidth = 0.06f;
                targetingLaser.endWidth = 0.06f;
                targetingLaser.positionCount = 2;
                targetingLaser.useWorldSpace = true;

                // Simple unlit laser material
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                mat.color = targetingLaserColor;
                targetingLaser.material = mat;
                targetingLaser.enabled = false;
            }
        }

        public void FindReferences()
        {
            if (targetPlayer == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Alita") ?? GameObject.Find("Alita_Hero") ?? GameObject.Find("Alita_BattleAngel_Hero");
                if (p != null) targetPlayer = p.transform;
            }

            if (turretTransform == null)
            {
                var t = transform.Find("SM_Veh_HoverTank_03_Turret_01");
                if (t != null) turretTransform = t;
            }

            if (barrelTransform == null && turretTransform != null)
            {
                var b = turretTransform.Find("SM_Veh_HoverTank_03_Barrel_01");
                if (b != null) barrelTransform = b;
            }

            if (muzzlePoint == null && barrelTransform != null)
            {
                var m = barrelTransform.Find("SM_Veh_HoverTank_03_Barrel_02");
                if (m != null) muzzlePoint = m;
            }

            // Hover pads
            if (hoverPads.Count == 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    if (ch.name.Contains("HoverPad"))
                    {
                        hoverPads.Add(ch);
                    }
                }
            }

            // Create cyan underglow point lights under hover pads
            if (_hoverThrusterLights.Count == 0)
            {
                foreach (var pad in hoverPads)
                {
                    var lightObj = new GameObject("Hover_Thruster_Glow");
                    lightObj.transform.SetParent(pad);
                    lightObj.transform.localPosition = new Vector3(0f, -0.2f, 0f);
                    var l = lightObj.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(0.1f, 0.85f, 1.0f);
                    l.range = 3.5f;
                    l.intensity = 1.8f;
                    l.enabled = false;
                    _hoverThrusterLights.Add(l);
                }
            }

            LoadPrefabsIfMissing();
        }

        private void LoadPrefabsIfMissing()
        {
#if UNITY_EDITOR
            if (explosionPrefab == null)
            {
                explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Explosion_01.prefab")
                               ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/FX/FX_Explosion.prefab");
            }
            if (muzzleFlashPrefab == null)
            {
                muzzleFlashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Laser_Blast_Large_01.prefab")
                                 ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/FX/FX_Laser_Shot.prefab");
            }
            if (laserBulletPrefab == null)
            {
                laserBulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/FX/FX_Laser_Bullet.prefab");
            }
#endif
        }

        private void SetThrustersActive(bool active)
        {
            foreach (var l in _hoverThrusterLights)
            {
                if (l != null) l.enabled = active;
            }
        }

        /// <summary>
        /// Activates the hover tank from the garage to attack Alita upon exiting the building.
        /// </summary>
        [ContextMenu("Activate Tank Assault")]
        public void ActivateTank()
        {
            if (currentState != TankCombatState.Dormant) return;

            if (targetPlayer == null) FindReferences();
            if (targetPlayer != null)
            {
                var swordCtrl = targetPlayer.GetComponent<InsaneGunSwordController>();
                if (swordCtrl != null)
                {
                    swordCtrl.targetEnemy = transform;
                }
            }

            StartCoroutine(PowerUpSequence());
        }

        private IEnumerator PowerUpSequence()
        {
            currentState = TankCombatState.PoweringUp;

            // 1. Spool-up SFX & Thruster Ignition
            SetThrustersActive(true);
            PlayEngineSpoolSound();

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("🚨 ALERT: SYNDICATE SIEGE HOVER TANK ENGAGED FROM SECTOR GARAGE!");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "GARAGE ENFORCER",
                    "HEAVY WEAPONS // OPERATOR",
                    "Target acquired outside the sanctuary pod! Powering plasma drive... turn her to slag!",
                    new Color(1f, 0.2f, 0.15f),
                    6.0f
                );
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "SIEGE WEAPON ASSAULT",
                    "► SURVIVE the Heavy Hover Tank barrage & escape to Landing Pad B-04!"
                );
                CyberpunkHUDController.Instance.ShowToast("WARNING: MK-IV SIEGE CANNON LOCK DETECTED", 4.0f);
            }

            // Power-up delay
            yield return new WaitForSeconds(1.4f);

            currentState = TankCombatState.CombatActive;
            if (targetingLaser != null) targetingLaser.enabled = true;
            _lastCannonFireTime = Time.time;
            StartCoroutine(FireCannonRoutine()); // Start shooting immediately!
        }

        private void Update()
        {
            // Proximity agro and developer shortcut [F8] when dormant
            if (currentState == TankCombatState.Dormant)
            {
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    ActivateTank();
                    return;
                }

                if (targetPlayer == null) FindReferences();
                if (targetPlayer != null)
                {
                    float dist = Vector3.Distance(transform.position, targetPlayer.position);
                    if (dist <= 14f)
                    {
                        ActivateTank();
                        return;
                    }
                }
                return;
            }

            if (currentState != TankCombatState.CombatActive) return;

            if (targetPlayer == null)
            {
                FindReferences();
                if (targetPlayer == null) return;
            }

            // 1. Hover Dynamics & Bobbing
            UpdateHoverMovement();

            // 2. Turret & Barrel Aiming Tracking
            UpdateTurretAiming();

            // 3. Targeting Laser Telegraph
            UpdateTargetingLaser();

            // 4. Combat Firing Loop
            UpdateCombatAttacks();

            // 5. Check if Player Escaped to Pad
            CheckPlayerEscape();
        }

        private void UpdateHoverMovement()
        {
            // Hover vertical bobbing
            float bob = Mathf.Sin(Time.time * hoverBobFrequency) * hoverBobAmplitude;
            Vector3 pos = transform.position;
            pos.y = _baseY + bob;

            // Move forward out of garage to combat deployment position
            if (!_hasDeployedToStreet)
            {
                pos = Vector3.MoveTowards(pos, new Vector3(combatDeployPos.x, pos.y, combatDeployPos.z), deployMoveSpeed * Time.deltaTime);
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(combatDeployPos.x, combatDeployPos.z)) < 0.2f)
                {
                    _hasDeployedToStreet = true;
                }
            }
            transform.position = pos;
        }

        private void UpdateTurretAiming()
        {
            if (turretTransform == null || targetPlayer == null) return;

            // Horizontal Yaw Rotation
            Vector3 targetDir = targetPlayer.position - turretTransform.position;
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude > 0.05f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(targetDir, Vector3.up);
                turretTransform.rotation = Quaternion.RotateTowards(turretTransform.rotation, targetYaw, turretTurnSpeed * Time.deltaTime);
            }

            // Vertical Pitch Rotation for Barrel
            if (barrelTransform != null)
            {
                Vector3 muzzlePos = muzzlePoint != null ? muzzlePoint.position : barrelTransform.position;
                Vector3 toTarget = (targetPlayer.position + Vector3.up * 1.15f) - muzzlePos;
                float horizDist = new Vector2(toTarget.x, toTarget.z).magnitude;
                float pitch = -Mathf.Atan2(toTarget.y, horizDist) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -18f, 28f);

                Quaternion targetPitch = Quaternion.Euler(pitch, 0f, 0f);
                barrelTransform.localRotation = Quaternion.RotateTowards(barrelTransform.localRotation, targetPitch, turretTurnSpeed * 0.8f * Time.deltaTime);
            }
        }

        private void UpdateTargetingLaser()
        {
            if (targetingLaser == null || !targetingLaser.enabled) return;

            Vector3 startPos = muzzlePoint != null ? muzzlePoint.position : (barrelTransform != null ? barrelTransform.position : transform.position + Vector3.up * 2f);
            Vector3 endPos = targetPlayer != null ? (targetPlayer.position + Vector3.up * 1.0f) : startPos + transform.forward * 40f;

            RaycastHit hit;
            if (Physics.Linecast(startPos, endPos, out hit, ~LayerMask.GetMask("Ignore Raycast")))
            {
                endPos = hit.point;
            }

            targetingLaser.SetPosition(0, startPos);
            targetingLaser.SetPosition(1, endPos);

            // Turn laser white-hot when charging cannon
            if (_isChargingCannon)
            {
                float pulse = Mathf.PingPong(Time.time * 18f, 1f);
                Color c = Color.Lerp(targetingLaserColor, chargingLaserColor, pulse);
                targetingLaser.startColor = c;
                targetingLaser.endColor = c;
                targetingLaser.startWidth = 0.12f;
                targetingLaser.endWidth = 0.12f;
            }
            else
            {
                targetingLaser.startColor = targetingLaserColor;
                targetingLaser.endColor = targetingLaserColor;
                targetingLaser.startWidth = 0.04f;
                targetingLaser.endWidth = 0.04f;
            }
        }

        private void UpdateCombatAttacks()
        {
            float dist = targetPlayer != null ? Vector3.Distance(transform.position, targetPlayer.position) : 999f;
            if (dist > attackRange) return;

            // Handle Cannon Charge & Discharge
            if (!_isChargingCannon && Time.time >= _lastCannonFireTime + cannonCooldown)
            {
                StartCoroutine(FireCannonRoutine());
            }

            // Rapid Pulse Laser Bursts between cannon charges
            if (!_isChargingCannon && Time.time >= _lastPulseBurstTime + 1.6f)
            {
                _lastPulseBurstTime = Time.time;
                StartCoroutine(FireLaserBurstRoutine());
            }
        }

        private IEnumerator FireCannonRoutine()
        {
            _isChargingCannon = true;
            _lastCannonFireTime = Time.time;

            // Play Charge Whine
            PlayCannonChargeSound();

            yield return new WaitForSeconds(CANNON_CHARGE_DURATION);

            if (currentState != TankCombatState.CombatActive)
            {
                _isChargingCannon = false;
                yield break;
            }

            // Fire Heavy Cannon!
            FireHeavyCannonBlast();
            _isChargingCannon = false;
        }

        private void FireHeavyCannonBlast()
        {
            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 2.2f;
            Vector3 aimTarget = targetPlayer != null ? (targetPlayer.position + Vector3.up * 1.0f) : spawnPos + transform.forward * 30f;
            if (intentionallyMissPlayer && targetPlayer != null) aimTarget = MissPoint(spawnPos, aimTarget, cannonMissDistance);
            Vector3 fireDir = (aimTarget - spawnPos).normalized;

            // 1. Muzzle Flash
            if (muzzleFlashPrefab != null)
            {
                var flash = Instantiate(muzzleFlashPrefab, spawnPos, Quaternion.LookRotation(fireDir));
                Destroy(flash, 1.5f);
            }

            // 2. Heavy Cannon Audio & Recoil
            PlayCannonBlastSound();
            StartCoroutine(BarrelRecoilRoutine());

            // 3. Spawn Heavy Plasma Projectile
            GameObject projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projObj.name = "Tank_Heavy_Plasma_Round";
            projObj.transform.position = spawnPos;
            projObj.transform.localScale = Vector3.one * 0.75f;

            var mr = projObj.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Mobile/Particles/Additive");
            var mat = new Material(shader);
            mat.color = new Color(1.0f, 0.45f, 0.1f);
            mr.material = mat;

            var trail = projObj.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.startWidth = 0.65f;
            trail.endWidth = 0.05f;
            trail.material = mat;

            var projLight = projObj.AddComponent<Light>();
            projLight.type = LightType.Point;
            projLight.color = new Color(1.0f, 0.5f, 0.15f);
            projLight.range = 8f;
            projLight.intensity = 3.5f;

            var col = projObj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var projScript = projObj.AddComponent<TankPlasmaProjectile>();
            projScript.Initialize(fireDir, 38f, cannonDamage, explosionPrefab, OnProjectileImpact, intentionallyMissPlayer, 1f);
        }

        private IEnumerator BarrelRecoilRoutine()
        {
            if (barrelTransform == null) yield break;
            Vector3 localPos = barrelTransform.localPosition;
            Vector3 recoilPos = localPos + Vector3.back * 0.45f;

            float t = 0f;
            while (t < 0.06f)
            {
                t += Time.deltaTime;
                barrelTransform.localPosition = Vector3.Lerp(localPos, recoilPos, t / 0.06f);
                yield return null;
            }
            t = 0f;
            while (t < 0.28f)
            {
                t += Time.deltaTime;
                barrelTransform.localPosition = Vector3.Lerp(recoilPos, localPos, t / 0.28f);
                yield return null;
            }
            barrelTransform.localPosition = localPos;
        }

        private IEnumerator FireLaserBurstRoutine()
        {
            for (int i = 0; i < 3; i++)
            {
                if (currentState != TankCombatState.CombatActive) yield break;

                Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 2f;
                // Offset slightly for left/right barrel alternation
                Vector3 offset = (i % 2 == 0) ? Vector3.left * 0.4f : Vector3.right * 0.4f;
                spawnPos += turretTransform.rotation * offset;

                Vector3 targetPos = targetPlayer != null ? (targetPlayer.position + Vector3.up * 1.0f + Random.insideUnitSphere * 0.4f) : spawnPos + transform.forward * 30f;
                if (intentionallyMissPlayer && targetPlayer != null) targetPos = MissPoint(spawnPos, targetPos, laserMissDistance);
                Vector3 dir = (targetPos - spawnPos).normalized;

                // Spawn laser bolt
                GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bolt.name = "Tank_Laser_Bolt";
                bolt.transform.position = spawnPos;
                bolt.transform.localScale = new Vector3(0.12f, 0.65f, 0.12f);
                bolt.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);

                var mr = bolt.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                mat.color = new Color(0.1f, 0.95f, 1.0f);
                mr.material = mat;

                var col = bolt.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                var projScript = bolt.AddComponent<TankPlasmaProjectile>();
                projScript.Initialize(dir, 55f, laserBurstDamage, null, null, intentionallyMissPlayer, 0f);

                PlayLaserSound();
                yield return new WaitForSeconds(0.12f);
            }
        }

        /// <summary>A point beside the target (random side, a little short or long) so the shot lands near Alita but never on her.</summary>
        private Vector3 MissPoint(Vector3 from, Vector3 target, float distance)
        {
            Vector3 toTarget = target - from;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) toTarget = transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized);
            float sign = Random.value < 0.5f ? -1f : 1f;
            float along = Random.Range(-2.5f, 3.5f);
            return target + side * sign * (distance + Random.Range(0f, 1.5f)) + toTarget.normalized * along;
        }

        private void OnProjectileImpact(Vector3 hitPoint)
        {
            // Screen shake when heavy shell impacts nearby
            if (targetPlayer != null && Camera.main != null)
            {
                float dist = Vector3.Distance(hitPoint, targetPlayer.position);
                if (dist < 15f)
                {
                    StartCoroutine(DoScreenShake(Mathf.Clamp01(1f - (dist / 15f))));
                }
            }
        }

        private IEnumerator DoScreenShake(float intensity)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Vector3 origPos = cam.transform.localPosition;
            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damp = 1f - (elapsed / duration);
                cam.transform.localPosition = origPos + (Vector3)Random.insideUnitCircle * (intensity * 0.32f * damp);
                yield return null;
            }
            cam.transform.localPosition = origPos;
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            TakeDamage(damage);
            SpawnHitSparks(hitPoint, hitDirection);
        }

        public void TakeDamage(float damage)
        {
            if (currentState == TankCombatState.Destroyed) return;

            // Wake up if attacked while dormant
            if (currentState == TankCombatState.Dormant)
            {
                ActivateTank();
            }

            currentHealth -= damage;
            PlayHitArmorSound();

            if (currentHealth <= 0f)
            {
                StartCoroutine(DieSequence());
            }
        }

        private void SpawnHitSparks(Vector3 hitPoint, Vector3 hitDirection)
        {
            if (!Application.isPlaying) return;

            GameObject sparkGO = new GameObject("FX_Tank_Armor_Spark");
            sparkGO.transform.position = hitPoint;

            var ps = sparkGO.AddComponent<ParticleSystem>();
            var psr = sparkGO.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 12f;
            main.startSize = 0.15f;
            main.startColor = new Color(1.0f, 0.75f, 0.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.rotation = Quaternion.LookRotation(-hitDirection).eulerAngles;

            psr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            psr.material.color = new Color(1.0f, 0.8f, 0.3f);

            ps.Play();
            Destroy(sparkGO, 0.5f);
        }

        private IEnumerator DieSequence()
        {
            currentState = TankCombatState.Destroyed;
            if (targetingLaser != null) targetingLaser.enabled = false;
            SetThrustersActive(false);

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("💥 SIEGE HOVER TANK DESTROYED! SPRINT TO PAD B-04 & LAUNCH TO MARS [E / F]!");
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "EMERGENCY EVAC",
                    "► Tank destroyed! Sprint to Landing Pad B-04 and launch starship to Mars [E / F]"
                );
            }

            // Secondary explosion detonations
            for (int i = 0; i < hoverPads.Count; i++)
            {
                if (hoverPads[i] != null && explosionPrefab != null)
                {
                    var exp = Instantiate(explosionPrefab, hoverPads[i].position, Quaternion.identity);
                    Destroy(exp, 3f);
                }
                if (hoverPads[i] != null) TankExplosionFX.Spawn(hoverPads[i].position, 0.7f, i == 0 ? TankExplosionFX.Sound.Shell : TankExplosionFX.Sound.None);
                yield return new WaitForSeconds(0.15f);
            }

            // Final Turret Explosion
            if (turretTransform != null && explosionPrefab != null)
            {
                var bigExp = Instantiate(explosionPrefab, turretTransform.position + Vector3.up * 1f, Quaternion.identity);
                bigExp.transform.localScale = Vector3.one * 2.5f;
                Destroy(bigExp, 4f);
            }
            if (turretTransform != null) TankExplosionFX.Spawn(turretTransform.position + Vector3.up * 1f, 3.4f, TankExplosionFX.Sound.Death);
            StopHoverLoop();

            // Drop chassis to ground
            Vector3 dropPos = transform.position;
            dropPos.y = _baseY - 0.4f;
            transform.position = dropPos;
        }

        private void CheckPlayerEscape()
        {
            // Pad B-04 at (22.0, 1.88, 33.5) and Western Flight Deck at (-28.05, 2.75, 55.12)
            Vector3 padPos = new Vector3(22.0f, 1.88f, 33.5f);
            Vector3 shipPos = new Vector3(-28.05f, 2.75f, 55.12f);
            if (targetPlayer != null && (Vector3.Distance(targetPlayer.position, padPos) < 14f || Vector3.Distance(targetPlayer.position, shipPos) < 16f))
            {
                // Player reached flight deck!
                if (targetingLaser != null) targetingLaser.enabled = false;
                if (BlackMarketNPCDialogueHUD.Instance != null)
                {
                    BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("FLIGHT DECK REACHED // LAUNCH STARSHIP TO MARS [E / F / R]");
                }
            }
        }

        #region Audio Synthesis
        private static AudioClip Authored(string name) => Resources.Load<AudioClip>("TankSfx/" + name);

        private AudioSource _hoverLoop;

        private void StartHoverLoop()
        {
            var clip = Authored("Tank_Hover_Loop");
            if (clip == null) return;
            if (_hoverLoop == null)
            {
                _hoverLoop = gameObject.AddComponent<AudioSource>();
                _hoverLoop.loop = true;
                _hoverLoop.playOnAwake = false;
                _hoverLoop.spatialBlend = 1f;
                _hoverLoop.minDistance = 7f;
                _hoverLoop.maxDistance = 70f;
                _hoverLoop.volume = 0.42f;
            }
            _hoverLoop.clip = clip;
            if (!_hoverLoop.isPlaying) _hoverLoop.Play();
        }

        private void StopHoverLoop()
        {
            if (_hoverLoop != null) _hoverLoop.Stop();
        }

        private void PlayEngineSpoolSound()
        {
            if (_audioSource == null) return;
            StartHoverLoop();
            AudioClip clip = Authored("Tank_Engine_Spool") ?? GenerateSpoolClip();
            _audioSource.PlayOneShot(clip, 0.85f);
        }

        private void PlayCannonChargeSound()
        {
            if (_audioSource == null) return;
            AudioClip clip = Authored("Tank_Cannon_Charge") ?? GenerateChargeClip();
            _audioSource.PlayOneShot(clip, 0.95f);
        }

        private void PlayCannonBlastSound()
        {
            if (_audioSource == null) return;
            AudioClip clip = Authored("Tank_Cannon_Blast") ?? GenerateCannonBlastClip();
            _audioSource.PlayOneShot(clip, 1.0f);
        }

        private void PlayLaserSound()
        {
            if (_audioSource == null) return;
            AudioClip clip = Authored("Tank_Laser_Bolt") ?? GenerateLaserBoltClip();
            _audioSource.PlayOneShot(clip, 0.65f);
        }

        private void PlayHitArmorSound()
        {
            if (_audioSource == null) return;
            AudioClip clip = Authored("Tank_Armor_Hit") ?? GenerateArmorHitClip();
            _audioSource.PlayOneShot(clip, 0.75f);
        }

        private static AudioClip GenerateSpoolClip()
        {
            int rate = 44100;
            int len = (int)(rate * 1.5f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float freq = Mathf.Lerp(45f, 220f, t / 1.5f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Lerp(0.1f, 0.6f, t / 1.5f);
            }
            var c = AudioClip.Create("Tank_Spool", len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip GenerateChargeClip()
        {
            int rate = 44100;
            int len = (int)(rate * 0.85f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float freq = Mathf.Lerp(180f, 850f, t / 0.85f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * (0.4f * (t / 0.85f));
            }
            var c = AudioClip.Create("Cannon_Charge", len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip GenerateCannonBlastClip()
        {
            int rate = 44100;
            int len = (int)(rate * 0.9f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-6f * t);
                float bass = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.7f;
                float noise = (Random.value * 2f - 1f) * 0.45f;
                data[i] = Mathf.Clamp((bass + noise) * env, -1f, 1f);
            }
            var c = AudioClip.Create("Cannon_Blast", len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip GenerateLaserBoltClip()
        {
            int rate = 44100;
            int len = (int)(rate * 0.18f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float freq = Mathf.Lerp(950f, 240f, t / 0.18f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-12f * t);
            }
            var c = AudioClip.Create("Laser_Bolt", len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip GenerateArmorHitClip()
        {
            int rate = 44100;
            int len = (int)(rate * 0.22f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float metallic = Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.3f;
                data[i] = metallic * Mathf.Exp(-16f * t);
            }
            var c = AudioClip.Create("Armor_Hit", len, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }
        #endregion

        #region HUD Boss Bar
        private void OnGUI()
        {
            if (currentState != TankCombatState.CombatActive) return;

            float sw = Screen.width;
            float sh = Screen.height;

            float barW = 440f;
            float barH = 22f;
            float barX = (sw - barW) * 0.5f;
            float barY = 56f;

            // Background card
            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(barX - 12, barY - 26, barW + 24, barH + 34), Texture2D.whiteTexture);

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.35f, 0.2f);
            GUI.Label(new Rect(barX, barY - 24, barW, 20), "SYNDICATE SIEGE HOVER TANK // MK-IV ENFORCER", titleStyle);

            // Bar background
            GUI.color = new Color(0.2f, 0.1f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            // Bar fill
            float pct = Mathf.Clamp01(currentHealth / maxHealth);
            GUI.color = Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(1f, 0.85f, 0.15f), pct);
            GUI.DrawTexture(new Rect(barX, barY, barW * pct, barH), Texture2D.whiteTexture);

            // Border
            GUI.color = new Color(1.0f, 0.25f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(barX - 1, barY - 1, barW + 2, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(barX - 1, barY + barH, barW + 2, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(barX - 1, barY - 1, 1, barH + 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(barX + barW, barY - 1, 1, barH + 2), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
        #endregion
    }

    /// <summary>
    /// High-velocity plasma shell projectile fired by the hover tank.
    /// </summary>
    public class TankPlasmaProjectile : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private GameObject _explosionPrefab;
        private System.Action<Vector3> _onHitCallback;
        private float _lifetime = 4.5f;
        private bool _harmless;
        private float _blastScale;

        public void Initialize(Vector3 dir, float speed, float damage, GameObject expPrefab, System.Action<Vector3> onHit, bool harmless = false, float blastScale = 0f)
        {
            _harmless = harmless;
            _blastScale = blastScale;
            _direction = dir.normalized;
            _speed = speed;
            _damage = damage;
            _explosionPrefab = expPrefab;
            _onHitCallback = onHit;
            Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, _direction, out hit, step, ~LayerMask.GetMask("Ignore Raycast")))
            {
                if (hit.collider.gameObject != gameObject)
                {
                    ExplodeAt(hit.point, hit.collider);
                    return;
                }
            }
            transform.position += _direction * step;
        }

        private void OnTriggerEnter(Collider other)
        {
            ExplodeAt(transform.position, other);
        }

        private void ExplodeAt(Vector3 point, Collider hitCol)
        {
            _onHitCallback?.Invoke(point);

            // Deal damage to Alita if hit (shots that are meant to miss never hurt her, even if she steps into the blast)
            if (!_harmless && hitCol != null)
            {
                var swordCtrl = hitCol.GetComponentInParent<InsaneGunSwordController>();
                if (swordCtrl != null)
                {
                    swordCtrl.TakeDamage(_damage, point, _direction, true);
                }
            }

            // Area damage for heavy cannon explosion
            if (!_harmless && _damage > 15f)
            {
                var nearby = Physics.OverlapSphere(point, 3.2f);
                foreach (var col in nearby)
                {
                    var sc = col.GetComponentInParent<InsaneGunSwordController>();
                    if (sc != null && sc != hitCol?.GetComponentInParent<InsaneGunSwordController>())
                    {
                        sc.TakeDamage(_damage * 0.6f, point, _direction, true);
                    }
                }
            }

            // Spawn explosion effect: the layered fireball / shockwave / smoke, plus the stock prefab
            if (_blastScale > 0f) TankExplosionFX.Spawn(point, _blastScale, TankExplosionFX.Sound.Shell);
            if (_explosionPrefab != null)
            {
                var exp = Instantiate(_explosionPrefab, point, Quaternion.identity);
                Destroy(exp, 3f);
            }

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Layered procedural explosion: white-hot flash light, fire ball, rolling smoke, flying sparks and an expanding
    /// ground shockwave, with an authored explosion sound. Everything cleans itself up.
    /// </summary>
    public static class TankExplosionFX
    {
        public enum Sound { None, Shell, Death }

        private static Material _additive;
        private static Material _smoke;
        private static Material _ring;

        private static Shader ParticleShader() =>
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

        private static Material Additive()
        {
            if (_additive != null) return _additive;
            _additive = MakeMaterial(true);
            return _additive;
        }

        private static Material Smoke()
        {
            if (_smoke != null) return _smoke;
            _smoke = MakeMaterial(false);
            return _smoke;
        }

        private static Material MakeMaterial(bool additive)
        {
            var m = new Material(ParticleShader());
            m.color = Color.white;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        public static void Spawn(Vector3 point, float scale, Sound sound)
        {
            if (!Application.isPlaying) return;
            var root = new GameObject("FX_Tank_Explosion");
            root.transform.position = point;
            float life = 3.5f + scale;

            // 1. Flash light: blinding for a few frames, then a falling ember glow
            var lightGo = new GameObject("Flash");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.6f * scale;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.25f);
            light.range = 22f * scale;
            light.intensity = 30f * scale;
            light.shadows = LightShadows.None;
            root.AddComponent<TankExplosionFlicker>().Init(light, 30f * scale, 0.9f + 0.25f * scale);

            // 2. Fireball (additive, expands fast, fades to deep orange)
            AddParticles(root.transform, "Fire", Additive(), scale, p =>
            {
                var main = p.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 7f * scale);
                main.startSize = new ParticleSystem.MinMaxCurve(1.6f * scale, 3.6f * scale);
                main.startColor = new Color(1f, 0.85f, 0.45f, 1f);
                main.gravityModifier = -0.15f;
                var em = p.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(26 * scale)) });
                var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.6f * scale;
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Ramp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.45f, 0.08f), new Color(0.35f, 0.08f, 0.02f));
                var size = p.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.35f)));
            });

            // 3. Smoke column (alpha blended, slow, lingers)
            AddParticles(root.transform, "Smoke", Smoke(), scale, p =>
            {
                var main = p.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * scale, 3.2f * scale);
                main.startSize = new ParticleSystem.MinMaxCurve(2.2f * scale, 4.6f * scale);
                main.startColor = new Color(0.16f, 0.15f, 0.15f, 0.8f);
                main.gravityModifier = -0.06f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var em = p.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0.08f, (short)Mathf.RoundToInt(14 * scale)) });
                var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.9f * scale;
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Ramp(new Color(0.35f, 0.3f, 0.28f, 0.0f), new Color(0.18f, 0.17f, 0.16f, 0.75f), new Color(0.1f, 0.1f, 0.1f, 0f), true);
                var size = p.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.6f)));
                var rot = p.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            });

            // 4. Sparks / molten debris (stretched streaks that arc and fall)
            AddParticles(root.transform, "Sparks", Additive(), scale, p =>
            {
                var main = p.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(9f * scale, 22f * scale);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                main.startColor = new Color(1f, 0.7f, 0.25f, 1f);
                main.gravityModifier = 1.4f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = p.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(42 * scale)) });
                var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.4f * scale;
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Ramp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.5f, 0.1f), new Color(0.5f, 0.1f, 0.02f), true);
                var r = p.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 3.2f; r.velocityScale = 0.04f;
            });

            // 5. Ground shockwave: a flat ring that expands and fades
            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Shockwave";
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.up * 0.08f;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var rr = ring.GetComponent<MeshRenderer>();
            rr.sharedMaterial = RingMaterial();
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.AddComponent<TankShockwaveRing>().Init(ring.transform, rr, 12f * scale, 0.55f + 0.1f * scale);

            // 6. Sound (3D, at the blast)
            if (sound != Sound.None)
            {
                var clip = Resources.Load<AudioClip>(sound == Sound.Death ? "TankSfx/Tank_Death_Explosion" : "TankSfx/Tank_Shell_Explosion");
                if (clip != null) AudioSource.PlayClipAtPoint(clip, point, sound == Sound.Death ? 1f : 0.9f);
            }

            Object.Destroy(root, life);
        }

        private static Material RingMaterial()
        {
            if (_ring != null) return _ring;
            _ring = MakeMaterial(true);
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 63.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.14f);   // thin bright ring near the rim
                    a = a * a * (d <= 1f ? 1f : 0f);
                    tex.SetPixel(x, y, new Color(1f, 0.8f, 0.5f, a));
                }
            }
            tex.Apply();
            if (_ring.HasProperty("_BaseMap")) _ring.SetTexture("_BaseMap", tex);
            _ring.mainTexture = tex;
            _ring.color = new Color(1f, 0.75f, 0.4f, 1f);
            return _ring;
        }

        private static void AddParticles(Transform parent, string name, Material mat, float scale, System.Action<ParticleSystem> configure)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var em = ps.emission; em.rateOverTime = 0f;
            configure(ps);
            ps.Play();
        }

        private static ParticleSystem.MinMaxGradient Ramp(Color a, Color b, Color c, bool useAlpha = false)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.45f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(useAlpha ? a.a : 1f, 0f), new GradientAlphaKey(useAlpha ? b.a : 0.9f, 0.45f), new GradientAlphaKey(useAlpha ? c.a : 0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }
    }

    /// <summary>Drives the flash light of a TankExplosionFX: white-hot spike, then a flickering ember glow that dies out.</summary>
    public class TankExplosionFlicker : MonoBehaviour
    {
        private Light _light;
        private float _peak;
        private float _duration;
        private float _t;

        public void Init(Light light, float peak, float duration)
        {
            _light = light;
            _peak = peak;
            _duration = duration;
        }

        private void Update()
        {
            if (_light == null) return;
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _duration);
            float spike = Mathf.Exp(-k * 9f);
            float ember = (1f - k) * 0.22f * (0.8f + 0.2f * Mathf.Sin(_t * 47f));
            _light.intensity = _peak * (spike + ember);
            _light.color = Color.Lerp(new Color(1f, 0.85f, 0.55f), new Color(1f, 0.4f, 0.1f), k);
            if (k >= 1f) _light.enabled = false;
        }
    }

    /// <summary>Expands and fades the flat ring of a TankExplosionFX.</summary>
    public class TankShockwaveRing : MonoBehaviour
    {
        private Transform _ring;
        private MeshRenderer _renderer;
        private float _maxSize;
        private float _duration;
        private float _t;
        private MaterialPropertyBlock _block;

        public void Init(Transform ring, MeshRenderer renderer, float maxSize, float duration)
        {
            _ring = ring;
            _renderer = renderer;
            _maxSize = maxSize;
            _duration = duration;
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (_ring == null) return;
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _duration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            _ring.localScale = Vector3.one * Mathf.Lerp(0.6f, _maxSize, eased);
            var c = new Color(1f, 0.75f, 0.4f, (1f - k) * 0.9f);
            _renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_Color", c);
            _renderer.SetPropertyBlock(_block);
            if (k >= 1f) _ring.gameObject.SetActive(false);
        }
    }
}
