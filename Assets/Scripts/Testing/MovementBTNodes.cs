using CoreBT;
using UnityEngine;

public class HasMoveOrderCondition : Node
{
    private UnitBlackboard _bb;

    public HasMoveOrderCondition(UnitBlackboard bb) => _bb = bb;

    public override NodeState Evaluate()
    {
        return _bb.hasMoveOrder ? NodeState.Success : NodeState.Failure;
    }
}

public class MoveToDestinationAction : Node
{
    private UnitBlackboard _bb;

    public MoveToDestinationAction(UnitBlackboard bb) => _bb = bb;

    public override NodeState Evaluate()
    {
        if (!_bb.navAgent.enabled) return NodeState.Failure;

        if (_bb.navAgent.destination != _bb.targetDestination)
        {
            _bb.navAgent.SetDestination(_bb.targetDestination);
        }

        if (_bb.navAgent.pathPending)
        {
            return NodeState.Running;
        }
        if (_bb.navAgent.remainingDistance <= _bb.stoppingDistance)
        {
            if (!_bb.navAgent.hasPath || _bb.navAgent.velocity.sqrMagnitude == 0f)
            {
                _bb.hasMoveOrder = false;
                return NodeState.Success;
            }
        }

        return NodeState.Running;
    }
}