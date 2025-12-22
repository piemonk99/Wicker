using UnityEngine;

[CreateAssetMenu(fileName = "NewHitboxWeaponConfig", menuName = "Weapons/Hitbox Weapon Config")]
public class HitboxWeaponConfig : ScriptableObject
{
    public HitboxWeaponMechanicsConfig mechanics;
    public HitboxWeaponVisualConfig visual;
    public HitboxWeaponSoundConfig sound;
}

[System.Serializable]
public class HitboxWeaponMechanicsConfig : WeaponMechanicsConfig
{
    [Header("Attack Settings")]
    public float attackCooldown = .5f;

    [Header("Hitbox Settings")]
    public Vector2 hitboxSize = new Vector2(1.5f, 0.8f);
    public Vector2 hitboxOffset = new Vector2(0.5f, 0f);
    public float attackDuration = 0.2f;
    public LayerMask hitLayers = ~0;

    [Header("Advanced")]
    public bool multiHit = false;
    public int maxHitsPerAttack = 1;
    public float knockbackForce = 5f;
}

[System.Serializable]
public class HitboxWeaponVisualConfig : WeaponVisualConfig
{
    [Header("Hitbox Visual Settings")]
    public AnimationClip attackAnimation;
    public Color hitboxDebugColor = new Color(1f, 0.5f, 0f, 0.3f);
}

[System.Serializable]
public class HitboxWeaponSoundConfig : WeaponSoundConfig
{
    
}