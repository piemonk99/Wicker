using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [System.Serializable]
    public class Transition
    {
        public AIBehavior fromBehavior;
        public AIBehavior toBehavior;
        public AICondition condition;
        public int priority = 0;
    }

    [Header("State Machine")]
    [SerializeField] private AIBehavior initialState;
    [SerializeField] private List<Transition> transitions = new List<Transition>();

    [Header("Debug")]
    public bool logTransitions = true;

    // Public getters
    public AIBehavior GetInitialState() => initialState;
    public List<Transition> GetTransitions() => transitions;

    // Validation
    public bool IsValid()
    {
        if (initialState == null)
        {
            Debug.LogError($"{name}: No initial state set");
            return false;
        }

        foreach (var transition in transitions)
        {
            if (transition.fromBehavior == null)
            {
                Debug.LogError($"{name}: Transition has null 'from' behavior");
                return false;
            }

            if (transition.toBehavior == null)
            {
                Debug.LogError($"{name}: Transition has null 'to' behavior");
                return false;
            }

            if (transition.condition == null)
            {
                Debug.LogError($"{name}: Transition has null condition");
                return false;
            }
        }

        return true;
    }
}