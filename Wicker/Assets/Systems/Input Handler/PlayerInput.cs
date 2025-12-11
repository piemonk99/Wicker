using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [Header("Input Settings")]
    public InputActionAsset inputAsset;

    [Header("Character")]
    public CharacterCore character;

    // Auto-detected actions
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction grappleAction;
    private InputAction dashAction;

    void Start()
    {
        if (inputAsset == null)
        {
            Debug.LogError("No InputActionAsset assigned!");
            CreateDefaultInputs();
            return;
        }

        FindAndSetupActions();
    }

    void FindAndSetupActions()
    {
        // Try to find action maps
        InputActionMap gameplayMap = inputAsset.FindActionMap("Gameplay") ??
                                     inputAsset.FindActionMap("Player") ??
                                     (inputAsset.actionMaps.Count > 0 ? inputAsset.actionMaps[0] : null);

        if (gameplayMap == null)
        {
            Debug.LogError("No action maps found!");
            CreateDefaultInputs();
            return;
        }

        // Auto-detect actions
        moveAction = FindAction(gameplayMap, new[] { "Move", "Movement", "Walk" });
        jumpAction = FindAction(gameplayMap, new[] { "Jump", "JumpButton" });
        attackAction = FindAction(gameplayMap, new[] { "Attack", "Fire", "Primary" });
        grappleAction = FindAction(gameplayMap, new[] { "Grapple", "Hook", "Secondary" });
        dashAction = FindAction(gameplayMap, new[] { "Dash", "Roll", "Evade" });

        EnableAllActions();
    }

    void CreateDefaultInputs()
    {
        Debug.Log("Creating default keyboard controls");

        // Create default movement (WASD/Arrows)
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        // Default buttons
        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        attackAction = new InputAction("Attack", InputActionType.Button, "<Keyboard>/leftCtrl");
        grappleAction = new InputAction("Grapple", InputActionType.Button, "<Keyboard>/rightCtrl");
        dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");

        EnableAllActions();
    }

    InputAction FindAction(InputActionMap map, string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var action = map.FindAction(name);
            if (action != null)
                return action;
        }
        return null;
    }

    void EnableAllActions()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
        attackAction?.Enable();
        grappleAction?.Enable();
        dashAction?.Enable();
    }

    void OnEnable() => EnableAllActions();
    void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
        attackAction?.Disable();
        grappleAction?.Disable();
        dashAction?.Disable();
    }

    void Update()
    {
        if (character == null) return;

        // Movement - always send event (even zero)
        if (moveAction != null)
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            character.RaiseEvent("move_input", moveInput);
        }

        // Button events with consistent naming
        if (jumpAction?.WasPressedThisFrame() ?? false)
            character.RaiseEvent("jump_pressed");

        if (jumpAction?.WasReleasedThisFrame() ?? false)
            character.RaiseEvent("jump_released");

        if (attackAction?.WasPressedThisFrame() ?? false)
            character.RaiseEvent("attack_pressed");

        if (grappleAction?.WasPressedThisFrame() ?? false)
            character.RaiseEvent("grapple_pressed");

        if (grappleAction?.WasReleasedThisFrame() ?? false)
            character.RaiseEvent("grapple_released");

        if (dashAction?.WasPressedThisFrame() ?? false)
            character.RaiseEvent("dash_pressed");
    }
}