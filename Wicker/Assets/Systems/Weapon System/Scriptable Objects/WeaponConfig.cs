using UnityEngine;

public enum WeaponType
{
    Hitbox,     // Basic swing with hitbox
    CursorWeapon, // Physics-based weapon following cursor
    AutoAttack,  // Passive attacks while moving
    Ranged       // For future implementation
}

// Weapon category for organization
public enum WeaponCategory
{
    Melee,
    Ranged,
    Special
}

/// <summary>
/// Base class for all weapon mechanics configurations.
/// Each weapon type will have its own implementation.
/// </summary>
[System.Serializable]
public class WeaponMechanicsConfig
{
    [Header("Basic Combat Settings")]
    public float baseDamage = 10f;
    public bool canAttackWhileMoving = true;

    [Header("Velocity Scaling")]
    public bool scalesWithVelocity = true;

    [Tooltip("Minimum velocity required to start getting damage multiplier")]
    public float minimumVelocityForMultiplier = 0f;

    [Tooltip("Velocity at which we reach maximum damage multiplier")]
    public float maxVelocityForMultiplier = 20f;

    [Tooltip("Maximum damage multiplier at max velocity (e.g., 2.0 = 2x damage)")]
    [Range(1f, 5f)]
    public float maxVelocityMultiplier = 2f;
}

/// <summary>
/// Base class for all weapon visual configurations.
/// </summary>
[System.Serializable]
public class WeaponVisualConfig
{
    [Header("Visual Settings")]
    public GameObject weaponPrefab;
    public Sprite icon;

    [Header("Debug Visualization")]
    public bool enableDebugVisualization = false;
}

/// <summary>
/// Base class for all weapon sound configurations.
/// Each weapon type can implement its own sound behavior.
/// </summary>
[System.Serializable]
public class WeaponSoundConfig
{
    [Header("Sound References")]
    public SoundNode weaponSoundSet;
}

