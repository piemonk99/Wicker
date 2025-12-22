using UnityEngine;

/// <summary>
/// Main weapon system controller that manages weapon behavior.
/// Similar to GrappleSystem structure.
/// </summary>
public abstract class WeaponSystem : MonoBehaviour, ICharacterComponent
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

    // Events
    public event System.Action<WeaponConfig> OnWeaponChanged;

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

        // Subscribe to equipment events
        equipment.OnWeaponChanged += OnWeaponChangedHandler;

        // Get initial weapon config from equipment
        currentConfig = equipment.CurrentWeapon;
        if (currentConfig == null)
        {
            Debug.LogWarning("No weapon equipped on initialization.");
        }
        else
        {
            InitializeWithConfig(currentConfig);
        }

        // Register for character events
        character.OnEvent += HandleEvent;
    }

    private void OnWeaponChangedHandler(WeaponConfig newConfig)
    {
        if (newConfig == null)
        {
            Debug.Log("Weapon unequipped");

            // Stop current attack if active
            if (isAttacking)
            {
                StopAttack();
            }

            // Clean up managers
            CleanupManagers();
            currentConfig = null;
            return;
        }

        Debug.Log($"Switching to weapon: {newConfig.weaponName}");

        // Stop current attack if active
        if (isAttacking)
        {
            StopAttack();
        }

        // Initialize with new config
        InitializeWithConfig(newConfig);
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

        Debug.Log($"WeaponSystem initialized with config: {config.weaponName}");
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
        float velocityBonus = Mathf.Min(
            velocity * mechanics.velocityDamageMultiplier,
            mechanics.maxVelocityBonus
        );

        return baseDamage + velocityBonus;
    }

    protected bool CanAttack()
    {
        return currentConfig != null && !isAttacking && attackCooldownTimer <= 0;
    }

    //////////////////////// Cleanup ///////////////////////////

    protected virtual void OnDestroy()
    {
        if (equipment != null)
        {
            equipment.OnWeaponChanged -= OnWeaponChangedHandler;
        }

        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        CleanupManagers();
    }
}