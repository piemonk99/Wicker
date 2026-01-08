using UnityEngine;
using System.Collections.Generic;

public class CharacterTurnaround
{
    // Configuration
    private float maxSpeed;
    private float turnaroundThreshold;
    private float turnaroundMagnitude;
    private float turnaroundDelay;
    private int turnaroundHistoryFrames;
    private int turnaroundHistoryDeadzone;
    private bool enableTurnaround;

    // Velocity history tracking with deadzone support
    private Queue<float> velocityHistory = new Queue<float>();
    private Queue<float> deadzoneHistory = new Queue<float>();
    private float velocityHistorySum = 0f;
    private float deadzoneHistorySum = 0f;

    // Turnaround state tracking
    private float turnaroundCooldownTimer = 0f;
    private float turnaroundDelayTimer = 0f;
    private bool isInTurnaroundDelay = false;
    private float turnaroundStartVelocityX = 0f;
    private float turnaroundTargetDirection = 0f;
    private float turnaroundActivationInputDirection = 0f;
    private const float TURNAROUND_COOLDOWN = 0.2f; // 200ms cooldown

    // Dependencies
    private CharacterCore character;
    private Rigidbody2D rb;
    private MovementStateManager stateManager;

    // Debug
    private bool debugTurnaround;

    public CharacterTurnaround(CharacterCore character, Rigidbody2D rb, MovementStateManager stateManager,
                              MovementConfig config, bool debugTurnaround = false)
    {
        this.character = character;
        this.rb = rb;
        this.stateManager = stateManager;
        this.debugTurnaround = debugTurnaround;

        UpdateConfig(config);
    }

    public void UpdateConfig(MovementConfig config)
    {
        maxSpeed = config.maxSpeed;
        turnaroundThreshold = config.turnaroundThreshold;
        turnaroundMagnitude = config.turnaroundMagnitude;
        turnaroundDelay = config.turnaroundDelay;
        turnaroundHistoryFrames = Mathf.Max(1, config.turnaroundHistoryFrames);
        turnaroundHistoryDeadzone = Mathf.Max(0, config.turnaroundHistoryDeadzone);
        enableTurnaround = config.enableTurnaround;

        InitializeHistoryQueues();
    }

    private void InitializeHistoryQueues()
    {
        velocityHistory.Clear();
        deadzoneHistory.Clear();
        velocityHistorySum = 0f;
        deadzoneHistorySum = 0f;

        // Initialize deadzone history
        for (int i = 0; i < turnaroundHistoryDeadzone; i++)
        {
            deadzoneHistory.Enqueue(0f);
            deadzoneHistorySum += 0f;
        }

        // Initialize main velocity history
        for (int i = 0; i < turnaroundHistoryFrames; i++)
        {
            velocityHistory.Enqueue(0f);
            velocityHistorySum += 0f;
        }
    }

    public void UpdateVelocityHistory(float currentVelocityX)
    {
        if (!enableTurnaround) return;

        // First, update deadzone history
        if (turnaroundHistoryDeadzone > 0)
        {
            float oldestDeadzoneVelocity = deadzoneHistory.Dequeue();
            deadzoneHistorySum -= Mathf.Abs(oldestDeadzoneVelocity);

            float newDeadzoneVelocity = currentVelocityX;
            deadzoneHistory.Enqueue(newDeadzoneVelocity);
            deadzoneHistorySum += Mathf.Abs(newDeadzoneVelocity);

            // Skip adding to main history if we're still in deadzone frames
            if (deadzoneHistory.Count > 0) // Always true, but check for safety
            {
                // Use the oldest value from deadzone queue for main history
                float valueForMainHistory = deadzoneHistory.Peek(); // Get oldest without removing

                // Update main velocity history with delayed value
                float oldestMainVelocity = velocityHistory.Dequeue();
                velocityHistorySum -= Mathf.Abs(oldestMainVelocity);

                velocityHistory.Enqueue(valueForMainHistory);
                velocityHistorySum += Mathf.Abs(valueForMainHistory);
            }
        }
        else
        {
            // No deadzone - update main history directly
            float oldestVelocity = velocityHistory.Dequeue();
            velocityHistorySum -= Mathf.Abs(oldestVelocity);

            float newVelocity = currentVelocityX;
            velocityHistory.Enqueue(newVelocity);
            velocityHistorySum += Mathf.Abs(newVelocity);
        }
    }

