using UnityEngine;

public class IdleBehavior : AIBehavior
{
    [System.Serializable]
    public class Settings
    {
        public float minIdleTime = 2f;
        public float maxIdleTime = 4f;
    }

    public Settings settings = new Settings();
    private float idleTimer;

    public override void OnActivate(AIBlackboard blackboard)
    {
        behaviorName = "Idle";
        
        blackboard.ClearMovementInput();
        
        idleTimer = Random.Range(settings.minIdleTime, settings.maxIdleTime);
        blackboard.StartTimer("Idle_Timer", idleTimer);
        
        Debug.Log($"{behaviorName}: Starting idle for {idleTimer:F2}s");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        blackboard.ClearMovementInput();
    }
}