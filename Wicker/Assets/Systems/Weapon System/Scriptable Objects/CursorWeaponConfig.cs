using UnityEngine;

[CreateAssetMenu(fileName = "NewCursorWeaponConfig", menuName = "Weapons/Cursor Weapon Config")]
public class CursorWeaponConfig : ScriptableObject
{
    public CursorWeaponMechanicsConfig mechanics;
    public CursorWeaponVisualConfig visual;
    public CursorWeaponSoundConfig sound;
}

[System.Serializable]
public class CursorWeaponMechanicsConfig : WeaponMechanicsConfig
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
    public float damagePerSpeedUnit = 0.2f;
    public float minimumDamageSpeed = 2f;
}

[System.Serializable]
public class CursorWeaponVisualConfig : WeaponVisualConfig
{
    [Header("Cursor Weapon Visual Settings")]
    public Color orbitDebugColor = Color.yellow;
    public Color swordTrailColor = Color.cyan;
}

[System.Serializable]
public class CursorWeaponSoundConfig : WeaponSoundConfig
{
    [Header("Cursor Weapon Specific Sounds")]
    public SoundNode swooshSound;
    public float swooshVelocityThreshold = 10f;
    public float swooshVolume = 1.0f;
}