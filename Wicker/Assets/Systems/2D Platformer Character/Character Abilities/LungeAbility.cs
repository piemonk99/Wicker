using UnityEngine;

[System.Serializable]
public class LungeAbility : CharacterAbility
{
    private float horizontalForce;
    private float verticalForce;
    private float duration;
    private float cooldown;
    private bool cancelVerticalVelocity;
    private ParticleSystem particles;
    private AudioClip sound;

    private float activeTimer = 0f;
    private float cooldownTimer = 0f;

    public LungeAbility()
    {
        AbilityName = "Lunge";
    }

    protected override void LoadConfig(CharacterConfig config)
    {
        IsEnabled = config.lunge.isEnabled;
        horizontalForce = config.lunge.horizontalForce;
        verticalForce = config.lunge.verticalForce;
        duration = config.lunge.duration;
        cooldown = config.lunge.cooldown;
        cancelVerticalVelocity = config.lunge.cancelVerticalVelocity;
        particles = config.lunge.particles;
        sound = config.lunge.sound;

        if (IsEnabled)
        {
            character.OnEvent += HandleEvent;
            Debug.Log($"Lunge ability loaded: Enabled={IsEnabled}, HorizontalForce={horizontalForce}, Cooldown={cooldown}");
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
        return IsEnabled && cooldownTimer <= 0 && !IsActive;
    }

    public override void Activate()
    {
        IsActive = true;
        activeTimer = duration;
        cooldownTimer = cooldown;

        // Get facing direction
        Vector2 facingDirection = movement.GetFacingDirection();

        // Calculate lunge force
        Vector2 lungeForce = new Vector2(
            facingDirection.x * horizontalForce,
            verticalForce
        );

        // Apply through movement system
        if (cancelVerticalVelocity)
        {
            movement.SetExternalVelocity(new Vector2(movement.GetHorizontalVelocity(), 0));
        }

        movement.ApplyExternalForce(lungeForce);

        // Start ability state
        var abilityState = new CharacterMovement.MovementState(
            name: "Lunging",
            allowMovement: false,
            applyGravity: true,
            applyDeceleration: false,
            canJump: false
        );

        character.RaiseEvent("movement_override_start", abilityState);

        // Visual/audio feedback
        if (particles != null)
            particles.Play();

        if (sound != null)
            AudioSource.PlayClipAtPoint(sound, transform.position);

        character.RaiseEvent("ability_used", AbilityName);
        OnActivated();
    }

    public override void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
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
        }
    }

    public float GetCooldownPercent() => cooldownTimer / cooldown;
}