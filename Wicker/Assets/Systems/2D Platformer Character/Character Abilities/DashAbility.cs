using UnityEngine;

[System.Serializable]
public class DashAbility : CharacterAbility
{
    // Config values
    private float force;
    private float duration;
    private float cooldown;
    private bool massDependent;
    private bool applyInstantForce;
    private bool applyContinuousForce;
    private float continuousForceMultiplier;
    private float preserveHorizontalVelocity;
    private float preserveVerticalVelocity;
    private bool applyPostDashDeceleration;
    private float postDashDecelerationForce;
    private float postDashDecelerationDuration;
    private GameObject trailPrefab;
    private AudioClip sound;

    // State
    private float activeTimer = 0f;
    private float cooldownTimer = 0f;
    private float postDashTimer = 0f;
    private GameObject trailInstance;
    private Vector2 dashDirection;
    private bool isInPostDashPhase = false;
    private Vector2 preDashVelocity;
    private Vector2 currentContinuousForce;

    public DashAbility()
    {
        AbilityName = "Dash";
    }

    protected override void LoadConfig(CharacterConfig config)
    {
        IsEnabled = config.dash.isEnabled;
        force = config.dash.force;
        duration = config.dash.duration;
        cooldown = config.dash.cooldown;
        massDependent = config.dash.massDependent;
        applyInstantForce = config.dash.applyInstantForce;
        applyContinuousForce = config.dash.applyContinuousForce;
        continuousForceMultiplier = config.dash.continuousForceMultiplier;
        preserveHorizontalVelocity = config.dash.preserveHorizontalVelocity;
        preserveVerticalVelocity = config.dash.preserveVerticalVelocity;
        applyPostDashDeceleration = config.dash.applyPostDashDeceleration;
        postDashDecelerationForce = config.dash.postDashDecelerationForce;
        postDashDecelerationDuration = config.dash.postDashDecelerationDuration;
        trailPrefab = config.dash.trailPrefab;
        sound = config.dash.sound;

        if (IsEnabled)
        {
            character.OnEvent += HandleEvent;
            Debug.Log($"Dash ability loaded: Enabled={IsEnabled}, Force={force}, MassDependent={massDependent}");
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
        activeTimer = duration;
        cooldownTimer = cooldown;
        isInPostDashPhase = false;

        // Store pre-dash velocity for preservation
        preDashVelocity = rb.linearVelocity;

        // Determine dash direction
        float movementDirection = movement.GetCurrentXDirection();
        dashDirection = new Vector2(movementDirection, 0).normalized;

        // Apply velocity preservation
        Vector2 preservedVelocity = new Vector2(
            preDashVelocity.x * preserveHorizontalVelocity,
            preDashVelocity.y * preserveVerticalVelocity
        );
        rb.linearVelocity = preservedVelocity;

        // Calculate base dash force
        Vector2 dashForce = dashDirection * force;

        // Apply instant force if enabled
        if (applyInstantForce)
        {
            ApplyForce(dashForce, true);
            Debug.Log($"Dash: Applied instant force of {dashForce} (massDependent: {massDependent})");
        }

        // Store continuous force if enabled
        if (applyContinuousForce)
        {
            currentContinuousForce = dashForce * continuousForceMultiplier;
        }

        // Start ability state
        var abilityState = new CharacterMovement.MovementState(
            name: "Dashing",
            allowMovement: false,
            applyGravity: applyPostDashDeceleration ? false : true,
            applyDeceleration: false,
            canJump: false
        );

        character.RaiseEvent("movement_override_start", abilityState);

        // Visual feedback
        if (trailPrefab != null)
            trailInstance = GameObject.Instantiate(trailPrefab, transform.position, Quaternion.identity);

        if (sound != null)
            AudioSource.PlayClipAtPoint(sound, transform.position);

        character.RaiseEvent("ability_used", AbilityName);
        OnActivated();
    }

    private void ApplyForce(Vector2 force, bool isInstant)
    {
        if (isInstant)
        {
            if (massDependent)
            {
                rb.AddForce(force, ForceMode2D.Impulse);
            }
            else
            {
                rb.linearVelocity += force;
            }
        }
        else
        {
            if (massDependent)
            {
                rb.AddForce(force, ForceMode2D.Force);
            }
            else
            {
                // Mass-independent: multiply by mass
                rb.AddForce(force * rb.mass, ForceMode2D.Force);
            }
        }
    }

    public override void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        currentContinuousForce = Vector2.zero;

        // Handle post-dash effects
        if (applyPostDashDeceleration)
        {
            isInPostDashPhase = true;
            postDashTimer = postDashDecelerationDuration;

            var postDashState = new CharacterMovement.MovementState(
                name: "PostDash",
                allowMovement: false,
                applyGravity: false,
                applyDeceleration: false,
                canJump: false
            );

            character.RaiseEvent("movement_override_start", postDashState);
        }
        else
        {
            Cleanup();
            character.RaiseEvent("movement_override_end", null);
        }

        character.RaiseEvent("ability_ended", AbilityName);
        OnDeactivated();
    }

    private void Cleanup()
    {
        if (trailInstance != null)
            GameObject.Destroy(trailInstance);
    }

    public override void Tick(float deltaTime)
    {
        if (cooldownTimer > 0) cooldownTimer -= deltaTime;

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
        // Apply continuous force if enabled
        if (applyContinuousForce && currentContinuousForce != Vector2.zero)
        {
            ApplyForce(currentContinuousForce * deltaTime * 60f, false);
        }

        activeTimer -= deltaTime;

        if (activeTimer <= 0)
        {
            Deactivate();
        }

        // Update trail position
        if (trailInstance != null)
            trailInstance.transform.position = transform.position;
    }

    private void UpdatePostDash(float deltaTime)
    {
        postDashTimer -= deltaTime;

        // Apply deceleration force opposite to dash direction
        if (applyPostDashDeceleration && postDashTimer > 0)
        {
            Vector2 decelerationForce = -dashDirection * postDashDecelerationForce * deltaTime * 60f;
            ApplyForce(decelerationForce, false);
        }

        if (postDashTimer <= 0)
        {
            EndPostDash();
        }
    }

    private void EndPostDash()
    {
        isInPostDashPhase = false;
        Cleanup();
        character.RaiseEvent("movement_override_end", null);
    }

    public float GetCooldownPercent() => cooldownTimer / cooldown;
    public bool IsInPostDash() => isInPostDashPhase;
    public float GetPostDashPercent() => postDashTimer / postDashDecelerationDuration;
}