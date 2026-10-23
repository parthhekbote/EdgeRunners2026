using System;
using UnityEngine;

namespace PolygonCyberCity.NPC.BT
{
    /// <summary>
    /// Component driving the Behavior Tree evaluation with dynamic AI LOD rate throttling.
    /// </summary>
    [DisallowMultipleComponent]
    public class BehaviorTreeRunner : MonoBehaviour
    {
        [Header("Runtime Info")]
        [SerializeField] private string _rootNodeState = "Not Initialized";
#pragma warning disable CS0414 // SerializeField reserved for future runtime tick-rate override
        [SerializeField] private float _tickInterval = 0.05f;
#pragma warning restore CS0414

        private BTNode _rootNode;
        private BTBlackboard _blackboard;
        private float _lastTickTime = 0f;
        private bool _isInitialized = false;

        public BTNode RootNode => _rootNode;
        public BTBlackboard Blackboard => _blackboard;

        public void Initialize(BTNode root, BTBlackboard bb)
        {
            _rootNode = root;
            _blackboard = bb;
            if (_rootNode != null)
            {
                _rootNode.Initialize(gameObject, _blackboard);
            }
            _isInitialized = true;
            _lastTickTime = Time.time + UnityEngine.Random.Range(0f, 0.05f); // Stagger ticks to prevent frame spikes
        }

        public void SetTree(BTNode newRoot)
        {
            _rootNode = newRoot;
            if (_rootNode != null && _blackboard != null)
            {
                _rootNode.Initialize(gameObject, _blackboard);
            }
        }

        private void Update()
        {
            if (!_isInitialized || _rootNode == null || _blackboard == null) return;

            // AI LOD tick rate control
            float interval = GetIntervalForLOD(_blackboard.CurrentLOD);
            if (interval < 0f) return; // Culled (LOD 3)

            if (Time.time - _lastTickTime >= interval)
            {
                _lastTickTime = Time.time;
                NodeState state = _rootNode.Evaluate();
                _rootNodeState = state.ToString();
            }
        }

        private float GetIntervalForLOD(int lod)
        {
            switch (lod)
            {
                case 0: return 0.03f; // ~30-60 Hz (near player)
                case 1: return 0.12f; // ~8 Hz (medium range)
                case 2: return 0.35f; // ~3 Hz (far range)
                default: return -1f;  // Culled
            }
        }

        public void ForceTick()
        {
            if (_rootNode != null)
            {
                _rootNode.Evaluate();
            }
        }

        public void ResetTree()
        {
            if (_rootNode != null)
            {
                _rootNode.ResetNode();
            }
        }
    }
}
