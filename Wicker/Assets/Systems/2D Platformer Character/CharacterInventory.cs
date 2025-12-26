using UnityEngine;
using System.Collections.Generic;
using System;


// CharacterInventory handles tracking of all items a character owns
public class CharacterInventory : MonoBehaviour, ICharacterComponent
{
    [Header("Inventory Settings")]
    public int maxWeapons = 10;
    public int maxGrappleHooks = 5;

    // Inventory State
    private List<WeaponConfig> ownedWeapons = new List<WeaponConfig>();
    private List<GrappleConfig> ownedGrappleHooks = new List<GrappleConfig>();

    // Currently equipped items
    private WeaponConfig equippedWeapon;
    private GrappleConfig equippedGrappleHook;

    // Reference to equipment manager
    private CharacterEquipment equipmentManager;

    // Events
    public event Action<WeaponConfig> OnWeaponEquipped;
    public event Action<GrappleConfig> OnGrappleHookEquipped;
    public event Action<WeaponConfig> OnWeaponAdded;
    public event Action<GrappleConfig> OnGrappleHookAdded;

    // Public Properties
    public WeaponConfig EquippedWeapon => equippedWeapon;
    public GrappleConfig EquippedGrappleHook => equippedGrappleHook;
    public IReadOnlyList<WeaponConfig> OwnedWeapons => ownedWeapons;
    public IReadOnlyList<GrappleConfig> OwnedGrappleHooks => ownedGrappleHooks;
    public int WeaponCount => ownedWeapons.Count;
    public int GrappleHookCount => ownedGrappleHooks.Count;

    public void Initialize(CharacterCore character)
    {
        equipmentManager = character.GetCharacterComponent<CharacterEquipment>();

        if (equipmentManager == null)
        {
            Debug.LogError("CharacterInventory requires CharacterEquipment component on the same GameObject");
            return;
        }
    }

    public void Tick(float deltaTime) { }
    public void PhysicsTick(float fixedDeltaTime) { }

    // Weapon Management
    public bool AddWeapon(WeaponConfig weapon)
    {
        if (weapon == null) return false;

        if (ownedWeapons.Contains(weapon))
        {
            Debug.LogWarning($"Weapon {weapon.weaponName} is already in inventory");
            return false;
        }

        if (ownedWeapons.Count >= maxWeapons)
        {
            Debug.LogWarning($"Cannot add weapon {weapon.weaponName}: inventory full (max {maxWeapons})");
            return false;
        }

        ownedWeapons.Add(weapon);
        OnWeaponAdded?.Invoke(weapon);
        Debug.Log($"Added weapon to inventory: {weapon.weaponName}");

        return true;
    }

    public bool RemoveWeapon(WeaponConfig weapon)
    {
        if (weapon == null || !ownedWeapons.Contains(weapon)) return false;

        // If removing equipped weapon, unequip it first
        if (equippedWeapon == weapon)
        {
            UnequipWeapon();
        }

        ownedWeapons.Remove(weapon);
        Debug.Log($"Removed weapon from inventory: {weapon.weaponName}");

        return true;
    }

    public bool EquipWeapon(WeaponConfig weapon)
    {
        if (weapon == null) return false;

        if (!ownedWeapons.Contains(weapon))
        {
            Debug.LogWarning($"Cannot equip weapon {weapon.weaponName}: not in inventory");
            return false;
        }

        // Tell equipment manager to equip this weapon
        if (equipmentManager != null)
        {
            equipmentManager.EquipWeapon(weapon);
            equippedWeapon = weapon;
            OnWeaponEquipped?.Invoke(weapon);
            Debug.Log($"Equipped weapon: {weapon.weaponName}");
            return true;
        }

        return false;
    }

    public void UnequipWeapon()
    {
        if (equippedWeapon != null)
        {
            equipmentManager?.UnequipWeapon();
            equippedWeapon = null;
            Debug.Log("Unequipped weapon");
        }
    }

