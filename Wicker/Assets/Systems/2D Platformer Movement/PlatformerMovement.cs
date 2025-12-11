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
        public float decelerationMultiplier = 1f;
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
            float decelerationMultiplier = 1f,
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
            this.decelerationMultiplier = decelerationMultiplier;
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
        decelerationMultiplier: 1f,
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
            case "move_input":
                if (currentState.allowMovement)
                {
                    Vector2 input = (Vector2)data;
                    HandleHorizontalMovement(input.x);
                }
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

        // Use current state-appropriate deceleration with multiplier
        float effectiveDeceleration = currentState.applyDeceleration ?
            (IsGrounded() ? deceleration * currentState.decelerationMultiplier :
             deceleration * airDecelerationMultiplier * currentState.decelerationMultiplier) :
            0f;

        // Apply acceleration multiplier
        float effectiveAcceleration = IsGrounded() ?
            acceleration * currentState.accelerationMultiplier :
            acceleration * airAccelerationMultiplier * currentState.accelerationMultiplier;

        float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? effectiveAcceleration : effectiveDeceleration;

        float movement = Mathf.Pow(Mathf.Abs(speedDiff) * accelRate, 0.5f) * Mathf.Sign(speedDiff);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement * Time.fixedDeltaTime, rb.linearVelocity.y);
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

    // External control (for grapple system)
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
            decelerationMultiplier: 0f,
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
}