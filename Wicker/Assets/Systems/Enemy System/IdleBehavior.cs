using UnityEngine;

public class IdleBehavior : AIBehavior
{
    // No settings needed for idle - timer is handled by condition
    public override void OnActivate(AIBlackboard blackboard)
    {
        behaviorName = "Idle";
        blackboard.ClearMovementInput();

        // Timer is started by the state machine via TimerExpiredCondition
        Debug.Log($"{behaviorName}: Starting idle");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        blackboard.ClearMovementInput();
    }
}