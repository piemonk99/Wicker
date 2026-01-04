using System.Collections.Generic;
using UnityEngine;

public class MovementStateManager : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugInfo = false;

    // State management
    private MovementState defaultBaseState = new MovementState("Default", MovementStateType.Base, 0);
    private List<MovementState> baseStateStack = new List<MovementState>(); // Stack for base states
    private List<MovementState> activeModifiers = new List<MovementState>();
    private MovementState cachedEffectiveState; // Cached combined state for performance
    private Dictionary<string, MovementState> stateRegistry = new Dictionary<string, MovementState>();

    // Events
    public System.Action<string, object> RaiseEvent;

    public void Initialize()
    {
        // Initialize with default base state at the bottom of the stack
        baseStateStack.Add(defaultBaseState);
        UpdateEffectiveState();
    }

    // Updates the effective state by combining current base state with all modifiers
    private void UpdateEffectiveState()
    {
        if (baseStateStack.Count == 0)
        {
            baseStateStack.Add(defaultBaseState);
        }

        // Get the current active base state (top of stack)
        MovementState currentBaseState = baseStateStack[baseStateStack.Count - 1];

        // Start with base state
        MovementState combinedState = currentBaseState;

        // Apply all modifiers
        foreach (var modifier in activeModifiers)
        {
            combinedState = combinedState.CombineWith(modifier);
        }

        cachedEffectiveState = combinedState;

        if (showDebugInfo)
        {
            Debug.Log($"Effective State: {cachedEffectiveState.name} " +
                     $"(Base: {currentBaseState.name}, Base Stack: {baseStateStack.Count}, Modifiers: {activeModifiers.Count})");
        }
    }

    // Returns the currently effective state (used by other methods)
    public MovementState GetEffectiveState()
    {
        return cachedEffectiveState;
    }

    #region Base State Management (Stack-based)

    /// <summary>
    /// Push a base state onto the stack if it's not already present at a higher priority
    /// </summary>
    public void PushBaseState(MovementState newBaseState)
    {
        if (newBaseState.type != MovementStateType.Base)
        {
            Debug.LogWarning($"Tried to push non-Base state as base: {newBaseState.name}");
            return;
        }

        // Check if this state is already in the stack
        int existingIndex = -1;
        for (int i = 0; i < baseStateStack.Count; i++)
        {
            if (baseStateStack[i].name == newBaseState.name)
            {
                existingIndex = i;
                break;
            }
        }

        // If state doesn't exist in stack, add it
        if (existingIndex == -1)
        {
            // Insert in priority order (highest priority at the end)
            int insertIndex = baseStateStack.Count;
            for (int i = 0; i < baseStateStack.Count; i++)
            {
                if (newBaseState.priority < baseStateStack[i].priority)
                {
                    insertIndex = i;
                    break;
                }
            }

            string previousState = GetCurrentBaseState().name;
            baseStateStack.Insert(insertIndex, newBaseState);
            UpdateEffectiveState();

            RaiseEvent?.Invoke("base_movement_state_changed",
                new BaseStateChangeData(previousState, GetCurrentBaseState().name));
        }
        // If state exists but with different properties, update it
        else if (!StatesAreEqual(baseStateStack[existingIndex], newBaseState))
        {
            string previousState = GetCurrentBaseState().name;
            baseStateStack[existingIndex] = newBaseState;
            UpdateEffectiveState();

            RaiseEvent?.Invoke("base_movement_state_changed",
                new BaseStateChangeData(previousState, GetCurrentBaseState().name));
        }
    }

    /// <summary>
    /// Remove a specific base state from the stack
    /// </summary>
    public void RemoveBaseState(string stateName)
    {
        bool stateWasActive = (GetCurrentBaseState().name == stateName);
        string previousState = GetCurrentBaseState().name;

        // Remove all instances of this state from the stack
        int removedCount = baseStateStack.RemoveAll(state => state.name == stateName);

        if (removedCount > 0)
        {
            // Ensure we always have at least the default state
            if (baseStateStack.Count == 0)
            {
                baseStateStack.Add(defaultBaseState);
            }

            UpdateEffectiveState();

            if (stateWasActive)
            {
                RaiseEvent?.Invoke("base_movement_state_changed",
                    new BaseStateChangeData(previousState, GetCurrentBaseState().name));
            }

            RaiseEvent?.Invoke("base_state_removed", stateName);
        }
    }

    /// <summary>
    /// Clear all base states except the default one
    /// </summary>
    public void ClearAllBaseStates()
    {
        if (baseStateStack.Count > 1)
        {
            string previousState = GetCurrentBaseState().name;

            // Keep only the default state (should be at index 0 if it exists)
            baseStateStack.RemoveAll(state => state.name != defaultBaseState.name);

            // If default state wasn't in the stack, add it
            if (baseStateStack.Count == 0)
            {
                baseStateStack.Add(defaultBaseState);
            }

            UpdateEffectiveState();

            if (previousState != defaultBaseState.name)
            {
                RaiseEvent?.Invoke("base_movement_state_changed",
                    new BaseStateChangeData(previousState, defaultBaseState.name));
            }
        }
    }

    /// <summary>
    /// Get the current active base state (top of stack)
    /// </summary>
    public MovementState GetCurrentBaseState()
    {
        return baseStateStack.Count > 0 ? baseStateStack[baseStateStack.Count - 1] : defaultBaseState;
    }

    /// <summary>
    /// Check if a specific base state is in the stack
    /// </summary>
    public bool HasBaseState(string stateName)
    {
        return baseStateStack.Exists(state => state.name == stateName);
    }

    /// <summary>
    /// Get all base states in the stack (for debugging)
    /// </summary>
    public List<string> GetBaseStateStack()
    {
        return baseStateStack.ConvertAll(state => $"{state.name} (Priority: {state.priority})");
    }

    #endregion

    #region Modifier Management

    public void AddModifier(MovementState modifier)
    {
        if (modifier.type != MovementStateType.Modifier)
        {
            Debug.LogWarning($"Tried to add non-Modifier as modifier: {modifier.name}");
            return;
        }

        // Check if modifier already exists
        for (int i = 0; i < activeModifiers.Count; i++)
        {
            if (activeModifiers[i].name == modifier.name)
            {
                activeModifiers[i] = modifier; // Update existing
                UpdateEffectiveState();
                return;
            }
        }

        // Add new modifier
        activeModifiers.Add(modifier);
        UpdateEffectiveState();

        RaiseEvent?.Invoke("modifier_added", modifier.name);
    }

    public void RemoveModifier(string modifierName)
    {
        for (int i = activeModifiers.Count - 1; i >= 0; i--)
        {
            if (activeModifiers[i].name == modifierName)
            {
                activeModifiers.RemoveAt(i);
                UpdateEffectiveState();
                RaiseEvent?.Invoke("modifier_removed", modifierName);
                return;
            }
        }
    }

    public void ClearAllModifiers()
    {
        if (activeModifiers.Count > 0)
        {
            activeModifiers.Clear();
            UpdateEffectiveState();
            RaiseEvent?.Invoke("modifiers_cleared", null);
        }
    }

    public List<string> GetActiveModifiers()
    {
        return activeModifiers.ConvertAll(m => m.name);
    }

    #endregion

    #region State Registry

    // Register a state for later use by name
    public void RegisterState(string name, MovementState state)
    {
        stateRegistry[name] = state;
    }

    public MovementState GetRegisteredState(string name)
    {
        return stateRegistry.ContainsKey(name) ? stateRegistry[name] : null;
    }

    #endregion

    #region Helper Methods

    private bool StatesAreEqual(MovementState a, MovementState b)
    {
        return a.name == b.name &&
               a.type == b.type &&
               a.priority == b.priority &&
               a.allowMovement == b.allowMovement &&
               a.applyGravity == b.applyGravity &&
               a.applyDeceleration == b.applyDeceleration &&
               a.canJump == b.canJump &&
               Mathf.Approximately(a.gravityMultiplier, b.gravityMultiplier) &&
               Mathf.Approximately(a.groundAccelerationMultiplier, b.groundAccelerationMultiplier) &&
               Mathf.Approximately(a.groundDecelerationMultiplier, b.groundDecelerationMultiplier) &&
               Mathf.Approximately(a.airAccelerationMultiplier, b.airAccelerationMultiplier) &&
               Mathf.Approximately(a.airDecelerationMultiplier, b.airDecelerationMultiplier) &&
               Mathf.Approximately(a.jumpForceMultiplier, b.jumpForceMultiplier) &&
               Mathf.Approximately(a.maxSpeedMultiplier, b.maxSpeedMultiplier);
    }

    #endregion
}