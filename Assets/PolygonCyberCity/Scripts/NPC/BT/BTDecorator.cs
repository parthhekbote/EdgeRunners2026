using System;
using UnityEngine;

namespace PolygonCyberCity.NPC.BT
{
    /// <summary>
    /// Base decorator node wrapping a single child node.
    /// </summary>
    public abstract class BTDecorator : BTNode
    {
        protected BTNode child;

        public BTDecorator(BTNode childNode)
        {
            child = childNode;
        }

        public override void Initialize(GameObject ownerGo, BTBlackboard bb)
        {
            base.Initialize(ownerGo, bb);
            if (child != null)
            {
                child.Initialize(ownerGo, bb);
            }
        }

        public override void ResetNode()
        {
            base.ResetNode();
            if (child != null)
            {
                child.ResetNode();
            }
        }
    }

    /// <summary>
    /// Inverts the result of the child node (Success <-> Failure).
    /// </summary>
    public class Inverter : BTDecorator
    {
        public Inverter(BTNode childNode) : base(childNode) { }

        public override NodeState Evaluate()
        {
            if (child == null) return NodeState.Failure;

            NodeState state = child.Evaluate();
            if (state == NodeState.Success)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }
            if (state == NodeState.Failure)
            {
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }
    }

    /// <summary>
    /// Condition decorator evaluating a predicate against the Blackboard.
    /// </summary>
    public class ConditionNode : BTDecorator
    {
        private readonly Func<BTBlackboard, bool> _predicate;

        public ConditionNode(Func<BTBlackboard, bool> predicate, BTNode childNode = null) : base(childNode)
        {
            _predicate = predicate;
        }

        public override NodeState Evaluate()
        {
            if (_predicate == null || !_predicate(blackboard))
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            if (child == null)
            {
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = child.Evaluate();
            return LastState;
        }
    }

    /// <summary>
    /// Cooldown decorator preventing execution until cooldown duration has elapsed.
    /// </summary>
    public class Cooldown : BTDecorator
    {
        private readonly float _cooldownDuration;
        private float _lastExecutionTime = -999f;

        public Cooldown(float durationSeconds, BTNode childNode) : base(childNode)
        {
            _cooldownDuration = durationSeconds;
        }

        public override NodeState Evaluate()
        {
            if (Time.time - _lastExecutionTime < _cooldownDuration)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            if (child == null)
            {
                _lastExecutionTime = Time.time;
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            NodeState state = child.Evaluate();
            if (state == NodeState.Success || state == NodeState.Failure)
            {
                _lastExecutionTime = Time.time;
            }

            LastState = state;
            return state;
        }
    }
}