/// <summary>
/// Complete weapon configuration as a ScriptableObject.
/// Create assets via: Right-click -> Create -> Weapons -> Weapon Config
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponConfig", menuName = "Weapons/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [Header("Basic Settings")]
    public string weaponName = "New Weapon";
    public WeaponType weaponType;
    public WeaponCategory category = WeaponCategory.Melee;

    [Header("Type-Specific Configuration")]
    [Tooltip("Reference to the specific weapon configuration asset")]
    public ScriptableObject typeSpecificConfig;

    // Helper properties to access the specific configs
    public WeaponMechanicsConfig MechanicsConfig
    {
        get
        {
            if (typeSpecificConfig == null) return null;

            return weaponType switch
            {
                WeaponType.Hitbox => (typeSpecificConfig as HitboxWeaponConfig)?.mechanics,
                WeaponType.CursorWeapon => (typeSpecificConfig as CursorWeaponConfig)?.mechanics,
                WeaponType.AutoAttack => (typeSpecificConfig as AutoAttackWeaponConfig)?.mechanics,
                _ => null
            };
        }
    }

    public WeaponVisualConfig VisualConfig
    {
        get
        {
            if (typeSpecificConfig == null) return null;

            return weaponType switch
            {
                WeaponType.Hitbox => (typeSpecificConfig as HitboxWeaponConfig)?.visual,
                WeaponType.CursorWeapon => (typeSpecificConfig as CursorWeaponConfig)?.visual,
                WeaponType.AutoAttack => (typeSpecificConfig as AutoAttackWeaponConfig)?.visual,
                _ => null
            };
        }
    }

    public WeaponSoundConfig SoundConfig
    {
        get
        {
            if (typeSpecificConfig == null) return null;

            return weaponType switch
            {
                WeaponType.Hitbox => (typeSpecificConfig as HitboxWeaponConfig)?.sound,
                WeaponType.CursorWeapon => (typeSpecificConfig as CursorWeaponConfig)?.sound,
                WeaponType.AutoAttack => (typeSpecificConfig as AutoAttackWeaponConfig)?.sound,
                _ => null
            };
        }
    }

    // Helper properties
    public string WeaponName => weaponName;

    /// <summary>
    /// Creates a deep copy of this config.
    /// </summary>
    public WeaponConfig Clone()
    {
        var clone = CreateInstance<WeaponConfig>();

        // Basic settings
        clone.weaponName = weaponName;
        clone.weaponType = weaponType;
        clone.category = category;

        // Clone the type-specific config if it exists
        if (typeSpecificConfig != null)
        {
            clone.typeSpecificConfig = CloneTypeSpecificConfig();
        }

        return clone;
    }

    private ScriptableObject CloneTypeSpecificConfig()
    {
        if (typeSpecificConfig == null) return null;

        switch (weaponType)
        {
            case WeaponType.Hitbox:
                var hitboxConfig = typeSpecificConfig as HitboxWeaponConfig;
                if (hitboxConfig == null) return null;

                var clonedHitbox = CreateInstance<HitboxWeaponConfig>();
                clonedHitbox.mechanics = CloneHitboxMechanics(hitboxConfig.mechanics);
                clonedHitbox.visual = CloneHitboxVisual(hitboxConfig.visual);
                clonedHitbox.sound = CloneHitboxSound(hitboxConfig.sound);
                return clonedHitbox;

            case WeaponType.CursorWeapon:
                var cursorConfig = typeSpecificConfig as CursorWeaponConfig;
                if (cursorConfig == null) return null;

                var clonedCursor = CreateInstance<CursorWeaponConfig>();
                clonedCursor.mechanics = CloneCursorMechanics(cursorConfig.mechanics);
                clonedCursor.visual = CloneCursorVisual(cursorConfig.visual);
                clonedCursor.sound = CloneCursorSound(cursorConfig.sound);
                return clonedCursor;

            case WeaponType.AutoAttack:
                var autoConfig = typeSpecificConfig as AutoAttackWeaponConfig;
                if (autoConfig == null) return null;

                var clonedAuto = CreateInstance<AutoAttackWeaponConfig>();
                clonedAuto.mechanics = CloneAutoAttackMechanics(autoConfig.mechanics);
                clonedAuto.visual = CloneAutoAttackVisual(autoConfig.visual);
                clonedAuto.sound = CloneAutoAttackSound(autoConfig.sound);
                return clonedAuto;

            default:
                return null;
        }
    }

    // Clone methods for each config type
    private HitboxWeaponMechanicsConfig CloneHitboxMechanics(HitboxWeaponMechanicsConfig source)
    {
        if (source == null) return null;

        return new HitboxWeaponMechanicsConfig()
        {
            baseDamage = source.baseDamage,
            canAttackWhileMoving = source.canAttackWhileMoving,
            scalesWithVelocity = source.scalesWithVelocity,
            minimumVelocityForMultiplier = source.minimumVelocityForMultiplier,
            maxVelocityForMultiplier = source.maxVelocityForMultiplier,
            maxVelocityMultiplier = source.maxVelocityMultiplier,
            hitboxSize = source.hitboxSize,
            hitboxOffset = source.hitboxOffset,
            attackDuration = source.attackDuration,
            hitLayers = source.hitLayers,
            multiHit = source.multiHit,
            maxHitsPerAttack = source.maxHitsPerAttack,
            knockbackForce = source.knockbackForce
        };
    }

    private HitboxWeaponVisualConfig CloneHitboxVisual(HitboxWeaponVisualConfig source)
    {
        if (source == null) return null;

        return new HitboxWeaponVisualConfig()
        {
            weaponPrefab = source.weaponPrefab,
            icon = source.icon,
            enableDebugVisualization = source.enableDebugVisualization,
            attackAnimation = source.attackAnimation,
            hitboxDebugColor = source.hitboxDebugColor
        };
    }

    private HitboxWeaponSoundConfig CloneHitboxSound(HitboxWeaponSoundConfig source)
    {
        if (source == null) return null;

        return new HitboxWeaponSoundConfig()
        {
            weaponSoundSet = source.weaponSoundSet
        };
    }

    private CursorWeaponMechanicsConfig CloneCursorMechanics(CursorWeaponMechanicsConfig source)
    {
        if (source == null) return null;

        return new CursorWeaponMechanicsConfig()
        {
            baseDamage = source.baseDamage,
            canAttackWhileMoving = source.canAttackWhileMoving,
            scalesWithVelocity = source.scalesWithVelocity,
            minimumVelocityForMultiplier = source.minimumVelocityForMultiplier,
            maxVelocityForMultiplier = source.maxVelocityForMultiplier,
            maxVelocityMultiplier = source.maxVelocityMultiplier,
            minOrbitRadius = source.minOrbitRadius,
            maxOrbitRadius = source.maxOrbitRadius,
            movementMode = source.movementMode,
            cursorFollowSpeed = source.cursorFollowSpeed,
            directModeSmoothing = source.directModeSmoothing,
            angularAcceleration = source.angularAcceleration,
            angularDeceleration = source.angularDeceleration,
            maxAngularVelocity = source.maxAngularVelocity,
            damagePerSpeedUnit = source.damagePerSpeedUnit,
            minimumDamageSpeed = source.minimumDamageSpeed,
            speedAverageFrames = source.speedAverageFrames,
            currentFrameWeight = source.currentFrameWeight,
            useAverageSpeedForDamage = source.useAverageSpeedForDamage,
            useAverageSpeedForKnockback = source.useAverageSpeedForKnockback,
            baseKnockback = source.baseKnockback,
            speedKnockbackMultiplier = source.speedKnockbackMultiplier,
            maxKnockback = source.maxKnockback,
            enemyLayers = source.enemyLayers,
            sweptCollisionAngleStep = source.sweptCollisionAngleStep,
            maxGhostCollidersPerFrame = source.maxGhostCollidersPerFrame,
            alwaysUseSweptCollision = source.alwaysUseSweptCollision
        };
    }

    private CursorWeaponVisualConfig CloneCursorVisual(CursorWeaponVisualConfig source)
    {
        if (source == null) return null;

        return new CursorWeaponVisualConfig()
        {
            weaponPrefab = source.weaponPrefab,
            icon = source.icon,
            enableDebugVisualization = source.enableDebugVisualization,
            minOrbitDebugColor = source.minOrbitDebugColor,
            maxOrbitDebugColor = source.maxOrbitDebugColor
        };
    }

    private CursorWeaponSoundConfig CloneCursorSound(CursorWeaponSoundConfig source)
    {
        if (source == null) return null;

        return new CursorWeaponSoundConfig()
        {
            weaponSoundSet = source.weaponSoundSet,
            swooshSound = source.swooshSound,
            swooshVelocityThreshold = source.swooshVelocityThreshold,
            swooshVolume = source.swooshVolume
        };
    }

    private AutoAttackWeaponMechanicsConfig CloneAutoAttackMechanics(AutoAttackWeaponMechanicsConfig source)
    {
        if (source == null) return null;

        return new AutoAttackWeaponMechanicsConfig()
        {
            baseDamage = source.baseDamage,
            canAttackWhileMoving = source.canAttackWhileMoving,
            scalesWithVelocity = source.scalesWithVelocity,
            minimumVelocityForMultiplier = source.minimumVelocityForMultiplier,
            maxVelocityForMultiplier = source.maxVelocityForMultiplier,
            maxVelocityMultiplier = source.maxVelocityMultiplier,
            detectionRadius = source.detectionRadius,
            attackInterval = source.attackInterval,
            velocityThreshold = source.velocityThreshold,
            onlyActiveDuringGrapple = source.onlyActiveDuringGrapple,
            grappleDamageMultiplier = source.grappleDamageMultiplier,
            maxGrappleRange = source.maxGrappleRange,
            enemyLayers = source.enemyLayers
        };
    }

    private AutoAttackWeaponVisualConfig CloneAutoAttackVisual(AutoAttackWeaponVisualConfig source)
    {
        if (source == null) return null;

        return new AutoAttackWeaponVisualConfig()
        {
            weaponPrefab = source.weaponPrefab,
            icon = source.icon,
            enableDebugVisualization = source.enableDebugVisualization,
            detectionRadiusColor = source.detectionRadiusColor,
            grappleRangeColor = source.grappleRangeColor
        };
    }

    private AutoAttackWeaponSoundConfig CloneAutoAttackSound(AutoAttackWeaponSoundConfig source)
    {
        if (source == null) return null;

        return new AutoAttackWeaponSoundConfig()
        {
            weaponSoundSet = source.weaponSoundSet,
            critVelocityThreshold = source.critVelocityThreshold
        };
    }
}