    public void CheckForTurnaroundActivation(float previousVelocityX, float currentVelocityX, float inputX, bool isGrounded)
    {
        if (!enableTurnaround) return;
        if (!Time.inFixedTimeStep) return;

        // Update cooldown timer
        turnaroundCooldownTimer -= Time.fixedDeltaTime;

        // Check if we're already in turnaround delay
        if (isInTurnaroundDelay)
        {
            turnaroundDelayTimer -= Time.fixedDeltaTime;

            // Check if delay has expired
            if (turnaroundDelayTimer <= 0f)
            {
                ExecuteTurnaroundDash();
            }
            return;
        }

        // Check if we have opposite direction input (not necessarily velocity crossing 0)
        bool isOppositeInput = Mathf.Abs(inputX) > 0.01f &&
                               Mathf.Sign(inputX) != Mathf.Sign(previousVelocityX);

        // Check if cooldown has expired
        bool cooldownExpired = turnaroundCooldownTimer <= 0f;

        // Check if we're moving fast enough in the old direction (using velocity history with deadzone)
        float averageSpeed = GetEffectiveAverageSpeed();
        float requiredAverageSpeed = maxSpeed * turnaroundThreshold;

        // Check if input magnitude is significant
        bool hasStrongInput = Mathf.Abs(inputX) > 0.5f; // Require at least 50% input

        // NEW: Activate turnaround when we have opposite input (not waiting for velocity to cross 0)
        if (isOppositeInput && isGrounded && cooldownExpired && hasStrongInput && averageSpeed >= requiredAverageSpeed)
        {
            StartTurnaroundDelay(Mathf.Sign(inputX), Mathf.Sign(previousVelocityX));

            if (debugTurnaround)
            {
                Debug.Log($"Turnaround activated! AvgSpeed={averageSpeed:F1}, Required={requiredAverageSpeed:F1}, " +
                         $"Direction={Mathf.Sign(inputX)}, Delay={turnaroundDelay}s");
            }
        }

        // Also check for traditional velocity sign change (for backwards compatibility)
        else
        {
            bool velocityChangedSign = Mathf.Sign(previousVelocityX) != Mathf.Sign(currentVelocityX) &&
                                      Mathf.Abs(previousVelocityX) > 0.1f &&
                                      Mathf.Abs(currentVelocityX) > 0.1f;

            bool inputMatchesNewDirection = Mathf.Abs(inputX) > 0.01f &&
                                           Mathf.Sign(inputX) == Mathf.Sign(currentVelocityX);

            if (velocityChangedSign && isOppositeInput && isGrounded && inputMatchesNewDirection &&
                cooldownExpired && averageSpeed >= requiredAverageSpeed)
            {
                StartTurnaroundDelay(Mathf.Sign(inputX), Mathf.Sign(previousVelocityX));

                if (debugTurnaround)
                {
                    Debug.Log($"Turnaround activated (velocity sign change)! AvgSpeed={averageSpeed:F1}, " +
                             $"Required={requiredAverageSpeed:F1}, Direction={Mathf.Sign(inputX)}");
                }
            }
        }
    }

    private void StartTurnaroundDelay(float targetDirection, float oppositeVelocityDirection)
    {
        isInTurnaroundDelay = true;
        turnaroundDelayTimer = turnaroundDelay;
        turnaroundStartVelocityX = rb.linearVelocity.x;
        turnaroundTargetDirection = targetDirection;
        turnaroundActivationInputDirection = targetDirection; // Store the input that activated the turnaround

        // Apply turnaround modifier state (only affects ground acceleration)
        stateManager.AddModifier(new MovementState(
            name: "TurnaroundDelay",
            type: MovementStateType.Modifier,
            allowMovement: true,
            applyGravity: true,
            applyDeceleration: true,
            canJump: false, // Prevent jumping during turnaround delay
            gravityMultiplier: 1f,
            groundAccelerationMultiplier: 0f,  // Only change this - zero acceleration during delay
            groundDecelerationMultiplier: 1f,
            airAccelerationMultiplier: 1f,
            airDecelerationMultiplier: 1f,
            jumpForceMultiplier: 1f,
            maxSpeedMultiplier: 1f
        ));

        character.RaiseEvent("turnaround_delay_started", turnaroundDelay);

        if (debugTurnaround)
        {
            Debug.Log($"Turnaround delay started for {turnaroundDelay}s. Target direction: {targetDirection}, " +
                     $"Opposite velocity direction: {oppositeVelocityDirection}");
        }
    }

