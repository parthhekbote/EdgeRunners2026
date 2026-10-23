using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Alita.UI;
using SyntyCombined;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Aesthetic Cyberpunk dual-layer navigation trail leading from the player's position
    /// to the unlocked Server Room door (Mission 5: Scene_Blackwall_Cyberspace) at (-17.50, -18.09, 58.75).
    /// Features:
    /// - Dual-layer emissive cyber ribbon: wide directional outer ribbon + razor-sharp laser core
    /// - Streaming chevron arrow animation synced with directional movement
    /// - Holographic destination marker and floor radar ring at Server Room entrance
    /// - Full integration with Mission1_UIManager waypoint HUD and CyberpunkHUDController
    /// </summary>
    public class ServerRoomNavTracer : MonoBehaviour
    {
        public static ServerRoomNavTracer Instance { get; private set; }

        public static readonly Vector3 ServerDoorPos = new Vector3(-17.50f, -18.09f, 58.75f);
        public const float ArrivalRadius = 3.8f;

        private GameObject _player;
        private GameObject _tracerRoot;
        private LineRenderer _ribbonLine;
        private LineRenderer _coreLine;
        private Material _ribbonMat;
        private Material _coreMat;
        private NavMeshPath _navPath;
        private GameObject _doorMarkerRoot;

        private float _lastUpdateTime;
        private Vector3 _lastPlayerPos;
        private Sprite _arrowSprite;
        private bool _isActive;

        public static void Begin()
        {
            if (Instance == null)
            {
                var go = new GameObject("[ServerRoom_NavTracer_Runtime]");
                Instance = go.AddComponent<ServerRoomNavTracer>();
            }
            Instance.StartTracer();
        }

        public static void Clear()
        {
            if (Instance != null)
            {
                Instance.StopTracer();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            StopTracer();
            if (Instance == this) Instance = null;
        }

        public void StartTracer()
        {
            _player = GameObject.FindWithTag("Player");
            _isActive = true;

            BuildDualLayerTracer();
            BuildDoorMarker();

            var hud = CyberpunkHUDController.Instance;
            if (hud != null)
            {
                hud.SetQuestObjective("// MISSION 5: THE SERVER ROOM",
                    "> Blackwall datacenter doors unlocked\n> Enter the Server Room to locate the syndicate data core");
            }

            UpdatePathGeometry();
        }

        public void StopTracer()
        {
            _isActive = false;
            if (_tracerRoot != null) Destroy(_tracerRoot);
            if (_doorMarkerRoot != null) Destroy(_doorMarkerRoot);
            _tracerRoot = null;
            _doorMarkerRoot = null;
            _ribbonLine = null;
            _coreLine = null;

            var ui = Mission1_UIManager.Instance != null ? Mission1_UIManager.Instance : FindAnyObjectByType<Mission1_UIManager>();
            if (ui != null) ui.UpdateWaypointHUD(Vector3.zero, false, null);
        }

        private void BuildDualLayerTracer()
        {
            if (_tracerRoot != null) Destroy(_tracerRoot);
            var existingTracer = GameObject.Find("ServerRoom_DualLayerTracer");
            if (existingTracer != null) Destroy(existingTracer);

            _tracerRoot = new GameObject("ServerRoom_DualLayerTracer");

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");

            // 1. Layer 1: Sleek Outer Directional Cyber Halo Aura
            var ribbonObj = new GameObject("Ribbon_Outer");
            ribbonObj.transform.SetParent(_tracerRoot.transform, false);
            _ribbonLine = ribbonObj.AddComponent<LineRenderer>();
            _ribbonLine.useWorldSpace = true;
            _ribbonLine.startWidth = 0.085f; // Sleek delicate aura
            _ribbonLine.endWidth = 0.085f;
            _ribbonLine.numCornerVertices = 4;
            _ribbonLine.numCapVertices = 4;
            _ribbonLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ribbonLine.receiveShadows = false;
            _ribbonLine.textureMode = LineTextureMode.Tile;
            _ribbonLine.alignment = LineAlignment.View;

            _ribbonMat = new Material(unlit);
            Color cyanOuter = new Color(0.0f, 0.86f, 1.0f, 0.38f);
            _ribbonMat.color = cyanOuter;
            if (_ribbonMat.HasProperty("_BaseColor")) _ribbonMat.SetColor("_BaseColor", cyanOuter);
            if (_ribbonMat.HasProperty("_EmissionColor"))
            {
                _ribbonMat.EnableKeyword("_EMISSION");
                _ribbonMat.SetColor("_EmissionColor", cyanOuter * 1.8f);
            }

#if UNITY_EDITOR
            _arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Arrow_Clean.png");
#endif
            if (_arrowSprite == null) _arrowSprite = Resources.Load<Sprite>("SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Arrow_Clean");
            if (_arrowSprite != null) _ribbonMat.mainTexture = _arrowSprite.texture;

            _ribbonLine.sharedMaterial = _ribbonMat;

            // 2. Layer 2: Ultra-Crisp Razor-Thin Laser Filament Core
            var coreObj = new GameObject("Laser_Core");
            coreObj.transform.SetParent(_tracerRoot.transform, false);
            _coreLine = coreObj.AddComponent<LineRenderer>();
            _coreLine.useWorldSpace = true;
            _coreLine.startWidth = 0.022f; // Razor-thin 2.2cm cyber thread
            _coreLine.endWidth = 0.022f;
            _coreLine.numCornerVertices = 4;
            _coreLine.numCapVertices = 4;
            _coreLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _coreLine.receiveShadows = false;
            _coreLine.alignment = LineAlignment.View;

            _coreMat = new Material(unlit);
            Color coreCyan = new Color(0.90f, 1.0f, 1.0f, 0.98f);
            _coreMat.color = coreCyan;
            if (_coreMat.HasProperty("_BaseColor")) _coreMat.SetColor("_BaseColor", coreCyan);
            if (_coreMat.HasProperty("_EmissionColor"))
            {
                _coreMat.EnableKeyword("_EMISSION");
                _coreMat.SetColor("_EmissionColor", coreCyan * 3.8f);
            }
            _coreLine.sharedMaterial = _coreMat;

            _navPath = new NavMeshPath();
        }

        private void BuildDoorMarker()
        {
            if (_doorMarkerRoot != null) Destroy(_doorMarkerRoot);
            var existingMarker = GameObject.Find("ServerRoom_DoorMarker");
            if (existingMarker != null) Destroy(existingMarker);

            _doorMarkerRoot = new GameObject("ServerRoom_DoorMarker");
            _doorMarkerRoot.transform.position = ServerDoorPos;

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
            Material mat = new Material(unlit);
            Color cyan = new Color(0f, 0.95f, 1f, 0.95f);
            mat.color = cyan;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cyan);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", cyan * 3.0f);
            }

            // 1. Floating Holographic Diamond Billboard above door
            var diamond = GameObject.CreatePrimitive(PrimitiveType.Quad);
            diamond.name = "HoloDiamond";
            diamond.transform.SetParent(_doorMarkerRoot.transform, false);
            diamond.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            diamond.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            Destroy(diamond.GetComponent<Collider>());
            diamond.GetComponent<Renderer>().sharedMaterial = mat;

            // 2. Cyan Floor Circle Ring in front of door
            var ringObj = new GameObject("FloorRing");
            ringObj.transform.SetParent(_doorMarkerRoot.transform, false);
            ringObj.transform.localPosition = new Vector3(0.8f, 0.05f, 0f);
            var ringLine = ringObj.AddComponent<LineRenderer>();
            ringLine.useWorldSpace = false;
            ringLine.startWidth = 0.025f; // Slim aesthetic floor circle
            ringLine.endWidth = 0.025f;
            ringLine.numCornerVertices = 4;
            ringLine.numCapVertices = 4;
            ringLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringLine.receiveShadows = false;
            int segs = 36;
            ringLine.positionCount = segs;
            float r = 1.4f;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                ringLine.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            ringLine.sharedMaterial = mat;

            // 3. Point Light
            var lightObj = new GameObject("MarkerLight");
            lightObj.transform.SetParent(_doorMarkerRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = cyan;
            l.range = 7.0f;
            l.intensity = 2.8f;
        }

        private void Update()
        {
            if (!_isActive) return;

            if (_player == null)
            {
                _player = GameObject.FindWithTag("Player");
                if (_player == null) return;
            }

            // Animate line texture flow
            if (_ribbonMat != null)
            {
                _ribbonMat.mainTextureOffset = new Vector2(-Time.time * 2.2f, 0f);
            }

            // Animate destination diamond
            if (_doorMarkerRoot != null)
            {
                var diamond = _doorMarkerRoot.transform.Find("HoloDiamond");
                if (diamond != null)
                {
                    diamond.Rotate(0f, 0f, 90f * Time.deltaTime, Space.Self);
                    float bob = Mathf.Sin(Time.time * 3.5f) * 0.15f;
                    diamond.localPosition = new Vector3(0f, 2.8f + bob, 0f);
                }
            }

            var ui = Mission1_UIManager.Instance != null ? Mission1_UIManager.Instance : FindAnyObjectByType<Mission1_UIManager>();
            if (ui != null) ui.UpdateWaypointHUD(ServerDoorPos, true, "SERVER ROOM : ARCHIVE 09");

            // Recalculate path throttled every 0.15s or on movement > 0.4m
            if (Time.time - _lastUpdateTime > 0.15f || Vector3.Distance(_player.transform.position, _lastPlayerPos) > 0.4f)
            {
                _lastUpdateTime = Time.time;
                _lastPlayerPos = _player.transform.position;
                UpdatePathGeometry();
            }

            float dist = Vector3.Distance(_player.transform.position, ServerDoorPos);
            if (dist <= ArrivalRadius || Cyberpunk.World.ServerRoomDoorTrigger.IsInTransition)
            {
                // Arrived at the door
                if (ui != null) ui.UpdateWaypointHUD(Vector3.zero, false, null);
                if (_tracerRoot != null && _tracerRoot.activeSelf) _tracerRoot.SetActive(false);
            }
            else
            {
                if (_tracerRoot != null && !_tracerRoot.activeSelf) _tracerRoot.SetActive(true);
            }
        }

        private void UpdatePathGeometry()
        {
            if (_ribbonLine == null || _coreLine == null || _player == null) return;

            Vector3 startPos = _player.transform.position;
            if (NavMesh.SamplePosition(startPos, out NavMeshHit sHit, 5.0f, NavMesh.AllAreas))
            {
                startPos = sHit.position;
            }

            Vector3 endPos = ServerDoorPos;
            if (NavMesh.SamplePosition(endPos, out NavMeshHit eHit, 5.0f, NavMesh.AllAreas))
            {
                endPos = eHit.position;
            }

            if (_navPath == null) _navPath = new NavMeshPath();

            bool hasPath = false;
            try
            {
                hasPath = NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, _navPath);
            }
            catch
            {
                _navPath = new NavMeshPath();
                try { hasPath = NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, _navPath); } catch { }
            }

            if (hasPath && _navPath.status != NavMeshPathStatus.PathInvalid && _navPath.corners.Length >= 2)
            {
                Vector3[] corners = _navPath.corners;
                int count = corners.Length;
                _ribbonLine.positionCount = count;
                _coreLine.positionCount = count;

                float totalLength = 0f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 pt = corners[i];
                    pt.y += 0.045f; // Sleek floor elevation
                    _ribbonLine.SetPosition(i, pt);
                    _coreLine.SetPosition(i, pt + Vector3.up * 0.005f);
                    if (i > 0) totalLength += Vector3.Distance(corners[i - 1], corners[i]);
                }

                if (_ribbonMat != null)
                {
                    _ribbonMat.mainTextureScale = new Vector2(Mathf.Max(1f, totalLength * 4.0f), 1f);
                }
            }
            else
            {
                _ribbonLine.positionCount = 2;
                _coreLine.positionCount = 2;
                Vector3 p0 = startPos + Vector3.up * 0.045f;
                Vector3 p1 = endPos + Vector3.up * 0.045f;
                _ribbonLine.SetPosition(0, p0);
                _ribbonLine.SetPosition(1, p1);
                _coreLine.SetPosition(0, p0 + Vector3.up * 0.005f);
                _coreLine.SetPosition(1, p1 + Vector3.up * 0.005f);

                if (_ribbonMat != null)
                {
                    _ribbonMat.mainTextureScale = new Vector2(Mathf.Max(1f, Vector3.Distance(startPos, endPos) * 4.0f), 1f);
                }
            }
        }
    }
}
