using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Alita.UI;
using SyntyCombined.World;

namespace SyntyCombined.BlackMarket
{
    /// <summary>
    /// Kiroshi-style guidance trail for the Black Market missions: a glowing line with direction chevrons on
    /// the ground from Alita to the current objective, plus a distance marker on the target.
    ///
    /// The target follows the live story (BlackMarketStoryCombatDirector):
    ///   InvestigateMarket    -> the next informant who has not been questioned yet, then Voss
    ///   CollectCores         -> the Nightshade (on foot), or the Pod Pad once the cores are aboard
    ///   ReturnToVoss         -> Voss
    ///   FleeAcrossCatwalk    -> the catwalk collapse point
    ///   EscapeToStarship     -> the escape landing pad
    /// and, when the older core-retrieval mission is active, its stages (Voss, ship, landing pad).
    ///
    /// The trail hides during cutscenes, dialogue, ship flight, combat states and once Alita has arrived.
    /// It routes over the NavMesh when one is baked; without one it draws a ground-hugging straight line.
    /// Starts itself in any scene whose name contains "BlackMarket".
    /// </summary>
    [DisallowMultipleComponent]
    public class BlackMarketMissionTrail : MonoBehaviour
    {
        private const float ArriveDistance = 3.0f;
        private const float MaxTrailLength = 220f;
        private const float ChevronSpacing = 2.4f;
        private const float RefreshInterval = 0.2f;

        private static readonly Color TrailColor = new Color(0f, 0.94f, 1f, 0.9f);

        private Transform _player;
        private LineRenderer _line;
        private Material _lineMat;
        private Material _chevronMat;
        private Texture2D _arrowTex;
        private readonly List<Transform> _chevrons = new List<Transform>();
        private readonly List<Vector3> _points = new List<Vector3>();
        private NavMeshPath _navPath;
        private bool _navMeshAvailable;

