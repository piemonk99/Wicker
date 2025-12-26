using System.Collections.Generic;
using UnityEngine;

public abstract class AIStateMachine : MonoBehaviour
{
    [System.Serializable]
    public class StateDefinition
    {
        public string name;
        public System.Type behaviorType;
        public object behaviorSettings;
    }

    [System.Serializable]
    public class TransitionDefinition
    {
        public List<string> fromStates;
        public string toState;
        public List<ConditionDefinition> conditions;
        public int priority;
    }

    [System.Serializable]
    public class ConditionDefinition
    {
        public enum LogicType
        {
            Condition,
            NOT,
            AND,
            OR
        }

        public LogicType type = LogicType.Condition;
        public string conditionName;
        public object conditionSettings;
        public List<ConditionDefinition> subConditions; // For AND/OR
    }

    protected abstract List<StateDefinition> GetStateDefinitions();
    protected abstract List<TransitionDefinition> GetTransitionDefinitions();

    private AIConfig runtimeConfig;
    private Dictionary<string, AIBehavior> behaviorCache = new Dictionary<string, AIBehavior>();

    public void BuildRuntimeConfig()
    {
        runtimeConfig = ScriptableObject.CreateInstance<AIConfig>();
        runtimeConfig.name = name + "_RuntimeConfig";

        // Build behaviors
        var states = GetStateDefinitions();
        foreach (var stateDef in states)
        {
            var behavior = CreateBehavior(stateDef);
            behaviorCache[stateDef.name] = behavior;
            runtimeConfig.AddBehavior(behavior);
        }

        // Set initial state
        if (states.Count > 0)
        {
            runtimeConfig.SetInitialState(behaviorCache[states[0].name]);
        }

        // Build transitions
        var transitions = GetTransitionDefinitions();
        foreach (var transDef in transitions)
        {
            var transition = CreateTransition(transDef);
            runtimeConfig.AddTransition(transition);
        }
    }

    public AIConfig GetRuntimeConfig()
    {
        if (runtimeConfig == null)
            BuildRuntimeConfig();
        return runtimeConfig;
    }

    private AIBehavior CreateBehavior(StateDefinition def)
    {
        // This is simplified - in reality you'd need to use reflection or a factory
        // But for now, let's assume we have a few known types
        AIBehavior behavior = null;

        switch (def.name)
        {
            case "Patrol":
                behavior = ScriptableObject.CreateInstance<Patrol>();
                if (def.behaviorSettings is Patrol.Settings)
                    ((Patrol)behavior).settings = (Patrol.Settings)def.behaviorSettings;
                break;
            case "Chase":
                behavior = ScriptableObject.CreateInstance<Chase>();
                if (def.behaviorSettings is Chase.ChaseSettings)
                    ((Chase)behavior).settings = (Chase.ChaseSettings)def.behaviorSettings;
                break;
            case "Lunge":
                behavior = ScriptableObject.CreateInstance<Lunge>();
                if (def.behaviorSettings is Lunge.LungeSettings)
                    ((Lunge)behavior).settings = (Lunge.LungeSettings)def.behaviorSettings;
                break;
            case "Idle":
                behavior = ScriptableObject.CreateInstance<Idle>();
                break;
        }

        if (behavior != null)
            behavior.behaviorName = def.name;

        return behavior;
    }

    private AIConfig.Transition CreateTransition(TransitionDefinition def)
    {
        var transition = new AIConfig.Transition();

        // Set from behaviors
        foreach (var fromState in def.fromStates)
        {
            if (behaviorCache.ContainsKey(fromState))
                transition.fromBehaviors.Add(behaviorCache[fromState]);
        }

        // Set to behavior
        if (behaviorCache.ContainsKey(def.toState))
            transition.toBehavior = behaviorCache[def.toState];

        // Set conditions
        foreach (var condDef in def.conditions)
        {
            var condition = CreateCondition(condDef);
            if (condition != null)
                transition.conditions.Add(condition);
        }

        transition.priority = def.priority;
        return transition;
    }

    private AICondition CreateCondition(ConditionDefinition def)
    {
        switch (def.type)
        {
            case ConditionDefinition.LogicType.Condition:
                return CreateSingleCondition(def);
            case ConditionDefinition.LogicType.NOT:
                return CreateNOTCondition(def);
            case ConditionDefinition.LogicType.AND:
                return CreateANDCondition(def);
            case ConditionDefinition.LogicType.OR:
                return CreateORCondition(def);
        }
        return null;
    }

    private AICondition CreateSingleCondition(ConditionDefinition def)
    {
        AICondition condition = null;

        switch (def.conditionName)
        {
            case "TimerExpired":
                condition = ScriptableObject.CreateInstance<TimerExpiredCondition>();
                break;
            case "PlayerDistance":
                condition = ScriptableObject.CreateInstance<PlayerDistanceCondition>();
                if (def.conditionSettings is PlayerDistanceCondition.ComparisonType)
                    ((PlayerDistanceCondition)condition).comparison = (PlayerDistanceCondition.ComparisonType)def.conditionSettings;
                break;
            case "PlayerDirection":
                condition = ScriptableObject.CreateInstance<PlayerDirectionCondition>();
                if (def.conditionSettings is PlayerDirectionCondition.DirectionType)
                    ((PlayerDirectionCondition)condition).directionType = (PlayerDirectionCondition.DirectionType)def.conditionSettings;
                break;
            case "AbilityReady":
                condition = ScriptableObject.CreateInstance<AbilityReadyCondition>();
                break;
        }

        if (condition != null)
            condition.conditionName = def.conditionName;

        return condition;
    }

    private AICondition CreateNOTCondition(ConditionDefinition def)
    {
        // You'd need to create a NOT condition class
        // For now, return null
        return null;
    }

    private AICondition CreateANDCondition(ConditionDefinition def)
    {
        var andCondition = ScriptableObject.CreateInstance<CompositeCondition>();
        andCondition.logicType = CompositeCondition.LogicType.AND;

        foreach (var subDef in def.subConditions)
        {
            var subCondition = CreateCondition(subDef);
            if (subCondition != null)
                andCondition.conditions.Add(subCondition);
        }

        return andCondition;
    }

    private AICondition CreateORCondition(ConditionDefinition def)
    {
        var orCondition = ScriptableObject.CreateInstance<CompositeCondition>();
        orCondition.logicType = CompositeCondition.LogicType.OR;

        foreach (var subDef in def.subConditions)
        {
            var subCondition = CreateCondition(subDef);
            if (subCondition != null)
                orCondition.conditions.Add(subCondition);
        }

        return orCondition;
    }
}