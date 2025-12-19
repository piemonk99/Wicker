using UnityEngine;

[System.Serializable]
public class DashAbility : CharacterAbility
{
    private float force;
    private float duration;
    private float cooldown;
    private bool preserveVerticalVelocity;
    private GameObject trailPrefab;
    private AudioClip sound;

    private float activeTimer = 0f;
    private float cooldownTimer = 0f;
    private GameObject trailInstance;

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
        preserveVerticalVelocity = config.dash.preserveVerticalVelocity;
        trailPrefab = config.dash.trailPrefab;
        sound = config.dash.sound;

        if (IsEnabled)
        {
            character.OnEvent += HandleEvent;
            Debug.Log($"Dash ability loaded: Enabled={IsEnabled}, Force={force}, Cooldown={cooldown}");
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
        return IsEnabled && cooldownTimer <= 0 && !IsActive;
    }

    public override void Activate()
    {
        IsActive = true;
        activeTimer = duration;
        cooldownTimer = cooldown;

        // Get facing direction
        Vector2 facingDirection = movement.GetFacingDirection();

        // Calculate dash force
        Vector2 dashForce = facingDirection * force;

        // Apply through movement system
        float preservedVertical = preserveVerticalVelocity ? movement.GetVerticalVelocity() : 0f;
        movement.SetExternalVelocity(new Vector2(0, preservedVertical));
        movement.ApplyExternalForce(dashForce);

        // Start ability state
        var abilityState = new CharacterMovement.MovementState(
            name: "Dashing",
            allowMovement: false,
            applyGravity: true,
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

    public override void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;

        // Clean up trail
        if (trailInstance != null)
            GameObject.Destroy(trailInstance);

        character.RaiseEvent("movement_override_end", null);
        character.RaiseEvent("ability_ended", AbilityName);
        OnDeactivated();
    }

    public override void Tick(float deltaTime)
    {
        if (cooldownTimer > 0) cooldownTimer -= deltaTime;

        if (IsActive)
        {
            activeTimer -= deltaTime;
            if (activeTimer <= 0)
            {
                Deactivate();
            }

            // Update trail position
            if (trailInstance != null)
                trailInstance.transform.position = transform.position;
        }
    }

    public float GetCooldownPercent() => cooldownTimer / cooldown;
}