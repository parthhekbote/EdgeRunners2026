using System;
using UnityEngine;
using PolygonCyberCity.NPC.BT;

namespace PolygonCyberCity.NPC.Spawner
{
    /// <summary>
    /// AAA Holographic Cyberpunk HUD Scanner and Target Telemetry.
    /// Casts rays from player crosshair to scan and display NPC profiles with
    /// high-tech framing, segmented LED vitality gauges, and dynamic status readouts.
    /// </summary>
    public class CyberNPCScannerHUD : MonoBehaviour
    {
        [Header("Scanner Settings")]
        public Camera playerCamera;
        public float maxScanDistance = 22f;
        public LayerMask scanLayerMask = ~0;

        [Header("Visual Colors")]
        public Color normalColor  = new Color(0.22f, 0.94f, 0.85f, 0.95f); // #38EFD7 Neon Cyan
        public Color warningColor = new Color(1.00f, 0.72f, 0.10f, 0.95f); // #FFB300 Neon Amber
        public Color dangerColor  = new Color(1.00f, 0.16f, 0.33f, 0.95f); // #FF2A55 Combat Crimson
        public Color fleeingColor = new Color(0.88f, 0.12f, 0.38f, 0.95f); // #E01F60 Neon Magenta
        public Color deadColor    = new Color(0.40f, 0.46f, 0.54f, 0.85f); // Muted Slate

        // Target state
        private CyberNPCController _currentTarget;
        private CyberNPCController _lastTarget;
        private float _fadeAlpha = 0f;
        private float _animTimer = 0f;

        // Cached Procedural Textures
        private Texture2D _whiteTex;
        private Texture2D _glassBgTex;
        private Texture2D _trackBgTex;

        // Fonts & Styles
        private Font _customFont;
        private GUIStyle _subHdrStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _lblStyle;
        private GUIStyle _valStyle;
        private GUIStyle _pctStyle;
        private GUIStyle _distStyle;

        // Audio
        private AudioSource _audioSource;

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            BuildTextures();
            SetupAudio();
        }

