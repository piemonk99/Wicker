using UnityEngine;

[CreateAssetMenu(menuName = "AI/Behaviors/Idle")]
public class Idle : AIBehavior
{
    [Header("Idle Duration")]
    public float minIdleTime = 3f;
    public float maxIdleTime = 5f;

    protected override void OnEnable()
    {
        base.OnEnable(); // This sets behaviorName from asset name
    }

    protected override void OnValidate()
    {
        base.OnValidate(); // This sets behaviorName from asset name
    }

    public override void OnActivate(AIBlackboard blackboard)
    {
        // Clear movement input
        blackboard.ClearMovementInput();

        // Start idle timer using behaviorName (auto-filled from asset name)
        float idleTime = Random.Range(minIdleTime, maxIdleTime);
        blackboard.StartTimer(behaviorName + "_Timer", idleTime);

        Debug.Log($"{blackboard.Get<Transform>("transform").name}: Starting {behaviorName} for {idleTime} seconds");
    }

    public override void Tick(AIBlackboard blackboard, float deltaTime) { }

    public override void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime)
    {
        // Ensure movement input stays cleared during idle
        blackboard.ClearMovementInput();
    }
}