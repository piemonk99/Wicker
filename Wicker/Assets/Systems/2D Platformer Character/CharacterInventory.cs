using UnityEngine;
using System.Collections.Generic;
using System;

// CharacterInventory ONLY tracks owned items - no equipping logic
public class CharacterInventory : MonoBehaviour, ICharacterComponent
{
    [Header("Inventory Settings")]
    public int maxWeapons = 10;
    public int maxGrappleHooks = 5;

    // Inventory State
    private List<WeaponConfig> ownedWeapons = new List<WeaponConfig>();
    private List<GrappleConfig> ownedGrappleHooks = new List<GrappleConfig>();

    // Events
    public event Action<WeaponConfig> OnWeaponAdded;
    public event Action<GrappleConfig> OnGrappleHookAdded;
    public event Action<WeaponConfig> OnWeaponRemoved;
    public event Action<GrappleConfig> OnGrappleHookRemoved;

    // Public Properties
    public IReadOnlyList<WeaponConfig> OwnedWeapons => ownedWeapons;
    public IReadOnlyList<GrappleConfig> OwnedGrappleHooks => ownedGrappleHooks;
    public int WeaponCount => ownedWeapons.Count;
    public int GrappleHookCount => ownedGrappleHooks.Count;

    public void Initialize(CharacterCore character)
    {
        // Inventory doesn't need equipment reference anymore
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

        ownedWeapons.Remove(weapon);
        OnWeaponRemoved?.Invoke(weapon);
        Debug.Log($"Removed weapon from inventory: {weapon.weaponName}");

        return true;
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

        ownedGrappleHooks.Remove(grappleHook);
        OnGrappleHookRemoved?.Invoke(grappleHook);
        Debug.Log($"Removed grapple hook from inventory: {grappleHook.GrappleName}");

        return true;
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

    // Save/Load Support
    public InventoryData GetInventoryData()
    {
        var data = new InventoryData();

        foreach (var weapon in ownedWeapons)
        {
            data.weaponNames.Add(weapon.weaponName);
        }

        foreach (var grapple in ownedGrappleHooks)
        {
            data.grappleHookNames.Add(grapple.GrappleName);
        }

        return data;
    }

    public void LoadInventoryData(InventoryData data, WeaponConfig[] availableWeapons, GrappleConfig[] availableGrappleHooks)
    {
        ownedWeapons.Clear();
        ownedGrappleHooks.Clear();

        foreach (string weaponName in data.weaponNames)
        {
            foreach (var weapon in availableWeapons)
            {
                if (weapon.weaponName == weaponName)
                {
                    ownedWeapons.Add(weapon);
                    break;
                }
            }
        }

        foreach (string grappleName in data.grappleHookNames)
        {
            foreach (var grapple in availableGrappleHooks)
            {
                if (grapple.GrappleName == grappleName)
                {
                    ownedGrappleHooks.Add(grapple);
                    break;
                }
            }
        }
    }

    public class InventoryData
    {
        public List<string> weaponNames = new List<string>();
        public List<string> grappleHookNames = new List<string>();
    }
}