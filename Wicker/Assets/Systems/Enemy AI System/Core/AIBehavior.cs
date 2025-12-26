using UnityEngine;

public abstract class AIBehavior
{
    public string behaviorName;
    
    public virtual void OnActivate(AIBlackboard blackboard) { }
    public virtual void OnDeactivate(AIBlackboard blackboard) { }
    public virtual void Tick(AIBlackboard blackboard, float deltaTime) { }
    public virtual void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime) { }
    
    // Optional: For debugging/visualization
    public virtual void DrawGizmos(Transform transform, AIBlackboard blackboard) { }
}