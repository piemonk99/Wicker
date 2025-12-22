// GameSetup.cs - Add to your player GameObject
using UnityEngine;

public class GameSetup : MonoBehaviour
{
    [Header("Starting Weapon")]
    public WeaponConfig startingWeapon;

    void Start()
    {
        var characterCore = GetComponent<CharacterCore>();

        // Equip starting weapon after a short delay (to ensure components are initialized)
        StartCoroutine(EquipStartingWeapon());
    }

    System.Collections.IEnumerator EquipStartingWeapon()
    {
        yield return null; // Wait one frame for initialization

        var inventory = GetComponent<InventoryComponent>();
        if (inventory != null && startingWeapon != null)
        {
            inventory.EquipWeapon(startingWeapon);
        }
    }

    // Example method to switch weapons (could be called from UI)
    public void SwitchWeapon(WeaponConfig newWeapon)
    {
        var inventory = GetComponent<InventoryComponent>();
        if (inventory != null)
        {
            inventory.EquipWeapon(newWeapon);
        }
    }
}