        private bool _active;
        private Vector3 _target;
        private string _label = "";
        private float _nextRefresh;
        private GUIStyle _labelStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (IsBlackMarket(SceneManager.GetActiveScene())) Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsBlackMarket(scene)) Ensure();
        }

        private static bool IsBlackMarket(Scene scene) =>
            scene.name.IndexOf("BlackMarket", System.StringComparison.OrdinalIgnoreCase) >= 0;

        public static BlackMarketMissionTrail Ensure()
        {
            var existing = FindAnyObjectByType<BlackMarketMissionTrail>();
            if (existing != null) return existing;
            return new GameObject("[BlackMarket_MissionTrail]").AddComponent<BlackMarketMissionTrail>();
        }

        private void Awake()
        {
            _navPath = new NavMeshPath();
            BuildVisuals();
        }

        private void Start()
        {
            // A baked NavMesh has vertices; an empty one makes every path request fail
            _navMeshAvailable = NavMesh.CalculateTriangulation().vertices.Length > 0;
        }

        private void OnDestroy()
        {
            if (_lineMat != null) Destroy(_lineMat);
            if (_chevronMat != null) Destroy(_chevronMat);
            if (_arrowTex != null) Destroy(_arrowTex);
        }

        // ── Visuals ──────────────────────────────────────────────────────────────

        private void BuildVisuals()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default");

            _lineMat = new Material(shader) { color = TrailColor };
            if (_lineMat.HasProperty("_BaseColor")) _lineMat.SetColor("_BaseColor", TrailColor);

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 0;
            _line.startWidth = _line.endWidth = 0.12f;
            _line.numCapVertices = 3;
            _line.numCornerVertices = 3;
            _line.alignment = LineAlignment.View;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.sharedMaterial = _lineMat;
            _line.enabled = false;

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(TrailColor, 0f), new GradientColorKey(TrailColor, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 1f) });
            _line.colorGradient = grad;

            _arrowTex = MakeArrowTexture();
            _chevronMat = new Material(Shader.Find("Sprites/Default") ?? shader) { mainTexture = _arrowTex, color = TrailColor };
        }

        // Procedural chevron so the trail needs no imported sprite
        private static Texture2D MakeArrowTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;    // -1..1 across
                    float v = (y + 0.5f) / n;              // 0 tail .. 1 tip
                    // Chevron: two bars angled back from the tip
                    float edge = Mathf.Abs(u) * 0.9f + (1f - v) * 0.85f;
                    bool inside = edge > 0.62f && edge < 0.98f && v > 0.05f;
                    px[y * n + x] = inside ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private Transform GetChevron(int i)
        {
            while (_chevrons.Count <= i)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Trail_Chevron";
                quad.transform.SetParent(transform, false);
                quad.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
                var col = quad.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = quad.GetComponent<Renderer>();
                rend.sharedMaterial = _chevronMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                _chevrons.Add(quad.transform);
            }
            return _chevrons[i];
        }

        private void HideTrail()
        {
            _active = false;
            if (_line != null && _line.enabled) _line.enabled = false;
            foreach (var c in _chevrons)
                if (c != null && c.gameObject.activeSelf) c.gameObject.SetActive(false);
        }

        // ── Update ───────────────────────────────────────────────────────────────

        /// <summary>True while a trail is being drawn.</summary>
        public bool IsShowing => _active;

        /// <summary>Name of the current objective target (empty when hidden).</summary>
        public string CurrentTargetLabel => _active ? _label : "";

        /// <summary>Recomputes target and path immediately instead of waiting for the next refresh tick.</summary>
        public void ForceRefresh()
        {
            _nextRefresh = 0f;
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        private void Refresh()
        {

            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
                if (_player == null) { HideTrail(); return; }
            }

            if (!ShouldShow() || !TryResolveTarget(out _target, out _label))
            {
                HideTrail();
                return;
            }

            Vector3 pPos = _player.position;
            float horiz = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(_target.x, _target.z));
            if (horiz <= ArriveDistance && Mathf.Abs(pPos.y - _target.y) <= 4f)
            {
                HideTrail();
                return;
            }

            _active = true;
            BuildPath(pPos, _target);
            ApplyPath();
        }

        private bool ShouldShow()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return false;
            if (CyberpunkDialogueHUD.Instance != null && CyberpunkDialogueHUD.Instance.isDialogueActive) return false;

            // No ground trail while flying either ship (the scene has two)
            foreach (var ship in FindObjectsByType<SpaceshipFlightController>())
                if (ship != null && ship.IsPiloted) return false;

            return true;
        }

        // ── Targets ──────────────────────────────────────────────────────────────

        private bool TryResolveTarget(out Vector3 target, out string label)
        {
            target = Vector3.zero;
            label = "";

            var story = BlackMarketStoryCombatDirector.Instance;
            if (story != null && story.isActiveAndEnabled)
            {
                switch (story.currentState)
                {
                    case BlackMarketStoryState.InvestigateMarket:
                        if (!story.HasEnoughLeads)
                        {
                            var lead = NearestUnfoundInformant(story);
                            if (lead != null)
                            {
                                target = lead.transform.position;
                                label = lead.characterName.ToUpperInvariant();
                                return true;
                            }
                        }
                        return TryVoss(story, out target, out label);

                    case BlackMarketStoryState.CollectCores:
                        // The flight mission drives the target: ship first, landing pad once all 4 cores are aboard
                        return TryResolveLegacyTarget(out target, out label);

                    case BlackMarketStoryState.ReturnToVoss:
                        return TryVoss(story, out target, out label);

                    case BlackMarketStoryState.FleeAcrossCatwalk:
                        var catwalk = BlackMarketCatwalkFallTrigger.Instance;
                        if (catwalk == null) return false;
                        target = catwalk.transform.position;
                        label = "UPPER GANTRY";
                        return true;

                    case BlackMarketStoryState.EscapeToStarship:
                        target = story.GetEscapePadPosition();
                        label = "NIGHTSHADE STRIKER";
                        return true;

                    default:
                        return false; // combat and cutscene-driven states
                }
            }

            return TryResolveLegacyTarget(out target, out label);
        }

        private bool TryVoss(BlackMarketStoryCombatDirector story, out Vector3 target, out string label)
        {
            target = Vector3.zero;
            label = "KAELEN VOSS";
            var voss = story.fixerVossNPC;
            if (voss == null)
            {
                foreach (var n in FindObjectsByType<BlackMarketNPC>())
                    if (n != null && n.isStoryObjectiveNPC) { voss = n; break; }
            }
            if (voss == null) return false;
            target = voss.transform.position;
            return true;
        }

        private BlackMarketNPC NearestUnfoundInformant(BlackMarketStoryCombatDirector story)
        {
            BlackMarketNPC best = null;
            float bestDist = float.MaxValue;
            foreach (var n in FindObjectsByType<BlackMarketNPC>())
            {
                if (n == null || string.IsNullOrEmpty(n.leadId) || story.IsLeadFound(n.leadId)) continue;
                float d = (n.transform.position - _player.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = n; }
            }
            return best;
        }

        // The older core-retrieval mission (only runs when the story director is absent)
        private bool TryResolveLegacyTarget(out Vector3 target, out string label)
        {
            target = Vector3.zero;
            label = "";
            var mission = BlackMarketCoreRetrievalMission.Instance;
            if (mission == null || !mission.isActiveAndEnabled) return false;

            switch (mission.currentStage)
            {
                case CoreRetrievalStage.Briefing:
                case CoreRetrievalStage.LandedReturnToCollector:
                    foreach (var n in FindObjectsByType<BlackMarketNPC>())
                        if (n != null && n.isStoryObjectiveNPC) { target = n.transform.position; label = "KAELEN VOSS"; return true; }
                    return false;

                case CoreRetrievalStage.CollectingMatrices:
                    if (mission.nightshadeShip == null) return false;
                    target = mission.nightshadeShip.transform.position;
                    label = "NIGHTSHADE STRIKER";
                    return true;

                case CoreRetrievalStage.ReturnToLandingPad:
                    target = mission.landingPadPos;
                    label = "LANDING PAD";
                    return true;

                default:
                    return false;
            }
        }

        // ── Path ─────────────────────────────────────────────────────────────────

        /// <summary>Computes (without drawing) the route the trail would take between two points.</summary>
        public List<Vector3> PreviewPath(Vector3 from, Vector3 to)
        {
            BuildPath(from, to);
            return new List<Vector3>(_points);
        }

        private void BuildPath(Vector3 from, Vector3 to)
        {
            _points.Clear();

            if (!(_navMeshAvailable && TryBuildNavMeshPath(from, to)))
            {
                AddGroundLine(from, to);
            }

            TrimToLength(MaxTrailLength);
        }

        // Follows the baked NavMesh (streets, stairs, detours around buildings). When the target sits where the
        // mesh does not reach (a ledge, a platform), the route ends at the closest reachable point and the last
        // stretch is drawn straight to the target.
        private bool TryBuildNavMeshPath(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out var sHit, 4f, NavMesh.AllAreas) &&
                !NavMesh.SamplePosition(from, out sHit, 8f, NavMesh.AllAreas)) return false;

            Vector3 goal = to;
            if (NavMesh.SamplePosition(to, out var eHit, 4f, NavMesh.AllAreas) ||
                NavMesh.SamplePosition(to, out eHit, 10f, NavMesh.AllAreas))
                goal = eHit.position;

            if (!NavMesh.CalculatePath(sHit.position, goal, NavMesh.AllAreas, _navPath)) return false;
            if (_navPath.status == NavMeshPathStatus.PathInvalid || _navPath.corners.Length < 2) return false;

            foreach (var c in _navPath.corners) _points.Add(c);

            if (_navPath.status == NavMeshPathStatus.PathPartial)
            {
                Vector3 last = _points[_points.Count - 1];
                if (Vector3.Distance(last, to) > 1.5f) AddGroundLine(last, to, skipFirst: true);
            }
            return true;
        }

        // Ground-hugging straight line, sampled every ~1.5 m
        private void AddGroundLine(Vector3 from, Vector3 to, bool skipFirst = false)
        {
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            int steps = Mathf.Max(1, Mathf.CeilToInt(flat.magnitude / 1.5f));
            for (int i = skipFirst ? 1 : 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y = GroundY(p, Mathf.Lerp(from.y, to.y, t));
                _points.Add(p);
            }
        }

        private float GroundY(Vector3 p, float fallback)
        {
            // Highest floor within a few metres of the interpolated height, ignoring Alita and NPC capsules
            var hits = Physics.RaycastAll(new Vector3(p.x, fallback + 3.5f, p.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (h.collider.isTrigger) continue;
                if (h.normal.y < 0.5f) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        private void TrimToLength(float maxLen)
        {
            float acc = 0f;
            for (int i = 1; i < _points.Count; i++)
            {
                float seg = Vector3.Distance(_points[i - 1], _points[i]);
                if (acc + seg > maxLen)
                {
                    float k = (maxLen - acc) / Mathf.Max(seg, 0.0001f);
                    _points[i] = Vector3.Lerp(_points[i - 1], _points[i], k);
                    _points.RemoveRange(i + 1, _points.Count - i - 1);
                    return;
                }
                acc += seg;
            }
        }

        private void ApplyPath()
        {
            if (_points.Count < 2) { HideTrail(); return; }

            const float lift = 0.12f;
            _line.enabled = true;
            _line.positionCount = _points.Count;
            for (int i = 0; i < _points.Count; i++)
                _line.SetPosition(i, _points[i] + Vector3.up * lift);

            // Chevrons at an even spacing along the path
            int used = 0;
            float carry = 0.8f;
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector3 a = _points[i], b = _points[i + 1];
                float seg = Vector3.Distance(a, b);
                if (seg < 0.01f) continue;
                Vector3 dir = (b - a) / seg;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                while (carry <= seg)
                {
                    var c = GetChevron(used++);
                    c.gameObject.SetActive(true);
                    c.position = a + dir * carry + Vector3.up * (lift + 0.02f);
                    c.rotation = Quaternion.Euler(90f, yaw, 0f);
                    carry += ChevronSpacing;
                }
                carry -= seg;
            }
            for (int i = used; i < _chevrons.Count; i++)
                if (_chevrons[i] != null) _chevrons[i].gameObject.SetActive(false);
        }

        // ── Target marker ────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_active || _player == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 world = _target + Vector3.up * 2.4f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.5f) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                _labelStyle.normal.textColor = TrailColor;
            }

            float dist = Vector3.Distance(_player.position, _target);
            float x = sp.x, y = Screen.height - sp.y;

            Color prev = GUI.color;
            GUI.color = TrailColor;
            // Diamond marker
            GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
            GUI.DrawTexture(new Rect(x - 6f, y - 6f, 12f, 12f), Texture2D.whiteTexture);
            GUI.matrix = Matrix4x4.identity;
            GUI.color = prev;

            GUI.Label(new Rect(x - 110f, y + 10f, 220f, 20f), $"{_label}  [{dist:F0} m]", _labelStyle);
        }
    }
}
