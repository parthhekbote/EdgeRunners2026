using System.Collections;
using UnityEngine;
using SyntyCombined.Cutscene;
using SyntyCombined.VFX;
using PolygonMech;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Alita's arrival on Mars: orbital overview of the planet, her Black Market hover-craft burning into the
    /// atmosphere, a low flyby over the Cydonia basin, a braking approach, touchdown, canopy release and Alita
    /// stepping out onto the red dust. Replaces the old three-shot arrival where the ship appeared from nowhere.
    /// </summary>
    public partial class MarsArrivalCutsceneDirector
    {
        // ── Staging ───────────────────────────────────────────────────────────
        private static readonly Vector3 SpaceStage = new Vector3(0f, 12000f, 0f);
        /// <summary>Where the hover-craft sets down (hull origin sits on the ground), nose toward the crashed warship.</summary>
        private static readonly Vector3 LandingPad = new Vector3(5f, 0f, -26f);
        private static readonly Vector3 AlitaStandPos = new Vector3(5f, 0.15f, -15f);

        // Cockpit geometry in the craft's local space (SM_Veh_HoverCraft_01)
        private static readonly Vector3 CockpitSeatLocal = new Vector3(0f, 0.9f, 1.1f);
        private static readonly Vector3 CockpitRimLocal = new Vector3(0f, 2.15f, 2.7f);
        private static readonly Vector3 CockpitGroundLocal = new Vector3(0f, 0f, 5.6f);

        [Header("Arrival Sound Overrides (leave empty to use the built-in sounds)")]
        public AudioClip sfxArrivalMusic;
        public AudioClip sfxEngineLoop;
        public AudioClip sfxBoostIgnite;
        public AudioClip sfxRocket;
        public AudioClip sfxAtmosphereEntry;
        public AudioClip sfxFlyby;
        public AudioClip sfxRetroBrake;
        public AudioClip sfxTouchdown;
        public AudioClip sfxCanopyOpen;

        private GameObject _spaceGlobe, _rimQuad;
        private Vector3 _globeAxis = Vector3.up;
        private System.Collections.Generic.List<Renderer> _hiddenRenderers = new System.Collections.Generic.List<Renderer>();
        private UnityEngine.Rendering.Volume _groundGrade;
        private AAAStarshipThrusterSystem _shipThrusters;
        private Transform _canopy;
        private Light _entryGlow;
        private AudioSource _shipEngine;
        private AudioClip _engineLoopClip, _bootsClip, _arrivalMusic;
        private GameObject[] _hiddenForSpace;
        private System.Collections.Generic.List<ParticleSystemRenderer> _hiddenParticles = new System.Collections.Generic.List<ParticleSystemRenderer>();

        private bool _stageActive;
        private Material _prevSkybox;
        private bool _prevFog;
        private CameraClearFlags _prevClear;
        private float _prevFar;
        private Color _prevBackground;

        private Color _overlayColor = Color.white;
        private float _overlayAlpha;

        private Vector3 _spF, _spR, _spU;

        private Vector3 SP(float r, float u, float f) => SpaceStage + _spR * r + _spU * u + _spF * f;

        // ═════════════════════════════════════════════════════════════════════
        // MAIN SEQUENCE
        // ═════════════════════════════════════════════════════════════════════
        private IEnumerator PlayOrbitalLandingSequence()
        {
            currentPhase = CutscenePhase.ArrivalLanding;
            ShipTouchdownPos = LandingPad;
            ShipTouchdownRot = Quaternion.identity;

            BindHoverCraft();
            LoadArrivalAudio();
            SetArrivalHudVisible(false);
            var titleCard = GotGTitleCardUI.GetOrCreate();
            if (titleCard != null) titleCard.SetLetterboxOnly(true, 0.8f);

            if (alitaHero != null) SetActorRenderersActive(alitaHero, false);
            if (starship != null)
            {
                var cols = starship.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
            }

            // Score and engine bed run underneath the whole arrival
            CrossfadeMusic(sfxArrivalMusic != null ? sfxArrivalMusic : (_arrivalMusic != null ? _arrivalMusic : combatMusic), 0.55f, 0.4f, 3.0f);
            StartEngineBed();

            // ── ORBIT ────────────────────────────────────────────────────────
            BeginSpaceStage();
            StartCoroutine(LogGlobeState());
            _overlayColor = Color.black; _overlayAlpha = 1f;

            // Shot 1 (11s): wide overview. The craft crosses in front of the red planet.
            if (titleCard != null)
            {
                titleCard.titleColor = new Color(1.0f, 0.45f, 0.15f);
                titleCard.titleShadowColor = new Color(0.85f, 0.12f, 0.25f, 0.85f);
                titleCard.subtitleColor = new Color(0.1f, 0.92f, 1.0f);
            }
            Vector3 shipA0 = SP(-300f, 14f, 400f);
            Vector3 shipA1 = SP(20f, 4f, 52f);
            Vector3 shipA2 = SP(170f, -14f, 120f);
            PlaceShip(shipA0, (shipA1 - shipA0).normalized, 0f);
            ClearShipTrails();
            bool titleShown = false, line1 = false;
            yield return RunTimed(11f, k =>
            {
                float t = k * 11f;
                _overlayAlpha = Mathf.Clamp01(1f - t / 2.2f);
                Vector3 p = k < 0.62f
                    ? Vector3.Lerp(shipA0, shipA1, Mathf.SmoothStep(0f, 1f, k / 0.62f))
                    : Vector3.Lerp(shipA1, shipA2, Mathf.SmoothStep(0f, 1f, (k - 0.62f) / 0.38f));
                Vector3 dir = k < 0.62f ? (shipA1 - shipA0) : (shipA2 - shipA1);
                PlaceShip(p, dir.normalized, 0f);
                SetThrust(0.55f + 0.2f * k, false);
                SetEngineBed(0.22f, 0.7f + 0.15f * k);

                Vector3 camPos = SP(0f, 0f, 0f) + _spF * (k * 24f);
                Vector3 look = Vector3.Lerp(p, SP(-20f, -6f, 260f), 0.45f);
                CamAt(camPos, look, 36f - 4f * k);

                if (!titleShown && t > 1.6f)
                {
                    titleShown = true;
                    if (titleCard != null) titleCard.ShowTitleCard("MARS", "CYDONIA BASIN // SECTOR 00", "ANCIENT URM EXCAVATION SITE  //  HIGH THREAT PROTOCOL  //  COORD 40.75N 9.46W", 4.2f);
                }
                if (!line1 && t > 5.2f)
                {
                    line1 = true;
                    TriggerCockpitTransmission("ALITA", "NIGHTSHADE // ORBITAL APPROACH",
                        "Mars. Everything the war left behind, still burning under that dust.", 4.2f);
                }
            });

            // Shot 2 (6s): chase cam. The craft banks toward the planet and lights its afterburners.
            Cue(sfxBoostIgnite, MechAudioSynth.GetThrusterIgnition(), 0.7f, 0.9f);
            Cue(sfxRocket, MechAudioSynth.GetMissileLaunch(), 0.8f, 0.75f); // rocket burn as the afterburners light
            TriggerCockpitTransmission("ALITA", "NIGHTSHADE // ORBITAL APPROACH",
                "The URM beacon is still broadcasting from Cydonia Basin. That is where the core is. Burning in.", 5.0f);
            Vector3 planetCenter = _spaceGlobe != null ? _spaceGlobe.transform.position : SP(0f, -3900f, 3000f);
            Vector3 chaseStart = shipA2;
            Vector3 chaseHead0 = (shipA2 - shipA1).normalized;
            Vector3 lastShipPos = chaseStart;
            yield return RunTimed(6f, k =>
            {
                float turn = Mathf.SmoothStep(0f, 1f, k);
                Vector3 toPlanet = (planetCenter - lastShipPos).normalized;
                Vector3 head = Vector3.Slerp(chaseHead0, toPlanet, turn).normalized;
                float speed = Mathf.Lerp(40f, 150f, k * k);
                lastShipPos += head * speed * Time.deltaTime;
                PlaceShip(lastShipPos, head, Mathf.Sin(turn * Mathf.PI) * -22f);
                SetThrust(Mathf.Lerp(0.8f, 1.65f, k), k > 0.25f);
                SetEngineBed(Mathf.Lerp(0.3f, 0.7f, k), Mathf.Lerp(0.9f, 1.4f, k));
                if (_entryGlow != null) _entryGlow.intensity = 14f * Mathf.InverseLerp(0.55f, 1f, k);

                Vector3 right = Vector3.Cross(Vector3.up, head).normalized;
                Vector3 camPos = lastShipPos - head * Mathf.Lerp(24f, 15f, k) + Vector3.up * 4.5f + right * 8f;
                CamAt(camPos, lastShipPos + head * 10f, Mathf.Lerp(42f, 66f, k));
                if (k > 0.55f) AddCameraTrauma(Time.deltaTime * 2.2f);
                if (k > 0.82f)
                {
                    _overlayColor = new Color(1f, 0.85f, 0.6f);
                    _overlayAlpha = Mathf.InverseLerp(0.82f, 1f, k);
                }
            });

            // ── ATMOSPHERIC ENTRY ────────────────────────────────────────────
            EndSpaceStage();
            Cue(sfxAtmosphereEntry, MechAudioSynth.GetSeismicSlam(), 0.55f, 0.6f);
            Cue(sfxRocket, MechAudioSynth.GetMissileLaunch(), 1.0f, 0.6f); // roaring rocket burn through the atmosphere
            if (dustStormSystem != null) dustStormSystem.SetStormIntensity(1.0f, 1.2f);
            Vector3[] path =
            {
                new Vector3(-70f, 360f, -560f),   // 0  high over the basin, in the burn
                new Vector3(5f, 52f, -250f),      // 1  levelling out
                new Vector3(5f, 19f, -112f),      // 2  passes the ground camera
                new Vector3(5f, 11f, -48f),       // 3  braking
                new Vector3(5f, 7f, LandingPad.z), // 4  hover above the pad
                new Vector3(5f, 0.1f, LandingPad.z), // 5 touchdown
            };
            float[] times = { 0f, 5.5f, 8.4f, 13.2f, 17.0f, 22.0f };
            const float totalFlight = 22.0f;

            PlaceShip(path[0], (path[1] - path[0]).normalized, 0f);
            ClearShipTrails();
            if (_entryGlow != null) _entryGlow.intensity = 16f;
            bool flybyPlayed = false, retroPlayed = false, landed = false, dustStarted = false, line3 = false;
            Vector3 groundCam = new Vector3(12f, 1.6f, -112f);
            yield return RunTimed(totalFlight, k =>
            {
                float t = k * totalFlight;
                Vector3 p = PathAt(path, times, t);
                Vector3 ahead = PathAt(path, times, Mathf.Min(totalFlight, t + 0.15f));
                Vector3 head = (ahead - p);
                if (head.sqrMagnitude < 0.0001f) head = Vector3.forward; else head.Normalize();
                // Flare: nose rises while the craft sheds speed, then levels for the touchdown
                float flare = Mathf.Sin(Mathf.InverseLerp(10.5f, 20.5f, t) * Mathf.PI) * -12f;
                PlaceShip(p, head, 0f, flare);

                // Thrust and glow: full burn in the entry, easing off as it brakes
                float burn = t < 8f ? 1.65f : Mathf.Lerp(1.65f, 0.3f, Mathf.InverseLerp(8f, 17f, t));
                SetThrust(Mathf.Clamp01(burn), t < 13f);
                if (_entryGlow != null) _entryGlow.intensity = Mathf.Lerp(16f, 0f, Mathf.InverseLerp(4f, 9f, t));
                SetEngineBed(Mathf.Lerp(0.75f, 0.35f, Mathf.InverseLerp(8f, 19f, t)), Mathf.Lerp(1.35f, 0.6f, Mathf.InverseLerp(7.5f, 18f, t)));
                if (t < 6f) AddCameraTrauma(Time.deltaTime * 1.4f);

                // Fade in from the entry flash
                _overlayColor = new Color(1f, 0.85f, 0.6f);
                _overlayAlpha = Mathf.Clamp01(1f - t / 0.9f);

                if (t < 4.6f)
                {
                    // Chase: above and behind, plasma streaming back over the hull
                    Vector3 right = Vector3.Cross(Vector3.up, head).normalized;
                    Vector3 camPos = p - head * 22f + Vector3.up * 6f - right * 9f;
                    CamAt(camPos, p + head * 8f, Mathf.Lerp(62f, 52f, t / 4.6f));
                }
                else if (t < 8.6f)
                {
                    // Ground camera: low in the dust, the craft screams in and over
                    Vector3 aim = p;
                    CamAt(groundCam, aim, Mathf.Lerp(40f, 58f, Mathf.InverseLerp(4.6f, 8.6f, t)));
                    if (!flybyPlayed && t > 6.4f)
                    {
                        flybyPlayed = true;
                        Cue(sfxFlyby, starshipFlybyAudio, 1.0f, 1f);
                        AddCameraTrauma(0.55f);
                    }
                }
                else if (t < 13.6f)
                {
                    // Side tracking as it brakes over the basin, the wreck sitting in the haze beyond
                    Vector3 camPos = new Vector3(28f, 3.4f, Mathf.Lerp(-70f, -34f, Mathf.InverseLerp(8.6f, 13.6f, t)));
                    CamAt(camPos, p + Vector3.up * 0.5f, 40f);
                    if (!retroPlayed && t > 10.4f)
                    {
                        retroPlayed = true;
                        Cue(sfxRetroBrake, MechAudioSynth.GetPneumaticRelease(), 0.7f, 0.8f);
                        Cue(sfxRocket, MechAudioSynth.GetMissileLaunch(), 0.9f, 0.85f); // retro-rockets firing to brake
                        AddCameraTrauma(0.3f);
                    }
                }
                else
                {
                    // Touchdown: low angle under the hull, dust rolling out across the pad
                    Vector3 camPos = new Vector3(14.5f, Mathf.Lerp(1.9f, 0.9f, Mathf.InverseLerp(13.4f, 22f, t)), -34.5f);
                    CamAt(camPos, p + Vector3.up * 1.4f, 44f);
                    if (!dustStarted && t > 17.5f) { dustStarted = true; StartLandingDust(); }
                    if (!landed && t > 21.6f)
                    {
                        landed = true;
                        AddCameraTrauma(0.75f);
                        if (sfxTouchdown != null) Sfx2D(sfxTouchdown, 1f, 1f);
                        else
                        {
                            Sfx2D(MechAudioSynth.GetSeismicSlam(), 0.55f, 1.15f);
                            Sfx2D(MechAudioSynth.GetPneumaticRelease(), 0.9f, 1f);
                        }
                    }
                }

                if (!line3 && t > 9.2f)
                {
                    line3 = true;
                    TriggerCockpitTransmission("ALITA", "NIGHTSHADE // LANDING",
                        "Dust is thick down here. Putting her down beside the basin.", 3.6f);
                }
            });

            // ── ON THE GROUND ────────────────────────────────────────────────
            PlaceShip(LandingPad + new Vector3(0f, 0f, 0f), Vector3.forward, 0f);
            SetThrust(0.15f, false);
            SetEngineBed(0.12f, 0.6f);
            AddCameraTrauma(0.2f);

            // Shot: canopy releases with a hiss of steam, the cockpit light spilling out
            if (sfxCanopyOpen != null) Sfx2D(sfxCanopyOpen, 1f, 1f);
            else
            {
                Sfx2D(MechAudioSynth.GetPneumaticRelease(), 0.8f, 0.9f);
                Sfx2D(MechAudioSynth.GetServoWhine(), 0.5f, 0.8f);
            }
            Vector3 aliSeat = ShipLocal(CockpitSeatLocal);
            if (alitaHero != null)
            {
                alitaHero.transform.position = aliSeat;
                alitaHero.transform.rotation = Quaternion.identity;
                SetActorRenderersActive(alitaHero, true);
                if (_alitaAnimator != null) { _alitaAnimator.SetFloat("MoveSpeed", 0f); _alitaAnimator.Play("Locomotion", 0, 0f); }
            }
            Vector3 camOpenA = LandingPad + new Vector3(8.8f, 1.0f, 12.0f);
            Vector3 camOpenB = LandingPad + new Vector3(6.2f, 1.7f, 10.0f);
            yield return RunTimed(4.2f, k =>
            {
                if (_canopy != null)
                    _canopy.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -62f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.7f, k))), 0f, 0f);
                CamAt(Vector3.Lerp(camOpenA, camOpenB, Mathf.SmoothStep(0f, 1f, k)),
                    ShipLocal(new Vector3(0f, 1.6f, 1.6f)), Mathf.Lerp(38f, 32f, k));
            });

            // Shot: Alita climbs out over the nose and drops to the ground
            Vector3 groundPos = ShipLocal(CockpitGroundLocal);
            groundPos.y = AlitaStandPos.y;
            Vector3 rimPos = ShipLocal(CockpitRimLocal);
            Vector3 camHop = LandingPad + new Vector3(10.5f, 1.5f, 6.0f);
            bool jumped = false, landedHop = false;
            yield return RunTimed(2.6f, k =>
            {
                float t = k * 2.6f;
                if (alitaHero != null)
                {
                    if (t < 0.9f)
                    {
                        alitaHero.transform.position = Vector3.Lerp(aliSeat, rimPos, Mathf.SmoothStep(0f, 1f, t / 0.9f));
                    }
                    else
                    {
                        float u = Mathf.Clamp01((t - 0.9f) / 0.75f);
                        Vector3 pos = Vector3.Lerp(rimPos, groundPos, u);
                        pos.y += Mathf.Sin(u * Mathf.PI) * 0.7f;
                        alitaHero.transform.position = pos;
                        if (!jumped) { jumped = true; if (_alitaAnimator != null) _alitaAnimator.CrossFadeInFixedTime("Jump", 0.08f); }
                        if (u >= 1f && !landedHop)
                        {
                            landedHop = true;
                            if (_alitaAnimator != null) _alitaAnimator.CrossFadeInFixedTime("Land", 0.05f);
                            Sfx2D(_bootsClip, 0.7f, 0.9f);
                            AddCameraTrauma(0.25f);
                        }
                    }
                }
                CamAt(camHop, alitaHero != null ? alitaHero.transform.position + Vector3.up * 1.0f : groundPos, 40f);
            });

            // Shot: she straightens and walks toward camera, the grounded craft behind her
            Vector3 walkA = groundPos, walkB = AlitaStandPos;
            if (_alitaAnimator != null) { _alitaAnimator.CrossFadeInFixedTime("Locomotion", 0.2f); _alitaAnimator.SetFloat("MoveSpeed", 0.5f); }
            Vector3 camWalk = new Vector3(9.2f, 1.25f, -9.0f);
            float nextStep = 0.4f;
            yield return RunTimed(4.6f, k =>
            {
                float t = k * 4.6f;
                if (alitaHero != null)
                {
                    alitaHero.transform.position = Vector3.Lerp(walkA, walkB, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 3.6f)));
                    if (t > nextStep && t < 3.6f) { nextStep += 0.55f; Sfx2D(_bootsClip, 0.18f, 1.25f); }
                }
                Vector3 look = (alitaHero != null ? alitaHero.transform.position : walkB) + Vector3.up * 1.15f;
                CamAt(camWalk + new Vector3(-k * 1.2f, 0f, k * 1.0f), look, Mathf.Lerp(34f, 28f, k));
                SetEngineBed(Mathf.Lerp(0.12f, 0.04f, k), 0.6f);
            });
            if (_alitaAnimator != null) _alitaAnimator.SetFloat("MoveSpeed", 0f);
            if (_alitaAnimator != null) _alitaAnimator.CrossFadeInFixedTime("Draw_Sword", 0.15f);
            if (bladeUnsheathAudio != null) Sfx2D(bladeUnsheathAudio, 0.7f, 1f);

            // Shot: close hero frame while she looks toward the wreck
            TriggerCockpitTransmission("ALITA", "CYDONIA BASIN",
                "The signal is coming from the wreck up ahead. Whatever is waiting there, I'm ready.", 4.4f);
            Vector3 camHeroA = AlitaStandPos + new Vector3(1.6f, 1.45f, 2.6f);
            Vector3 camHeroB = AlitaStandPos + new Vector3(0.9f, 1.55f, 1.9f);
            yield return RunTimed(4.6f, k =>
            {
                Vector3 look = AlitaStandPos + new Vector3(0f, 1.45f, 0f);
                CamAt(Vector3.Lerp(camHeroA, camHeroB, Mathf.SmoothStep(0f, 1f, k)), look, Mathf.Lerp(30f, 24f, k));
            });

            FinishArrivalStage(false);
        }

        // ═════════════════════════════════════════════════════════════════════
        // STAGE / CLEANUP
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Finds the craft's parts and gives it the same plasma thruster rig the flyable Nightshade uses.</summary>
        private void BindHoverCraft()
        {
            if (starship == null) return;
            starship.SetActive(true);
            _canopy = FindDeep(starship.transform, "SM_Veh_HoverCraft_01_Door_01");
            if (_canopy != null) _canopy.localRotation = Quaternion.identity;

            _shipThrusters = starship.GetComponent<AAAStarshipThrusterSystem>();
            if (_shipThrusters == null) _shipThrusters = starship.AddComponent<AAAStarshipThrusterSystem>();

            if (_entryGlow == null)
            {
                var go = new GameObject("Entry_Plasma_Glow");
                go.transform.SetParent(starship.transform, false);
                go.transform.localPosition = new Vector3(0f, 1.6f, 4.2f);
                _entryGlow = go.AddComponent<Light>();
                _entryGlow.type = LightType.Point;
                _entryGlow.color = new Color(1f, 0.5f, 0.18f);
                _entryGlow.range = 55f;
                _entryGlow.intensity = 0f;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private void LoadArrivalAudio()
        {
#if UNITY_EDITOR
            if (_engineLoopClip == null) _engineLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Chassis_Reactor_Loop.wav");
            if (_bootsClip == null) _bootsClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Jump_Landing_Boots_02.wav");
            if (_arrivalMusic == null) _arrivalMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Tears_in_the_Rain.mp3");
#endif
        }

        private void BeginSpaceStage()
        {
            if (mainCamera == null) return;
            _stageActive = true;
            _prevSkybox = RenderSettings.skybox;
            _prevFog = RenderSettings.fog;
            _prevClear = mainCamera.clearFlags;
            _prevFar = mainCamera.farClipPlane;
            _prevBackground = mainCamera.backgroundColor;

#if UNITY_EDITOR
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonSciFiSpace/Materials/Misc/PolygonScifiSpace_Skybox_01.mat");
            if (sky != null) RenderSettings.skybox = sky;
#endif
            RenderSettings.fog = false;
            // The scene's Martian colour grade tints everything orange, which flattens the planet's own surface colours
            var gv = GameObject.Find("Global Volume");
            _groundGrade = gv != null ? gv.GetComponent<UnityEngine.Rendering.Volume>() : null;
            if (_groundGrade != null) _groundGrade.enabled = false;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.farClipPlane = 25000f;

            // Dust volumes follow the camera, so they would foul the vacuum
            _hiddenForSpace = new[] { GameObject.Find("Camera_Martian_Dust_Volume"), GameObject.Find("Atmospheric_Dust_Emitters") };
            for (int i = 0; i < _hiddenForSpace.Length; i++) if (_hiddenForSpace[i] != null) _hiddenForSpace[i].SetActive(false);

            // Every other particle system in the scene (storm dust, embers) would float in the vacuum
            _hiddenParticles.Clear();
            foreach (var pr in FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!pr.enabled) continue;
                if (starship != null && pr.transform.IsChildOf(starship.transform) && pr.name.StartsWith("AAA_")) continue;
                pr.enabled = false;
                _hiddenParticles.Add(pr);
            }

            // The ground scene's own sky dome and moon are huge orange meshes that would cut across the vacuum
            foreach (var n in new[] { "Martian_SkyDome", "Celestial_Phobos" })
            {
                var o = GameObject.Find(n);
                if (o == null) continue;
                foreach (var rd in o.GetComponentsInChildren<Renderer>())
                {
                    if (!rd.enabled) continue;
                    rd.enabled = false;
                    _hiddenRenderers.Add(rd);
                }
            }

            // View frame: the sun sits behind the camera, rotated so the planet shows a lit gibbous with a terminator
            var sun = GameObject.Find("Martian_Sun");
            Vector3 sunFwd = sun != null ? sun.transform.forward : new Vector3(-0.45f, -0.62f, 0.65f);
            _spF = (Quaternion.AngleAxis(58f, Vector3.up) * sunFwd).normalized;
            _spR = Vector3.Cross(Vector3.up, _spF).normalized;
            _spU = Vector3.Cross(_spF, _spR).normalized;

            // Planet radius 3200 (mesh diameter 1, scale 6400); its limb sits about 12 degrees below the view axis
            _spaceGlobe = new GameObject("Arrival_Mars_Globe");
            _spaceGlobe.transform.position = SpaceStage + _spF * 2751f - _spU * 3279f;
            _spaceGlobe.transform.localScale = Vector3.one * 6400f;
            // Stand the pole axis across the view so the camera looks at the equatorial band (canyons, basalt plains)
            // rather than the smooth polar region the old tilt pointed at.
            Vector3 toCam = (SpaceStage - _spaceGlobe.transform.position).normalized;
            _globeAxis = Vector3.Cross(_spR, toCam).normalized;
            if (Vector3.Dot(_globeAxis, _spU) < 0f) _globeAxis = -_globeAxis;
            _spaceGlobe.transform.rotation = Quaternion.FromToRotation(Vector3.up, _globeAxis);
            var mf = _spaceGlobe.AddComponent<MeshFilter>();
            var mr = _spaceGlobe.AddComponent<MeshRenderer>();
#if UNITY_EDITOR
            var sphere = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Environments/Mesh_HighDef_Planet_Sphere.asset");
            if (sphere != null) mf.sharedMesh = sphere;
            // Unlit so the scene's grade and grazing sun cannot wash the surface map out
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(shader) { name = "Mat_Arrival_Mars" };
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Planets/Mars_Realistic_Albedo_2K.jpg");
            if (albedo != null)
            {
                var tex = BuildEnhancedAlbedo(albedo);
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
            }
            mat.SetColor("_BaseColor", new Color(1.05f, 0.98f, 0.92f));
            mr.sharedMaterial = mat;
#endif
            AimGlobeAtU(0.43f);
        }

        /// <summary>Tileable multi-octave noise centred on mid-grey so it modulates (x2) the albedo without shifting its colour.</summary>
        private static Texture2D BuildSurfaceDetailTexture()
        {
            const int n = 512;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Mars_SurfaceDetail" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n, amp = 0.5f, sum = 0f, norm = 0f;
                    for (int o = 0; o < 5; o++)
                    {
                        float f = 4f * (1 << o);
                        // Blend four offset samples across the edges so the tile has no seam
                        float a = Mathf.PerlinNoise(u * f, v * f);
                        float b = Mathf.PerlinNoise((u - 1f) * f, v * f);
                        float c = Mathf.PerlinNoise(u * f, (v - 1f) * f);
                        float d = Mathf.PerlinNoise((u - 1f) * f, (v - 1f) * f);
                        float val = Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
                        sum += val * amp; norm += amp; amp *= 0.5f;
                    }
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0.12f, 0.88f, sum / norm) * 255f), 0, 255);
                    px[y * n + x] = new Color32(g, g, g, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// Copies the planet map through a render target and pushes its contrast and saturation, so the dark basalt
        /// plains, canyons and craters read clearly instead of averaging out into one orange.
        /// </summary>
        private static Texture2D BuildEnhancedAlbedo(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { name = "Mars_Albedo_Enhanced", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float rr = px[i].r / 255f, gg = px[i].g / 255f, bb = px[i].b / 255f;
                float lum = rr * 0.3f + gg * 0.59f + bb * 0.11f;
                // saturation up, then contrast about a darker mid so shadows deepen
                rr = lum + (rr - lum) * 1.05f; gg = lum + (gg - lum) * 1.05f; bb = lum + (bb - lum) * 1.05f;
                rr = (rr - 0.40f) * 1.45f + 0.40f; gg = (gg - 0.40f) * 1.45f + 0.40f; bb = (bb - 0.40f) * 1.45f + 0.40f;
                px[i] = new Color32((byte)Mathf.Clamp(rr * 255f, 0, 255), (byte)Mathf.Clamp(gg * 255f, 0, 255), (byte)Mathf.Clamp(bb * 255f, 0, 255), 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        private static Material _marsSky;

        /// <summary>Dusty red-orange Martian sky for the ground scene (the stock default skybox is blue).</summary>
        private static Material GetMartianSky(Material fallback)
        {
            if (_marsSky != null) return _marsSky;
            var sh = Shader.Find("Skybox/Procedural");
            if (sh == null) return fallback;
            _marsSky = new Material(sh) { name = "Mat_Martian_Sky" };
            _marsSky.SetColor("_SkyTint", new Color(0.92f, 0.38f, 0.2f));
            _marsSky.SetColor("_GroundColor", new Color(0.42f, 0.18f, 0.1f));
            _marsSky.SetFloat("_AtmosphereThickness", 2.6f);
            _marsSky.SetFloat("_Exposure", 1.15f);
            _marsSky.SetFloat("_SunSize", 0.035f);
            _marsSky.SetFloat("_SunSizeConvergence", 5f);
            return _marsSky;
        }

        /// <summary>Longitude (UV u) of the surface point currently facing the camera stage.</summary>
        private float FacingU()
        {
            var mesh = _spaceGlobe.GetComponent<MeshFilter>().sharedMesh;
            Vector3 local = _spaceGlobe.transform.InverseTransformPoint(SpaceStage).normalized;
            var verts = mesh.vertices; var uvs = mesh.uv;
            int best = 0; float bestDot = -2f;
            for (int i = 0; i < verts.Length; i++)
            {
                float d = Vector3.Dot(verts[i].normalized, local);
                if (d > bestDot) { bestDot = d; best = i; }
            }
            return uvs[best].x;
        }

        /// <summary>Spins the globe so the chosen map longitude (0..1) faces the camera, e.g. the dark Syrtis Major basalt.</summary>
        private void AimGlobeAtU(float targetU)
        {
            if (_spaceGlobe == null) return;
            float u0 = FacingU();
            _spaceGlobe.transform.Rotate(_globeAxis, 10f, Space.World);
            float u1 = FacingU();
            _spaceGlobe.transform.Rotate(_globeAxis, -10f, Space.World);
            float du = Mathf.DeltaAngle(u0 * 360f, u1 * 360f) / 360f; // per 10 degrees
            if (Mathf.Abs(du) < 0.0001f) return;
            float need = Mathf.DeltaAngle(u0 * 360f, targetU * 360f) / 360f;
            _spaceGlobe.transform.Rotate(_globeAxis, 10f * need / du, Space.World);
        }

        private IEnumerator LogGlobeState()
        {
            yield return null; yield return null;
            var sb = new System.Text.StringBuilder("[MarsArrival] globe diag: ");
            if (_spaceGlobe != null)
            {
                var mr = _spaceGlobe.GetComponent<MeshRenderer>();
                sb.Append("globeVisible=").Append(mr.isVisible).Append(" shader=").Append(mr.sharedMaterial.shader.name)
                  .Append(" tex=").Append(mr.sharedMaterial.GetTexture("_BaseMap") != null ? mr.sharedMaterial.GetTexture("_BaseMap").name : "NULL");
                var t2 = mr.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
                if (t2 != null)
                {
                    sb.Append(" px:");
                    foreach (float u in new[] { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f }) sb.Append(' ').Append(t2.GetPixelBilinear(u, 0.5f).ToString("F2"));
                }
                var mf2 = _spaceGlobe.GetComponent<MeshFilter>();
                sb.Append(" meshUV0=").Append(mf2.sharedMesh.uv[0]).Append(" uvMid=").Append(mf2.sharedMesh.uv[mf2.sharedMesh.uv.Length / 2]);
            }
            else sb.Append("NO GLOBE");
            foreach (var rd in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (rd.enabled && rd.isVisible && rd.bounds.size.magnitude > 1500f) sb.Append(" | big visible: ").Append(rd.name).Append(" size=").Append((int)rd.bounds.size.magnitude);
            Debug.Log(sb.ToString());
        }

        private void EndSpaceStage()
        {
            if (!_stageActive) return;
            _stageActive = false;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = _prevClear;
                mainCamera.farClipPlane = _prevFar;
                mainCamera.backgroundColor = _prevBackground;
            }
            RenderSettings.skybox = GetMartianSky(_prevSkybox);
            DynamicGI.UpdateEnvironment();
            RenderSettings.fog = _prevFog;
            if (_groundGrade != null) _groundGrade.enabled = true;
            if (_spaceGlobe != null) Destroy(_spaceGlobe);
                        _spaceGlobe = null; _rimQuad = null;
            for (int i = 0; i < _hiddenParticles.Count; i++) if (_hiddenParticles[i] != null) _hiddenParticles[i].enabled = true;
            _hiddenParticles.Clear();
            for (int i = 0; i < _hiddenRenderers.Count; i++) if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
            _hiddenRenderers.Clear();
            if (_hiddenForSpace != null)
                for (int i = 0; i < _hiddenForSpace.Length; i++) if (_hiddenForSpace[i] != null) _hiddenForSpace[i].SetActive(true);
        }

        /// <summary>Safe to call more than once (the skip path calls it too). Leaves Alita beside her landed craft.</summary>
        private void FinishArrivalStage(bool skipped)
        {
            EndSpaceStage();
            _overlayAlpha = 0f;
            SetArrivalHudVisible(true);
            var titleCard = GotGTitleCardUI.GetOrCreate();
            if (titleCard != null) titleCard.SetLetterboxOnly(false, 0.7f);

            if (starship != null)
            {
                starship.transform.SetPositionAndRotation(LandingPad, Quaternion.identity);
                var cols = starship.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = true;
                if (_canopy != null) _canopy.localRotation = Quaternion.Euler(-62f, 0f, 0f);
            }
            if (_shipThrusters != null) { _shipThrusters.currentThrottleFactor = 0.15f; _shipThrusters.isBoosting = false; }
            if (_entryGlow != null) _entryGlow.intensity = 0f;

            if (alitaHero != null)
            {
                alitaHero.transform.SetPositionAndRotation(AlitaStandPos, Quaternion.identity);
                SetActorRenderersActive(alitaHero, true);
                if (_alitaAnimator != null) _alitaAnimator.SetFloat("MoveSpeed", 0f);
            }
            if (dustStormSystem != null) dustStormSystem.SetStormIntensity(0.45f, 2f);
            if (_shipEngine != null) { StartCoroutine(MarsSfx.FadeOutAndStop(_shipEngine, 2.0f)); }
            PlayPostLandingAudio();
        }

        private static void SetArrivalHudVisible(bool visible)
        {
            foreach (var name in new[] { "Canvas_InGameHUD", "Canvas_Hub_Inventory" })
            {
                var go = GameObject.Find(name);
                var canvas = go != null ? go.GetComponent<Canvas>() : null;
                if (canvas != null) canvas.enabled = visible;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator RunTimed(float duration, System.Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
        }

        /// <summary>Rotation first, then position, so the shake's rotational jitter survives.</summary>
        private void CamAt(Vector3 pos, Vector3 lookAt, float fov)
        {
            if (mainCamera == null) return;
            if (_stageActive && _spaceGlobe != null) _spaceGlobe.transform.Rotate(_globeAxis, 1.6f * Time.deltaTime, Space.World); // slow planetary spin
            Vector3 d = lookAt - pos;
            if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
            mainCamera.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            mainCamera.transform.position = ApplyCameraShake(pos);
            mainCamera.fieldOfView = fov;
        }

        /// <summary>Teleporting the craft would otherwise leave a long ribbon streaking back to where it came from.</summary>
        private void ClearShipTrails()
        {
            if (starship == null) return;
            foreach (var tr in starship.GetComponentsInChildren<TrailRenderer>()) tr.Clear();
        }

        private void PlaceShip(Vector3 pos, Vector3 heading, float roll, float pitchOffset = 0f)
        {
            if (starship == null) return;
            if (heading.sqrMagnitude < 0.0001f) heading = Vector3.forward;
            starship.transform.position = pos;
            starship.transform.rotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(pitchOffset, 0f, roll);
        }

        private Vector3 ShipLocal(Vector3 local)
        {
            return starship != null ? starship.transform.TransformPoint(local) : LandingPad + local;
        }

        private void SetThrust(float throttle, bool boost)
        {
            if (_shipThrusters == null) return;
            _shipThrusters.currentThrottleFactor = throttle;
            _shipThrusters.isBoosting = boost;
        }

        private void StartLandingDust()
        {
            if (starship == null) return;
#if UNITY_EDITOR
            var dust = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Ship_Hover_Dust_01.prefab");
            if (dust != null)
            {
                var d = Instantiate(dust, LandingPad + Vector3.up * 0.1f, Quaternion.identity);
                d.transform.localScale = Vector3.one * 2.2f;
                Destroy(d, 14f);
            }
            var big = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Dust_Big_01.prefab");
            if (big != null)
            {
                var d = Instantiate(big, LandingPad + Vector3.up * 0.2f, Quaternion.identity);
                d.transform.localScale = Vector3.one * 2.0f;
                Destroy(d, 12f);
            }
#endif
            if (armyManager != null) armyManager.SpawnPodLandingDustRing(LandingPad);
        }

        private void StartEngineBed()
        {
            if (_engineLoopClip == null && sfxEngineLoop == null) return;
            if (_shipEngine == null) _shipEngine = gameObject.AddComponent<AudioSource>();
            _shipEngine.clip = sfxEngineLoop != null ? sfxEngineLoop : _engineLoopClip;
            _shipEngine.loop = true;
            _shipEngine.volume = 0f;
            _shipEngine.pitch = 0.7f;
            _shipEngine.Play();
        }

        private void SetEngineBed(float volume, float pitch)
        {
            if (_shipEngine == null) return;
            _shipEngine.volume = Mathf.Lerp(_shipEngine.volume, volume, Time.deltaTime * 3f);
            _shipEngine.pitch = Mathf.Lerp(_shipEngine.pitch, pitch, Time.deltaTime * 3f);
        }

        /// <summary>Plays the user's override clip if one is assigned, otherwise the built-in fallback.</summary>
        private static void Cue(AudioClip custom, AudioClip fallback, float volume, float pitch)
        {
            if (custom != null) Sfx2D(custom, 1f, 1f);
            else Sfx2D(fallback, volume, pitch);
        }

        private static void Sfx2D(AudioClip clip, float volume, float pitch)
        {
            MarsSfx.Play(clip, volume, pitch);
        }

        /// <summary>Catmull-Rom style curve through time-keyed points, so the flight has no kinks.</summary>
        private static Vector3 PathAt(Vector3[] pts, float[] times, float t)
        {
            int n = pts.Length;
            if (t <= times[0]) return pts[0];
            if (t >= times[n - 1]) return pts[n - 1];
            int i = 0;
            while (i < n - 2 && t > times[i + 1]) i++;
            float t0 = times[i], t1 = times[i + 1];
            float u = (t - t0) / (t1 - t0);
            Vector3 p0 = pts[i], p1 = pts[i + 1];
            Vector3 m0 = i > 0 ? (pts[i + 1] - pts[i - 1]) / (times[i + 1] - times[i - 1]) : Vector3.zero;
            Vector3 m1 = i + 2 < n ? (pts[i + 2] - pts[i]) / (times[i + 2] - times[i]) : Vector3.zero;
            float dt = t1 - t0;
            float u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * p0 + (u3 - 2f * u2 + u) * dt * m0 + (-2f * u3 + 3f * u2) * p1 + (u3 - u2) * dt * m1;
        }

        /// <summary>Full-screen colour wash used for the entry flash and fades. Called from OnGUI.</summary>
        private void DrawArrivalOverlay()
        {
            if (_overlayAlpha <= 0.002f) return;
            var prev = GUI.color;
            GUI.color = new Color(_overlayColor.r, _overlayColor.g, _overlayColor.b, _overlayAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
