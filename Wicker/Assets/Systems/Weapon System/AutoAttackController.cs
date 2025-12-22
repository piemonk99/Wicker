// AutoAttackController.cs
using UnityEngine;
using System.Collections.Generic;
using TMPro; // For text display

public class AutoAttackController : MonoBehaviour, IWeaponController
{
    private WeaponComponent owner;
    private AutoAttackWeaponConfig config;
    private CharacterCore character;
    private Transform characterTransform;
    private Rigidbody2D characterRb;
    private GrappleSystem grappleSystem;

    // State
    private float attackTimer = 0f;
    private List<GameObject> recentTargets = new List<GameObject>();

    public void Initialize(WeaponConfig baseConfig, CharacterCore character, WeaponComponent owner)
    {
        this.config = baseConfig as AutoAttackWeaponConfig;
        this.character = character;
        this.owner = owner;
        this.characterTransform = character.transform;
        this.characterRb = character.GetComponent<Rigidbody2D>();
        this.grappleSystem = character.GetComponent<GrappleSystem>();

        if (this.config == null)
        {
            Debug.LogError($"AutoAttackController requires AutoAttackWeaponConfig, got {baseConfig.GetType().Name}");
            return;
        }

        attackTimer = this.config.attackInterval; // Start ready to attack
    }

    public bool TryAttack()
    {
        // Auto-attack weapons might not have manual attack input
        // They could toggle on/off, or always be active
        return true;
    }

    public bool IsAttacking() => attackTimer <= 0 && ShouldAttack();

    public void Tick(float deltaTime)
    {
        if (config == null) return;

        // Update timer
        attackTimer -= deltaTime;

        // Check if we should attack
        if (attackTimer <= 0 && ShouldAttack())
        {
            PerformAutoAttack();
            attackTimer = config.attackInterval;
        }

        // Clean up recent targets list
        if (recentTargets.Count > 10)
        {
            recentTargets.RemoveAt(0);
        }
    }

    private bool ShouldAttack()
    {
        // Check if weapon is only active during grapple
        if (config.onlyActiveDuringGrapple && !owner.IsGrappling())
            return false;

        // Check velocity threshold
        if (characterRb != null && characterRb.linearVelocity.magnitude < config.velocityThreshold)
            return false;

        // Check if there are enemies in range
        return CheckForEnemiesInRange().Count > 0;
    }

    private List<Collider2D> CheckForEnemiesInRange()
    {
        var enemies = new List<Collider2D>();

        // Check for enemies in detection radius
        var hits = Physics2D.OverlapCircleAll(
            characterTransform.position,
            config.detectionRadius,
            config.enemyLayers
        );

        foreach (var hit in hits)
        {
            // Skip if recently hit
            if (recentTargets.Contains(hit.gameObject))
                continue;

            // Check if enemy is in grapple range if grappling
            if (owner.IsGrappling())
            {
                float distance = Vector2.Distance(characterTransform.position, hit.transform.position);
                if (distance > config.maxGrappleRange)
                    continue;
            }

            enemies.Add(hit);
        }

        return enemies;
    }

    private void PerformAutoAttack()
    {
        var enemies = CheckForEnemiesInRange();

        foreach (var enemy in enemies)
        {
            // Skip if recently hit
            if (recentTargets.Contains(enemy.gameObject))
                continue;

            // Calculate damage
            float damage = config.autoAttackDamage;

            // Apply grapple multiplier if grappling
            if (owner.IsGrappling())
            {
                damage *= config.grappleDamageMultiplier;
            }

            // Add velocity bonus
            damage = owner.CalculateDamage(damage);

            // Apply damage
            ApplyDamage(enemy.gameObject, damage);
            recentTargets.Add(enemy.gameObject);
        }
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        // Get or add HealthComponent
        var health = target.GetComponent<HealthComponent>();
        if (health == null)
        {
            // For testing, add a health component if none exists
            health = target.AddComponent<HealthComponent>();
        }

        if (health != null)
        {
            health.TakeDamage(damage, target.transform.position);

            // Raise hit event
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "AutoAttack",
                isGrappling = owner.IsGrappling()
            });
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates if needed
    }

    // Visualize detection radius in editor
    void OnDrawGizmosSelected()
    {
        if (config != null && characterTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(characterTransform.position, config.detectionRadius);

            if (config.onlyActiveDuringGrapple)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(characterTransform.position, config.maxGrappleRange);
            }
        }
    }
}