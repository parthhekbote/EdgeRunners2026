using System;
using System.Collections.Generic;
using UnityEngine;

namespace PolygonCyberCity.NPC.BT
{
    public enum NPCMacroState
    {
        Normal,
        Suspicious,
        Aggressive,
        Fleeing,
        Cowering,
        PerformingActivity,
        Loitering,
        Dying,
        Dead
    }

    /// <summary>
    /// Per-NPC memory and state container for Behavior Tree evaluation.
    /// </summary>
    public class BTBlackboard
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        // High-frequency cached fields for zero-allocation access
        public NPCMacroState CurrentState = NPCMacroState.Normal;
        public Transform PlayerTransform;
        public Vector3 LastThreatPosition;
        public Vector3 CurrentDestination;
        public float DistanceToPlayer;
        public bool IsPlayerNear;
        public bool WasAttacked;
        public float Health = 100f;
        public float MaxHealth = 100f;
        public int CurrentLOD = 0; // 0 = close (high fidelity), 1 = medium, 2 = far, 3 = culled
        public float StateTimer = 0f;
        public bool WeaponDrawn = false;
        public bool HasReachedDestination = false;
        public float MoveSpeed = 1.35f;
        public bool IsCornered = false;

        public void Set<T>(string key, T value)
        {
            _data[key] = value;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_data.TryGetValue(key, out object val) && val is T typedVal)
            {
                return typedVal;
            }
            return defaultValue;
        }

        public bool Has(string key)
        {
            return _data.ContainsKey(key);
        }

        public void Remove(string key)
        {
            if (_data.ContainsKey(key))
            {
                _data.Remove(key);
            }
        }

        public void Clear()
        {
            _data.Clear();
            CurrentState = NPCMacroState.Normal;
            WasAttacked = false;
            StateTimer = 0f;
            IsCornered = false;
        }
    }
}
