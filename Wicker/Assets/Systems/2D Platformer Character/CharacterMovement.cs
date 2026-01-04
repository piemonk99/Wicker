using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class CharacterMovement : MonoBehaviour, ICharacterComponent
{
    private MovementConfig movementConfig;

    private float maxSpeed;
    private float groundAcceleration;
    private float groundDeceleration;
    private float airAcceleration;
    private float airDeceleration;
    private float jumpForce;
    private float gravity;
    private float coyoteTime;
    private float jumpBufferTime;
    private bool enableVariableJump;
    private float jumpCutMultiplier;
    private bool smoothDropdown;
    private float maxDropdownTime;
    private float minGroundedTime;
    private LayerMask groundLayer;
    private LayerMask platformLayer;
    private float groundCheckRadius;

    // Layers to currently ground check
    private LayerMask standableLayers;

    // Collision layers for the player
    private int originalLayer;
    private int dropLayer;

    [Header("Ground Check Reference")]
    public Transform groundCheck;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // State
    private CharacterCore character;
    private Rigidbody2D rb;
    private float groundedTimer;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float dropDownTimer;
    private bool isDroppingDown;
    private bool isJumping;
    private bool wasGrounded;
    private bool jumpWasReleased;

    // Input tracking
    private float currentInputX = 0f;
    private float lastInputDirection = 1f; // Default to right (positive)

    // State management
    private MovementStateManager stateManager;

    public void Initialize(CharacterCore core)
    {
        character = core;
        rb = GetComponent<Rigidbody2D>();

        // Initialize state manager
        stateManager = gameObject.AddComponent<MovementStateManager>();
        stateManager.showDebugInfo = showDebugInfo;
        stateManager.RaiseEvent += HandleStateEvent;
        stateManager.Initialize();

        movementConfig = character.GetConfig().movement;
        if (movementConfig != null)
        {
            maxSpeed = movementConfig.maxSpeed;
            groundAcceleration = movementConfig.groundAcceleration;
            groundDeceleration = movementConfig.groundDeceleration;
            airAcceleration = movementConfig.airAcceleration;
            airDeceleration = movementConfig.airDeceleration;
            jumpForce = movementConfig.jumpForce;
            gravity = movementConfig.gravity;
            coyoteTime = movementConfig.coyoteTime;
            jumpBufferTime = movementConfig.jumpBufferTime;
            enableVariableJump = movementConfig.enableVariableJump;
            jumpCutMultiplier = movementConfig.jumpCutMultiplier;
            smoothDropdown = movementConfig.smoothDropdown;
            maxDropdownTime = movementConfig.maxDropdownTime;
            minGroundedTime = movementConfig.minGroundedTime;
            groundLayer = movementConfig.groundLayer;
            platformLayer = movementConfig.platformLayer;
            groundCheckRadius = movementConfig.groundCheckRadius;
        }
        else
        {
            Debug.LogError("No CharacterConfig found!");
            movementConfig = new MovementConfig(); // Fallback
        }

        standableLayers = groundLayer + platformLayer;

        // Save original layer and get drop layer
        originalLayer = gameObject.layer;
        dropLayer = LayerMask.NameToLayer("CharacterDroppingDown");

        character.OnEvent -= HandleEvent;
        character.OnEvent += HandleEvent;
    }

    private void HandleStateEvent(string type, object data)
    {
        // Forward state manager events to character
        character.RaiseEvent(type, data);
    }

    // Event handling
    private void HandleEvent(string type, object data)
    {
        switch (type)
        {
            case "move_input":
                if (stateManager.GetEffectiveState().allowMovement)
                {
                    Vector2 input = (Vector2)data;
                    HandleHorizontalMovement(input.x);

                    // Update input tracking
                    UpdateInputDirection(input.x);
                }
                break;

            case "jump_pressed":
                jumpBufferTimer = jumpBufferTime;
                if (stateManager.GetEffectiveState().canJump && IsGrounded() && !isJumping)
                    PerformJump();
                break;

            case "jump_released":
                if (enableVariableJump && isJumping && rb.linearVelocity.y > 0 && !jumpWasReleased)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
                    jumpWasReleased = true;
                }
                break;

            case "down_held":
                bool isGrappling = character.CharacterContext.TryGetValue("grapple_isGrappling", out var value) && value is bool b && b;
                bool canDrop = ((IsGrounded() && groundedTimer >= minGroundedTime) || smoothDropdown) && !isDroppingDown && !isGrappling;

                if (canDrop) StartDropDown();
                break;

            case "down_released":
                if (isDroppingDown && smoothDropdown && dropDownTimer <= 0f) { StopDropDown(); }
                break;

            // Two-tier state management events - now handled by state manager
            case "movement_base_set":
                stateManager.PushBaseState((MovementState)data);
                break;

            case "movement_base_clear":
                stateManager.RemoveBaseState((string)data);
                break;

            case "movement_modifier_add":
                stateManager.AddModifier((MovementState)data);
                break;

            case "movement_modifier_remove":
                stateManager.RemoveModifier((string)data);
                break;

            case "movement_modifiers_clear":
                stateManager.ClearAllModifiers();
                break;

            case "movement_state_update":
                // Update existing state (used by grapple dynamic updates)
                MovementState updatedState = (MovementState)data;
                if (updatedState.type == MovementStateType.Base &&
                    stateManager.GetCurrentBaseState().name == updatedState.name)
                {
                    stateManager.PushBaseState(updatedState); // This will update existing state
                }
                break;

            case "movement_force":
                Vector2 force = (Vector2)data;
                ApplyExternalForce(force);
                break;

            case "movement_set_velocity":
                Vector2 velocity = (Vector2)data;
                SetExternalVelocity(velocity);
                break;

            case "config_changed":
                CharacterConfig newConfig = (CharacterConfig)data;
                movementConfig = newConfig.movement;
                break;
        }
    }

    private void HandleHorizontalMovement(float inputX)
    {
        MovementState effectiveState = stateManager.GetEffectiveState();
        float stateMaxSpeed = maxSpeed * effectiveState.maxSpeedMultiplier;
        float targetSpeed = inputX * stateMaxSpeed;

        float effectiveAcceleration = GetEffectiveAcceleration();
        float effectiveDeceleration = GetEffectiveDeceleration();

        float accelRate;
        if (Mathf.Abs(inputX) > 0.01f)
        {
            if (Mathf.Abs(rb.linearVelocity.x) < Mathf.Abs(stateMaxSpeed) ||
                Mathf.Sign(inputX) != Mathf.Sign(rb.linearVelocity.x))
            {
                accelRate = effectiveAcceleration;
            }
            else
            {
                accelRate = effectiveDeceleration;
            }
        }
        else
        {
            accelRate = effectiveDeceleration;
        }

        float acceleration = Mathf.Sign(targetSpeed - rb.linearVelocity.x) * accelRate;
        rb.linearVelocity = new Vector2(
            Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, Mathf.Abs(acceleration) * Time.fixedDeltaTime),
            rb.linearVelocity.y
        );
    }

    private void UpdateInputDirection(float inputX)
    {
        currentInputX = inputX;

        // Update last direction when there's significant input
        if (Mathf.Abs(inputX) > 0.01f)
        {
            lastInputDirection = Mathf.Sign(inputX);
        }
    }

    private float GetEffectiveAcceleration()
    {
        MovementState effectiveState = stateManager.GetEffectiveState();
        if (IsGrounded())
        {
            return groundAcceleration * effectiveState.groundAccelerationMultiplier;
        }
        else
        {
            return airAcceleration * effectiveState.airAccelerationMultiplier;
        }
    }

    private float GetEffectiveDeceleration()
    {
        MovementState effectiveState = stateManager.GetEffectiveState();
        if (!effectiveState.applyDeceleration) return 0f;

        if (IsGrounded())
        {
            return groundDeceleration * effectiveState.groundDecelerationMultiplier;
        }
        else
        {
            return airDeceleration * effectiveState.airDecelerationMultiplier;
        }
    }

    public void Tick(float deltaTime)
    {
        coyoteTimer -= deltaTime;
        jumpBufferTimer -= deltaTime;

        // Determines automatic ending of dropdown state if smoothDropdown = false
        if (isDroppingDown)
        {
            dropDownTimer -= deltaTime;

            if (dropDownTimer <= 0f)
            {
                StopDropDown();
            }
        }

        bool isGrounded = CheckGround();
        if (!wasGrounded && isGrounded) // Landed
        {
            character.RaiseEvent("landed", rb.linearVelocity.y);
            isJumping = false;
            jumpWasReleased = false;

            groundedTimer = 0f;

            if (jumpBufferTimer > 0 && stateManager.GetEffectiveState().canJump)
            {
                PerformJump();
            }
        }

        if (wasGrounded && !isGrounded) // Left the ground
        {
            groundedTimer = 0f;
        }

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            groundedTimer += deltaTime;
        }

        wasGrounded = isGrounded;
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        MovementState effectiveState = stateManager.GetEffectiveState();
        if (effectiveState.applyGravity && !IsGrounded())
        {
            float currentGravity = gravity * effectiveState.gravityMultiplier;
            rb.linearVelocity += Vector2.down * currentGravity * fixedDeltaTime;
        }

        character.RaiseEvent("velocity_changed", rb.linearVelocity);
    }

    private void PerformJump()
    {
        MovementState effectiveState = stateManager.GetEffectiveState();
        float stateJumpForce = jumpForce * effectiveState.jumpForceMultiplier;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, stateJumpForce);
        isJumping = true;
        jumpWasReleased = false;
        coyoteTimer = 0;
        jumpBufferTimer = 0;
        character.RaiseEvent("jumped", stateJumpForce);
    }

    private void StartDropDown()
    {
        dropDownTimer = maxDropdownTime;
        isDroppingDown = true;

        // Switch to dropping layer
        gameObject.layer = dropLayer;

        // Also update ground check
        standableLayers &= ~platformLayer;

        character.RaiseEvent("drop_down_started", null);
    }

    private void StopDropDown()
    {
        dropDownTimer = 0f;
        isDroppingDown = false;

        // Restore original layer
        gameObject.layer = originalLayer;

        // Restore ground check
        standableLayers |= platformLayer;

        character.RaiseEvent("drop_down_ended", null);
    }

    private bool CheckGround()
    {
        if (groundCheck == null) return false;

        // Player must be moving downward or stationary to be grounded
        if (rb.linearVelocity.y > 0.001f) return false;

        // Define ground check rectangle dimensions
        float checkWidth = 0.8f; // 80% of player width
        float checkHeight = 0.1f; // Thin rectangle below player
        float checkYOffset = -0.05f; // Position slightly below player center

        // Get player collider bounds (assuming BoxCollider2D)
        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null) return false;

        Bounds playerBounds = playerCollider.bounds;

        // Create two rectangle positions
        Vector2 groundCheckCenter = new Vector2(
            groundCheck.position.x,
            playerBounds.min.y - (checkHeight / 2f) + checkYOffset
        );

        Vector2 insideCheckCenter = new Vector2(
            groundCheck.position.x,
            playerBounds.min.y + (playerBounds.size.y * 0.25f) // Check bottom quarter of player
        );

        // Perform rectangle casts
        Vector2 checkSize = new Vector2(checkWidth, checkHeight);

        // Cast 1: Ground check (below player)
        Collider2D[] groundHits = Physics2D.OverlapBoxAll(
            groundCheckCenter,
            checkSize,
            0f,
            standableLayers
        );

        // Cast 2: Inside check (bottom of player)
        Vector2 insideCheckSize = new Vector2(checkWidth * 0.9f, playerBounds.size.y * 0.25f);
        Collider2D[] insideHits = Physics2D.OverlapBoxAll(
            insideCheckCenter,
            insideCheckSize,
            0f,
            standableLayers
        );

        // Filter: groundHits minus insideHits
        foreach (Collider2D groundHit in groundHits)
        {
            if (groundHit.isTrigger) continue;

            // Skip if this collider is also in insideHits
            bool isInsideCollider = false;
            foreach (Collider2D insideHit in insideHits)
            {
                if (insideHit == groundHit)
                {
                    isInsideCollider = true;
                    break;
                }
            }

            if (isInsideCollider) continue;

            // Regular collider - valid ground
            return true;
        }

        return false;
    }

    private bool IsGrounded()
    {
        return CheckGround() || coyoteTimer > 0;
    }

    // 
    public Vector2 GetVelocity() => rb.linearVelocity;
    public float GetHorizontalVelocity() => rb.linearVelocity.x;
    public float GetVerticalVelocity() => rb.linearVelocity.y;
    public bool IsMoving() => Mathf.Abs(rb.linearVelocity.x) > 0.1f;

    // Updated: Get current input direction with last direction fallback
    public float GetCurrentXDirection()
    {
        // If there's current input, use it
        if (Mathf.Abs(currentInputX) > 0.01f)
        {
            return Mathf.Sign(currentInputX);
        }

        // Otherwise return last direction (defaults to right/1)
        return lastInputDirection;
    }

    // Additional helper methods for input state
    public float GetCurrentInputX() => currentInputX;
    public float GetLastInputDirection() => lastInputDirection;
    public bool HasActiveInput() => Mathf.Abs(currentInputX) > 0.01f;

    // External control methods
    public void ApplyExternalForce(Vector2 force, ForceMode2DExtended forceMode = ForceMode2DExtended.Force)
    {
        // Implementation remains the same as before
        switch (forceMode)
        {
            case ForceMode2DExtended.Force:
                rb.AddForce(force);
                break;
            case ForceMode2DExtended.Impulse:
                rb.AddForce(force, ForceMode2D.Impulse);
                break;
            case ForceMode2DExtended.VelocityChange:
                rb.linearVelocity += force;
                break;
            case ForceMode2DExtended.Acceleration:
                rb.AddForce(force * rb.mass);
                break;
        }
    }

    public void SetExternalVelocity(Vector2 velocity) { rb.linearVelocity = velocity; }
    public void AddExternalVelocity(Vector2 velocity) { rb.linearVelocity += velocity; }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Collider2D playerCollider = GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                Bounds playerBounds = playerCollider.bounds;

                // Draw ground check rectangle
                Gizmos.color = Color.green;
                Vector2 checkSize = new Vector2(playerBounds.size.x * 0.8f, 0.1f);
                Vector2 checkCenter = new Vector2(
                    playerBounds.center.x,
                    playerBounds.min.y - 0.05f
                );
                Gizmos.DrawWireCube(checkCenter, checkSize);

                // Draw inside check area
                Gizmos.color = Color.red;
                Vector2 insideSize = new Vector2(playerBounds.size.x * 0.8f, playerBounds.size.y * 0.25f);
                Vector2 insideCenter = new Vector2(
                    playerBounds.center.x,
                    playerBounds.min.y + (playerBounds.size.y * 0.125f)
                );
                Gizmos.DrawWireCube(insideCenter, insideSize);
            }
        }
    }

    public void Freeze()
    {
        // Now uses modifier system through state manager
        stateManager.AddModifier(new MovementState(
            name: "Frozen",
            type: MovementStateType.Modifier,
            allowMovement: false,
            applyGravity: false,
            applyDeceleration: false,
            canJump: false,
            gravityMultiplier: 0f,
            groundAccelerationMultiplier: 0f,
            groundDecelerationMultiplier: 0f,
            airAccelerationMultiplier: 0f,
            airDecelerationMultiplier: 0f,
            jumpForceMultiplier: 0f,
            maxSpeedMultiplier: 0f
        ));
    }

    public void Unfreeze()
    {
        stateManager.RemoveModifier("Frozen");
    }
}
public enum ForceMode2DExtended
{
    Force,          // mass-dependent continuous force
    Impulse,        // mass-dependent instant force
    VelocityChange, // mass-independent instant velocity change
    Acceleration    // mass-independent continuous acceleration
}