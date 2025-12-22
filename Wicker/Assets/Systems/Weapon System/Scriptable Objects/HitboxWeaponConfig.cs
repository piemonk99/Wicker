// HitboxWeaponConfig.cs
using UnityEngine;

[CreateAssetMenu(fileName = "HitboxWeapon", menuName = "Weapons/Hitbox Weapon")]
public class HitboxWeaponConfig : WeaponConfig
{
    [Header("Hitbox Settings")]
    public Vector2 hitboxSize = new Vector2(1.5f, 0.8f);
    public Vector2 hitboxOffset = new Vector2(0.5f, 0f);
    public float attackDuration = 0.2f;
    public AnimationClip attackAnimation;
    public LayerMask hitLayers = ~0;

    [Header("Advanced")]
    public bool multiHit = false;
    public int maxHitsPerAttack = 1;
    public float knockbackForce = 5f;

    public override void InitializeWeapon(GameObject weaponInstance, CharacterCore character)
    {
        // Will be handled by HitboxWeaponController
    }
}