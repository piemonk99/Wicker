using UnityEngine;

/// <summary>
/// Main weapon system controller that manages weapon behavior.
/// Similar to CharacterGrapple structure.
/// </summary>
public abstract class CharacterWeapon : MonoBehaviour, ICharacterComponent
{
    [Header("References")]
    public Transform weaponOrigin;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Subsystem managers
    protected WeaponSoundManager soundManager;

    // References to other components
    protected CharacterCore character;
    protected CharacterEquipment equipment;
    protected Rigidbody2D rb;

    // Current config (will be set from CharacterEquipment)
    protected WeaponConfig currentConfig;

    // State
    protected bool isAttacking = false;
    protected float attackCooldownTimer = 0f;

    // Public
    public bool IsAttacking { get; protected set; }

    //////////////////////// Initialization ////////////////////////

    public virtual void Initialize(CharacterCore core)
    {
        character = core;
        rb = character.GetComponent<Rigidbody2D>();
        equipment = character.GetComponent<CharacterEquipment>();

        if (equipment == null)
        {
            Debug.LogError("WeaponSystem requires CharacterEquipment component");
            return;
        }


        // Clear old subscriptions before adding new ones
        if (character != null)
        {
            character.OnEvent -= HandleEvent;
            character.OnEvent += HandleEvent;
        }
    }

    public void SetWeaponConfig(WeaponConfig config)
    {
        if (config == null)
        {
            Debug.LogError("Cannot set null weapon config");
            return;
        }

        currentConfig = config;
        InitializeWithConfig(config);
    }

    protected virtual void InitializeWithConfig(WeaponConfig config)
    {
        if (config == null) return;

        currentConfig = config;

        // Initialize sound manager if sound config exists
        if (config.SoundConfig != null)
        {
            soundManager = new WeaponSoundManager(config.SoundConfig, this);
        }
    }

    protected virtual void CleanupManagers()
    {
        if (soundManager != null)
        {
            soundManager.Cleanup();
            soundManager = null;
        }
    }

    //////////////////////// Core Methods ////////////////////////

    public virtual void Tick(float deltaTime)
    {
        // Update cooldown
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= deltaTime;
        }
    }

    public virtual void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates in derived classes
    }

    protected virtual void HandleEvent(string type, object data)
    {
        if (type == "attack_pressed")
        {
            if (!isAttacking && attackCooldownTimer <= 0)
            {
                TryAttack();
            }
        }
    }

    protected abstract void TryAttack();
    protected abstract void StopAttack();

    //////////////////////// Helper Methods ////////////////////////

    public float CalculateDamage(float baseDamage)
    {
        if (currentConfig?.MechanicsConfig == null)
            return baseDamage;

        var mechanics = currentConfig.MechanicsConfig;

        if (!mechanics.scalesWithVelocity)
            return baseDamage;

        float velocity = rb != null ? rb.linearVelocity.magnitude : 0f;

        // Calculate multiplier using lerp
        float velocityMultiplier = CalculateVelocityMultiplier(velocity, mechanics);

        return baseDamage * velocityMultiplier;
    }

    private float CalculateVelocityMultiplier(float currentVelocity, WeaponMechanicsConfig mechanics)
    {
        // If velocity is 0 or below minimum threshold, return base multiplier (1x)
        if (currentVelocity <= mechanics.minimumVelocityForMultiplier)
            return 1f;

        // Calculate normalized velocity (0 to 1) within our scaling range
        float normalizedVelocity = Mathf.Clamp01(
            (currentVelocity - mechanics.minimumVelocityForMultiplier) /
            (mechanics.maxVelocityForMultiplier - mechanics.minimumVelocityForMultiplier)
        );

        // Lerp between base multiplier (1x) and max multiplier
        float multiplier = Mathf.Lerp(1f, mechanics.maxVelocityMultiplier, normalizedVelocity);

        return multiplier;
    }

    protected bool CanAttack()
    {
        return currentConfig != null && !isAttacking && attackCooldownTimer <= 0;
    }

    //////////////////////// Cleanup ///////////////////////////

    protected virtual void OnDestroy()
    {

        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        CleanupManagers();

        character = null;
        equipment = null;
        rb = null;
        currentConfig = null;
    }
}