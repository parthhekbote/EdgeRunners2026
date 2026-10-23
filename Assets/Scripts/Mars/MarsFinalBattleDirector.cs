using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PolygonMech;

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Master Encounter Director for the Final Martian Boss Battle.
    /// Manages:
    /// - Cinematic Title Card Intro: "MARS // CYDONIA BASIN // TITAN SIEGE"
    /// - Boss state HUD warnings (Cyclone Spin, Seismic Quake, Hydraulic Punish Window)
    /// - Dynamic Combat BGM and Martian dust storm audio
    /// - Camera Shake on heavy hammer slams and shockwaves
    /// - Victory condition and cinematic outro
    /// </summary>
    public class MarsFinalBattleDirector : MonoBehaviour
    {
        public static MarsFinalBattleDirector Instance { get; private set; }

        [Header("Combatants")]
        public HammerMechAI bossAI;
        public Transform playerTransform;

        [Header("Audio & Ambience")]
        public AudioClip combatMusic;
        public AudioClip dustStormAmbient;
        public AudioClip sfxAlarmWarning;
        private AudioSource musicSource;
        private AudioSource ambientSource;
        private AudioSource sfxSource;

        [Header("Atmospheric Controls")]
        public ParticleSystem redDustStormFX;
        public Light martianSunLight;

        // UI State
        private float introTimer = 0f;
        private const float IntroDuration = 5.0f;
        private bool isIntroActive = true;
        private string tacticalAlertText = "";
        private float tacticalAlertTimer = 0f;
        private MechCombatState lastLoggedBossState = MechCombatState.Spawning;

        // GUI Styles
        private GUIStyle titleHeaderStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle missionBriefStyle;
        private GUIStyle tacticalAlertStyle;
        private GUIStyle boxBackdropStyle;
        private bool stylesInitialized = false;

        private void Awake()
        {
            Instance = this;

            // Audio Sources
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0.55f;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.volume = 0.35f;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        private void Start()
        {
            if (bossAI == null) bossAI = FindAnyObjectByType<HammerMechAI>();
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

#if UNITY_EDITOR
            if (combatMusic == null)
            {
                combatMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Metal_Heart.mp3");
            }
#endif
            if (sfxAlarmWarning == null)
            {
                sfxAlarmWarning = MechAudioSynth.GetOverdriveAlarm();
            }

            // Check if Mars Arrival Cutscene Director is active
            var cutscene = MarsArrivalCutsceneDirector.Instance ?? FindFirstObjectByType<MarsArrivalCutsceneDirector>();
            if (cutscene != null && (cutscene.playOnStart || MarsArrivalCutsceneDirector.ForcePlayCutsceneOnSceneLoad))
            {
                isIntroActive = false;
                introTimer = 0f;
                return;
            }

            StartCombatEncounter();
        }

        public void StartCombatEncounter()
        {
            // The arrival/invasion director owns the music (crossfaded); this track is only for scenes without it
            bool cutsceneOwnsMusic = MarsArrivalCutsceneDirector.Instance != null;
            if (!cutsceneOwnsMusic && combatMusic != null && !musicSource.isPlaying)
            {
                musicSource.clip = combatMusic;
                musicSource.Play();
            }

            if (dustStormAmbient != null && !ambientSource.isPlaying)
            {
                ambientSource.clip = dustStormAmbient;
                ambientSource.Play();
            }

            introTimer = IntroDuration;
            isIntroActive = true;
        }

        private void Update()
        {
            if (isIntroActive)
            {
                introTimer -= Time.deltaTime;
                if (introTimer <= 0f) isIntroActive = false;
            }

            if (tacticalAlertTimer > 0f)
            {
                tacticalAlertTimer -= Time.deltaTime;
                if (tacticalAlertTimer <= 0f) tacticalAlertText = "";
            }

            // Monitor Boss State for Tactical HUD Alerts
            if (bossAI != null && bossAI.currentState != lastLoggedBossState)
            {
                lastLoggedBossState = bossAI.currentState;
                OnBossStateChanged(lastLoggedBossState);
            }
        }

        private void OnBossStateChanged(MechCombatState newState)
        {
            switch (newState)
            {
                case MechCombatState.SeismicQuake:
                    TriggerAlert("[!] SEISMIC WARNING: GROUND SHOCKWAVE PROPAGATING [!]", 2.2f, true);
                    break;
                case MechCombatState.LeapGroundSlam:
                    TriggerAlert("[!] BALLISTIC ALERT: ROCKET-ASSISTED LEAP SLAM [!]", 2.0f, true);
                    break;
                case MechCombatState.HydraulicVenting:
                    TriggerAlert("[VULNERABLE] HYDRAULIC OVERHEAT: VENTING STEAM - PUNISH NOW!", 1.5f, false);
                    break;
                case MechCombatState.DefensiveGuard:
                    TriggerAlert("[DEFENSE] HAMMER GUARD BRACED - PREPARE FOR COUNTER", 1.2f, false);
                    break;
                case MechCombatState.Staggered:
                    TriggerAlert("[SYSTEM FAULT] POISE BROKEN // MECH MALFUNCTIONING", 1.8f, false);
                    break;
                case MechCombatState.Dead:
                    TriggerAlert("TARGET DESTROYED // MARS CYDONIA EXCAVATION SECURED", 6.0f, false);
                    break;
            }
        }

        public void TriggerAlert(string alert, float duration, bool playBeep)
        {
            tacticalAlertText = alert;
            tacticalAlertTimer = duration;
            if (playBeep && sfxAlarmWarning != null)
            {
                sfxSource.PlayOneShot(sfxAlarmWarning, 0.7f);
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            titleHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleHeaderStyle.normal.textColor = new Color(1f, 0.42f, 0.2f); // Mars Burnt Orange

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            subHeaderStyle.normal.textColor = new Color(0f, 0.88f, 1f); // Neon Cyan

            missionBriefStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            missionBriefStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f, 0.9f);

            tacticalAlertStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            boxBackdropStyle = new GUIStyle(GUI.skin.box);
            boxBackdropStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.03f, 0.02f, 0.88f));

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            InitStyles();

            // 1. Mission Intro Cinematic Card (First 5 seconds)
            if (isIntroActive)
            {
                float alpha = Mathf.Clamp01(introTimer / 1.0f);
                float cardW = 680f;
                float cardH = 100f;
                float cardX = (Screen.width - cardW) * 0.5f;
                float cardY = 85f;

                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, boxBackdropStyle);
                GUI.Label(new Rect(cardX, cardY + 8, cardW, 28), "OPERATION RED HORIZON // MARS CYDONIA BASIN", titleHeaderStyle);
                GUI.Label(new Rect(cardX, cardY + 38, cardW, 20), "URM ANCIENT VAULT EXCAVATION SITE - SECTOR 00", subHeaderStyle);
                GUI.Label(new Rect(cardX, cardY + 62, cardW, 22), "DIRECTIVE: NEUTRALIZE COLOSSUS-06 TITAN WAR MECH // RECOVER ANTIMATTER CORE", missionBriefStyle);

                GUI.color = prevColor;
            }

            // 2. Real-time Tactical Move Alert Banner
            if (!string.IsNullOrEmpty(tacticalAlertText))
            {
                bool isDanger = tacticalAlertText.Contains("THREAT") || tacticalAlertText.Contains("WARNING") || tacticalAlertText.Contains("ALERT");
                tacticalAlertStyle.normal.textColor = isDanger ? new Color(1f, 0.25f, 0.15f) : new Color(0.2f, 1f, 0.45f);

                float bannerW = 600f;
                float bannerH = 34f;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = Screen.height - 85f;

                GUI.Box(new Rect(bannerX, bannerY, bannerW, bannerH), GUIContent.none, boxBackdropStyle);
                GUI.Label(new Rect(bannerX, bannerY + 6, bannerW, 24), tacticalAlertText, tacticalAlertStyle);
            }
        }
    }
}
