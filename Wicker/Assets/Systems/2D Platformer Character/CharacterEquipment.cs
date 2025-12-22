// CharacterEquipment.cs
using UnityEngine;
using System.Collections.Generic;

public class CharacterEquipment : MonoBehaviour, ICharacterComponent
{
    [Header("References")]
    public Transform weaponOrigin; // Set this in inspector for weapon positioning

    [Header("Debug")]
    public bool showWeaponDebug = false;

    // References
    private CharacterCore character;
    private Rigidbody2D rb;
    private GrappleSystem grappleSystem;

    // Equipment State
    private WeaponConfig currentWeapon;
    private GrappleConfig currentGrappleHook;

    // Active Weapon System
    private WeaponSystem currentWeaponSystem;

    // Weapon State
    private float attackCooldownTimer = 0f;
    private Vector2 lastAttackDirection = Vector2.right;
    private float currentVelocityMagnitude;

    // Events
    public event System.Action<WeaponConfig> OnWeaponChanged;
    public event System.Action<GrappleConfig> OnGrappleHookChanged;

    // Public Properties
    public WeaponConfig CurrentWeapon => currentWeapon;
    public GrappleConfig CurrentGrappleHook => currentGrappleHook;
    public bool IsOnCooldown => attackCooldownTimer > 0;
    public bool IsAttacking => currentWeaponSystem != null && currentWeaponSystem.IsAttacking;
    public Transform WeaponOrigin => weaponOrigin;

    public void Initialize(CharacterCore character)
    {
        this.character = character;
        rb = character.GetComponent<Rigidbody2D>();
        grappleSystem = character.GetCharacterComponent<GrappleSystem>();

        // Set up weapon origin if not set
        if (weaponOrigin == null)
        {
            weaponOrigin = transform;
            Debug.Log($"Weapon origin not set, using character transform");
        }

        // Subscribe to events
        character.OnEvent += HandleCharacterEvent;

        Debug.Log($"CharacterEquipment initialized for {character.gameObject.name}");
    }

    public void Tick(float deltaTime)
    {
        // Update cooldown
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= deltaTime;
        }

        // Update current velocity for damage calculations
        if (rb != null)
        {
            currentVelocityMagnitude = rb.linearVelocity.magnitude;
        }

        // Tick current weapon system
        if (currentWeaponSystem != null)
        {
            currentWeaponSystem.Tick(deltaTime);
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics tick for current weapon system
        if (currentWeaponSystem != null)
        {
            currentWeaponSystem.PhysicsTick(fixedDeltaTime);
        }
    }

    private void HandleCharacterEvent(string type, object data)
    {
        switch (type)
        {
            case "attack_pressed":
                if (!IsOnCooldown && currentWeapon != null && currentWeaponSystem != null)
                {
                    // Let the WeaponSystem handle the attack
                    character.RaiseEvent("weapon_attack_attempt", currentWeapon);
                }
                break;

            case "weapon_equipped":
                if (data is WeaponConfig weapon)
                {
                    EquipWeapon(weapon);
                }
                break;

            case "weapon_unequipped":
                UnequipWeapon();
                break;

            case "grapple_hook_equipped":
                if (data is GrappleConfig grappleHook)
                {
                    EquipGrappleHook(grappleHook);
                }
                break;

            case "config_changed":
                // Re-initialize with current equipment if config changed
                if (currentWeapon != null)
                {
                    EquipWeapon(currentWeapon);
                }
                break;
        }
    }

    // Weapon Equipment Methods
    public void EquipWeapon(WeaponConfig weapon)
    {
        if (weapon == currentWeapon) return;

        UnequipWeapon();
        currentWeapon = weapon;

        if (weapon == null)
        {
            OnWeaponChanged?.Invoke(null);
            character.RaiseEvent("weapon_equipped", null);
            return;
        }

        // Determine which WeaponSystem to use based on weapon type
        switch (weapon.weaponType)
        {
            case WeaponType.Hitbox:
                currentWeaponSystem = gameObject.AddComponent<HitboxWeaponSystem>();
                break;
            case WeaponType.CursorWeapon:
                currentWeaponSystem = gameObject.AddComponent<CursorWeaponSystem>();
                break;
            case WeaponType.AutoAttack:
                currentWeaponSystem = gameObject.AddComponent<AutoAttackWeaponSystem>();
                break;
            default:
                Debug.LogError($"Unknown weapon type: {weapon.weaponType}");
                currentWeapon = null;
                return;
        }

        if (currentWeaponSystem != null)
        {
            // Configure the weapon system
            currentWeaponSystem.weaponOrigin = weaponOrigin;
            currentWeaponSystem.showDebugInfo = showWeaponDebug;

            // Initialize with character core
            currentWeaponSystem.Initialize(character);

            // Subscribe to weapon system events
            currentWeaponSystem.OnWeaponChanged += HandleWeaponSystemChanged;

            Debug.Log($"Equipped {weapon.weaponName} ({weapon.weaponType})");
        }
        else
        {
            Debug.LogError($"Failed to create WeaponSystem for {weapon.weaponName}");
            currentWeapon = null;
            return;
        }

        OnWeaponChanged?.Invoke(weapon);
        character.RaiseEvent("weapon_equipped", weapon);
    }

