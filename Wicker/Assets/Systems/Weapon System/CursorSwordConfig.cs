// CursorSwordConfig.cs
using UnityEngine;

[CreateAssetMenu(fileName = "CursorSword", menuName = "Weapons/Cursor Sword")]
public class CursorSwordConfig : WeaponConfig
{
    [Header("Cursor Sword Settings")]
    public float orbitRadius = 2f;
    public float orbitSpeed = 5f;
    public float swordMass = 1f;
    public float swordDrag = 0.5f;
    public float maxSwordSpeed = 20f;
    public float returnForce = 10f;

    [Header("Control Settings")]
    public float cursorFollowSpeed = 15f;
    public float maxAnglePerSecond = 180f;
    public bool usePhysicsBasedMovement = true;

    [Header("Combat")]
    public float damagePerSpeedUnit = 0.2f; // Extra damage per unit of sword speed
    public float minimumDamageSpeed = 2f;

    public override void InitializeWeapon(GameObject weaponInstance, CharacterCore character)
    {
        // Will be handled by CursorSwordController
    }
}