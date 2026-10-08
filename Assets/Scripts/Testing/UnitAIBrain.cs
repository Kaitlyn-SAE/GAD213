using System.Collections.Generic;
using UnityEngine;
using CoreBT;

[RequireComponent(typeof(UnitBlackboard))]
public class UnitAIBrain : MonoBehaviour
{
    private UnitBlackboard _blackboard;
    private Node _rootNode;

    private void Start()
    {
        _blackboard = GetComponent<UnitBlackboard>();
        BuildBehaviorTree();
    }

    private void BuildBehaviorTree()
    {
        var moveSequence = new Sequence(new List<Node>
        {
            new HasMoveOrderCondition(_blackboard),
            new MoveToDestinationAction(_blackboard)
        });

        _rootNode = new Selector(new List<Node>
        {
            moveSequence
        });
    }

    private void Update()
    {
        _rootNode?.Evaluate();
    }
}