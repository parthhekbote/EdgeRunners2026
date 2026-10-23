using System.Collections.Generic;
using UnityEngine;

namespace PolygonCyberCity.NPC.BT
{
    /// <summary>
    /// Base class for composite nodes that hold and execute child nodes.
    /// </summary>
    public abstract class BTComposite : BTNode
    {
        protected List<BTNode> children = new List<BTNode>();

        public BTComposite(params BTNode[] nodes)
        {
            if (nodes != null)
            {
                children.AddRange(nodes);
            }
        }

        public void AddChild(BTNode node)
        {
            if (node != null)
            {
                children.Add(node);
            }
        }

        public override void Initialize(GameObject ownerGo, BTBlackboard bb)
        {
            base.Initialize(ownerGo, bb);
            for (int i = 0; i < children.Count; i++)
            {
                children[i].Initialize(ownerGo, bb);
            }
        }

        public override void ResetNode()
        {
            base.ResetNode();
            for (int i = 0; i < children.Count; i++)
            {
                children[i].ResetNode();
            }
        }
    }

    /// <summary>
    /// Selector (OR logic): Evaluates children in sequence until one succeeds or runs.
    /// If a child succeeds/runs, Selector returns that state. If all fail, returns Failure.
    /// </summary>
    public class Selector : BTComposite
    {
        public Selector(params BTNode[] nodes) : base(nodes) { }

        public override NodeState Evaluate()
        {
            for (int i = 0; i < children.Count; i++)
            {
                NodeState state = children[i].Evaluate();
                if (state == NodeState.Running)
                {
                    LastState = NodeState.Running;
                    return NodeState.Running;
                }
                if (state == NodeState.Success)
                {
                    LastState = NodeState.Success;
                    return NodeState.Success;
                }
            }

            LastState = NodeState.Failure;
            return NodeState.Failure;
        }
    }

    /// <summary>
    /// Sequence (AND logic): Evaluates children in sequence until one fails or runs.
    /// If all succeed, Sequence returns Success. If one fails, returns Failure.
    /// </summary>
    public class Sequence : BTComposite
    {
        private int _currentChildIndex = 0;

        public Sequence(params BTNode[] nodes) : base(nodes) { }

        public override NodeState Evaluate()
        {
            while (_currentChildIndex < children.Count)
            {
                NodeState state = children[_currentChildIndex].Evaluate();
                if (state == NodeState.Running)
                {
                    LastState = NodeState.Running;
                    return NodeState.Running;
                }
                if (state == NodeState.Failure)
                {
                    _currentChildIndex = 0;
                    LastState = NodeState.Failure;
                    return NodeState.Failure;
                }

                _currentChildIndex++;
            }

            _currentChildIndex = 0;
            LastState = NodeState.Success;
            return NodeState.Success;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _currentChildIndex = 0;
        }
    }

    /// <summary>
    /// Parallel: Runs all children concurrently.
    /// </summary>
    public class Parallel : BTComposite
    {
        public enum Policy
        {
            RequireAllSuccess,
            RequireOneSuccess
        }

        private readonly Policy _policy;

        public Parallel(Policy policy, params BTNode[] nodes) : base(nodes)
        {
            _policy = policy;
        }

        public override NodeState Evaluate()
        {
            int successCount = 0;
            int failureCount = 0;
            bool anyRunning = false;

            for (int i = 0; i < children.Count; i++)
            {
                NodeState state = children[i].Evaluate();
                if (state == NodeState.Success) successCount++;
                else if (state == NodeState.Failure) failureCount++;
                else if (state == NodeState.Running) anyRunning = true;
            }

            if (_policy == Policy.RequireOneSuccess && successCount > 0)
            {
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            if (_policy == Policy.RequireAllSuccess && successCount == children.Count)
            {
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            if (failureCount > 0 && _policy == Policy.RequireAllSuccess)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            LastState = anyRunning ? NodeState.Running : NodeState.Failure;
            return LastState;
        }
    }
}
