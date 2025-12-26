// GameSetup.cs
using UnityEngine;
using System.Collections;

public class GameSetup : MonoBehaviour
{
    [Header("Starting Equipment")]
    public WeaponConfig startingWeapon;
    public GrappleConfig startingGrappleHook;

    [Header("Additional Items (Optional)")]
    public WeaponConfig[] additionalWeapons;
    public GrappleConfig[] additionalGrappleHooks;

    void Start()
    {
        // Ensure required components exist
        EnsureCharacterComponents();

        // Initialize equipment
        StartCoroutine(InitializeEquipment());
    }

    private void EnsureCharacterComponents()
    {
        var characterCore = GetComponent<CharacterCore>();

        if (characterCore == null)
        {
            Debug.LogError("GameSetup requires CharacterCore component");
            return;
        }
    }

    private IEnumerator InitializeEquipment()
    {
        yield return null; // Wait one frame for initialization

        var inventory = GetComponent<CharacterInventory>();
        var condition = GetComponent<CharacterCondition>();

        if (inventory == null || condition == null)
        {
            Debug.LogError("Failed to get required components");
            yield break;
        }

        // Add starting weapon
        if (startingWeapon != null)
        {
            inventory.AddWeapon(startingWeapon);
            inventory.EquipWeapon(startingWeapon);
        }

        // Add starting grapple hook
        if (startingGrappleHook != null)
        {
            inventory.AddGrappleHook(startingGrappleHook);
            inventory.EquipGrappleHook(startingGrappleHook);
        }

        // Add additional weapons
        if (additionalWeapons != null)
        {
            foreach (var weapon in additionalWeapons)
            {
                if (weapon != null)
                    inventory.AddWeapon(weapon);
            }
        }

        // Add additional grapple hooks
        if (additionalGrappleHooks != null)
        {
            foreach (var grappleHook in additionalGrappleHooks)
            {
                if (grappleHook != null)
                    inventory.AddGrappleHook(grappleHook);
            }
        }
    }

    // Public API for switching equipment (could be called from UI)
    public void SwitchWeapon(WeaponConfig newWeapon)
    {
        var inventory = GetComponent<CharacterInventory>();
        if (inventory != null && newWeapon != null)
        {
            inventory.EquipWeapon(newWeapon);
        }
    }

    public void SwitchGrappleHook(GrappleConfig newGrappleHook)
    {
        var inventory = GetComponent<CharacterInventory>();
        if (inventory != null && newGrappleHook != null)
        {
            inventory.EquipGrappleHook(newGrappleHook);
        }
    }

    public void AddWeaponToInventory(WeaponConfig weapon)
    {
        var inventory = GetComponent<CharacterInventory>();
        if (inventory != null && weapon != null)
        {
            inventory.AddWeapon(weapon);
        }
    }

    public void AddGrappleHookToInventory(GrappleConfig grappleHook)
    {
        var inventory = GetComponent<CharacterInventory>();
        if (inventory != null && grappleHook != null)
        {
            inventory.AddGrappleHook(grappleHook);
        }
    }
}