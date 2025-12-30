using UnityEngine;

[System.Serializable]
public class GrappleDashAbility : CharacterAbility
{
    private GrappleDashConfig config;
    private CharacterGrapple characterGrapple;

    // State
    private float activeTimer = 0f;
    private float cooldownTimer = 0f;
    private GameObject trailInstance;
    private Vector2 dashDirection;
    private Vector2 currentContinuousForce;

    public GrappleDashAbility()
    {
        AbilityName = "GrappleDash";
    }

    protected override void LoadConfig(CharacterConfig characterConfig)
    {
        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        config = characterConfig.grappleDash;
        IsEnabled = config.isEnabled;

        if (IsEnabled)
        {
            character.OnEvent += HandleEvent;
        }
    }

    private void HandleEvent(string type, object data)
    {
        if (type == "dash_pressed" && CanActivate())
        {
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
        // Must be enabled, off cooldown, not already active, AND grappling
        if (!IsEnabled || cooldownTimer > 0 || IsActive)
            return false;

        // Get grapple system reference if needed
        if (characterGrapple == null)
            characterGrapple = character.GetComponent<CharacterGrapple>();

        // Can only activate while grappling
        return characterGrapple != null && characterGrapple.IsGrappling();
    }

    public override void Activate()
    {
        IsActive = true;
        activeTimer = config.duration;
        character.RaiseEvent("dash_cooldown_set", config.cooldown); // Puts all dashes on cooldown

        // Get grapple system if needed
        if (characterGrapple == null)
            characterGrapple = character.GetComponent<CharacterGrapple>();

        if (characterGrapple == null || !characterGrapple.IsGrappling())
        {
            Deactivate();
            return;
        }

        // Store pre-dash velocity
        Vector2 preDashVelocity = rb.linearVelocity;

        // Determine dash direction based on grapple state
        dashDirection = CalculateDashDirection();

        // Apply velocity preservation
        bool isTangentDash = ShouldUseTangentDash();
        if (isTangentDash && config.preserveAllVelocityOnTangentDash)
        {
            // Full preservation for tangent dash
            rb.linearVelocity = preDashVelocity;
        }
        else
        {
            // Normal preservation
            rb.linearVelocity = new Vector2(
                preDashVelocity.x * config.normalPreserveHorizontalVelocity,
                preDashVelocity.y * config.normalPreserveVerticalVelocity
            );
        }

        // Calculate and apply dash force
        Vector2 dashForce = dashDirection * config.force;

        if (config.applyInstantForce)
            ApplyForce(dashForce, true);

        if (config.applyContinuousForce)
            currentContinuousForce = dashForce * config.continuousForceMultiplier;

        // Set GRAPPLEDASH as base state
        character.RaiseEvent("movement_base_set", new CharacterMovement.MovementState(
            name: "GrappleDashing",
            type: CharacterMovement.MovementStateType.Base,
            priority: 20, // Same as normal dash
            allowMovement: false,
            applyGravity: false,
            applyDeceleration: false,
            canJump: false
        ));

        // Visual/audio feedback
        if (config.trailPrefab != null)
        {
            trailInstance = GameObject.Instantiate(config.trailPrefab, transform.position, Quaternion.identity);
            // Optionally set color
            if (trailInstance.TryGetComponent<SpriteRenderer>(out var sr))
                sr.color = config.dashColor;
        }

        if (config.sound != null)
            AudioSource.PlayClipAtPoint(config.sound, transform.position);

        character.RaiseEvent("ability_used", AbilityName);
        OnActivated();
    }

    private bool ShouldUseTangentDash()
    {
        if (characterGrapple == null) return false;

        // Get current rope state
        var ropeState = characterGrapple.GetCurrentRopeState();
        if (!ropeState.HasValue) return false;

        // Use tangent if rope is significantly stretched/squashed
        return Mathf.Abs(ropeState.Value.ratio) >= config.minRopeRatioThreshold;
    }

    private Vector2 CalculateDashDirection()
    {
        // Get the base direction (before tangent calculation)
        Vector2 baseDirection = GetBaseDirection();

        // Check if we should use tangent
        if (!ShouldUseTangentDash())
        {
            return baseDirection; // Normal dash while grappling
        }

        // Use tangent direction
        Vector2 radialDirection = characterGrapple.GetRadialDirection();
        Vector2 tangentDirection = characterGrapple.GetTangentDirection();

        if (radialDirection == Vector2.zero || tangentDirection == Vector2.zero)
            return baseDirection;

        // Calculate which tangent is closest to base direction
        Vector2 tangentLeft = -tangentDirection;

        float angleToRight = Vector2.Angle(baseDirection, tangentDirection);
        float angleToLeft = Vector2.Angle(baseDirection, tangentLeft);

        // Only use tangent if it's within acceptable angle difference
        if (Mathf.Min(angleToRight, angleToLeft) <= config.maxAngleDifference)
        {
            Vector2 chosenTangent = (angleToRight <= angleToLeft) ? tangentDirection : tangentLeft;

            // Debug visualization
            Debug.DrawRay(transform.position, chosenTangent * 3f, config.dashColor, 1f);
            Debug.DrawRay(transform.position, radialDirection * 2f, Color.red, 1f);

            return chosenTangent;
        }

        // Fall back to base direction if tangents are too far
        return baseDirection;
    }

    private Vector2 GetBaseDirection()
    {
        float currentSpeed = rb.linearVelocity.magnitude;

        // If moving fast enough, use movement direction
        if (currentSpeed >= 5f)
        {
            Vector2 velocityDir = rb.linearVelocity.normalized;

            // For horizontal preference, we could project onto x-axis
            // Or use raw velocity direction
            return new Vector2(Mathf.Sign(velocityDir.x), 0).normalized;
        }
        else
        {
            // Slow or stationary: use input direction
            float movementDirection = movement.GetCurrentXDirection();
            return new Vector2(movementDirection, 0).normalized;
        }
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

        // Clear the grapple dash base state
        character.RaiseEvent("movement_base_clear", "GrappleDashing");

        // Cleanup
        if (trailInstance != null)
        {
            GameObject.Destroy(trailInstance);
            trailInstance = null;
        }

        character.RaiseEvent("ability_ended", AbilityName);
        OnDeactivated();
    }

    public override void Tick(float deltaTime)
    {
        if (cooldownTimer > 0)
            cooldownTimer -= deltaTime * 1.01f; // Make the grapple dash cool down slightly faster to avoid race conditions

        if (IsActive)
            UpdateActiveDash(deltaTime);
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

    public override void PhysicsTick(float fixedDeltaTime) { }

    public float GetCooldownPercent() => cooldownTimer / config.cooldown;
}