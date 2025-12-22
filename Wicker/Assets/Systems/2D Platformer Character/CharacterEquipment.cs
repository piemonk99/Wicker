// CharacterEquipment.cs
using UnityEngine;
using System.Collections.Generic;

public class CharacterEquipment : MonoBehaviour, ICharacterComponent
{
    // References
    private CharacterCore character;
    private Rigidbody2D rb;
    private GrappleSystem grappleSystem;

    // Equipment State
    private WeaponConfig currentWeapon;
    private GrappleConfig currentGrappleHook;

    // Active Equipment Instances
    private GameObject weaponInstance;
    private IWeaponController weaponController;

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
    public bool IsAttacking => weaponController?.IsAttacking() ?? false;

    public void Initialize(CharacterCore character)
    {
        this.character = character;
        rb = character.GetComponent<Rigidbody2D>();
        grappleSystem = character.GetCharacterComponent<GrappleSystem>();

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

        // Tick current weapon controller
        if (weaponController != null)
        {
            weaponController.Tick(deltaTime);
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics tick for current weapon controller
        if (weaponController != null)
        {
            weaponController.PhysicsTick(fixedDeltaTime);
        }
    }

    private void HandleCharacterEvent(string type, object data)
    {
        switch (type)
        {
            case "attack_pressed":
                if (!IsOnCooldown && currentWeapon != null)
                {
                    TryAttack();
                }
                break;

            case "weapon_equipped":
                if (data is WeaponConfig weapon)
                {
                    EquipWeapon(weapon);
                }
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

        // Unequip current weapon
        UnequipWeapon();

        currentWeapon = weapon;

        Debug.Log($"Equipping weapon: {weapon.weaponName}");

        // Create weapon instance if prefab exists
        if (weapon.weaponPrefab != null)
        {
            weaponInstance = Instantiate(
                weapon.weaponPrefab,
                transform.position,
                Quaternion.identity,
                transform
            );

            // Get controller based on weapon type
            weaponController = CreateWeaponController(weapon.weaponType);

            if (weaponController != null)
            {
                weaponController.Initialize(weapon, character, this);
            }
        }
        else
        {
            // If no prefab, create an empty GameObject
            weaponInstance = new GameObject($"{weapon.weaponName}_Instance");
            weaponInstance.transform.SetParent(transform);
            weaponInstance.transform.localPosition = Vector3.zero;

            weaponController = CreateWeaponController(weapon.weaponType);
            if (weaponController != null)
            {
                weaponController.Initialize(weapon, character, this);
            }
        }

        // Raise event
        OnWeaponChanged?.Invoke(weapon);
        character.RaiseEvent("weapon_changed", weapon);
    }

    private IWeaponController CreateWeaponController(WeaponType weaponType)
    {
        if (weaponInstance == null) return null;

        switch (weaponType)
        {
            case WeaponType.Hitbox:
                return weaponInstance.AddComponent<HitboxWeaponController>();
            case WeaponType.CursorWeapon:
                return weaponInstance.AddComponent<CursorWeaponController>();
            case WeaponType.AutoAttack:
                return weaponInstance.AddComponent<AutoAttackWeaponController>();
            default:
                Debug.LogWarning($"No controller found for weapon type: {weaponType}");
                return null;
        }
    }

    public void UnequipWeapon()
    {
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
            weaponInstance = null;
        }

        weaponController = null;
        currentWeapon = null;

        character.RaiseEvent("weapon_unequipped", null);
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
            // Could set grapple system to a default config or disable it
            currentGrappleHook = null;
            character.RaiseEvent("grapple_hook_unequipped", null);
            Debug.Log("Unequipped grapple hook");
        }
    }

    // Combat Methods
    private void TryAttack()
    {
        if (currentWeapon == null || IsOnCooldown) return;

        // Delegate to weapon controller
        if (weaponController != null && weaponController.TryAttack())
        {
            // Set cooldown
            attackCooldownTimer = currentWeapon.attackCooldown;

            // Raise attack event
            character.RaiseEvent("attack_performed", currentWeapon);
        }
    }

    // Helper methods for weapons
    public float GetCurrentVelocityMagnitude() => currentVelocityMagnitude;
    public bool IsGrappling() => grappleSystem?.IsGrappling() ?? false;
    public Vector2 GetGrappleVelocity()
    {
        if (grappleSystem != null)
        {
            // You might need to add a GetVelocity() method to GrappleSystem
            return rb?.linearVelocity ?? Vector2.zero;
        }
        return rb?.linearVelocity ?? Vector2.zero;
    }

    public Vector2 GetAttackDirection() => lastAttackDirection;

    public void SetAttackDirection(Vector2 direction)
    {
        lastAttackDirection = direction.normalized;
    }

    // Calculate damage with velocity scaling
    public float CalculateDamage(float baseDamage)
    {
        if (currentWeapon == null || !currentWeapon.scalesWithVelocity)
            return baseDamage;

        float velocityBonus = Mathf.Min(
            currentVelocityMagnitude * currentWeapon.velocityDamageMultiplier,
            currentWeapon.maxVelocityBonus
        );

        return baseDamage + velocityBonus;
    }

    // Cleanup
    private void OnDestroy()
    {
        if (character != null)
        {
            character.OnEvent -= HandleCharacterEvent;
        }
        UnequipWeapon();
    }
}