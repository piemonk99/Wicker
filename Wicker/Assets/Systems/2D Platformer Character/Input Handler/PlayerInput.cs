using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [Header("Input Settings")]
    public InputActionAsset inputAsset;

    [Header("Character")]
    public CharacterCore character;

    [Header("Auto-Detection Settings")]
    [Tooltip("Prefix to remove from action names (e.g., 'Player/' or 'Gameplay/')")]
    public string actionNamePrefix = "";

    [Tooltip("Convert action names to lowercase for event names")]
    public bool useLowercaseEvents = true;

    [Tooltip("Action map to use (leave empty for first found)")]
    public string targetActionMap = "Player";

    [Header("Debug")]
    [SerializeField] private bool logEvents = false;

    private List<InputActionMapping> actionMappings = new List<InputActionMapping>();
    private InputActionMap currentActionMap;

    void Start()
    {
        if (inputAsset == null)
        {
            Debug.LogError("No InputActionAsset assigned!");
            CreateDefaultInputs();
            return;
        }

        SetupAutoDetectedActions();
    }

    void SetupAutoDetectedActions()
    {
        // Find the action map
        if (!string.IsNullOrEmpty(targetActionMap))
        {
            currentActionMap = inputAsset.FindActionMap(targetActionMap);
        }

        if (currentActionMap == null && inputAsset.actionMaps.Count > 0)
        {
            currentActionMap = inputAsset.actionMaps[0];
            Debug.Log($"Using first action map: {currentActionMap.name}");
        }

        if (currentActionMap == null)
        {
            Debug.LogError("No action maps found in InputActionAsset!");
            CreateDefaultInputs();
            return;
        }

        // Auto-detect all actions in the map
        foreach (var action in currentActionMap.actions)
        {
            string eventName = GenerateEventName(action.name);

            InputActionMapping mapping = new InputActionMapping
            {
                action = action,
                eventName = eventName,
                actionType = action.type
            };

            actionMappings.Add(mapping);

            Debug.Log($"Auto-detected action: {action.name} -> {eventName} ({action.type})");
        }

        EnableAllActions();
    }

    string GenerateEventName(string actionName)
    {
        // Remove prefix if specified
        string processedName = actionName;
        if (!string.IsNullOrEmpty(actionNamePrefix) && actionName.StartsWith(actionNamePrefix))
        {
            processedName = actionName.Substring(actionNamePrefix.Length);
        }

        // Convert to lowercase if enabled
        if (useLowercaseEvents)
        {
            processedName = processedName.ToLower();
        }

        return processedName;
    }

    void CreateDefaultInputs()
    {
        Debug.Log("Creating default inputs");

        // Create a simple default action map
        currentActionMap = new InputActionMap("Default");

        // Default 1D Axis movement
        InputAction moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");
        currentActionMap.AddAction("Move");

        // Default buttons
        DefaultButton[] defaultButtons = new DefaultButton[]
        {
            new DefaultButton { name = "Jump", binding = "<Keyboard>/space" },
            new DefaultButton { name = "Attack", binding = "<Keyboard>/leftCtrl" },
            new DefaultButton { name = "Grapple", binding = "<Keyboard>/rightCtrl" },
            new DefaultButton { name = "Down", binding = "<Keyboard>/s" },
            new DefaultButton { name = "Dash", binding = "<Keyboard>/leftShift" },
            new DefaultButton { name = "Lunge", binding = "<Keyboard>/q" }
        };

        foreach (var button in defaultButtons)
        {
            InputAction action = new InputAction(button.name, InputActionType.Button, button.binding);
            currentActionMap.AddAction(button.name);
        }

        // Enable the map
        currentActionMap.Enable();

        // Auto-detect from our created map
        SetupAutoDetectedActions();
    }

    private struct DefaultButton
    {
        public string name;
        public string binding;
    }

    void EnableAllActions()
    {
        if (currentActionMap != null)
        {
            currentActionMap.Enable();
        }
    }

    void OnEnable() => EnableAllActions();
    void OnDisable()
    {
        if (currentActionMap != null)
        {
            currentActionMap.Disable();
        }
    }

    void Update()
    {
        if (character == null) return;

        // Only handle non-movement inputs in Update
        foreach (var mapping in actionMappings)
        {
            if (mapping.eventName == "move") continue;
            mapping.Update(character, logEvents);
        }
    }

    void FixedUpdate()
    {
        if (character == null) return;

        // Handle movement ONLY in FixedUpdate
        HandleMovementContinuous();
    }

    private void HandleMovementContinuous()
    {
        var moveAction = currentActionMap?.FindAction("Move");
        if (moveAction == null) return;

        try
        {
            // Read the current value every frame
            float currentValue = moveAction.ReadValue<float>();

            // Send move_input event with the current value (even if it's 0)
            if (logEvents && Mathf.Abs(currentValue) > 0.01f)
            {
                Debug.Log($"Movement continuous: {currentValue}");
            }

            // Always send the event, even when input is 0
            character.RaiseEvent("move_input", new Vector2(currentValue, 0));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading movement: {e.Message}");
        }
    }
}

