// WeaponConfig.cs - This stays as is
using UnityEngine;

public enum WeaponType
{
    Hitbox,     // Basic swing with hitbox
    CursorSword, // Physics-based sword following cursor
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

// Base config that all weapons share
public abstract class WeaponConfig : ScriptableObject
{
    [Header("Basic Settings")]
    public string weaponName = "New Weapon";
    public WeaponType weaponType;
    public WeaponCategory category = WeaponCategory.Melee;
    public Sprite icon;
    public GameObject weaponPrefab; // Optional visual prefab

    [Header("Combat Settings")]
    public float baseDamage = 10f;
    public float attackCooldown = 0.5f;
    public bool canAttackWhileMoving = true;

    [Header("Velocity Scaling")]
    public bool scalesWithVelocity = true;
    public float velocityDamageMultiplier = 0.5f; // Damage = base + (velocity * multiplier)
    public float maxVelocityBonus = 20f;

    public abstract void InitializeWeapon(GameObject weaponInstance, CharacterCore character);
}