using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AI/AI Config")]
public class AIConfig : ScriptableObject
{
    [System.Serializable]
    public class Transition
    {
        [Header("From States")]
        public List<AIBehavior> fromBehaviors = new List<AIBehavior>(); // Multiple possible source states

        [Header("To State")]
        public AIBehavior toBehavior;

        [Header("Conditions (ALL must be true)")]
        public List<AICondition> conditions = new List<AICondition>(); // AND logic

        [Header("Settings")]
        public int priority = 0;
        public bool enabled = true;

        // Helper method to check if this transition applies
        public bool AppliesTo(AIBehavior currentBehavior)
        {
            if (!enabled) return false;
            if (fromBehaviors == null || fromBehaviors.Count == 0) return false;
            return fromBehaviors.Contains(currentBehavior);
        }

        // Helper method to evaluate all conditions
        public bool EvaluateConditions(AIBlackboard blackboard)
        {
            if (conditions == null || conditions.Count == 0) return false;

            foreach (var condition in conditions)
            {
                if (condition == null) return false;
                if (!condition.Evaluate(blackboard)) return false;
            }

            return true; // All conditions passed
        }
    }

    [Header("Behaviors")]
    [SerializeField] private List<AIBehavior> behaviors = new List<AIBehavior>();
    [SerializeField] private AIBehavior initialState;

    [Header("Transitions")]
    [SerializeField] private List<Transition> transitions = new List<Transition>();

    [Header("Debug")]
    public bool logTransitions = true;

    // Public getters
    public AIBehavior GetInitialState() => initialState;
    public List<Transition> GetTransitions() => transitions;
    public List<AIBehavior> GetBehaviors() => behaviors;

    // Add behavior
    public void AddBehavior(AIBehavior behavior)
    {
        if (!behaviors.Contains(behavior))
            behaviors.Add(behavior);
    }

    // Add transition
    public void AddTransition(Transition transition)
    {
        transitions.Add(transition);

        // Auto-add behaviors if not already in list
        foreach (var fromBehavior in transition.fromBehaviors)
            AddBehavior(fromBehavior);

        AddBehavior(transition.toBehavior);
    }

    // Get transitions for a specific state
    public List<Transition> GetTransitionsForState(AIBehavior fromBehavior)
    {
        List<Transition> result = new List<Transition>();

        Debug.Log($"Looking for transitions from behavior: {fromBehavior?.behaviorName}");

        foreach (var transition in transitions)
        {
            bool applies = transition.AppliesTo(fromBehavior);
            Debug.Log($"Transition to {transition.toBehavior?.behaviorName} applies: {applies}");

            if (applies)
                result.Add(transition);
        }

        Debug.Log($"Found {result.Count} applicable transitions");

        // Sort by priority (highest first)
        result.Sort((a, b) => b.priority.CompareTo(a.priority));
        return result;
    }

    // Validation
    public bool IsValid()
    {
        if (initialState == null)
        {
            Debug.LogError($"{name}: No initial state set");
            return false;
        }

        if (!behaviors.Contains(initialState))
        {
            Debug.LogError($"{name}: Initial state not in behaviors list");
            return false;
        }

        foreach (var transition in transitions)
        {
            if (transition.fromBehaviors == null || transition.fromBehaviors.Count == 0)
            {
                Debug.LogError($"{name}: Transition has no 'from' behaviors");
                return false;
            }

            if (transition.toBehavior == null)
            {
                Debug.LogError($"{name}: Transition has null 'to' behavior");
                return false;
            }

            if (transition.conditions == null || transition.conditions.Count == 0)
            {
                Debug.LogError($"{name}: Transition has no conditions");
                return false;
            }

            foreach (var condition in transition.conditions)
            {
                if (condition == null)
                {
                    Debug.LogError($"{name}: Transition has null condition");
                    return false;
                }
            }
        }

        return true;
    }

    // Editor helper - validate all behaviors are in the list
    public void ValidateBehaviors()
    {
        // Collect all behaviors from transitions
        HashSet<AIBehavior> allBehaviors = new HashSet<AIBehavior>();

        if (initialState != null)
            allBehaviors.Add(initialState);

        foreach (var transition in transitions)
        {
            foreach (var fromBehavior in transition.fromBehaviors)
                allBehaviors.Add(fromBehavior);

            if (transition.toBehavior != null)
                allBehaviors.Add(transition.toBehavior);
        }

        // Update behaviors list
        behaviors.Clear();
        behaviors.AddRange(allBehaviors);
    }
}