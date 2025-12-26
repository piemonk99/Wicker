using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AIStateMachine : MonoBehaviour, ICharacterController
{
    [System.Serializable]
    public class Transition
    {
        public List<AIBehavior> fromStates = new List<AIBehavior>();
        public AIBehavior toState;
        public List<AICondition> conditions = new List<AICondition>();
        public int priority = 0;
        public bool enabled = true;

        public bool Evaluate(AIBehavior currentState, AIBlackboard blackboard)
        {
            if (!enabled) return false;
            if (!fromStates.Contains(currentState)) return false;

            foreach (var condition in conditions)
            {
                if (!condition.Evaluate(blackboard))
                    return false;
            }
            return true;
        }
    }

    [System.Serializable]
    public class StateMachineData
    {
        public AIBehavior initialState;
        public List<Transition> transitions = new List<Transition>();
        public bool logTransitions = true;
    }

    // Enable/disable debugging
    [SerializeField] protected bool showDebug = false;

    [Header("Condition Settings")]
    [SerializeField] protected List<AICondition> conditions = new List<AICondition>();

    // Runtime state
    protected AIBehavior currentState;
    protected AIBlackboard blackboard;
    protected CharacterCore character;

    // Configuration (set in derived class)
    protected abstract StateMachineData GetStateMachineData();

    // Event handling
    private bool isEnabled = true;
    private StateMachineData config;

    

    public void Initialize(CharacterCore characterCore)
    {
        character = characterCore;

        // Get or create blackboard
        blackboard = GetComponent<AIBlackboard>();
        if (blackboard == null)
            blackboard = gameObject.AddComponent<AIBlackboard>();

        // Get configuration from derived class
        config = GetStateMachineData();

        // Start with initial state
        if (config.initialState != null)
        {
            SwitchToState(config.initialState);
        }
    }

    public void UpdateController(float deltaTime)
    {
        if (!isEnabled || currentState == null) return;

        // Update ALL blackboard data (player, movement, abilities, timers)
        blackboard.UpdateBlackboard(deltaTime);

        // Update current state
        currentState.Tick(blackboard, deltaTime);

        // Check for transitions
        CheckTransitions();
    }

    public void FixedUpdateController(float fixedDeltaTime)
    {
        if (!isEnabled || currentState == null) return;

        // Update current state (physics)
        currentState.PhysicsTick(blackboard, fixedDeltaTime);

        // Send movement input from blackboard
        SendMovementInput();
    }

    private void SendMovementInput()
    {
        if (character == null) return;

        Vector2 currentMovement = blackboard.GetMovementInput();
        character.RaiseEvent("move_input", currentMovement);
    }

    private void CheckTransitions()
    {
        if (currentState == null) return;

        // Sort transitions by priority (highest first)
        var sortedTransitions = new List<Transition>(config.transitions);
        sortedTransitions.Sort((a, b) => b.priority.CompareTo(a.priority));

        foreach (var transition in sortedTransitions)
        {
            if (transition.Evaluate(currentState, blackboard))
            {
                SwitchToState(transition.toState);
                break;
            }
        }
    }

    protected virtual void SwitchToState(AIBehavior newState)
    {
        if (newState == null) return;

        // Deactivate current state
        if (currentState != null)
        {
            currentState.OnDeactivate(blackboard);

            if (config.logTransitions)
                Debug.Log($"{gameObject.name}: Exiting {currentState.behaviorName}");
        }

        // Activate new state
        currentState = newState;
        currentState.OnActivate(blackboard);

        if (config.logTransitions)
            Debug.Log($"{gameObject.name}: Entering {currentState.behaviorName}");

        character?.RaiseEvent("ai_state_changed", currentState.behaviorName);
    }

    // Helper to get condition by type
    protected T GetCondition<T>() where T : AICondition
    {
        foreach (var condition in conditions)
        {
            if (condition is T typedCondition)
                return typedCondition;
        }
        return null;
    }

    // ICharacterController implementation
    public void Enable() => isEnabled = true;
    public void Disable() => isEnabled = false;
}