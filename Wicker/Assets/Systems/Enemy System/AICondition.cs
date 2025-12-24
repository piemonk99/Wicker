using UnityEngine;

public abstract class AICondition : ScriptableObject
{
    [Header("Condition Info")]
    [HideInInspector] public string conditionName; // Hidden - auto-filled

    protected virtual void OnEnable()
    {
        // Auto-fill condition name from asset name
        if (string.IsNullOrEmpty(conditionName))
        {
            conditionName = name;
        }
    }

    protected virtual void OnValidate()
    {
        // Also auto-fill in editor
        if (string.IsNullOrEmpty(conditionName))
        {
            conditionName = name;
        }
    }

    public abstract bool Evaluate(AIBlackboard blackboard);
}