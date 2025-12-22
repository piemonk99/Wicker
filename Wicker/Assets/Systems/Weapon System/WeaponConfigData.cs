// WeaponConfigData.cs
using UnityEngine;

// ==================== BASE CONFIG CLASSES ====================

/// <summary>
/// Base class for all weapon mechanics configurations.
/// Each weapon type will have its own implementation.
/// </summary>
[System.Serializable]
public abstract class WeaponMechanicsConfig
{
    [Header("Basic Combat Settings")]
    public float baseDamage = 10f;
    public float attackCooldown = 0.5f;
    public bool canAttackWhileMoving = true;

    [Header("Velocity Scaling")]
    public bool scalesWithVelocity = true;
    public float velocityDamageMultiplier = 0.5f;
    public float maxVelocityBonus = 20f;
}

/// <summary>
/// Base class for all weapon visual configurations.
/// </summary>
[System.Serializable]
public abstract class WeaponVisualConfig
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
public abstract class WeaponSoundConfig
{
    [Header("Sound References")]
    public SoundNode weaponSoundSet;

    [Header("Volume Settings")]
    [Range(0f, 2f)] public float swingVolume = 1.0f;
    [Range(0f, 2f)] public float critVolume = 1.2f;
    public float swingCooldown = 0.1f;
}

// ==================== SPECIFIC MECHANICS CONFIGS ====================

[System.Serializable]
public class HitboxWeaponMechanicsConfig : WeaponMechanicsConfig
{
    [Header("Hitbox Settings")]
    public Vector2 hitboxSize = new Vector2(1.5f, 0.8f);
    public Vector2 hitboxOffset = new Vector2(0.5f, 0f);
    public float attackDuration = 0.2f;
    public LayerMask hitLayers = ~0;

    [Header("Advanced")]
    public bool multiHit = false;
    public int maxHitsPerAttack = 1;
    public float knockbackForce = 5f;
}

[System.Serializable]
public class CursorWeaponMechanicsConfig : WeaponMechanicsConfig
{
    [Header("Cursor Sword Settings")]
    public float orbitRadius = 2f;
    public float orbitSpeed = 5f;
    public float swordMass = 1f;
    public float swordDrag = 0.5f;
    public float maxSwordSpeed = 20f;
    public float returnForce = 10f;

    [Header("Control Settings")]
    public float cursorFollowSpeed = 15f;
    public float maxAnglePerSecond = 180f;
    public bool usePhysicsBasedMovement = true;

    [Header("Combat")]
    public float damagePerSpeedUnit = 0.2f;
    public float minimumDamageSpeed = 2f;
}

[System.Serializable]
public class AutoAttackWeaponMechanicsConfig : WeaponMechanicsConfig
{
    [Header("Auto Attack Settings")]
    public float detectionRadius = 3f;
    public float attackInterval = 0.3f;
    public float velocityThreshold = 3f;

    [Header("Grapple Enhancement")]
    public bool onlyActiveDuringGrapple = false;
    public float grappleDamageMultiplier = 1.5f;
    public float maxGrappleRange = 10f;

    [Header("Attack Parameters")]
    public float autoAttackDamage = 5f;
    public LayerMask enemyLayers;
}

// ==================== SPECIFIC SOUND CONFIGS ====================

[System.Serializable]
public class HitboxWeaponSoundConfig : WeaponSoundConfig
{
    [Header("Hitbox Specific Sounds")]
    public float hitVolume = 1.0f;
}

[System.Serializable]
public class CursorWeaponSoundConfig : WeaponSoundConfig
{
    [Header("Cursor Weapon Specific Sounds")]
    public SoundNode swooshSound;
    public float swooshVelocityThreshold = 10f;
    public float swooshVolume = 1.0f;
}

[System.Serializable]
public class AutoAttackWeaponSoundConfig : WeaponSoundConfig
{
    [Header("Auto Attack Specific Sounds")]
    public float critVelocityThreshold = 20f;
}

// ==================== SPECIFIC VISUAL CONFIGS ====================

[System.Serializable]
public class HitboxWeaponVisualConfig : WeaponVisualConfig
{
    [Header("Hitbox Visual Settings")]
    public AnimationClip attackAnimation;
    public Color hitboxDebugColor = new Color(1f, 0.5f, 0f, 0.3f);
}

[System.Serializable]
public class CursorWeaponVisualConfig : WeaponVisualConfig
{
    [Header("Cursor Weapon Visual Settings")]
    public Color orbitDebugColor = Color.yellow;
    public Color swordTrailColor = Color.cyan;
}

[System.Serializable]
public class AutoAttackWeaponVisualConfig : WeaponVisualConfig
{
    [Header("Auto Attack Visual Settings")]
    public Color detectionRadiusColor = Color.yellow;
    public Color grappleRangeColor = Color.red;
}