    private void HandleWeaponSystemChanged(WeaponConfig weapon)
    {
        // Handle any weapon system configuration changes
        Debug.Log($"Weapon system configuration changed: {weapon?.weaponName}");
    }

    public void UnequipWeapon()
    {
        if (currentWeapon != null)
        {
            // Clean up current weapon system
            if (currentWeaponSystem != null)
            {
                // Unsubscribe from events
                currentWeaponSystem.OnWeaponChanged -= HandleWeaponSystemChanged;

                // Destroy the component
                Destroy(currentWeaponSystem);
                currentWeaponSystem = null;
            }

            Debug.Log($"Unequipped {currentWeapon.weaponName}");

            currentWeapon = null;
            OnWeaponChanged?.Invoke(null);
            character.RaiseEvent("weapon_unequipped", null);
        }
    }

    // Grapple Hook Equipment Methods
    public void EquipGrappleHook(GrappleConfig grappleHook)
    {
        if (grappleHook == null || grappleHook == currentGrappleHook) return;

        currentGrappleHook = grappleHook;

        // Apply to grapple system
        if (grappleSystem != null)
        {
            grappleSystem.SwitchGrappleConfig(grappleHook);
        }

        Debug.Log($"Equipped grapple hook: {grappleHook.GrappleName}");

        // Raise event
        OnGrappleHookChanged?.Invoke(grappleHook);
        character.RaiseEvent("grapple_hook_changed", grappleHook);
    }

    public void UnequipGrappleHook()
    {
        if (currentGrappleHook != null)
        {
            currentGrappleHook = null;
            character.RaiseEvent("grapple_hook_unequipped", null);
            Debug.Log("Unequipped grapple hook");
        }
    }

    // Combat Methods - Now handled by WeaponSystem
    // The actual attack logic is in the specific WeaponSystem components

    // Helper methods for weapons
    public float GetCurrentVelocityMagnitude() => currentVelocityMagnitude;

    public bool IsGrappling() => grappleSystem?.IsGrappling() ?? false;

    public Vector2 GetGrappleVelocity()
    {
        if (grappleSystem != null && rb != null)
        {
            return rb.linearVelocity;
        }
        return Vector2.zero;
    }

    public Vector2 GetAttackDirection() => lastAttackDirection;

    public void SetAttackDirection(Vector2 direction)
    {
        lastAttackDirection = direction.normalized;
    }

    // Calculate damage with velocity scaling - now uses config manager
    public float CalculateDamage(float baseDamage)
    {
        if (currentWeapon == null || currentWeaponSystem == null)
            return baseDamage;

        return currentWeaponSystem.CalculateDamage(baseDamage);
    }

    // Public API for external systems
    public string GetCurrentWeaponInfo()
    {
        if (currentWeapon == null) return "No weapon equipped";

        string info = $"{currentWeapon.weaponName} ({currentWeapon.weaponType})";

        if (currentWeaponSystem != null && currentWeaponSystem.IsAttacking)
        {
            info += " [Attacking]";
        }
        else if (IsOnCooldown)
        {
            info += $" [Cooldown: {attackCooldownTimer:F2}s]";
        }

        return info;
    }

    // Force update cooldown (can be called by WeaponSystem)
    public void SetAttackCooldown(float cooldown)
    {
        attackCooldownTimer = cooldown;
    }

    // Cleanup
    private void OnDestroy()
    {
        if (character != null)
        {
            character.OnEvent -= HandleCharacterEvent;
        }

        UnequipWeapon();

        // Clean up events
        OnWeaponChanged = null;
        OnGrappleHookChanged = null;
    }

    // Unity Editor helper
#if UNITY_EDITOR
    void OnValidate()
    {
        // Ensure weapon origin is set
        if (weaponOrigin == null)
        {
            weaponOrigin = transform;
        }
    }
#endif
}