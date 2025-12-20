using UnityEngine;

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
            Activate();
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
        cooldownTimer = config.cooldown;
        isInPostDashPhase = false;

        // Store pre-dash velocity
        Vector2 preDashVelocity = rb.linearVelocity;

        // Determine dash direction
        float movementDirection = movement.GetCurrentXDirection();
        dashDirection = new Vector2(movementDirection, 0).normalized;

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

        // Start dash state (no movement control during dash)
        character.RaiseEvent("movement_override_start", new CharacterMovement.MovementState(
            name: "Dashing",
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

        // End dash state FIRST (pop it from stack)
        character.RaiseEvent("movement_override_end", null);

        // Start post-dash if enabled
        if (config.applyPostDashDeceleration)
        {
            isInPostDashPhase = true;
            postDashTimer = config.postDashDecelerationDuration;

            // Post-dash state: allow movement but with high deceleration
            character.RaiseEvent("movement_override_start", new CharacterMovement.MovementState(
                name: "PostDash",
                allowMovement: true,  // Player can move during post-dash
                applyGravity: true,
                applyDeceleration: true,
                canJump: true,
                gravityMultiplier: 1f,
                accelerationMultiplier: 1f,
                airAccelerationMultiplier: 1f,
                decelerationMultiplier: config.postDashDecelerationMultilplier,  // This is the multiplier!
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

        // End the post-dash movement state (return to default movement)
        character.RaiseEvent("movement_override_end", null);
    }

    public float GetCooldownPercent() => cooldownTimer / config.cooldown;
    public bool IsInPostDash() => isInPostDashPhase;
    public float GetPostDashPercent() => postDashTimer / config.postDashDecelerationDuration;
}