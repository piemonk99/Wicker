using UnityEngine;

[CreateAssetMenu(fileName = "NewAutoAttackWeaponConfig", menuName = "Weapons/Auto Attack Weapon Config")]
public class AutoAttackWeaponConfig : ScriptableObject
{
    public AutoAttackWeaponMechanicsConfig mechanics;
    public AutoAttackWeaponVisualConfig visual;
    public AutoAttackWeaponSoundConfig sound;
}

[System.Serializable]
public class AutoAttackWeaponMechanicsConfig : WeaponMechanicsConfig
{
    [Header("Auto Attack Settings")]
    public float detectionRadius = 4f;
    public float attackInterval = .1f;
    public float enemyRehitCooldown = 1f;
    public float velocityThreshold = 10f;

    [Header("Grapple Enhancement")]
    public bool onlyActiveDuringGrapple = false;
    public float grappleDamageMultiplier = 1.5f;
    public float maxGrappleRange = 6f;

    [Header("Target Parameters")]
    public LayerMask enemyLayers;
    public string enemyTag = "Enemy";
}

[System.Serializable]
public class AutoAttackWeaponVisualConfig : WeaponVisualConfig
{
    [Header("Auto Attack Visual Settings")]
    public Color detectionRadiusColor = Color.yellow;
    public Color grappleRangeColor = Color.red;
}

[System.Serializable]
public class AutoAttackWeaponSoundConfig : WeaponSoundConfig
{
    [Header("Auto Attack Specific Sounds")]
    public float critVelocityThreshold = 20f;
}