    // Grapple Hook Management
    public bool AddGrappleHook(GrappleConfig grappleHook)
    {
        if (grappleHook == null) return false;

        if (ownedGrappleHooks.Contains(grappleHook))
        {
            Debug.LogWarning($"Grapple hook {grappleHook.GrappleName} is already in inventory");
            return false;
        }

        if (ownedGrappleHooks.Count >= maxGrappleHooks)
        {
            Debug.LogWarning($"Cannot add grapple hook {grappleHook.GrappleName}: inventory full (max {maxGrappleHooks})");
            return false;
        }

        ownedGrappleHooks.Add(grappleHook);
        OnGrappleHookAdded?.Invoke(grappleHook);
        Debug.Log($"Added grapple hook to inventory: {grappleHook.GrappleName}");

        return true;
    }

    public bool RemoveGrappleHook(GrappleConfig grappleHook)
    {
        if (grappleHook == null || !ownedGrappleHooks.Contains(grappleHook)) return false;

        // If removing equipped grapple, unequip it first
        if (equippedGrappleHook == grappleHook)
        {
            UnequipGrappleHook();
        }

        ownedGrappleHooks.Remove(grappleHook);
        Debug.Log($"Removed grapple hook from inventory: {grappleHook.GrappleName}");

        return true;
    }

    public bool EquipGrappleHook(GrappleConfig grappleHook)
    {
        if (grappleHook == null) return false;

        if (!ownedGrappleHooks.Contains(grappleHook))
        {
            Debug.LogWarning($"Cannot equip grapple hook {grappleHook.GrappleName}: not in inventory");
            return false;
        }

        // Tell equipment manager to equip this grapple hook
        if (equipmentManager != null)
        {
            equipmentManager.EquipGrappleHook(grappleHook);
            equippedGrappleHook = grappleHook;
            OnGrappleHookEquipped?.Invoke(grappleHook);
            Debug.Log($"Equipped grapple hook: {grappleHook.GrappleName}");
            return true;
        }

        return false;
    }

    public void UnequipGrappleHook()
    {
        if (equippedGrappleHook != null)
        {
            equipmentManager?.UnequipGrappleHook();
            equippedGrappleHook = null;
            Debug.Log("Unequipped grapple hook");
        }
    }

    // Inventory Queries
    public bool HasWeapon(WeaponConfig weapon)
    {
        return weapon != null && ownedWeapons.Contains(weapon);
    }

    public bool HasGrappleHook(GrappleConfig grappleHook)
    {
        return grappleHook != null && ownedGrappleHooks.Contains(grappleHook);
    }

    public List<WeaponConfig> GetWeaponsByType(WeaponType type)
    {
        var result = new List<WeaponConfig>();
        foreach (var weapon in ownedWeapons)
        {
            if (weapon.weaponType == type)
            {
                result.Add(weapon);
            }
        }
        return result;
    }

    public WeaponConfig GetWeaponByName(string name)
    {
        return ownedWeapons.Find(w => w.weaponName == name);
    }

    public GrappleConfig GetGrappleHookByName(string name)
    {
        return ownedGrappleHooks.Find(g => g.GrappleName == name);
    }

    // Save/Load Support (simplified)
    public InventoryData GetInventoryData()
    {
        var data = new InventoryData();

        // Store weapon names
        foreach (var weapon in ownedWeapons)
        {
            data.weaponNames.Add(weapon.weaponName);
        }

        // Store grapple hook names
        foreach (var grapple in ownedGrappleHooks)
        {
            data.grappleHookNames.Add(grapple.GrappleName);
        }

        // Store equipped items
        data.equippedWeaponName = equippedWeapon?.weaponName;
        data.equippedGrappleHookName = equippedGrappleHook?.GrappleName;

        return data;
    }

    public class InventoryData
    {
        public List<string> weaponNames = new List<string>();
        public List<string> grappleHookNames = new List<string>();
        public string equippedWeaponName;
        public string equippedGrappleHookName;
    }
}