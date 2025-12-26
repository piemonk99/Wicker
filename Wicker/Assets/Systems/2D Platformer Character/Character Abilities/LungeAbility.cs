using UnityEngine;

[System.Serializable]
public class LungeAbility : CharacterAbility
{
    // Store the config reference directly
    private LungeConfig config;

    // State
    private float activeTimer = 0f;
    private float cooldownTimer = 0f;
    private float postLungeTimer = 0f;
    private GameObject trailInstance;
    private Vector2 lungeDirection;
    private bool isInPostLungePhase = false;
    private Vector2 currentContinuousForce;

    public LungeAbility()
    {
        AbilityName = "Lunge";
    }

    protected override void LoadConfig(CharacterConfig characterConfig)
    {
        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        config = characterConfig.lunge;
        IsEnabled = config.isEnabled;

        if (IsEnabled)
        {
            character.OnEvent += HandleEvent;
            Debug.Log($"Lunge ability loaded: Enabled={IsEnabled}, Force=({config.force.x}, {config.force.y})");
        }
    }

    private void HandleEvent(string type, object data)
    {
        if (type == "lunge_pressed" && CanActivate())
        {
            Activate();
        }
    }

    public override bool CanActivate()
    {
        return IsEnabled && cooldownTimer <= 0 && !IsActive && !isInPostLungePhase;
    }

    public override void Activate()
    {
        IsActive = true;
        activeTimer = config.duration;
        cooldownTimer = config.cooldown;
        isInPostLungePhase = false;

        // Store pre-lunge velocity
        Vector2 preLungeVelocity = rb.linearVelocity;

        // Determine lunge direction based on input
        lungeDirection = CalculateLungeDirection();

        // Apply velocity preservation
        if (config.cancelVerticalVelocity)
        {
            // Cancel vertical velocity completely
            rb.linearVelocity = new Vector2(
                preLungeVelocity.x * config.preserveHorizontalVelocity,
                0f
            );
        }
        else
        {
            // Normal preservation
            rb.linearVelocity = new Vector2(
                preLungeVelocity.x * config.preserveHorizontalVelocity,
                preLungeVelocity.y * config.preserveVerticalVelocity
            );
        }

        // Calculate and apply lunge force
        Vector2 lungeForce = new Vector2(
            lungeDirection.x * config.force.x,
            lungeDirection.y * config.force.y
        );

        if (config.applyInstantForce)
            ApplyForce(lungeForce, true);

        if (config.applyContinuousForce)
            currentContinuousForce = lungeForce * config.continuousForceMultiplier;

        // Set LUNGE as base state (priority 20, same as dash)
        character.RaiseEvent("movement_base_set", new CharacterMovement.MovementState(
            name: "Lunging",
            type: CharacterMovement.MovementStateType.Base,
            priority: 20,
            allowMovement: false,
            applyGravity: true, 
            applyDeceleration: false,
            canJump: false
        ));

        // Visual/audio feedback
        if (config.trailPrefab != null)
            trailInstance = GameObject.Instantiate(config.trailPrefab, transform.position, Quaternion.identity);

        if (config.particles != null)
            config.particles.Play();

        if (config.sound != null)
            AudioSource.PlayClipAtPoint(config.sound, transform.position);

        character.RaiseEvent("ability_used", AbilityName);
        OnActivated();
    }

    private Vector2 CalculateLungeDirection()
    {
        // Get movement input direction for horizontal
        float movementDirection = movement.GetCurrentXDirection();

        // Default vertical direction is up (positive Y)
        float verticalDirection = 1f;

        // Optional: Could add logic for downward lunges based on input
        // if (movement.IsHoldingDown()) verticalDirection = -1f;

        return new Vector2(movementDirection, verticalDirection).normalized;
    }

    private void ApplyForce(Vector2 force, bool isInstant)
    {
        if (isInstant)
        {
            if (config.massDependent)
                rb.AddForce(force, ForceMode2D.Impulse);
            else
                rb.linearVelocity += force;
        }
        else
        {
            if (config.massDependent)
                rb.AddForce(force, ForceMode2D.Force);
            else
                rb.AddForce(force * rb.mass, ForceMode2D.Force);
        }
    }

    public override void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        currentContinuousForce = Vector2.zero;

        // Clear the lunge base state
        character.RaiseEvent("movement_base_clear", "Lunging");

        // Start post-lunge if enabled
        if (config.applyPostLungeDeceleration)
        {
            isInPostLungePhase = true;
            postLungeTimer = config.postLungeDecelerationDuration;

            // Set POSTLUNGE as base state with lower priority (10)
            character.RaiseEvent("movement_base_set", new CharacterMovement.MovementState(
                name: "PostLunge",
                type: CharacterMovement.MovementStateType.Base,
                priority: 10,
                allowMovement: true,
                applyGravity: true,
                applyDeceleration: true,
                canJump: true,
                gravityMultiplier: 1f,
                groundAccelerationMultiplier: 1f,
                groundDecelerationMultiplier: config.postLungeGroundDecelerationMultilplier,
                airAccelerationMultiplier: 1f,
                airDecelerationMultiplier: config.postLungeAirDecelerationMultilplier,
                jumpForceMultiplier: 1f,
                maxSpeedMultiplier: 1f
            ));
        }
        else
        {
            CleanupTrail();
        }

        character.RaiseEvent("ability_ended", AbilityName);
        OnDeactivated();
    }

    private void CleanupTrail()
    {
        if (trailInstance != null)
        {
            GameObject.Destroy(trailInstance);
            trailInstance = null;
        }

        if (config.particles != null)
            config.particles.Stop();
    }

    public override void Tick(float deltaTime)
    {
        if (cooldownTimer > 0)
            cooldownTimer -= deltaTime;

        if (isInPostLungePhase)
        {
            UpdatePostLunge(deltaTime);
        }
        else if (IsActive)
        {
            UpdateActiveLunge(deltaTime);
        }
    }

    private void UpdateActiveLunge(float deltaTime)
    {
        // Apply continuous force
        if (config.applyContinuousForce && currentContinuousForce != Vector2.zero)
            ApplyForce(currentContinuousForce * deltaTime * 60f, false);

        // Update timer
        activeTimer -= deltaTime;
        if (activeTimer <= 0)
            Deactivate();

        // Update trail
        if (trailInstance != null)
            trailInstance.transform.position = transform.position;
    }

    private void UpdatePostLunge(float deltaTime)
    {
        postLungeTimer -= deltaTime;

        // End post-lunge phase when timer expires
        if (postLungeTimer <= 0)
            EndPostLunge();
    }

    private void EndPostLunge()
    {
        isInPostLungePhase = false;
        CleanupTrail();

        // Clear the PostLunge base state (returns to Default)
        character.RaiseEvent("movement_base_clear", "PostLunge");
    }

    public override void PhysicsTick(float fixedDeltaTime)
    {
        // Optional: Add physics-specific behavior here
    }

    public float GetCooldownPercent() => cooldownTimer / config.cooldown;
    public bool IsInPostLunge() => isInPostLungePhase;
    public float GetPostLungePercent() => postLungeTimer / config.postLungeDecelerationDuration;
}