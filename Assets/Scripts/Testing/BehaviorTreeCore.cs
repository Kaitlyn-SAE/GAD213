using System.Collections.Generic;
using UnityEngine;

namespace CoreBT
{
    public enum NodeState { Running, Success, Failure }

    public abstract class Node
    {
        public abstract NodeState Evaluate();
    }

    public class Selector : Node
    {
        protected List<Node> nodes = new List<Node>();

        public Selector(List<Node> nodes) => this.nodes = nodes;

        public override NodeState Evaluate()
        {
            foreach (var node in nodes)
            {
                switch (node.Evaluate())
                {
                    case NodeState.Running:
                        return NodeState.Running;
                    case NodeState.Success:
                        return NodeState.Success;
                    case NodeState.Failure:
                        continue;
                }
            }
            return NodeState.Failure;
        }
    }

    public class Sequence : Node
    {
        protected List<Node> nodes = new List<Node>();

        public Sequence(List<Node> nodes) => this.nodes = nodes;

        public override NodeState Evaluate()
        {
            bool anyChildRunning = false;

            foreach (var node in nodes)
            {
                switch (node.Evaluate())
                {
                    case NodeState.Failure:
                        return NodeState.Failure;
                    case NodeState.Success:
                        continue;
                    case NodeState.Running:
                        anyChildRunning = true;
                        return NodeState.Running;
                }
            }
            return anyChildRunning ? NodeState.Running : NodeState.Success;
        }
    }
}