    public void CheckTurnaroundCancellation(float inputX, bool isGrounded)
    {
        if (!enableTurnaround || !isInTurnaroundDelay) return;

        // Cancel if no longer grounded
        if (!isGrounded)
        {
            CancelTurnaround("left ground");
            return;
        }

        // Cancel if input no longer opposes the original velocity direction
        // We need to check if input is now in the same direction as when we started the turnaround
        // OR if input is neutral (released)
        if (Mathf.Abs(inputX) < 0.01f)
        {
            // Input released - cancel turnaround
            CancelTurnaround("input released");
            return;
        }

        // Check if input direction has changed from activation direction
        float currentInputDirection = Mathf.Sign(inputX);
        if (Mathf.Abs(currentInputDirection - turnaroundActivationInputDirection) > 0.1f)
        {
            // Input direction changed - cancel turnaround
            CancelTurnaround("input direction changed");
        }
    }

    private void ExecuteTurnaroundDash()
    {
        // Only execute if we're still in the turnaround state
        if (!isInTurnaroundDelay) return;

        // Remove the delay modifier
        stateManager.RemoveModifier("TurnaroundDelay");
        isInTurnaroundDelay = false;

        // Apply turnaround dash!
        float dashDirection = turnaroundTargetDirection;
        float dashVelocity = turnaroundMagnitude * dashDirection;

        rb.linearVelocity = new Vector2(
            dashVelocity,  // Start fresh with dash velocity
            rb.linearVelocity.y
        );

        // Set cooldown
        turnaroundCooldownTimer = TURNAROUND_COOLDOWN;

        // Raise event for animations/effects
        character.RaiseEvent("turnaround_dash", dashVelocity);

        if (debugTurnaround)
        {
            Debug.Log($"Turnaround dash executed! Direction: {dashDirection}, " +
                     $"Added velocity: {dashVelocity}, New velocity: {rb.linearVelocity.x}");
        }
    }

    public void CancelTurnaround(string reason)
    {
        if (!enableTurnaround || !isInTurnaroundDelay) return;

        stateManager.RemoveModifier("TurnaroundDelay");
        isInTurnaroundDelay = false;
        turnaroundDelayTimer = 0f;

        // Reset cooldown so player can try again immediately
        turnaroundCooldownTimer = 0f;

        character.RaiseEvent("turnaround_cancelled", reason);

        if (debugTurnaround)
        {
            Debug.Log($"Turnaround cancelled: {reason}");
        }
    }

    private float GetEffectiveAverageSpeed()
    {
        if (turnaroundHistoryFrames == 0) return 0f;

        // Calculate average speed from history queue with each value capped at maxSpeed
        float cappedSum = 0f;
        foreach (float velocity in velocityHistory)
        {
            // Cap each individual velocity value at maxSpeed before summing
            float cappedVelocity = Mathf.Clamp(velocity, -maxSpeed, maxSpeed);
            cappedSum += Mathf.Abs(cappedVelocity);
        }

        return Mathf.Abs(cappedSum / turnaroundHistoryFrames);
    }

    public void Tick(float deltaTime)
    {
        if (!enableTurnaround) return;

        turnaroundCooldownTimer -= deltaTime;

        // Update turnaround delay timer
        if (isInTurnaroundDelay)
        {
            turnaroundDelayTimer -= deltaTime;

            // Check if delay has expired (in case we missed it in fixed update)
            if (turnaroundDelayTimer <= 0f)
            {
                ExecuteTurnaroundDash();
            }
        }
    }

    public void HandleEvent(string type, object data)
    {
        if (!enableTurnaround) return;

        switch (type)
        {
            case "grapple_started":
                if (isInTurnaroundDelay)
                {
                    CancelTurnaround("grapple started");
                }
                break;

            case "dash_pressed":
                if (isInTurnaroundDelay)
                {
                    CancelTurnaround("dash pressed");
                }
                break;

            case "jump_pressed":
                if (isInTurnaroundDelay)
                {
                    CancelTurnaround("jump pressed");
                }
                break;

            case "down_held":
                if (isInTurnaroundDelay)
                {
                    CancelTurnaround("dropdown");
                }
                break;
        }
    }

    // Public getters
    public bool IsInTurnaroundDelay() => isInTurnaroundDelay;
    public float GetTurnaroundDelayRemaining() => Mathf.Max(0f, turnaroundDelayTimer);
    public float GetTurnaroundAverageSpeed() => GetEffectiveAverageSpeed();
    public bool IsEnabled() => enableTurnaround;
}