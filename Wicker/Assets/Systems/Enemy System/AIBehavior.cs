using UnityEngine;

public abstract class AIBehavior : ScriptableObject
{
    [Header("Behavior Info")]
    [HideInInspector] public string behaviorName; // Hidden - auto-filled

    [TextArea] public string description;

    [Header("Timing")]
    public float tickRate = 0.1f;
    protected float tickTimer = 0f;

    protected virtual void OnEnable()
    {
        // Auto-fill behavior name from asset name when the asset is loaded
        if (string.IsNullOrEmpty(behaviorName))
        {
            behaviorName = name;
        }
    }

    protected virtual void OnValidate()
    {
        // Also auto-fill in editor when changes are made
        if (string.IsNullOrEmpty(behaviorName))
        {
            behaviorName = name;
        }
    }

    public virtual void OnActivate(AIBlackboard blackboard) { }
    public virtual void OnDeactivate(AIBlackboard blackboard) { }
    public virtual void Tick(AIBlackboard blackboard, float deltaTime) { }
    public virtual void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime) { }

    protected bool ShouldTick(float deltaTime)
    {
        tickTimer -= deltaTime;
        if (tickTimer <= 0)
        {
            tickTimer = tickRate;
            return true;
        }
        return false;
    }

    protected void ResetTickTimer()
    {
        tickTimer = 0;
    }
}