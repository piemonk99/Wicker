using UnityEngine;

// Base class for a character ability. Character abilities handle their own calls and behavior, and are managed by the CharacterAbilities component.
public abstract class CharacterAbility : ICharacterComponent
{
    public string AbilityName { get; protected set; }
    public bool IsEnabled { get; protected set; }
    public bool IsActive { get; protected set; }

    protected CharacterCore character;
    protected CharacterMovement movement;
    protected Rigidbody2D rb;
    protected Transform transform;

    public virtual void Initialize(CharacterCore core)
    {
        character = core;
        movement = core.GetCharacterComponent<CharacterMovement>();
        rb = core.GetComponent<Rigidbody2D>();
        transform = core.transform;

        // Load config from CharacterCore
        LoadConfig(core.GetConfig());
    }

    protected abstract void LoadConfig(CharacterConfig config);

    public virtual void Tick(float deltaTime) { }
    public virtual void PhysicsTick(float fixedDeltaTime) { }

    public abstract bool CanActivate();
    public abstract void Activate();
    public abstract void Deactivate();

    protected virtual void OnActivated() { }
    protected virtual void OnDeactivated() { }
}