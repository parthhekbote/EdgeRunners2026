using System;
using UnityEngine;

namespace PolygonCyberCity.NPC.BT
{
    /// <summary>
    /// Possible execution return states for Behavior Tree Nodes.
    /// </summary>
    public enum NodeState
    {
        Running,
        Success,
        Failure
    }

    /// <summary>
    /// Abstract base class for all Behavior Tree Nodes.
    /// </summary>
    public abstract class BTNode
    {
        public string NodeName { get; protected set; }
        public NodeState LastState { get; protected set; } = NodeState.Failure;

        protected BTBlackboard blackboard;
        protected GameObject owner;

        public virtual void Initialize(GameObject ownerGo, BTBlackboard bb)
        {
            owner = ownerGo;
            blackboard = bb;
            NodeName = GetType().Name;
        }

        /// <summary>
        /// Evaluates the node logic and returns Running, Success, or Failure.
        /// </summary>
        public abstract NodeState Evaluate();

        /// <summary>
        /// Called when the node is reset or interrupted.
        /// </summary>
        public virtual void ResetNode()
        {
            LastState = NodeState.Failure;
        }
    }
}
