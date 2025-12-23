using UnityEngine;

// Dash ability - propels the character forward horizontally in the direction of movement.
[System.Serializable]
public class DashAbility : CharacterAbility
{
    // Store the config reference directly
    private DashConfig config;

    // State
    private float activeTimer = 0f;
    private float cooldownTimer = 0f;
    private float postDashTimer = 0f;
    private GameObject trailInstance;
    private Vector2 dashDirection;
    private bool isInPostDashPhase = false;
    private Vector2 currentContinuousForce;

    private CharacterAbilities characterAbilities;

    public DashAbility()
    {
        AbilityName = "Dash";
    }

    protected override void LoadConfig(CharacterConfig characterConfig)
    {
        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        config = characterConfig.dash;
        IsEnabled = config.isEnabled;
        characterAbilities = character.GetComponent<CharacterAbilities>();

        if (IsEnabled)
        {
            character.OnEvent += HandleEvent;
            Debug.Log($"Dash ability loaded: Enabled={IsEnabled}, Force={config.force}");
        }
    }

    private void HandleEvent(string type, object data)
    {
        if (type == "dash_pressed" && CanActivate())
        {
            if (characterAbilities != null && characterAbilities.CanUseAbility("grappledash"))
                return;

            Activate();
        }

        else if (type == "dash_cooldown_set")
        {
            float newCooldown = (float)data;
            if (newCooldown > cooldownTimer)
            {
                cooldownTimer = newCooldown;
            }
        }
    }

    public override bool CanActivate()
    {
        return IsEnabled && cooldownTimer <= 0 && !IsActive && !isInPostDashPhase;
    }

    public override void Activate()
    {
        IsActive = true;
        activeTimer = config.duration;
        character.RaiseEvent("dash_cooldown_set", config.cooldown); // Puts all dashes on cooldown
        isInPostDashPhase = false;

        // Store pre-dash velocity
        Vector2 preDashVelocity = rb.linearVelocity;

        // Determine dash direction based on input
        dashDirection = CalculateDashDirection();

        // Apply velocity preservation
        rb.linearVelocity = new Vector2(
            preDashVelocity.x * config.preserveHorizontalVelocity,
            preDashVelocity.y * config.preserveVerticalVelocity
        );

        // Calculate and apply dash force
        Vector2 dashForce = dashDirection * config.force;

        if (config.applyInstantForce)
            ApplyForce(dashForce, true);

        if (config.applyContinuousForce)
            currentContinuousForce = dashForce * config.continuousForceMultiplier;

        // Set DASH as base state (priority 20)
        character.RaiseEvent("movement_base_set", new CharacterMovement.MovementState(
            name: "Dashing",
            type: CharacterMovement.MovementStateType.Base,
            priority: 20,
            allowMovement: false,
            applyGravity: false,
            applyDeceleration: false,
            canJump: false
        ));

        // Visual/audio feedback
        if (config.trailPrefab != null)
            trailInstance = GameObject.Instantiate(config.trailPrefab, transform.position, Quaternion.identity);

        if (config.sound != null)
            AudioSource.PlayClipAtPoint(config.sound, transform.position);

        character.RaiseEvent("ability_used", AbilityName);
        OnActivated();
    }

    private Vector2 CalculateDashDirection()
    {
        // Get movement input direction
        float movementDirection = movement.GetCurrentXDirection();
        return new Vector2(movementDirection, 0).normalized;
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

        // Clear the dash base state
        character.RaiseEvent("movement_base_clear", "Dashing");

        // Start post-dash if enabled
        if (config.applyPostDashDeceleration)
        {
            isInPostDashPhase = true;
            postDashTimer = config.postDashDecelerationDuration;

            // Set POSTDASH as base state with lower priority (10)
            character.RaiseEvent("movement_base_set", new CharacterMovement.MovementState(
                name: "PostDash",
                type: CharacterMovement.MovementStateType.Base,
                priority: 10, // Lower than Dashing (20), higher than Default (0)
                allowMovement: true,  // Player can move during post-dash
                applyGravity: true,
                applyDeceleration: true,
                canJump: true,
                gravityMultiplier: 1f,
                groundAccelerationMultiplier: 1f,
                groundDecelerationMultiplier: ((config.postDashDecelerationMultilplier - 1) / 10) + 1,
                airAccelerationMultiplier: 1f,
                airDecelerationMultiplier: config.postDashDecelerationMultilplier,
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
    }

    public override void Tick(float deltaTime)
    {
        if (cooldownTimer > 0)
            cooldownTimer -= deltaTime;

        if (isInPostDashPhase)
        {
            UpdatePostDash(deltaTime);
        }
        else if (IsActive)
        {
            UpdateActiveDash(deltaTime);
        }
    }

    private void UpdateActiveDash(float deltaTime)
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

    private void UpdatePostDash(float deltaTime)
    {
        postDashTimer -= deltaTime;

        // End post-dash phase when timer expires
        if (postDashTimer <= 0)
            EndPostDash();
    }

    private void EndPostDash()
    {
        isInPostDashPhase = false;
        CleanupTrail();

        // Clear the PostDash base state (returns to Default)
        character.RaiseEvent("movement_base_clear", "PostDash");
    }

    public float GetCooldownPercent() => cooldownTimer / config.cooldown;
    public bool IsInPostDash() => isInPostDashPhase;
    public float GetPostDashPercent() => postDashTimer / config.postDashDecelerationDuration;
}