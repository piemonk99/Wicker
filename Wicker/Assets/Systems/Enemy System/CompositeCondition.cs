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

// Timer Condition
public class TimerExpiredCondition : AICondition
{
    private string timerKey;
    
    public TimerExpiredCondition(string timerKey)
    {
        this.timerKey = timerKey;
        conditionName = $"Timer_{timerKey}";
    }
    
    public override bool Evaluate(AIBlackboard blackboard)
    {
        return blackboard.IsTimerExpired(timerKey);
    }
}

// Player Distance Condition
public class PlayerDistanceCondition : AICondition
{
    public enum ComparisonType { LessThan, GreaterThan, WithinRange }
    
    public ComparisonType comparison = ComparisonType.LessThan;
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 5f;
    
    public PlayerDistanceCondition(float distance = 5f, ComparisonType comparison = ComparisonType.LessThan)
    {
        this.distance = distance;
        this.comparison = comparison;
        conditionName = $"PlayerDistance_{comparison}_{distance}";
    }
    
    public override bool Evaluate(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        Transform player = blackboard.Get<Transform>("player");
        
        if (self == null || player == null) return false;
        
        float currentDistance = Vector2.Distance(self.position, player.position);
        blackboard.Set("player_distance", currentDistance);
        
        switch (comparison)
        {
            case ComparisonType.LessThan: return currentDistance < distance;
            case ComparisonType.GreaterThan: return currentDistance > distance;
            case ComparisonType.WithinRange: return currentDistance >= minDistance && currentDistance <= maxDistance;
            default: return false;
        }
    }
}

// Player Direction Condition
public class PlayerDirectionCondition : AICondition
{
    public enum DirectionType { InFront, Behind, EitherSide }
    
    public DirectionType directionType = DirectionType.InFront;
    public float maxAngle = 45f;
    
    public PlayerDirectionCondition(DirectionType directionType = DirectionType.InFront)
    {
        this.directionType = directionType;
        conditionName = $"PlayerDirection_{directionType}";
    }
    
    public override bool Evaluate(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        Transform player = blackboard.Get<Transform>("player");
        
        if (self == null || player == null) return false;
        
        float facingDirection = blackboard.Get<float>("facing_direction", 1f);
        Vector2 toPlayer = player.position - self.position;
        float playerDirection = Mathf.Sign(toPlayer.x);
        
        blackboard.Set("player_direction", playerDirection);
        
        switch (directionType)
        {
            case DirectionType.InFront:
                bool sameDirection = Mathf.Sign(facingDirection) == Mathf.Sign(playerDirection);
                if (maxAngle < 180f && sameDirection)
                {
                    Vector2 facingVector = new Vector2(facingDirection, 0);
                    float angle = Vector2.Angle(facingVector, toPlayer);
                    return angle <= maxAngle;
                }
                return sameDirection;
                
            case DirectionType.Behind:
                return Mathf.Sign(facingDirection) != Mathf.Sign(playerDirection);
                
            case DirectionType.EitherSide:
                return true;
                
            default: return false;
        }
    }
}

// Ability Ready Condition
public class AbilityReadyCondition : AICondition
{
    private string abilityName;
    
    public AbilityReadyCondition(string abilityName = "lunge")
    {
        this.abilityName = abilityName;
        conditionName = $"AbilityReady_{abilityName}";
    }
    
    public override bool Evaluate(AIBlackboard blackboard)
    {
        CharacterCore character = blackboard.Get<CharacterCore>("character");
        if (character == null) return false;
        
        CharacterAbilities abilities = character.GetCharacterComponent<CharacterAbilities>();
        return abilities != null && abilities.CanUseAbility(abilityName);
    }
}