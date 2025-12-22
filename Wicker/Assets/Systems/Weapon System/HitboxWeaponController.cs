// HitboxWeaponController.cs
using UnityEngine;
using System.Collections.Generic;

public class HitboxWeaponController : MonoBehaviour, IWeaponController
{
    private CharacterEquipment owner;
    private HitboxWeaponConfig config;
    private CharacterCore character;
    private Transform characterTransform;

    private bool isActive = false;
    private float activeTimer = 0f;
    private List<GameObject> alreadyHit = new List<GameObject>();

    public void Initialize(WeaponConfig baseConfig, CharacterCore character, CharacterEquipment owner)
    {
        this.config = baseConfig as HitboxWeaponConfig;
        this.character = character;
        this.owner = owner;
        this.characterTransform = character.transform;

        if (this.config == null)
        {
            Debug.LogError($"HitboxWeaponController requires HitboxWeaponConfig, got {baseConfig.GetType().Name}");
            return;
        }
    }

    public bool TryAttack()
    {
        if (config == null) return false;

        // Determine attack direction
        Vector2 attackDirection = owner.GetAttackDirection();

        // Start attack
        isActive = true;
        activeTimer = config.attackDuration;
        alreadyHit.Clear();

        // Check initial hits
        CheckHitbox(attackDirection);

        return true;
    }

    public bool IsAttacking() => isActive;

    private void CheckHitbox(Vector2 direction)
    {
        // Calculate hitbox position
        Vector2 hitboxPosition = (Vector2)characterTransform.position +
                                 config.hitboxOffset.x * direction +
                                 new Vector2(0, config.hitboxOffset.y);

        // Use the updated Physics2D.OverlapBox method
        var hitColliders = Physics2D.OverlapBoxAll(
            hitboxPosition,
            config.hitboxSize,
            0f,
            config.hitLayers
        );

        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == character.gameObject || alreadyHit.Contains(hit.gameObject))
                continue;

            // Check max hits
            if (!config.multiHit && alreadyHit.Count >= config.maxHitsPerAttack)
                break;

            // Calculate damage
            float damage = owner.CalculateDamage(config.baseDamage);

            // Apply damage
            ApplyDamage(hit.gameObject, damage, direction);

            alreadyHit.Add(hit.gameObject);
        }
    }

    private void ApplyDamage(GameObject target, float damage, Vector2 direction)
    {
        // Get or add CharacterCondition
        var health = target.GetComponent<CharacterCondition>();
        if (health == null)
        {
            // For testing, add a health component if none exists
            health = target.AddComponent<CharacterCondition>();
        }

        if (health != null)
        {
            health.TakeDamage(damage, target.transform.position);

            // Apply knockback
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null && config.knockbackForce > 0)
            {
                rb.AddForce(direction * config.knockbackForce, ForceMode2D.Impulse);
            }

            // Raise hit event
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position
            });
        }
    }

    public void Tick(float deltaTime)
    {
        if (isActive)
        {
            activeTimer -= deltaTime;
            if (activeTimer <= 0)
            {
                isActive = false;
            }
            else
            {
                // Continue checking during active frames if multi-hit
                if (config != null && config.multiHit && activeTimer > 0)
                {
                    Vector2 attackDirection = owner.GetAttackDirection();
                    CheckHitbox(attackDirection);
                }
            }
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics-based updates if needed
    }
}