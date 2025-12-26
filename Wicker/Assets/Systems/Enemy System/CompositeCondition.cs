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

    [System.Serializable]
    public class Settings
    {
        public ComparisonType comparison = ComparisonType.LessThan;
        public float distance = 5f;
        public float minDistance = 2f;
        public float maxDistance = 5f;
    }

    public PlayerDistanceCondition(Settings settings)
    {
        this.settings = settings;
        conditionName = $"PlayerDistance_{settings.comparison}_{settings.distance}";
    }

    public Settings settings = new Settings();

    public override bool Evaluate(AIBlackboard blackboard)
    {
        // Distance is auto-updated in blackboard
        float currentDistance = blackboard.Get<float>("player_distance", Mathf.Infinity);

        switch (settings.comparison)
        {
            case ComparisonType.LessThan:
                return currentDistance < settings.distance;

            case ComparisonType.GreaterThan:
                return currentDistance > settings.distance;

            case ComparisonType.WithinRange:
                return currentDistance >= settings.minDistance && currentDistance <= settings.maxDistance;

            default:
                return false;
        }
    }
}

// Player Direction Condition
public class PlayerDirectionCondition : AICondition
{
    public enum DirectionType { InFront, Behind, InView }

    [System.Serializable]
    public class Settings
    {
        public DirectionType directionType = DirectionType.InFront;
        public float maxAngle = 45f; // For InFront/InView types
    }

    public Settings settings = new Settings();

    // Default constructor
    public PlayerDirectionCondition() { }

    // Constructor for code-based settings
    public PlayerDirectionCondition(Settings settings)
    {
        this.settings = settings;
        conditionName = $"PlayerDirection_{settings.directionType}";
    }

    public override bool Evaluate(AIBlackboard blackboard)
    {
        // Get pre-calculated data from blackboard
        float facingDirection = blackboard.Get<float>("facing_direction", 1f);
        float playerAngle = blackboard.Get<float>("player_angle", 180f);
        float playerDirection = blackboard.Get<float>("player_direction", 0f);

        // For InView: just check angle
        if (settings.directionType == DirectionType.InView)
        {
            return playerAngle <= settings.maxAngle;
        }

        // For InFront/Behind: check both angle and direction
        bool withinAngle = playerAngle <= settings.maxAngle;
        bool sameDirection = Mathf.Sign(facingDirection) == Mathf.Sign(playerDirection);

        return settings.directionType == DirectionType.InFront
            ? (sameDirection && withinAngle)
            : (!sameDirection || !withinAngle);
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