// Updated InputActionMapping class with proper 1D Axis support
[System.Serializable]
public class InputActionMapping
{
    public InputAction action;
    public string eventName;
    public InputActionType actionType;

    private float lastValue; // Changed to float for 1D axis

    public void Update(CharacterCore character, bool logEvents = false)
    {
        if (character == null || action == null) return;

        switch (actionType)
        {
            case InputActionType.Button:
                HandleButton(character, logEvents);
                break;
            case InputActionType.Value:
                HandleValue(character, logEvents);
                break;
            case InputActionType.PassThrough:
                HandlePassThrough(character, logEvents);
                break;
        }
    }

    private void HandleButton(CharacterCore character, bool logEvents)
    {
        if (action.WasPressedThisFrame())
        {
            if (logEvents) Debug.Log($"Button pressed: {eventName}");
            character.RaiseEvent($"{eventName}_pressed");
        }

        if (action.WasReleasedThisFrame())
        {
            if (logEvents) Debug.Log($"Button released: {eventName}");
            character.RaiseEvent($"{eventName}_released");
        }

        if (action.IsPressed())
        {
            character.RaiseEvent($"{eventName}_held");
        }
    }

    private void HandleValue(CharacterCore character, bool logEvents)
    {
        // SPECIAL CASE: For Move action, we need to handle it differently
        if (eventName == "move")
        {
            HandleMovement(character, logEvents);
            return;
        }

        // For other value actions, try to read as Vector2 first
        try
        {
            Vector2 currentValue = action.ReadValue<Vector2>();
            if (currentValue != Vector2.zero || lastValue != 0f)
            {
                if (logEvents) Debug.Log($"Value: {eventName} = {currentValue}");
                character.RaiseEvent($"{eventName}_input", currentValue);
                lastValue = currentValue.magnitude;
            }
        }
        catch
        {
            // If Vector2 fails, try float
            try
            {
                float currentValue = action.ReadValue<float>();
                if (Mathf.Abs(currentValue - lastValue) > 0.01f)
                {
                    if (logEvents) Debug.Log($"Value (float): {eventName} = {currentValue}");
                    character.RaiseEvent($"{eventName}_input", currentValue);
                    lastValue = currentValue;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to read value for {eventName}: {e.Message}");
            }
        }
    }

    private void HandleMovement(CharacterCore character, bool logEvents)
    {
        try
        {
            // Try to read as float (for 1D axis)
            float currentValue = action.ReadValue<float>();

            // Only send event if value changed significantly
            if (Mathf.Abs(currentValue - lastValue) > 0.01f)
            {
                if (logEvents) Debug.Log($"Move input: {currentValue}");

                // Send as Vector2 with X component only
                character.RaiseEvent("move_input", new Vector2(currentValue, 0));
                lastValue = currentValue;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to read movement value: {e.Message}");

            // Fallback: try Vector2
            try
            {
                Vector2 vecValue = action.ReadValue<Vector2>();
                if (logEvents) Debug.Log($"Move input (Vector2): {vecValue}");
                character.RaiseEvent("move_input", new Vector2(vecValue.x, 0));
            }
            catch
            {
                // Last resort: check button states
                Debug.LogWarning("Using button fallback for movement");
                float direction = 0f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    direction -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    direction += 1f;

                character.RaiseEvent("move_input", new Vector2(direction, 0));
            }
        }
    }

    private void HandlePassThrough(CharacterCore character, bool logEvents)
    {
        try
        {
            var value = action.ReadValueAsObject();
            if (logEvents) Debug.Log($"PassThrough: {eventName} = {value}");
            character.RaiseEvent($"{eventName}_input", value);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to read pass-through for {eventName}: {e.Message}");
        }
    }
}