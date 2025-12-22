// InventoryComponent.cs
using UnityEngine;

public class InventoryComponent : MonoBehaviour, ICharacterComponent
{
    private CharacterCore character;
    private WeaponComponent weaponComponent;

    // Currently equipped weapon
    private WeaponConfig currentWeapon;

    public WeaponConfig CurrentWeapon => currentWeapon;

    public void Initialize(CharacterCore character)
    {
        this.character = character;
        weaponComponent = character.GetCharacterComponent<WeaponComponent>();

        Debug.Log($"Inventory initialized for {character.gameObject.name}");
    }

    public void Tick(float deltaTime) { }
    public void PhysicsTick(float fixedDeltaTime) { }

    // Public API
    public void EquipWeapon(WeaponConfig weapon)
    {
        if (weapon == null) return;

        currentWeapon = weapon;

        // Tell weapon component to equip
        if (weaponComponent != null)
        {
            character.RaiseEvent("weapon_equipped", weapon);
        }

        Debug.Log($"Equipped weapon: {weapon.weaponName}");
    }

    public void UnequipWeapon()
    {
        currentWeapon = null;
        character.RaiseEvent("weapon_unequipped", null);
        Debug.Log("Unequipped weapon");
    }

    // Check if has weapon
    public bool HasWeapon() => currentWeapon != null;
}