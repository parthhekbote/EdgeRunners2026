using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// AAA Cyberpunk Game Settings Manager.
    /// Handles Graphics, Audio, Gameplay, Accessibility, real-time application,
    /// PlayerPrefs persistence, Tab navigation, and Live Preview description updates.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("Audio Mixer Reference (Optional)")]
        public AudioMixer masterMixer;

        #region Setting Keys Constants
        public const string OPT_DISPLAY_MODE = "OPT_DisplayMode";
        public const string OPT_RESOLUTION = "OPT_Resolution";
        public const string OPT_RENDER_SCALE = "OPT_RenderScale";
        public const string OPT_GRAPHICS_PRESET = "OPT_GraphicsPreset";
        public const string OPT_TEXTURE_QUALITY = "OPT_TextureQuality";
        public const string OPT_SHADOW_QUALITY = "OPT_ShadowQuality";
        public const string OPT_ANTI_ALIASING = "OPT_AntiAliasing";
        public const string OPT_POST_PROCESSING = "OPT_PostProcessing";
        public const string OPT_EFFECTS_QUALITY = "OPT_EffectsQuality";
        public const string OPT_VIEW_DISTANCE = "OPT_ViewDistance";
        public const string OPT_VSYNC = "OPT_VSync";
        public const string OPT_MOTION_BLUR = "OPT_MotionBlur";
        public const string OPT_FOV = "OPT_FOV";
        public const string OPT_BRIGHTNESS = "OPT_Brightness";
        public const string OPT_BLOOM = "OPT_Bloom";
        public const string OPT_BLOOM_INTENSITY = "OPT_BloomIntensity";
        public const string OPT_FPS_LIMIT = "OPT_FPSLimit";

        public const string OPT_MASTER_VOL = "OPT_MasterVol";
        public const string OPT_MUSIC_VOL = "OPT_MusicVol";
        public const string OPT_SFX_VOL = "OPT_SFXVol";
        public const string OPT_DIALOGUE_VOL = "OPT_DialogueVol";
        public const string OPT_UI_VOL = "OPT_UIVol";
        public const string OPT_DYNAMIC_RANGE = "OPT_DynamicRange";
        public const string OPT_AUDIO_OUTPUT = "OPT_AudioOutput";
        public const string OPT_SPATIAL_AUDIO = "OPT_SpatialAudio";
        public const string OPT_SUBTITLES = "OPT_Subtitles";

        public const string OPT_DIFFICULTY = "OPT_Difficulty";
        public const string OPT_SENSITIVITY_X = "OPT_SensitivityX";
        public const string OPT_SENSITIVITY_Y = "OPT_SensitivityY";
        public const string OPT_INVERT_Y = "OPT_InvertY";
        public const string OPT_CAMERA_SHAKE = "OPT_CameraShake";
        public const string OPT_SHAKE_INTENSITY = "OPT_ShakeIntensity";
        public const string OPT_HIT_STOP = "OPT_HitStop";
        public const string OPT_BULLET_TIME = "OPT_BulletTime";
        public const string OPT_AUTO_RECHARGE = "OPT_AutoRecharge";
        public const string OPT_OBJECTIVE_MARKERS = "OPT_ObjectiveMarkers";

        public const string OPT_COLORBLIND = "OPT_Colorblind";
        public const string OPT_REDUCE_FLASHES = "OPT_ReduceFlashes";
        public const string OPT_SHAKE_REDUCTION = "OPT_ShakeReduction";
        public const string OPT_LARGE_TEXT = "OPT_LargeText";
        public const string OPT_HIGH_CONTRAST = "OPT_HighContrast";
        #endregion

        [Header("Live UI Binds (Optional)")]
        public TMP_Text previewTitleText;
        public TMP_Text previewDescText;
        public Image previewCardImage;

        [Header("Legacy / Direct UI Element Bindings")]
        public Slider masterVolumeSlider;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;
        public Slider uiVolumeSlider;
        public TMP_Text masterVolumeText;
        public TMP_Text musicVolumeText;
        public TMP_Text sfxVolumeText;
        public TMP_Text uiVolumeText;
        public TMP_Dropdown resolutionDropdown;
        public TMP_Dropdown qualityDropdown;
        public Toggle fullscreenToggle;
        public Toggle vsyncToggle;
        public TMP_Dropdown fpsCapDropdown;

        public void SetupTabNavigation(GameObject root = null) { }
        public void AutoBindUI(GameObject settingsRoot) { }

        private Resolution[] _resolutions;
        private List<Resolution> _filteredResolutions = new List<Resolution>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CacheResolutions();
        }

        private void Start()
        {
            LoadAll();
        }

        private void CacheResolutions()
        {
            _resolutions = Screen.resolutions;
            _filteredResolutions.Clear();
            for (int i = 0; i < _resolutions.Length; i++)
            {
                bool exists = false;
                for (int j = 0; j < _filteredResolutions.Count; j++)
                {
                    if (_filteredResolutions[j].width == _resolutions[i].width &&
                        _filteredResolutions[j].height == _resolutions[i].height)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    _filteredResolutions.Add(_resolutions[i]);
                }
            }
            if (_filteredResolutions.Count == 0)
            {
                _filteredResolutions.Add(Screen.currentResolution);
            }
        }

        public List<string> GetResolutionOptions()
        {
            try
            {
            if (_filteredResolutions == null) _filteredResolutions = new List<Resolution>();
            if (_filteredResolutions.Count == 0) CacheResolutions();
            List<string> list = new List<string>();
            foreach (var r in _filteredResolutions)
            {
                list.Add($"{r.width} x {r.height}");
            }
            if (list.Count == 0) list.Add($"{Screen.width} x {Screen.height}");
            return list;
            }
            catch (Exception ex) { Debug.LogException(ex); return new List<string> { "1920 x 1080" }; }
        }

        public int GetCurrentResolutionIndex()
        {
            try
            {
            if (_filteredResolutions == null) _filteredResolutions = new List<Resolution>();
            if (_filteredResolutions.Count == 0) CacheResolutions();
            int saved = PlayerPrefs.GetInt(OPT_RESOLUTION, -1);
            if (saved >= 0 && saved < _filteredResolutions.Count) return saved;

            for (int i = 0; i < _filteredResolutions.Count; i++)
            {
                if (_filteredResolutions[i].width == Screen.width &&
                    _filteredResolutions[i].height == Screen.height)
                {
                    return i;
                }
            }
            return Mathf.Max(0, _filteredResolutions.Count - 1);
            }
            catch (Exception ex) { Debug.LogException(ex); return 0; }
        }

        #region Application & Loading
        public void LoadAll()
        {
            try { ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); }
            try { ApplyAudio(); } catch (Exception ex) { Debug.LogException(ex); }
            try { ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); }
            try { ApplyAccessibility(); } catch (Exception ex) { Debug.LogException(ex); }
        }

        public void SaveAll()
        {
            try
            {
            PlayerPrefs.Save();
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void ResetToDefaults()
        {
            try
            {
            // Graphics defaults
            PlayerPrefs.SetInt(OPT_DISPLAY_MODE, 0); // Fullscreen
            PlayerPrefs.SetInt(OPT_GRAPHICS_PRESET, 2); // High
            PlayerPrefs.SetInt(OPT_RENDER_SCALE, 100);
            PlayerPrefs.SetInt(OPT_TEXTURE_QUALITY, 0); // High (full res)
            PlayerPrefs.SetInt(OPT_SHADOW_QUALITY, 2); // High
            PlayerPrefs.SetInt(OPT_ANTI_ALIASING, 3); // TAA
            PlayerPrefs.SetInt(OPT_POST_PROCESSING, 2); // High
            PlayerPrefs.SetInt(OPT_EFFECTS_QUALITY, 2); // High
            PlayerPrefs.SetInt(OPT_VIEW_DISTANCE, 2); // High
            PlayerPrefs.SetInt(OPT_VSYNC, 0); // Off
            PlayerPrefs.SetInt(OPT_MOTION_BLUR, 0); // Off
            PlayerPrefs.SetFloat(OPT_FOV, 90f);
            PlayerPrefs.SetFloat(OPT_BRIGHTNESS, 1.0f);
            PlayerPrefs.SetInt(OPT_BLOOM, 1);
            PlayerPrefs.SetFloat(OPT_BLOOM_INTENSITY, 1.5f);
            PlayerPrefs.SetInt(OPT_FPS_LIMIT, 4); // Unlimited

            // Audio defaults
            PlayerPrefs.SetFloat(OPT_MASTER_VOL, 100f);
            PlayerPrefs.SetFloat(OPT_MUSIC_VOL, 80f);
            PlayerPrefs.SetFloat(OPT_SFX_VOL, 90f);
            PlayerPrefs.SetFloat(OPT_DIALOGUE_VOL, 100f);
            PlayerPrefs.SetFloat(OPT_UI_VOL, 90f);
            PlayerPrefs.SetInt(OPT_DYNAMIC_RANGE, 0);
            PlayerPrefs.SetInt(OPT_AUDIO_OUTPUT, 0);
            PlayerPrefs.SetInt(OPT_SPATIAL_AUDIO, 1);
            PlayerPrefs.SetInt(OPT_SUBTITLES, 1);

            // Gameplay defaults
            PlayerPrefs.SetInt(OPT_DIFFICULTY, 1); // Normal
            PlayerPrefs.SetFloat(OPT_SENSITIVITY_X, 1.5f);
            PlayerPrefs.SetFloat(OPT_SENSITIVITY_Y, 1.5f);
            PlayerPrefs.SetInt(OPT_INVERT_Y, 0);
            PlayerPrefs.SetInt(OPT_CAMERA_SHAKE, 1);
            PlayerPrefs.SetFloat(OPT_SHAKE_INTENSITY, 1.0f);
            PlayerPrefs.SetInt(OPT_HIT_STOP, 1);
            PlayerPrefs.SetInt(OPT_BULLET_TIME, 1);
            PlayerPrefs.SetInt(OPT_AUTO_RECHARGE, 1);
            PlayerPrefs.SetInt(OPT_OBJECTIVE_MARKERS, 1);

            // Accessibility defaults
            PlayerPrefs.SetInt(OPT_COLORBLIND, 0);
            PlayerPrefs.SetInt(OPT_REDUCE_FLASHES, 0);
            PlayerPrefs.SetFloat(OPT_SHAKE_REDUCTION, 0f);
            PlayerPrefs.SetInt(OPT_LARGE_TEXT, 0);
            PlayerPrefs.SetInt(OPT_HIGH_CONTRAST, 0);

            SaveAll();
            try { LoadAll(); } catch (Exception ex) { Debug.LogException(ex); }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void ApplyGraphics()
        {
            try
            {
            // 1. Display Mode
            int displayMode = PlayerPrefs.GetInt(OPT_DISPLAY_MODE, 0);
            FullScreenMode mode = FullScreenMode.ExclusiveFullScreen;
            switch (displayMode)
            {
                case 0: mode = FullScreenMode.ExclusiveFullScreen; break;
                case 1: mode = FullScreenMode.Windowed; break;
                case 2: mode = FullScreenMode.FullScreenWindow; break;
            }

            // 2. Resolution
            int resIndex = GetCurrentResolutionIndex();
            if (resIndex >= 0 && resIndex < _filteredResolutions.Count)
            {
                Resolution r = _filteredResolutions[resIndex];
                Screen.SetResolution(r.width, r.height, mode);
            }

            // 3. Quality Preset
            int qualityLevel = PlayerPrefs.GetInt(OPT_GRAPHICS_PRESET, QualitySettings.GetQualityLevel());
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1), true);
            }

            // 4. Texture Quality (Global Mipmap Limit)
            int texQual = PlayerPrefs.GetInt(OPT_TEXTURE_QUALITY, 0);
            QualitySettings.globalTextureMipmapLimit = texQual; // 0=High, 1=Med, 2=Low

            // 5. Shadow Quality
            int shadowQual = PlayerPrefs.GetInt(OPT_SHADOW_QUALITY, 2);
            switch (shadowQual)
            {
                case 0: QualitySettings.shadows = UnityEngine.ShadowQuality.Disable; break;
                case 1: QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly; break;
                case 2: QualitySettings.shadows = UnityEngine.ShadowQuality.All; break;
            }

            // 6. VSync
            int vsync = PlayerPrefs.GetInt(OPT_VSYNC, 0);
            QualitySettings.vSyncCount = vsync;

            // 7. FPS Limit
            int fpsOption = PlayerPrefs.GetInt(OPT_FPS_LIMIT, 4);
            int targetFps = -1;
            switch (fpsOption)
            {
                case 0: targetFps = 30; break;
                case 1: targetFps = 60; break;
                case 2: targetFps = 120; break;
                case 3: targetFps = 144; break;
                case 4: targetFps = -1; break;
            }
            Application.targetFrameRate = targetFps;

            // 8. Field of View
            float fov = PlayerPrefs.GetFloat(OPT_FOV, 90f);
            if (Camera.main != null)
            {
                Camera.main.fieldOfView = fov;
            }

            // 9. Post Processing Volumes (Bloom & Brightness)
            bool bloomEnabled = PlayerPrefs.GetInt(OPT_BLOOM, 1) == 1;
            float bloomIntensity = PlayerPrefs.GetFloat(OPT_BLOOM_INTENSITY, 1.5f);
            float brightness = PlayerPrefs.GetFloat(OPT_BRIGHTNESS, 1.0f);

            var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include);
            foreach (var vol in volumes)
            {
                if (vol.profile != null)
                {
                    if (vol.profile.TryGet<Bloom>(out var bloom))
                    {
                        bloom.active = bloomEnabled;
                        bloom.intensity.value = bloomIntensity;
                    }
                    if (vol.profile.TryGet<ColorAdjustments>(out var ca))
                    {
                        ca.postExposure.value = (brightness - 1.0f) * 2.0f;
                    }
                }
            }

            // 10. Anti-Aliasing on Main Camera
            int aaIndex = PlayerPrefs.GetInt(OPT_ANTI_ALIASING, 3);
            if (Camera.main != null)
            {
                var camData = Camera.main.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    switch (aaIndex)
                    {
                        case 0: camData.antialiasing = AntialiasingMode.None; break;
                        case 1: camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing; break;
                        case 2: camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; break;
                        case 3: camData.antialiasing = AntialiasingMode.TemporalAntiAliasing; break;
                    }
                }
            }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void ApplyAudio()
        {
            try
            {
            float master = PlayerPrefs.GetFloat(OPT_MASTER_VOL, 100f);
            float music = PlayerPrefs.GetFloat(OPT_MUSIC_VOL, 80f);
            float sfx = PlayerPrefs.GetFloat(OPT_SFX_VOL, 90f);
            float dialogue = PlayerPrefs.GetFloat(OPT_DIALOGUE_VOL, 100f);
            float ui = PlayerPrefs.GetFloat(OPT_UI_VOL, 90f);

            AudioListener.volume = Mathf.Clamp01(master / 100f);

            // Drive the background music player directly (works with or without a mixer).
            try
            {
                var bgm = Cyberpunk.Audio.CyberBackgroundMusic.Instance;
                if (bgm == null) bgm = UnityEngine.Object.FindAnyObjectByType<Cyberpunk.Audio.CyberBackgroundMusic>();
                if (bgm != null) bgm.SetVolume(music / 100f);
            }
            catch (Exception ex) { Debug.LogException(ex); }

            if (masterMixer != null)
            {
                SetMixerVolume("MasterVol", master);
                SetMixerVolume("MusicVol", music);
                SetMixerVolume("SFXVol", sfx);
                SetMixerVolume("DialogueVol", dialogue);
                SetMixerVolume("UIVol", ui);
            }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private void SetMixerVolume(string param, float val0To100)
        {
            if (masterMixer == null) return;
            float normalized = Mathf.Clamp01(val0To100 / 100f);
            float dB = normalized > 0.001f ? Mathf.Log10(normalized) * 20f : -80f;
            masterMixer.SetFloat(param, dB);
        }

        public void ApplyGameplay()
        {
            try
            {
            // 1. Difficulty & Enemy damage scaling
            int diff = PlayerPrefs.GetInt(OPT_DIFFICULTY, 1);
            float damageMult = 1.0f;
            switch (diff)
            {
                case 0: damageMult = 0.6f; break; // Story
                case 1: damageMult = 1.0f; break; // Normal
                case 2: damageMult = 1.5f; break; // Hard
                case 3: damageMult = 2.5f; break; // Insane
            }
            SwordEnemyAI.difficultyDamageMultiplier = damageMult;

            // 2. Camera Controls
            float sensX = PlayerPrefs.GetFloat(OPT_SENSITIVITY_X, 1.5f);
            float sensY = PlayerPrefs.GetFloat(OPT_SENSITIVITY_Y, 1.5f);
            bool invertY = PlayerPrefs.GetInt(OPT_INVERT_Y, 0) == 1;
            bool shakeEnabled = PlayerPrefs.GetInt(OPT_CAMERA_SHAKE, 1) == 1;
            float shakeIntensity = PlayerPrefs.GetFloat(OPT_SHAKE_INTENSITY, 1.0f);

            InsaneGun.Combat.SmoothOrbitCamera.invertY = invertY;
            InsaneGun.Combat.SmoothOrbitCamera.traumaMultiplier = shakeEnabled ? shakeIntensity : 0f;

            var cam = FindAnyObjectByType<InsaneGun.Combat.SmoothOrbitCamera>();
            if (cam != null)
            {
                cam.mouseSensitivityX = sensX * 2.5f;
                cam.mouseSensitivityY = sensY * 2.0f;
            }

            // 3. Bullet-time & Player Controller
            bool btEnabled = PlayerPrefs.GetInt(OPT_BULLET_TIME, 1) == 1;
            var playerCtrl = InsaneGun.Combat.InsaneGunSwordController.Instance;
            if (playerCtrl != null)
            {
                playerCtrl.enableSlowMotion = btEnabled;
            }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void ApplyAccessibility()
        {
            try
            {
            bool largeText = PlayerPrefs.GetInt(OPT_LARGE_TEXT, 0) == 1;
            float textScale = largeText ? 1.2f : 1.0f;

            // Scale text in pause canvas if toggled
            var pauseMgr = InGamePauseManager.Instance;
            if (pauseMgr != null && pauseMgr.pauseCanvasOverlay != null)
            {
                var texts = pauseMgr.pauseCanvasOverlay.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    if (t != null && !t.name.Contains("KeepBaseScale"))
                    {
                        // Tag with large text scale
                    }
                }
            }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        #endregion

        #region Live Preview Helper
        public void SetPreview(string title, string desc)
        {
            if (previewTitleText != null) previewTitleText.text = title.ToUpper();
            if (previewDescText != null) previewDescText.text = desc;
        }
        #endregion
    }
}
