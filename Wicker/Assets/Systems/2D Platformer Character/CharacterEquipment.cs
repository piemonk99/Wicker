using UnityEngine;
using System.Collections.Generic;

// CharacterEquipment handles equipping items and checks inventory
public class CharacterEquipment : MonoBehaviour, ICharacterComponent
{
    [Header("References")]
    public Transform weaponOrigin;

    [Header("Debug")]
    public bool showWeaponDebug = false;

    // References
    private CharacterCore character;
    private CharacterInventory inventory;
    private Rigidbody2D rb;
    private CharacterGrapple characterGrapple;

    // Equipment State
    private WeaponConfig currentWeapon;
    private GrappleConfig currentGrappleHook;

    // Active Systems
    private CharacterWeapon currentWeaponSystem;

    // Events
    public event System.Action<WeaponConfig> OnWeaponChanged;
    public event System.Action<GrappleConfig> OnGrappleHookChanged;

    // Public Properties
    public WeaponConfig CurrentWeapon => currentWeapon;
    public GrappleConfig CurrentGrappleHook => currentGrappleHook;

    public void Initialize(CharacterCore character)
    {
        this.character = character;
        rb = character.GetComponent<Rigidbody2D>();
        inventory = character.GetCharacterComponent<CharacterInventory>();
        characterGrapple = character.GetCharacterComponent<CharacterGrapple>();

        if (inventory == null)
        {
            Debug.LogError("CharacterEquipment requires CharacterInventory component");
            return;
        }

        if (weaponOrigin == null)
        {
            weaponOrigin = transform;
            Debug.Log($"Weapon origin not set, using character transform");
        }

        // Subscribe to events
        if (character != null)
        {
            character.OnEvent -= HandleEvent; // Remove old
            character.OnEvent += HandleEvent; // Add new
        }
    }

    public void Tick(float deltaTime)
    {
        // Tick current weapon system
        if (currentWeaponSystem != null)
        {
            currentWeaponSystem.Tick(deltaTime);
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        if (currentWeaponSystem != null)
        {
            currentWeaponSystem.PhysicsTick(fixedDeltaTime);
        }
    }

    private void HandleEvent(string type, object data)
    {
        switch (type)
        {
            case "attack_pressed":
                if (currentWeapon != null && currentWeaponSystem != null)
                {
                    character.RaiseEvent("weapon_attack_attempt", currentWeapon);
                }
                break;

            case "equip_weapon":
                if (data is WeaponConfig weapon)
                {
                    EquipWeapon(weapon);
                }
                break;

            case "unequip_weapon":
                UnequipWeapon();
                break;

            case "equip_grapple_hook":
                if (data is GrappleConfig grappleHook)
                {
                    EquipGrappleHook(grappleHook);
                }
                break;
            case "config_changed":
                // Re-equip current weapon with new config
                if (currentWeapon != null)
                {
                    WeaponConfig tempWeapon = currentWeapon;
                    currentWeapon = null; // Force re-equip
                    EquipWeapon(tempWeapon);
                }
                break;
        }
    }

    // Public API for equipping weapons
    public bool EquipWeapon(WeaponConfig weapon)
    {
        if (weapon == null)
        {
            // Unequip if null is passed
            UnequipWeapon();
            return true;
        }

        if (weapon == currentWeapon) return true;

        // Check if weapon is in inventory
        if (inventory != null && !inventory.HasWeapon(weapon))
        {
            Debug.LogWarning($"Cannot equip {weapon.weaponName}: not in inventory");
            return false;
        }

        UnequipWeapon();
        currentWeapon = weapon;

        // Create appropriate CharacterWeapon
        CharacterWeapon weaponSystem = CreateWeaponSystem(weapon.weaponType);

        if (weaponSystem == null)
        {
            Debug.LogError($"Failed to create CharacterWeapon for {weapon.weaponName}");
            currentWeapon = null;
            return false;
        }

        currentWeaponSystem = weaponSystem;

        // Configure and initialize
        currentWeaponSystem.weaponOrigin = weaponOrigin;
        currentWeaponSystem.showDebugInfo = showWeaponDebug;
        currentWeaponSystem.Initialize(character);
        currentWeaponSystem.SetWeaponConfig(weapon);

        Debug.Log($"Equipped {weapon.weaponName} ({weapon.weaponType})");

        OnWeaponChanged?.Invoke(weapon);
        character.RaiseEvent("weapon_equipped", weapon);

        return true;
    }

    private CharacterWeapon CreateWeaponSystem(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Hitbox => gameObject.AddComponent<CharacterHitboxWeapon>(),
            WeaponType.CursorWeapon => gameObject.AddComponent<CharacterCursorWeapon>(),
            WeaponType.AutoAttack => gameObject.AddComponent<CharacterAutoAttackWeapon>(),
            _ => null
        };
    }

    public void UnequipWeapon()
    {
        if (currentWeapon != null)
        {
            if (currentWeaponSystem != null)
            {
                Destroy(currentWeaponSystem);
                currentWeaponSystem = null;
            }

            Debug.Log($"Unequipped {currentWeapon.weaponName}");
            OnWeaponChanged?.Invoke(null);
            character.RaiseEvent("weapon_unequipped", null);

            currentWeapon = null;
        }
    }

    // Public API for equipping grapple hooks
    public bool EquipGrappleHook(GrappleConfig grappleHook)
    {
        if (grappleHook == null || grappleHook == currentGrappleHook)
            return false;

        // Check if grapple hook is in inventory
        if (inventory != null && !inventory.HasGrappleHook(grappleHook))
        {
            Debug.LogWarning($"Cannot equip {grappleHook.GrappleName}: not in inventory");
            return false;
        }

        currentGrappleHook = grappleHook;

        // Apply to grapple system
        if (characterGrapple != null)
        {
            characterGrapple.SwitchGrappleConfig(grappleHook);
        }

        Debug.Log($"Equipped grapple hook: {grappleHook.GrappleName}");

        OnGrappleHookChanged?.Invoke(grappleHook);
        character.RaiseEvent("grapple_hook_changed", grappleHook);

        return true;
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

    // Helper methods
    public float CalculateDamage(float baseDamage)
    {
        if (currentWeapon == null || currentWeaponSystem == null)
            return baseDamage;

        return currentWeaponSystem.CalculateDamage(baseDamage);
    }

    public string GetCurrentWeaponInfo()
    {
        if (currentWeapon == null) return "No weapon equipped";

        string info = $"{currentWeapon.weaponName} ({currentWeapon.weaponType})";

        if (currentWeaponSystem != null && currentWeaponSystem.IsAttacking)
        {
            info += " [Attacking]";
        }

        return info;
    }

    // Cleanup
    private void OnDestroy()
    {
        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        UnequipWeapon();
        OnWeaponChanged = null;
        OnGrappleHookChanged = null;
    }
}