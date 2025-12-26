public abstract class AICondition
{
    public string conditionName;
    
    public abstract bool Evaluate(AIBlackboard blackboard);
}