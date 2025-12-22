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

    [Header("Sound Settings")]
    public WeaponSoundConfig soundConfig = new WeaponSoundConfig();

    public abstract void InitializeWeapon(GameObject weaponInstance, CharacterCore character);
}

[System.Serializable]
public class WeaponSoundConfig : ScriptableObject
{
    [Header("Sound Nodes")]
    public SoundNode weaponSoundSet;

    [Header("Volume Settings")]
    [Range(0f, 2f)] public float swingVolume = 1.0f;
    [Range(0f, 2f)] public float critVolume = 1.2f;
    [Range(0f, 2f)] public float hitVolume = 1.0f;

    [Header("Sound Triggers")]
    public float critVelocityThreshold = 20f;
    public float swingCooldown = 0.1f;

    [Header("Optional Sounds")]
    public SoundNode drawSound;
    public SoundNode sheatheSound;
    public SoundNode blockSound;
    public SoundNode parrySound;
}

// Updated IWeaponController interface
public interface IWeaponController
{
    void Initialize(WeaponConfig config, CharacterCore character, CharacterEquipment owner);
    bool TryAttack();
    bool IsAttacking();
    void Tick(float deltaTime);
    void PhysicsTick(float fixedDeltaTime);
}