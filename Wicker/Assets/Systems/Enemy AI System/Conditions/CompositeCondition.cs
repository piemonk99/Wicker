using System.Collections.Generic;
using UnityEngine;

public abstract class CompositeCondition : AICondition
{
    public List<AICondition> conditions = new List<AICondition>();
    
    public override bool Evaluate(AIBlackboard blackboard)
    {
        if (conditions.Count == 0) return false;
        
        bool result = EvaluateFirst(conditions[0], blackboard);
        
        for (int i = 1; i < conditions.Count; i++)
        {
            bool current = EvaluateCondition(conditions[i], blackboard);
            result = CombineResults(result, current);
        }
        
        return result;
    }
    
    protected virtual bool EvaluateFirst(AICondition condition, AIBlackboard blackboard)
    {
        return condition.Evaluate(blackboard);
    }
    
    protected virtual bool EvaluateCondition(AICondition condition, AIBlackboard blackboard)
    {
        return condition.Evaluate(blackboard);
    }
    
    protected abstract bool CombineResults(bool a, bool b);
}

// AND Condition (all must be true)
public class AndCondition : CompositeCondition
{
    public AndCondition(params AICondition[] conditions)
    {
        this.conditions.AddRange(conditions);
        conditionName = "AND";
    }
    
    protected override bool CombineResults(bool a, bool b) => a && b;
}

// OR Condition (at least one must be true)
public class OrCondition : CompositeCondition
{
    public OrCondition(params AICondition[] conditions)
    {
        this.conditions.AddRange(conditions);
        conditionName = "OR";
    }
    
    protected override bool CombineResults(bool a, bool b) => a || b;
}

// NOT Condition (invert result)
public class NotCondition : AICondition
{
    public AICondition condition;
    
    public NotCondition(AICondition condition)
    {
        this.condition = condition;
        conditionName = "NOT";
    }
    
    public override bool Evaluate(AIBlackboard blackboard)
    {
        return condition != null && !condition.Evaluate(blackboard);
    }
}