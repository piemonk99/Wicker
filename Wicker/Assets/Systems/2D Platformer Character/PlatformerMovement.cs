using UnityEngine;
using System.Collections.Generic;

public class PlatformerMovement : MonoBehaviour, ICharacterComponent
{
    [System.Serializable]
    public class MovementState
    {
        public string name;

        // Input control
        public bool allowMovement = true;

        // Physics control
        public bool applyGravity = true;
        public bool applyDeceleration = true;
        public bool canJump = true;

        // Multipliers
        public float gravityMultiplier = 1f;
        public float accelerationMultiplier = 1f;
        public float airAccelerationMultiplier = 1f;
        public float decelerationMultiplier = 1f;
        public float airDecelerationMultiplier = 1f;
        public float jumpForceMultiplier = 1f;
        public float maxSpeedMultiplier = 1f;

        // Constructor for easier initialization
        public MovementState(
            string name = "Unnamed State",
            bool allowMovement = true,
            bool applyGravity = true,
            bool applyDeceleration = true,
            bool canJump = true,
            float gravityMultiplier = 1f,
            float accelerationMultiplier = 1f,
            float airAccelerationMultiplier = 1f,
            float decelerationMultiplier = 1f,
            float airDecelerationMultiplier = 1f,
            float jumpForceMultiplier = 1f,
            float maxSpeedMultiplier = 1f
        )
        {
            this.name = name;
            this.allowMovement = allowMovement;
            this.applyGravity = applyGravity;
            this.applyDeceleration = applyDeceleration;
            this.canJump = canJump;
            this.gravityMultiplier = gravityMultiplier;
            this.accelerationMultiplier = accelerationMultiplier;
            this.airAccelerationMultiplier = airAccelerationMultiplier;
            this.decelerationMultiplier = decelerationMultiplier;
            this.airDecelerationMultiplier = airDecelerationMultiplier;
            this.jumpForceMultiplier = jumpForceMultiplier;
            this.maxSpeedMultiplier = maxSpeedMultiplier;
        }
    }

    [Header("Movement Settings")]
    public float maxSpeed = 6f;
    public float acceleration = 20f;
    public float deceleration = 15f;
    public float jumpForce = 13f;
    public float gravity = 30f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.1f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airDecelerationMultiplier = 0.5f;
    [Range(0f, 1f)] public float airAccelerationMultiplier = 0.8f;

