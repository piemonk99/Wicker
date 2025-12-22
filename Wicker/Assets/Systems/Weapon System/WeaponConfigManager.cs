// WeaponConfigManager.cs
using UnityEngine;

/// <summary>
/// Manages weapon configuration data and provides helper methods.
/// Works with WeaponConfig ScriptableObject.
/// </summary>
public class WeaponConfigManager
{
    private WeaponConfig config;

    /// <summary>
    /// Initializes a new instance with a WeaponConfig ScriptableObject.
    /// </summary>
    public WeaponConfigManager(WeaponConfig config)
    {
        this.config = config ?? CreateDefaultConfig();
    }

    /// <summary>
    /// Creates a default configuration for safety.
    /// </summary>
    private WeaponConfig CreateDefaultConfig()
    {
        Debug.LogWarning("No weapon config provided, using defaults");
        return ScriptableObject.CreateInstance<WeaponConfig>();
    }

    /// <summary>
    /// Gets the weapon name for UI or debugging.
    /// </summary>
    public string GetWeaponName()
    {
        return config.WeaponName;
    }

    /// <summary>
    /// Gets the mechanics config cast to specific type.
    /// </summary>
    public T GetMechanicsConfig<T>() where T : WeaponMechanicsConfig
    {
        return config.mechanicsConfig as T;
    }

    /// <summary>
    /// Gets the visual config cast to specific type.
    /// </summary>
    public T GetVisualConfig<T>() where T : WeaponVisualConfig
    {
        return config.visualConfig as T;
    }

    /// <summary>
    /// Gets the sound config cast to specific type.
    /// </summary>
    public T GetSoundConfig<T>() where T : WeaponSoundConfig
    {
        return config.soundConfig as T;
    }

    /// <summary>
    /// Gets the weapon type.
    /// </summary>
    public WeaponType GetWeaponType()
    {
        return config.weaponType;
    }

    /// <summary>
    /// Gets the base damage from mechanics config.
    /// </summary>
    public float GetBaseDamage()
    {
        return config.mechanicsConfig?.baseDamage ?? 10f;
    }

    /// <summary>
    /// Gets the attack cooldown from mechanics config.
    /// </summary>
    public float GetAttackCooldown()
    {
        return config.mechanicsConfig?.attackCooldown ?? 0.5f;
    }

    /// <summary>
    /// Checks if weapon scales with velocity.
    /// </summary>
    public bool ScalesWithVelocity()
    {
        return config.mechanicsConfig?.scalesWithVelocity ?? true;
    }

    /// <summary>
    /// Gets the velocity damage multiplier.
    /// </summary>
    public float GetVelocityDamageMultiplier()
    {
        return config.mechanicsConfig?.velocityDamageMultiplier ?? 0.5f;
    }

    /// <summary>
    /// Gets the maximum velocity bonus.
    /// </summary>
    public float GetMaxVelocityBonus()
    {
        return config.mechanicsConfig?.maxVelocityBonus ?? 20f;
    }
}