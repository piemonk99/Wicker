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

    // Debug visualization
    private bool showDebugHitbox = false;
    private Vector2 lastHitboxPosition;
    private Vector2 lastAttackDirection;
    private Color debugColor = new Color(1f, 0.5f, 0f, 0.3f); // Orange with transparency

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

        // Enable debug if configured
        showDebugHitbox = this.config.enableDebugVisualization;
        if (showDebugHitbox)
        {
            Debug.Log($"Hitbox weapon debug visualization enabled for {this.config.weaponName}");
        }
    }

    public bool TryAttack()
    {
        if (config == null) return false;

        // Determine attack direction
        lastAttackDirection = owner.GetAttackDirection();

        // Start attack
        isActive = true;
        activeTimer = config.attackDuration;
        alreadyHit.Clear();

        // Check initial hits
        CheckHitbox(lastAttackDirection);

        Debug.Log($"Hitbox weapon attack started with direction {lastAttackDirection}");

        return true;
    }

    public bool IsAttacking() => isActive;

    private void CheckHitbox(Vector2 direction)
    {
        // Calculate hitbox position
        lastHitboxPosition = (Vector2)characterTransform.position +
                             config.hitboxOffset.x * direction +
                             new Vector2(0, config.hitboxOffset.y);

        // Draw debug visualization if enabled
        if (showDebugHitbox)
        {
            DrawDebugHitbox(lastHitboxPosition, direction);
        }

        // Use the updated Physics2D.OverlapBox method
        var hitColliders = Physics2D.OverlapBoxAll(
            lastHitboxPosition,
            config.hitboxSize,
            0f,
            config.hitLayers
        );

        Debug.Log($"Hitbox check found {hitColliders.Length} colliders");

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
            Debug.Log($"Hit {hit.gameObject.name} for {damage} damage");
        }
    }

    private void DrawDebugHitbox(Vector2 position, Vector2 direction)
    {
        // Draw the hitbox as a wire cube
        Vector3[] corners = new Vector3[4];
        Vector2 halfSize = config.hitboxSize * 0.5f;

        corners[0] = position + new Vector2(-halfSize.x, -halfSize.y);
        corners[1] = position + new Vector2(halfSize.x, -halfSize.y);
        corners[2] = position + new Vector2(halfSize.x, halfSize.y);
        corners[3] = position + new Vector2(-halfSize.x, halfSize.y);

        // Draw the box
        for (int i = 0; i < 4; i++)
        {
            Debug.DrawLine(corners[i], corners[(i + 1) % 4], debugColor, config.attackDuration);
        }

        // Draw direction indicator
        Debug.DrawRay(position, direction * 0.5f, Color.red, config.attackDuration);

        // Draw hitbox center
        Debug.DrawRay(position, Vector2.up * 0.1f, Color.green, config.attackDuration);
        Debug.DrawRay(position, Vector2.right * 0.1f, Color.green, config.attackDuration);
    }

    private void ApplyDamage(GameObject target, float damage, Vector2 direction)
    {
        // Get or add CharacterCondition
        var condition = target.GetComponent<CharacterCondition>();
        if (condition == null)
        {
            // For testing, add a CharacterCondition if none exists
            condition = target.AddComponent<CharacterCondition>();
            condition.maxHealth = 100f;
            condition.currentHealth = 100f;
        }

        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position);

            // Apply knockback
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null && config.knockbackForce > 0)
            {
                rb.AddForce(direction * config.knockbackForce, ForceMode2D.Impulse);
                Debug.Log($"Applied {config.knockbackForce} knockback to {target.name}");
            }

            // Raise hit event
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "HitboxWeapon"
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
                Debug.Log("Hitbox weapon attack ended");
            }
            else
            {
                // Continue checking during active frames if multi-hit
                if (config != null && config.multiHit && activeTimer > 0)
                {
                    Vector2 attackDirection = owner.GetAttackDirection();
                    CheckHitbox(attackDirection);
                }

                // Update debug visualization
                if (showDebugHitbox && activeTimer > 0)
                {
                    UpdateDebugVisualization();
                }
            }
        }
    }

    private void UpdateDebugVisualization()
    {
        // Update hitbox position based on current character position and direction
        Vector2 currentDirection = owner.GetAttackDirection();
        Vector2 currentPosition = (Vector2)characterTransform.position +
                                 config.hitboxOffset.x * currentDirection +
                                 new Vector2(0, config.hitboxOffset.y);

        // Only redraw if position changed significantly
        if (Vector2.Distance(currentPosition, lastHitboxPosition) > 0.1f)
        {
            lastHitboxPosition = currentPosition;
            lastAttackDirection = currentDirection;
            DrawDebugHitbox(currentPosition, currentDirection);
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics-based updates if needed
    }
}