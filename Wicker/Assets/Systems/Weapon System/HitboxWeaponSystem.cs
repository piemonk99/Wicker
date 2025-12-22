using UnityEngine;
using System.Collections.Generic;

public class HitboxWeaponSystem : WeaponSystem
{
    // Visual instance
    private GameObject weaponInstance;

    // Attack state
    private float activeTimer = 0f;
    private List<GameObject> alreadyHit = new List<GameObject>();

    // Config references
    private HitboxWeaponMechanicsConfig hitboxMechanics;
    private HitboxWeaponVisualConfig hitboxVisual;
    private HitboxWeaponSoundConfig hitboxSound;

    // Debug
    private Vector2 lastHitboxPosition;
    private Vector2 lastAttackDirection;

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);

        if (currentConfig == null) return;

        // Get specific configs from the main config
        hitboxMechanics = currentConfig.MechanicsConfig as HitboxWeaponMechanicsConfig;
        hitboxVisual = currentConfig.VisualConfig as HitboxWeaponVisualConfig;
        hitboxSound = currentConfig.SoundConfig as HitboxWeaponSoundConfig;

        if (hitboxMechanics == null || hitboxVisual == null)
        {
            Debug.LogError($"HitboxWeaponSystem requires HitboxWeapon configs");
            return;
        }

        // Create visual instance if prefab exists
        if (hitboxVisual.weaponPrefab != null && weaponOrigin != null)
        {
            weaponInstance = Instantiate(
                hitboxVisual.weaponPrefab,
                weaponOrigin.position,
                Quaternion.identity,
                weaponOrigin
            );

            Debug.Log($"Created weapon instance: {weaponInstance.name}");
        }

        // Set debug mode
        showDebugInfo = hitboxVisual.enableDebugVisualization;

        Debug.Log($"HitboxWeaponSystem initialized: {config.weaponName}");
        Debug.Log($"  Attack Duration: {hitboxMechanics.attackDuration}");
        Debug.Log($"  Cooldown: {hitboxMechanics.attackCooldown}");
        Debug.Log($"  Hitbox Size: {hitboxMechanics.hitboxSize}");
        Debug.Log($"  Debug Visualization: {hitboxVisual.enableDebugVisualization}");
    }

    protected override void TryAttack()
    {
        if (!CanAttack() || hitboxMechanics == null) return;

        isAttacking = true;
        IsAttacking = isAttacking;
        activeTimer = hitboxMechanics.attackDuration;
        attackCooldownTimer = hitboxMechanics.attackCooldown;
        alreadyHit.Clear();

        // Get attack direction
        lastAttackDirection = equipment.GetAttackDirection();

        // Play swing sound
        float velocity = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (soundManager != null)
        {
            soundManager.PlaySwingSound(velocity);
        }

        // Check initial hits
        CheckHitbox();

        // Raise attack event
        character.RaiseEvent("weapon_attack_started", currentConfig.weaponName);

        Debug.Log($"Hitbox weapon attack started (velocity: {velocity:F1})");
    }

    protected override void StopAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;
        IsAttacking = isAttacking;
        alreadyHit.Clear();

        // Raise attack ended event
        character.RaiseEvent("weapon_attack_ended", currentConfig.weaponName);

        Debug.Log("Hitbox weapon attack ended");
    }

    private void CheckHitbox()
    {
        if (character == null || hitboxMechanics == null) return;

        // Calculate hitbox position
        lastHitboxPosition = (Vector2)character.transform.position +
                             hitboxMechanics.hitboxOffset.x * lastAttackDirection +
                             new Vector2(0, hitboxMechanics.hitboxOffset.y);

        // Draw debug visualization
        if (showDebugInfo && hitboxVisual != null)
        {
            DrawDebugHitbox(lastHitboxPosition, lastAttackDirection);
        }

        // Check for hits
        var hitColliders = Physics2D.OverlapBoxAll(
            lastHitboxPosition,
            hitboxMechanics.hitboxSize,
            0f,
            hitboxMechanics.hitLayers
        );

        Debug.Log($"Hitbox check found {hitColliders.Length} colliders");

        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == character.gameObject || alreadyHit.Contains(hit.gameObject))
                continue;

            // Check max hits for non-multi-hit weapons
            if (!hitboxMechanics.multiHit && alreadyHit.Count >= hitboxMechanics.maxHitsPerAttack)
                break;

            // Calculate damage
            float damage = CalculateDamage(hitboxMechanics.baseDamage);

            // Apply damage
            ApplyDamage(hit.gameObject, damage, lastAttackDirection);

            alreadyHit.Add(hit.gameObject);

            // Play hit sound if available
            if (soundManager != null && hitboxSound != null)
            {
                soundManager.PlaySound("Hit");
            }

            Debug.Log($"Hit {hit.gameObject.name} for {damage:F1} damage");
        }
    }

    private void DrawDebugHitbox(Vector2 position, Vector2 direction)
    {
        if (hitboxVisual == null) return;

        // Draw the hitbox as a wire cube
        Vector3[] corners = new Vector3[4];
        Vector2 halfSize = hitboxMechanics.hitboxSize * 0.5f;

        corners[0] = position + new Vector2(-halfSize.x, -halfSize.y);
        corners[1] = position + new Vector2(halfSize.x, -halfSize.y);
        corners[2] = position + new Vector2(halfSize.x, halfSize.y);
        corners[3] = position + new Vector2(-halfSize.x, halfSize.y);

        // Draw the box
        for (int i = 0; i < 4; i++)
        {
            Debug.DrawLine(corners[i], corners[(i + 1) % 4], hitboxVisual.hitboxDebugColor, hitboxMechanics.attackDuration);
        }

        // Draw direction indicator
        Debug.DrawRay(position, direction * 0.5f, Color.red, hitboxMechanics.attackDuration);

        // Draw hitbox center
        Debug.DrawRay(position, Vector2.up * 0.1f, Color.green, hitboxMechanics.attackDuration);
        Debug.DrawRay(position, Vector2.right * 0.1f, Color.green, hitboxMechanics.attackDuration);
    }

    private void ApplyDamage(GameObject target, float damage, Vector2 direction)
    {
        if (target == null) return;

        // Get or add CharacterCondition
        var condition = target.GetComponent<CharacterCondition>();
        if (condition == null)
        {
            condition = target.GetComponentInParent<CharacterCondition>();
        }

        if (condition == null)
        {
            // For testing, add a CharacterCondition if none exists
            condition = target.AddComponent<CharacterCondition>();
            condition.maxHealth = 100f;
            condition.currentHealth = 100f;
            Debug.Log($"Added CharacterCondition to {target.name} for testing");
        }

        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position);

            // Apply knockback
            var targetRb = target.GetComponent<Rigidbody2D>();
            if (targetRb != null && hitboxMechanics.knockbackForce > 0)
            {
                targetRb.AddForce(direction * hitboxMechanics.knockbackForce, ForceMode2D.Impulse);
                Debug.Log($"Applied {hitboxMechanics.knockbackForce} knockback to {target.name}");
            }

            // Raise hit event
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "HitboxWeapon",
                configName = currentConfig.weaponName
            });
        }
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);

        if (isAttacking)
        {
            activeTimer -= deltaTime;
            if (activeTimer <= 0)
            {
                StopAttack();
            }
            else if (hitboxMechanics.multiHit && activeTimer > 0)
            {
                // Continue checking for multi-hit weapons
                CheckHitbox();
            }

            // Update debug visualization during attack
            if (showDebugInfo && hitboxVisual != null && activeTimer > 0)
            {
                UpdateDebugVisualization();
            }
        }
    }

    private void UpdateDebugVisualization()
    {
        // Update hitbox position based on current character position
        Vector2 currentDirection = equipment.GetAttackDirection();
        Vector2 currentPosition = (Vector2)character.transform.position +
                                 hitboxMechanics.hitboxOffset.x * currentDirection +
                                 new Vector2(0, hitboxMechanics.hitboxOffset.y);

        // Only redraw if position changed significantly
        if (Vector2.Distance(currentPosition, lastHitboxPosition) > 0.1f)
        {
            lastHitboxPosition = currentPosition;
            lastAttackDirection = currentDirection;
            DrawDebugHitbox(currentPosition, currentDirection);
        }
    }

    public override void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates if needed
    }

    protected override void CleanupManagers()
    {
        base.CleanupManagers();

        // Clean up visual instance
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
            weaponInstance = null;
        }

        // Clear config references
        hitboxMechanics = null;
        hitboxVisual = null;
        hitboxSound = null;
    }
}