        private void SetupAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }
        }

        private void PlayLockChirp()
        {
            if (_audioSource == null) return;
            int sampleRate = 44100;
            float duration = 0.06f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (float)i / samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * 820f * t) * env * 0.12f;
            }
            var clip = AudioClip.Create("ScanLockChirp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, 0.35f);
        }

        private void BuildTextures()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _whiteTex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
                _whiteTex.Apply();
            }

            if (_glassBgTex == null)
            {
                _glassBgTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color glassCol = new Color(0.025f, 0.045f, 0.075f, 0.92f);
                _glassBgTex.SetPixels(new Color[] { glassCol, glassCol, glassCol, glassCol });
                _glassBgTex.Apply();
            }

            if (_trackBgTex == null)
            {
                _trackBgTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color trackCol = new Color(0.015f, 0.025f, 0.045f, 0.95f);
                _trackBgTex.SetPixels(new Color[] { trackCol, trackCol, trackCol, trackCol });
                _trackBgTex.Apply();
            }
        }

        private void EnsureStyles()
        {
            if (_customFont == null)
            {
                _customFont = Resources.Load<Font>("Fonts/ZenDots");
                if (_customFont == null)
                    _customFont = Resources.Load<Font>("SyntyUI/Fonts/Orbitron/Orbitron-ExtraBold");
            }

            if (_subHdrStyle == null)
            {
                _subHdrStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            if (_customFont != null) _subHdrStyle.font = _customFont;

            if (_distStyle == null)
            {
                _distStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight
                };
            }
            if (_customFont != null) _distStyle.font = _customFont;

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            if (_customFont != null) _titleStyle.font = _customFont;

            if (_lblStyle == null)
            {
                _lblStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            if (_customFont != null) _lblStyle.font = _customFont;

            if (_valStyle == null)
            {
                _valStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            if (_customFont != null) _valStyle.font = _customFont;

            if (_pctStyle == null)
            {
                _pctStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight
                };
            }
            if (_customFont != null) _pctStyle.font = _customFont;
        }

        private void Update()
        {
            _animTimer += Time.unscaledDeltaTime;

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            CyberNPCController detected = null;

            if (Physics.Raycast(ray, out RaycastHit hit, maxScanDistance, scanLayerMask))
            {
                var npc = hit.collider.GetComponentInParent<CyberNPCController>();
                if (npc != null && !npc.IsDead)
                {
                    detected = npc;
                }
            }

            _currentTarget = detected;

            // Audio chirp on acquire
            if (_currentTarget != null && _currentTarget != _lastTarget)
            {
                PlayLockChirp();
            }
            _lastTarget = _currentTarget;

            // Smooth fade
            float targetAlpha = (_currentTarget != null) ? 1.0f : 0.0f;
            _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, targetAlpha, Time.unscaledDeltaTime * 6f);
        }

        private void OnGUI()
        {
            if (_fadeAlpha <= 0.005f || playerCamera == null || _lastTarget == null) return;

            BuildTextures();
            EnsureStyles();

            Vector3 worldHeadPos = _lastTarget.transform.position + Vector3.up * 1.9f;
            Vector3 screenPos = playerCamera.WorldToScreenPoint(worldHeadPos);

            if (screenPos.z < 0) return; // Behind camera

            float screenX = screenPos.x;
            float screenY = Screen.height - screenPos.y;

            // State evaluation
            Color stateColor = normalColor;
            string stateLabel = "[ NORMAL ]";
            ThreatRating threat = _lastTarget.scannerProfile.Threat;

            if (_lastTarget.Blackboard != null)
            {
                switch (_lastTarget.Blackboard.CurrentState)
                {
                    case NPCMacroState.Suspicious:
                        stateColor = warningColor;
                        stateLabel = "[ CAUTION // ALERT ]";
                        break;
                    case NPCMacroState.Aggressive:
                        stateColor = dangerColor;
                        stateLabel = "[ COMBAT // HOSTILE ]";
                        break;
                    case NPCMacroState.Fleeing:
                        stateColor = fleeingColor;
                        stateLabel = "[ PANIC // EVADING ]";
                        break;
                    case NPCMacroState.Cowering:
                        stateColor = warningColor;
                        stateLabel = "[ COWERING ]";
                        break;
                    case NPCMacroState.Dying:
                    case NPCMacroState.Dead:
                        stateColor = deadColor;
                        stateLabel = "[ OFFLINE // NO SIGNAL ]";
                        break;
                }
            }

            float distance = Vector3.Distance(playerCamera.transform.position, _lastTarget.transform.position);

            // 1. Holographic Target Reticle in World/Screen
            DrawTargetReticle(screenX, screenY, stateColor);

            // 2. HUD Card
            float cardW = 330f;
            float cardH = 205f;
            float cardX = screenX + 38f;
            float cardY = screenY - (cardH * 0.5f);

            // Screen boundary clamping
            if (cardX + cardW > Screen.width - 15f)
            {
                cardX = screenX - cardW - 38f;
            }
            cardY = Mathf.Clamp(cardY, 20f, Screen.height - cardH - 20f);

            // 3. Leader Line connecting reticle to card
            DrawLeaderLine(screenX, screenY, cardX, cardY, cardW, cardH, stateColor);

            // 4. Draw HUD Holographic Card
            DrawCard(cardX, cardY, cardW, cardH, stateColor, stateLabel, distance);
        }

        private void DrawTargetReticle(float x, float y, Color col)
        {
            float size = 32f;
            float arm = 8f;
            float thick = 2f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(_animTimer * 5f);

            Color c = new Color(col.r, col.g, col.b, col.a * _fadeAlpha * pulse);
            GUI.color = c;

            float hx = size * 0.5f;

            // 4 Corner Brackets around target
            // Top-Left
            GUI.DrawTexture(new Rect(x - hx, y - hx, arm, thick), _whiteTex);
            GUI.DrawTexture(new Rect(x - hx, y - hx, thick, arm), _whiteTex);
            // Top-Right
            GUI.DrawTexture(new Rect(x + hx - arm, y - hx, arm, thick), _whiteTex);
            GUI.DrawTexture(new Rect(x + hx - thick, y - hx, thick, arm), _whiteTex);
            // Bottom-Left
            GUI.DrawTexture(new Rect(x - hx, y + hx - thick, arm, thick), _whiteTex);
            GUI.DrawTexture(new Rect(x - hx, y + hx - arm, thick, arm), _whiteTex);
            // Bottom-Right
            GUI.DrawTexture(new Rect(x + hx - arm, y + hx - thick, arm, thick), _whiteTex);
            GUI.DrawTexture(new Rect(x + hx - thick, y + hx - arm, thick, arm), _whiteTex);

            // Center target pip
            float pip = 3f;
            GUI.DrawTexture(new Rect(x - pip * 0.5f, y - pip * 0.5f, pip, pip), _whiteTex);
        }

        private void DrawLeaderLine(float rx, float ry, float cx, float cy, float cw, float ch, Color col)
        {
            float attachX = (cx > rx) ? cx : cx + cw;
            float attachY = cy + 24f;

            Color c = new Color(col.r, col.g, col.b, 0.45f * _fadeAlpha);
            GUI.color = c;

            // Horizontal segment
            float minX = Mathf.Min(rx, attachX);
            float maxX = Mathf.Max(rx, attachX);
            float w = Mathf.Max(1f, maxX - minX);
            GUI.DrawTexture(new Rect(minX, attachY, w, 1.5f), _whiteTex);
        }

        private void DrawCard(float x, float y, float w, float h, Color stateCol, string stateLabel, float dist)
        {
            // 1. Dark Glass Backing
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.DrawTexture(new Rect(x, y, w, h), _glassBgTex);

            // 2. Outer State Border
            float bThick = 1.5f;
            Color borderC = new Color(stateCol.r, stateCol.g, stateCol.b, 0.65f * _fadeAlpha);
            GUI.color = borderC;
            GUI.DrawTexture(new Rect(x, y, w, bThick), _whiteTex);
            GUI.DrawTexture(new Rect(x, y + h - bThick, w, bThick), _whiteTex);
            GUI.DrawTexture(new Rect(x, y, bThick, h), _whiteTex);
            GUI.DrawTexture(new Rect(x + w - bThick, y, bThick, h), _whiteTex);

            // 3. Thick Left Accent Bar
            GUI.color = new Color(stateCol.r, stateCol.g, stateCol.b, 0.95f * _fadeAlpha);
            GUI.DrawTexture(new Rect(x, y, 4f, h), _whiteTex);

            // 4. Corner Reticle Brackets (Neon Magenta)
            Color magC = new Color(fleeingColor.r, fleeingColor.g, fleeingColor.b, 0.90f * _fadeAlpha);
            GUI.color = magC;
            float cArm = 10f;
            float cThick = 2.5f;
            // Top-Left
            GUI.DrawTexture(new Rect(x, y, cArm, cThick), _whiteTex);
            GUI.DrawTexture(new Rect(x, y, cThick, cArm), _whiteTex);
            // Top-Right
            GUI.DrawTexture(new Rect(x + w - cArm, y, cArm, cThick), _whiteTex);
            GUI.DrawTexture(new Rect(x + w - cThick, y, cThick, cArm), _whiteTex);
            // Bottom-Left
            GUI.DrawTexture(new Rect(x, y + h - cThick, cArm, cThick), _whiteTex);
            GUI.DrawTexture(new Rect(x, y + h - cArm, cThick, cArm), _whiteTex);
            // Bottom-Right
            GUI.DrawTexture(new Rect(x + w - cArm, y + h - cThick, cArm, cThick), _whiteTex);
            GUI.DrawTexture(new Rect(x + w - cThick, y + h - cArm, cThick, cArm), _whiteTex);

            // --- Card Content ---
            float padX = 14f;

            // Sub-header: BIO-SCAN // TARGET LOCKED & Distance
            _subHdrStyle.normal.textColor = new Color(stateCol.r, stateCol.g, stateCol.b, 0.75f * _fadeAlpha);
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.Label(new Rect(x + padX, y + 8f, 190f, 16f), "BIO-SCAN // TARGET LOCKED", _subHdrStyle);

            _distStyle.normal.textColor = new Color(fleeingColor.r, fleeingColor.g, fleeingColor.b, 0.95f * _fadeAlpha);
            GUI.Label(new Rect(x + w - padX - 100f, y + 8f, 100f, 16f), $"DIST: {dist:0.0}M", _distStyle);

            // Main Hero Title: SCAN // {Name}
            _titleStyle.normal.textColor = new Color(stateCol.r, stateCol.g, stateCol.b, 1.0f * _fadeAlpha);
            string charName = _lastTarget.scannerProfile.CharacterName;
            if (string.IsNullOrEmpty(charName)) charName = _lastTarget.gameObject.name;
            GUI.Label(new Rect(x + padX, y + 26f, w - padX * 2, 22f), $"SCAN // {charName}", _titleStyle);

            // Divider Rule
            GUI.color = new Color(stateCol.r, stateCol.g, stateCol.b, 0.35f * _fadeAlpha);
            GUI.DrawTexture(new Rect(x + padX, y + 51f, w - padX * 2, 1f), _whiteTex);

            // Structured Rows
            float rowY = y + 57f;
            float rowH = 19f;

            Color dimCol = new Color(0.55f, 0.72f, 0.82f, 0.90f * _fadeAlpha);
            Color brightCol = new Color(0.92f, 0.97f, 1.00f, 0.95f * _fadeAlpha);

            void DrawRow(string label, string val, Color valCol)
            {
                _lblStyle.normal.textColor = dimCol;
                _valStyle.normal.textColor = valCol;
                GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
                GUI.Label(new Rect(x + padX, rowY, 80f, rowH), label, _lblStyle);
                GUI.Label(new Rect(x + padX + 80f, rowY, w - padX * 2 - 80f, rowH), val, _valStyle);
                rowY += rowH;
            }

            DrawRow("STATE:", stateLabel, new Color(stateCol.r, stateCol.g, stateCol.b, _fadeAlpha));

            string faction = _lastTarget.scannerProfile.Faction.ToString().ToUpperInvariant();
            DrawRow("FACTION:", faction, brightCol);

            string implants = _lastTarget.scannerProfile.Cyberware;
            if (string.IsNullOrEmpty(implants)) implants = "STANDARD BIO-CHASSIS";
            DrawRow("IMPLANTS:", "▪ " + implants.ToUpperInvariant(), brightCol);

            // Threat Pips
            string threatText = GetThreatText(_lastTarget.scannerProfile.Threat);
            DrawRow("THREAT:", threatText, GetThreatColor(_lastTarget.scannerProfile.Threat));

            // Optional Bounty
            if (_lastTarget.scannerProfile.Bounty > 0f)
            {
                DrawRow("BOUNTY:", $"¥ {_lastTarget.scannerProfile.Bounty:N0}", new Color(1f, 0.8f, 0.1f, _fadeAlpha));
            }

            // 5. Segmented LED Vitality Gauge (Replacing the crude green bar!)
            DrawVitalityGauge(x + padX, y + h - 34f, w - padX * 2, 14f, stateCol);
        }

        private string GetThreatText(ThreatRating threat)
        {
            switch (threat)
            {
                case ThreatRating.Low:      return "LOW  ● ○ ○ ○";
                case ThreatRating.Moderate: return "MOD  ● ● ○ ○";
                case ThreatRating.High:     return "HIGH ● ● ● ○";
                case ThreatRating.Lethal:   return "LETHAL ● ● ● ●";
                default:                    return "UNKNOWN";
            }
        }

        private Color GetThreatColor(ThreatRating threat)
        {
            switch (threat)
            {
                case ThreatRating.Low:      return new Color(normalColor.r, normalColor.g, normalColor.b, _fadeAlpha);
                case ThreatRating.Moderate: return new Color(warningColor.r, warningColor.g, warningColor.b, _fadeAlpha);
                case ThreatRating.High:     return new Color(1f, 0.45f, 0.1f, _fadeAlpha);
                case ThreatRating.Lethal:   return new Color(dangerColor.r, dangerColor.g, dangerColor.b, _fadeAlpha);
                default:                    return Color.white;
            }
        }

        private void DrawVitalityGauge(float gx, float gy, float gw, float gh, Color stateCol)
        {
            float hpPct = Mathf.Clamp01(_lastTarget.HealthPercent);

            // Integrity Header & Percentage Readout
            _subHdrStyle.normal.textColor = new Color(0.55f, 0.72f, 0.82f, 0.85f * _fadeAlpha);
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.Label(new Rect(gx, gy - 14f, 100f, 14f), "INTEGRITY", _subHdrStyle);

            Color pctColor = (hpPct > 0.45f) ? normalColor : (hpPct > 0.20f ? warningColor : dangerColor);
            _pctStyle.normal.textColor = new Color(pctColor.r, pctColor.g, pctColor.b, _fadeAlpha);
            GUI.Label(new Rect(gx + gw - 60f, gy - 14f, 60f, 14f), $"{Mathf.RoundToInt(hpPct * 100f)}%", _pctStyle);

            // Gauge Track Background
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.DrawTexture(new Rect(gx, gy, gw, gh), _trackBgTex);

            // Gauge Fine Border
            GUI.color = new Color(stateCol.r, stateCol.g, stateCol.b, 0.35f * _fadeAlpha);
            GUI.DrawTexture(new Rect(gx, gy, gw, 1f), _whiteTex);
            GUI.DrawTexture(new Rect(gx, gy + gh - 1f, gw, 1f), _whiteTex);
            GUI.DrawTexture(new Rect(gx, gy, 1f, gh), _whiteTex);
            GUI.DrawTexture(new Rect(gx + gw - 1f, gy, 1f, gh), _whiteTex);

            // 16 Segmented LED Cells
            int totalCells = 16;
            float gap = 2f;
            float pad = 2f;
            float availW = gw - (pad * 2f);
            float cellW = (availW - gap * (totalCells - 1)) / totalCells;
            float cellH = gh - (pad * 2f);

            int filledCells = Mathf.CeilToInt(hpPct * totalCells);

            Color activeCellColor = (hpPct > 0.45f) 
                ? normalColor 
                : (hpPct > 0.20f ? warningColor : dangerColor);

            // Critical pulse
            if (hpPct <= 0.20f)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(_animTimer * 10f);
                activeCellColor = new Color(dangerColor.r, dangerColor.g, dangerColor.b, dangerColor.a * pulse);
            }

            for (int i = 0; i < totalCells; i++)
            {
                float cx = gx + pad + i * (cellW + gap);
                float cy = gy + pad;

                if (i < filledCells)
                {
                    GUI.color = new Color(activeCellColor.r, activeCellColor.g, activeCellColor.b, activeCellColor.a * _fadeAlpha);
                }
                else
                {
                    // Empty cell
                    GUI.color = new Color(0.10f, 0.16f, 0.22f, 0.40f * _fadeAlpha);
                }

                GUI.DrawTexture(new Rect(cx, cy, cellW, cellH), _whiteTex);
            }

            GUI.color = Color.white;
        }
    }
}
