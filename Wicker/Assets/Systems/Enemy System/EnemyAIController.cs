using System.Collections.Generic;
using UnityEngine;

public class EnemyAIController : MonoBehaviour, ICharacterController
{
    [Header("Configuration")]
    [SerializeField] private AIConfig config;

    // Runtime state
    private AIBehavior currentBehavior;
    private Stack<AIBehavior> behaviorHistory = new Stack<AIBehavior>();
    private CharacterCore character;
    private AIBlackboard blackboard;
    private bool isEnabled = true;

    public void Initialize(CharacterCore characterCore)
    {
        character = characterCore;

        if (config == null)
        {
            Debug.LogError($"No AIConfig assigned to {gameObject.name}");
            return;
        }

        if (!config.IsValid())
        {
            Debug.LogError($"Invalid AIConfig on {gameObject.name}");
            return;
        }

        // Get or create blackboard
        blackboard = GetComponent<AIBlackboard>();
        if (blackboard == null)
            blackboard = gameObject.AddComponent<AIBlackboard>();

        // Initialize blackboard
        blackboard.Set("transform", transform);
        blackboard.Set("character", character);

        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            blackboard.Set("player", player.transform);
        }

        // Start with initial behavior
        currentBehavior = config.GetInitialState();
        if (currentBehavior != null)
        {
            currentBehavior.OnActivate(blackboard);
            if (config.logTransitions)
                Debug.Log($"{gameObject.name}: Started with behavior '{currentBehavior.behaviorName}'");
        }
        else
        {
            Debug.LogError($"{gameObject.name}: No initial behavior found");
        }
    }

    private void SwitchToBehavior(AIBehavior newBehavior)
    {
        if (newBehavior == null)
        {
            Debug.LogError("Tried to switch to null behavior!");
            return;
        }

        if (currentBehavior != null)
        {
            behaviorHistory.Push(currentBehavior);
            currentBehavior.OnDeactivate(blackboard);

            if (config.logTransitions)
                Debug.Log($"{gameObject.name}: Exiting {currentBehavior.behaviorName}");
        }

        currentBehavior = newBehavior;
        currentBehavior.OnActivate(blackboard);

        if (config.logTransitions)
            Debug.Log($"{gameObject.name}: Entering {currentBehavior.behaviorName}");

        character?.RaiseEvent("ai_state_changed", currentBehavior.behaviorName);
    }

    public void UpdateController(float deltaTime)
    {
        if (!isEnabled || currentBehavior == null) return;

        // Update blackboard timers
        blackboard.UpdateTimers(deltaTime);

        // Update current behavior
        currentBehavior.Tick(blackboard, deltaTime);

        // Check for transitions
        CheckTransitions();
    }

    public void FixedUpdateController(float fixedDeltaTime)
    {
        if (!isEnabled || currentBehavior == null) return;

        // Update current behavior (physics)
        currentBehavior.PhysicsTick(blackboard, fixedDeltaTime);

        // Send movement input from blackboard
        SendMovementInput();
    }

    private void SendMovementInput()
    {
        if (character == null) return;

        // Get current movement input from blackboard
        Vector2 currentMovement = blackboard.GetMovementInput();

        character.RaiseEvent("move_input", currentMovement);
    }

    private void CheckTransitions()
    {
        if (currentBehavior == null) return;

        foreach (var transition in config.GetTransitions())
        {
            // Check if this transition starts from current behavior
            if (transition.fromBehavior == currentBehavior)
            {
                // Check condition
                if (transition.condition.Evaluate(blackboard))
                {
                    // Found a valid transition
                    SwitchToBehavior(transition.toBehavior);
                    break; // Only take the first valid transition
                }
            }
        }
    }

    // ICharacterController implementation
    public void Enable() => isEnabled = true;
    public void Disable() => isEnabled = false;

    // Debug drawing - ALWAYS draw in Scene view when selected
    void OnDrawGizmosSelected()
    {
        if (blackboard != null && currentBehavior is Patrol patrolBehavior)
        {
            patrolBehavior.DrawGizmos(transform, blackboard);
        }
    }
}