// WeaponConfig.cs - This stays as is
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

    [Header("Mechanics Configuration")]
    public WeaponMechanicsConfig mechanicsConfig;

    [Header("Visual Configuration")]
    public WeaponVisualConfig visualConfig;

    [Header("Sound Configuration")]
    public WeaponSoundConfig soundConfig;

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

        // Clone mechanics config based on type
        clone.mechanicsConfig = CloneMechanicsConfig();

        // Clone visual config based on type
        clone.visualConfig = CloneVisualConfig();

        // Clone sound config based on type
        clone.soundConfig = CloneSoundConfig();

        return clone;
    }

    private WeaponMechanicsConfig CloneMechanicsConfig()
    {
        if (mechanicsConfig == null) return null;

        switch (weaponType)
        {
            case WeaponType.Hitbox:
                var hitboxMech = mechanicsConfig as HitboxWeaponMechanicsConfig;
                if (hitboxMech == null) return null;

                return new HitboxWeaponMechanicsConfig()
                {
                    baseDamage = hitboxMech.baseDamage,
                    attackCooldown = hitboxMech.attackCooldown,
                    canAttackWhileMoving = hitboxMech.canAttackWhileMoving,
                    scalesWithVelocity = hitboxMech.scalesWithVelocity,
                    velocityDamageMultiplier = hitboxMech.velocityDamageMultiplier,
                    maxVelocityBonus = hitboxMech.maxVelocityBonus,
                    hitboxSize = hitboxMech.hitboxSize,
                    hitboxOffset = hitboxMech.hitboxOffset,
                    attackDuration = hitboxMech.attackDuration,
                    hitLayers = hitboxMech.hitLayers,
                    multiHit = hitboxMech.multiHit,
                    maxHitsPerAttack = hitboxMech.maxHitsPerAttack,
                    knockbackForce = hitboxMech.knockbackForce
                };

            case WeaponType.CursorWeapon:
                var cursorMech = mechanicsConfig as CursorWeaponMechanicsConfig;
                if (cursorMech == null) return null;

                return new CursorWeaponMechanicsConfig()
                {
                    baseDamage = cursorMech.baseDamage,
                    attackCooldown = cursorMech.attackCooldown,
                    canAttackWhileMoving = cursorMech.canAttackWhileMoving,
                    scalesWithVelocity = cursorMech.scalesWithVelocity,
                    velocityDamageMultiplier = cursorMech.velocityDamageMultiplier,
                    maxVelocityBonus = cursorMech.maxVelocityBonus,
                    orbitRadius = cursorMech.orbitRadius,
                    orbitSpeed = cursorMech.orbitSpeed,
                    swordMass = cursorMech.swordMass,
                    swordDrag = cursorMech.swordDrag,
                    maxSwordSpeed = cursorMech.maxSwordSpeed,
                    returnForce = cursorMech.returnForce,
                    cursorFollowSpeed = cursorMech.cursorFollowSpeed,
                    maxAnglePerSecond = cursorMech.maxAnglePerSecond,
                    usePhysicsBasedMovement = cursorMech.usePhysicsBasedMovement,
                    damagePerSpeedUnit = cursorMech.damagePerSpeedUnit,
                    minimumDamageSpeed = cursorMech.minimumDamageSpeed
                };

            case WeaponType.AutoAttack:
                var autoMech = mechanicsConfig as AutoAttackWeaponMechanicsConfig;
                if (autoMech == null) return null;

                return new AutoAttackWeaponMechanicsConfig()
                {
                    baseDamage = autoMech.baseDamage,
                    attackCooldown = autoMech.attackCooldown,
                    canAttackWhileMoving = autoMech.canAttackWhileMoving,
                    scalesWithVelocity = autoMech.scalesWithVelocity,
                    velocityDamageMultiplier = autoMech.velocityDamageMultiplier,
                    maxVelocityBonus = autoMech.maxVelocityBonus,
                    detectionRadius = autoMech.detectionRadius,
                    attackInterval = autoMech.attackInterval,
                    velocityThreshold = autoMech.velocityThreshold,
                    onlyActiveDuringGrapple = autoMech.onlyActiveDuringGrapple,
                    grappleDamageMultiplier = autoMech.grappleDamageMultiplier,
                    maxGrappleRange = autoMech.maxGrappleRange,
                    autoAttackDamage = autoMech.autoAttackDamage,
                    enemyLayers = autoMech.enemyLayers
                };

            default:
                return null;
        }
    }

    private WeaponVisualConfig CloneVisualConfig()
    {
        if (visualConfig == null) return null;

        switch (weaponType)
        {
            case WeaponType.Hitbox:
                var hitboxVis = visualConfig as HitboxWeaponVisualConfig;
                if (hitboxVis == null) return null;

                return new HitboxWeaponVisualConfig()
                {
                    weaponPrefab = hitboxVis.weaponPrefab,
                    icon = hitboxVis.icon,
                    enableDebugVisualization = hitboxVis.enableDebugVisualization,
                    attackAnimation = hitboxVis.attackAnimation,
                    hitboxDebugColor = hitboxVis.hitboxDebugColor
                };

            case WeaponType.CursorWeapon:
                var cursorVis = visualConfig as CursorWeaponVisualConfig;
                if (cursorVis == null) return null;

                return new CursorWeaponVisualConfig()
                {
                    weaponPrefab = cursorVis.weaponPrefab,
                    icon = cursorVis.icon,
                    enableDebugVisualization = cursorVis.enableDebugVisualization,
                    orbitDebugColor = cursorVis.orbitDebugColor,
                    swordTrailColor = cursorVis.swordTrailColor
                };

            case WeaponType.AutoAttack:
                var autoVis = visualConfig as AutoAttackWeaponVisualConfig;
                if (autoVis == null) return null;

                return new AutoAttackWeaponVisualConfig()
                {
                    weaponPrefab = autoVis.weaponPrefab,
                    icon = autoVis.icon,
                    enableDebugVisualization = autoVis.enableDebugVisualization,
                    detectionRadiusColor = autoVis.detectionRadiusColor,
                    grappleRangeColor = autoVis.grappleRangeColor
                };

            default:
                return null;
        }
    }

    private WeaponSoundConfig CloneSoundConfig()
    {
        if (soundConfig == null) return null;

        switch (weaponType)
        {
            case WeaponType.Hitbox:
                var hitboxSound = soundConfig as HitboxWeaponSoundConfig;
                if (hitboxSound == null) return null;

                return new HitboxWeaponSoundConfig()
                {
                    weaponSoundSet = hitboxSound.weaponSoundSet,
                    swingVolume = hitboxSound.swingVolume,
                    critVolume = hitboxSound.critVolume,
                    swingCooldown = hitboxSound.swingCooldown,
                    hitVolume = hitboxSound.hitVolume
                };

            case WeaponType.CursorWeapon:
                var cursorSound = soundConfig as CursorWeaponSoundConfig;
                if (cursorSound == null) return null;

                return new CursorWeaponSoundConfig()
                {
                    weaponSoundSet = cursorSound.weaponSoundSet,
                    swingVolume = cursorSound.swingVolume,
                    critVolume = cursorSound.critVolume,
                    swingCooldown = cursorSound.swingCooldown,
                    swooshSound = cursorSound.swooshSound,
                    swooshVelocityThreshold = cursorSound.swooshVelocityThreshold,
                    swooshVolume = cursorSound.swooshVolume
                };

            case WeaponType.AutoAttack:
                var autoSound = soundConfig as AutoAttackWeaponSoundConfig;
                if (autoSound == null) return null;

                return new AutoAttackWeaponSoundConfig()
                {
                    weaponSoundSet = autoSound.weaponSoundSet,
                    swingVolume = autoSound.swingVolume,
                    critVolume = autoSound.critVolume,
                    swingCooldown = autoSound.swingCooldown,
                    critVelocityThreshold = autoSound.critVelocityThreshold
                };

            default:
                return null;
        }
    }
}