    [Header("Variable Jump Height")]
    public bool enableVariableJump = true;
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("Movement States")]
    public MovementState defaultState = new MovementState(
        name: "Default",
        allowMovement: true,
        applyGravity: true,
        applyDeceleration: true,
        canJump: true,
        gravityMultiplier: 1f,
        accelerationMultiplier: 1f,
        airAccelerationMultiplier: 1f,
        decelerationMultiplier: 1f,
        airDecelerationMultiplier: 1f,
        jumpForceMultiplier: 1f,
        maxSpeedMultiplier: 1f
    );

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;

    // State
    private CharacterCore character;
    private Rigidbody2D rb;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isJumping;
    private bool wasGrounded;
    private bool jumpWasReleased;

    // Movement state management
    private MovementState currentState;
    private Stack<MovementState> stateStack = new Stack<MovementState>();

    // Cached movement values
    private float currentDeceleration;
    private float currentAcceleration;

    public void Initialize(CharacterCore core)
    {
        character = core;
        rb = GetComponent<Rigidbody2D>();
        character.OnEvent += HandleEvent;

        // Initialize with default state
        currentState = defaultState;
        currentDeceleration = deceleration;
        currentAcceleration = acceleration;
    }

    public void Tick(float deltaTime)
    {
        coyoteTimer -= deltaTime;
        jumpBufferTimer -= deltaTime;

        bool isGrounded = CheckGround();

        // Landing detection
        if (!wasGrounded && isGrounded)
        {
            character.RaiseEvent("landed", rb.linearVelocity.y);
            isJumping = false;
            jumpWasReleased = false;

            // Buffered jump check
            if (jumpBufferTimer > 0 && currentState.canJump)
            {
                PerformJump();
            }
        }

        // Coyote time reset
        if (isGrounded)
            coyoteTimer = coyoteTime;

        wasGrounded = isGrounded;
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Apply gravity if state allows
        if (currentState.applyGravity && !IsGrounded())
        {
            float currentGravity = gravity * currentState.gravityMultiplier;
            rb.linearVelocity += Vector2.down * currentGravity * fixedDeltaTime;
        }

        character.RaiseEvent("velocity_changed", rb.linearVelocity);
    }

    private void HandleEvent(string type, object data)
    {
        switch (type)
        {
            // Player input events
            case "move_input":
                Vector2 input = (Vector2)data;
                HandleHorizontalMovement(input.x);
                break;

            case "jump_pressed":
                jumpBufferTimer = jumpBufferTime;

                if (currentState.canJump && IsGrounded() && !isJumping)
                    PerformJump();
                break;

            case "jump_released":
                if (enableVariableJump && isJumping && rb.linearVelocity.y > 0 && !jumpWasReleased)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
                    jumpWasReleased = true;
                }
                break;

            // State management events
            case "movement_override_start":
                MovementState overrideState = (MovementState)data;
                PushState(overrideState);
                break;

            case "movement_override_end":
                PopState();
                break;

            case "movement_force":
                Vector2 force = (Vector2)data;
                ApplyExternalForce(force);
                break;

            case "movement_set_velocity":
                Vector2 velocity = (Vector2)data;
                SetExternalVelocity(velocity);
                break;
        }
    }

    private void HandleHorizontalMovement(float inputX)
    {
        // Apply max speed multiplier
        float stateMaxSpeed = maxSpeed * currentState.maxSpeedMultiplier;
        float targetSpeed = inputX * stateMaxSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;

        // Use current state-appropriate acceleration and deceleration
        float effectiveAcceleration = GetEffectiveAcceleration();
        float effectiveDeceleration = GetEffectiveDeceleration();

        // Determine acceleration rate based on situation
        float accelRate;

        if (Mathf.Abs(inputX) > 0.01f)
        {
            // Player is trying to move
            if (Mathf.Abs(rb.linearVelocity.x) < Mathf.Abs(stateMaxSpeed))
            {
                // Below max speed - accelerate normally
                accelRate = effectiveAcceleration;
            }
            else if (Mathf.Sign(inputX) != Mathf.Sign(rb.linearVelocity.x))
            {
                // Trying to move opposite direction we are currently moving - add directionless deceleration with a negative acceleration
                accelRate = effectiveDeceleration + effectiveAcceleration;
            }
            else
            {
                // Attempting to move in the same direction we are currently going above the max speed in, use directionless deceleration
                accelRate = effectiveDeceleration;
            }
        }
        else
        {
            // No input - decelerate
            accelRate = effectiveDeceleration;
        }

        float movement = Mathf.Pow(Mathf.Abs(speedDiff) * accelRate, 0.5f) * Mathf.Sign(speedDiff);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement * Time.fixedDeltaTime, rb.linearVelocity.y);
    }

    private float GetEffectiveAcceleration()
    {
        float baseAccel = IsGrounded() ? acceleration : acceleration * airAccelerationMultiplier;
        float stateMultiplier = IsGrounded() ?
            currentState.accelerationMultiplier :
            currentState.accelerationMultiplier * currentState.airAccelerationMultiplier;

        return baseAccel * stateMultiplier;
    }

    private float GetEffectiveDeceleration()
    {
        if (!currentState.applyDeceleration) return 0f;

        float baseDecel = IsGrounded() ? deceleration : deceleration * airDecelerationMultiplier;
        float stateMultiplier = IsGrounded() ?
            currentState.decelerationMultiplier :
            currentState.decelerationMultiplier * currentState.airDecelerationMultiplier;

        return baseDecel * stateMultiplier;
    }

    // State management
    public void PushState(MovementState newState)
    {
        stateStack.Push(currentState);
        currentState = newState;
        character.RaiseEvent("movement_state_changed", newState.name);
    }

    public void PopState()
    {
        if (stateStack.Count > 0)
        {
            currentState = stateStack.Pop();
            character.RaiseEvent("movement_state_changed", currentState.name);
        }
        else
        {
            currentState = defaultState;
            character.RaiseEvent("movement_state_changed", "Default");
        }
    }

    public MovementState GetCurrentState() => currentState;

    // External control
    public void ApplyExternalForce(Vector2 force)
    {
        rb.AddForce(force, ForceMode2D.Force);
    }

    public void SetExternalVelocity(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
    }

    public void AddExternalVelocity(Vector2 velocity)
    {
        rb.linearVelocity += velocity;
    }

    public void Freeze()
    {
        PushState(new MovementState(
            name: "Frozen",
            allowMovement: false,
            applyGravity: false,
            applyDeceleration: false,
            canJump: false,
            gravityMultiplier: 0f,
            accelerationMultiplier: 0f,
            airAccelerationMultiplier: 0f,
            decelerationMultiplier: 0f,
            airDecelerationMultiplier: 0f,
            jumpForceMultiplier: 0f,
            maxSpeedMultiplier: 0f
        ));
    }

    public void Unfreeze()
    {
        PopState();
    }

    private void PerformJump()
    {
        // Apply jump force multiplier
        float stateJumpForce = jumpForce * currentState.jumpForceMultiplier;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, stateJumpForce);
        isJumping = true;
        jumpWasReleased = false;
        coyoteTimer = 0;
        jumpBufferTimer = 0;
        character.RaiseEvent("jumped", stateJumpForce);
    }

    private bool CheckGround()
    {
        if (groundCheck == null) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private bool IsGrounded()
    {
        return CheckGround() || coyoteTimer > 0;
    }

    // External references
    public Vector2 GetVelocity() => rb.linearVelocity;
    public float GetHorizontalVelocity() => rb.linearVelocity.x;
    public float GetVerticalVelocity() => rb.linearVelocity.y;
    public float GetCurrentXDirection() => Mathf.Sign(rb.linearVelocity.x);
    public bool IsMoving() => Mathf.Abs(rb.linearVelocity.x) > 0.1f;
}