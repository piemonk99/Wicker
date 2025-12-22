using UnityEngine;

[CreateAssetMenu(fileName = "AutoAttackWeapon", menuName = "Weapons/Auto Attack")]
public class AutoAttackWeaponConfig : WeaponConfig
{
    [Header("Auto Attack Settings")]
    public float detectionRadius = 3f;
    public float attackInterval = 0.3f;
    public float velocityThreshold = 3f;

    [Header("Grapple Enhancement")]
    public bool onlyActiveDuringGrapple = false;
    public float grappleDamageMultiplier = 1.5f;
    public float maxGrappleRange = 10f;

    [Header("Attack Parameters")]
    public float autoAttackDamage = 5f;
    public float attackWidth = 1f;
    public LayerMask enemyLayers;

    [Header("Debug")]
    public bool enableDebugVisualization = false;

    public override void InitializeWeapon(GameObject weaponInstance, CharacterCore character)
    {
        // Will be handled by AutoAttackController
    }
}