// CursorWeaponConfig.cs
using UnityEngine;

[CreateAssetMenu(fileName = "CursorWeapon", menuName = "Weapons/Cursor Weapon")]
public class CursorWeaponConfig : WeaponConfig
{
    [Header("Cursor Weapon Settings")]
    public float orbitRadius = 2f;
    public float orbitSpeed = 5f;
    public float weaponMass = 1f;
    public float weaponDrag = 0.5f;
    public float maxWeaponSpeed = 20f;
    public float returnForce = 10f;

    [Header("Control Settings")]
    public float cursorFollowSpeed = 15f;
    public float maxAnglePerSecond = 180f;
    public bool usePhysicsBasedMovement = true;

    [Header("Combat")]
    public float damagePerSpeedUnit = 0.2f; // Extra damage per unit of weapon speed
    public float minimumDamageSpeed = 2f;

    public override void InitializeWeapon(GameObject weaponInstance, CharacterCore character)
    {
        // Will be handled by CursorWeaponController
    }
}