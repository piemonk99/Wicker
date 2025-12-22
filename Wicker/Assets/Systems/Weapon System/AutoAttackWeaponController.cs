// AutoAttackWeaponController.cs
using UnityEngine;
using System.Collections.Generic;

public class AutoAttackWeaponController : MonoBehaviour, IWeaponController
{
    private CharacterEquipment owner;
    private AutoAttackWeaponConfig config;
    private CharacterCore character;
    private Transform characterTransform;
    private Rigidbody2D characterRb;
    private GrappleSystem grappleSystem;

    // State
    private float attackTimer = 0f;
    private bool isEnabled = true;

    // Recent hits tracking with timestamps
    private class RecentHit
    {
        public GameObject enemy;
        public float timestamp;

        public RecentHit(GameObject enemy, float timestamp)
        {
            this.enemy = enemy;
            this.timestamp = timestamp;
        }
    }

    private List<RecentHit> recentHits = new List<RecentHit>();
    private float hitCooldown = 1f; // Cooldown before hitting same enemy again

    // Debug
    private bool showDebugInfo = false;
    private List<Vector3> lastDetectionPositions = new List<Vector3>();
    private int totalAttacks = 0;
    private int totalHits = 0;
    private float debugLineDuration = 0.002f;

    // Cache
    private int enemyLayerMask;
    private string enemyTag = "Enemy";

    private WeaponSoundManager soundManager;

    public void Initialize(WeaponConfig baseConfig, CharacterCore character, CharacterEquipment owner)
    {
        this.config = baseConfig as AutoAttackWeaponConfig;
        this.character = character;
        this.owner = owner;
        this.characterTransform = character.transform;
        this.characterRb = character.GetComponent<Rigidbody2D>();
        this.grappleSystem = character.GetComponent<GrappleSystem>();

        if (this.config == null)
        {
            Debug.LogError($"AutoAttackWeaponController requires AutoAttackWeaponConfig, got {baseConfig.GetType().Name}");
            return;
        }

        // Initialize sound manager if sound settings exist
        if (config.soundConfig.weaponSoundSet != null)
        {
            soundManager = gameObject.AddComponent<WeaponSoundManager>();
            soundManager.InitializeWithSoundNode(
                config.soundConfig.weaponSoundSet,
                config.soundConfig.swingVolume,
                config.soundConfig.critVolume,
                config.soundConfig.critVelocityThreshold
            );
        }
        else
        {
            Debug.LogWarning($"Auto-attack weapon {config.weaponName} has no sound set configured");
        }

        // Build layer mask from config
        enemyLayerMask = config.enemyLayers.value;

        // Use attack interval as cooldown or a fixed value
        hitCooldown = Mathf.Max(0.5f, config.attackInterval * 2f);

        attackTimer = this.config.attackInterval;

        // Enable debug if configured
        showDebugInfo = config.enableDebugVisualization;

        Debug.Log($"Auto-attack weapon initialized:");
        Debug.Log($"  Detection radius: {config.detectionRadius}");
        Debug.Log($"  Attack interval: {config.attackInterval}");
        Debug.Log($"  Hit cooldown: {hitCooldown}");
        Debug.Log($"  Sound Manager: {(soundManager.IsReady() ? "Ready" : "Not Ready")}");
    }

    public bool TryAttack()
    {
        // Toggle auto-attack on/off
        isEnabled = !isEnabled;

        if (isEnabled)
        {
            Debug.Log("Auto-attack weapon ENABLED");
            attackTimer = 0f;
        }
        else
        {
            Debug.Log("Auto-attack weapon DISABLED");
        }

        return true;
    }

    public bool IsAttacking() => isEnabled && attackTimer <= 0 && ShouldAttack();

    public void Tick(float deltaTime)
    {
        if (config == null || !isEnabled) return;

        // Update timers
        attackTimer -= deltaTime;
        CleanupRecentHits(deltaTime);

        // Check if we should attack
        if (attackTimer <= 0 && ShouldAttack())
        {
            PerformAutoAttack();
            attackTimer = config.attackInterval;
        }

        // Draw debug info
        if (showDebugInfo)
        {
            DrawDebugInfo();
        }
    }

    private void CleanupRecentHits(float deltaTime)
    {
        // Update timestamps and remove expired hits
        float currentTime = Time.time;

        for (int i = recentHits.Count - 1; i >= 0; i--)
        {
            // Remove null enemies
            if (recentHits[i].enemy == null)
            {
                recentHits.RemoveAt(i);
                continue;
            }

            // Remove expired hits (older than cooldown)
            if (currentTime - recentHits[i].timestamp > hitCooldown)
            {
                if (showDebugInfo)
                    Debug.Log($"Hit cooldown expired for {recentHits[i].enemy.name}");
                recentHits.RemoveAt(i);
            }
        }
    }

    private bool ShouldAttack()
    {
        // Check if weapon is only active during grapple
        if (config.onlyActiveDuringGrapple && !owner.IsGrappling())
        {
            return false;
        }

        // Check velocity threshold
        if (characterRb != null && characterRb.linearVelocity.magnitude < config.velocityThreshold)
        {
            return false;
        }

        // Check if there are valid enemies in range
        return HasValidEnemiesInRange();
    }

    private bool HasValidEnemiesInRange()
    {
        var enemies = FindValidEnemiesInRange();
        return enemies.Count > 0;
    }

    private List<GameObject> FindValidEnemiesInRange()
    {
        var validEnemies = new List<GameObject>();

        if (characterTransform == null) return validEnemies;

        // Find all colliders in detection radius
        Collider2D[] allColliders = Physics2D.OverlapCircleAll(
            characterTransform.position,
            config.detectionRadius,
            enemyLayerMask
        );

        foreach (var collider in allColliders)
        {
            GameObject obj = collider.gameObject;

            // Skip self
            if (obj == character.gameObject) continue;

            // Check if it has the Enemy tag
            if (!obj.CompareTag(enemyTag))
            {
                continue;
            }

            // Check if recently hit (with cooldown)
            if (IsRecentlyHit(obj))
            {
                continue;
            }

            // Check if enemy is in grapple range if grappling
            if (owner.IsGrappling())
            {
                float distance = Vector2.Distance(characterTransform.position, obj.transform.position);
                if (distance > config.maxGrappleRange)
                {
                    continue;
                }
            }

            // Check if this is a valid enemy root
            if (IsValidEnemyRoot(obj))
            {
                validEnemies.Add(obj);
                if (showDebugInfo)
                    Debug.Log($"Valid enemy found: {obj.name}");
            }
        }

        // Store for debug visualization
        if (showDebugInfo)
        {
            lastDetectionPositions.Clear();
            foreach (var enemy in validEnemies)
            {
                if (enemy != null)
                {
                    lastDetectionPositions.Add(enemy.transform.position);
                }
            }
        }

        return validEnemies;
    }

    private bool IsRecentlyHit(GameObject enemy)
    {
        foreach (var hit in recentHits)
        {
            if (hit.enemy == enemy)
            {
                float timeSinceHit = Time.time - hit.timestamp;
                return timeSinceHit <= hitCooldown;
            }
        }
        return false;
    }

    private float GetLastHitTime(GameObject enemy)
    {
        foreach (var hit in recentHits)
        {
            if (hit.enemy == enemy)
            {
                return hit.timestamp;
            }
        }
        return -1f;
    }

    private bool IsValidEnemyRoot(GameObject obj)
    {
        // Check if this is likely the root enemy object
        bool hasCharacterCore = obj.GetComponent<CharacterCore>() != null;
        bool hasCharacterCondition = obj.GetComponent<CharacterCondition>() != null;
        bool isChildOfEnemy = false;

        // Check if parent is also an enemy
        if (obj.transform.parent != null)
        {
            isChildOfEnemy = obj.transform.parent.CompareTag(enemyTag);
        }

        // Prefer root objects that have core components
        if (hasCharacterCore || hasCharacterCondition)
        {
            return true;
        }

        // If it doesn't have core components but isn't a child of another enemy, it might still be valid
        return !isChildOfEnemy;
    }

    private void PerformAutoAttack()
    {
        var enemies = FindValidEnemiesInRange();
        totalAttacks++;

        if (enemies.Count == 0)
        {
            if (showDebugInfo)
                Debug.Log($"Auto-attack #{totalAttacks}: No valid enemies in range");
            return;
        }

        Debug.Log($"Auto-attack #{totalAttacks}: Attacking {enemies.Count} enemies");

        float currentVelocity = characterRb != null ? characterRb.linearVelocity.magnitude : 0f;

        // Play swing sound if sound manager is ready
        if (soundManager != null && soundManager.IsReady())
        {
            soundManager.PlaySwingSound(currentVelocity);
        }

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

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
            ApplyDamage(enemy, damage);

            // Add to recent hits with timestamp
            recentHits.Add(new RecentHit(enemy, Time.time));

            totalHits++;

            if (showDebugInfo)
                Debug.Log($"  Hit {enemy.name} for {damage:F1} damage (velocity: {currentVelocity:F1})");
        }
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        if (target == null) return;

        // Try to find CharacterCondition
        var condition = target.GetComponent<CharacterCondition>();
        if (condition == null)
        {
            condition = target.GetComponentInParent<CharacterCondition>();
        }

        if (condition == null)
        {
            condition = target.AddComponent<CharacterCondition>();
            condition.maxHealth = 100f;
            condition.currentHealth = 100f;
        }

        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position);

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

    private void DrawDebugInfo()
    {
        if (characterTransform == null) return;

        // Draw detection radius
        DrawCircle(characterTransform.position, config.detectionRadius, 24, Color.yellow);

        // Draw grapple range if grappling
        if (owner.IsGrappling())
        {
            DrawCircle(characterTransform.position, config.maxGrappleRange, 24, Color.red);
        }

        // Draw recent enemy positions
        foreach (var pos in lastDetectionPositions)
        {
            Debug.DrawRay(pos, Vector2.up * 0.2f, Color.green, debugLineDuration);
            Debug.DrawRay(pos, Vector2.right * 0.2f, Color.green, debugLineDuration);
            Debug.DrawLine(characterTransform.position, pos, new Color(0, 1, 0, 0.5f), debugLineDuration);
        }

        // Draw velocity indicator
        if (characterRb != null && characterRb.linearVelocity.magnitude > 0.1f)
        {
            Debug.DrawRay(characterTransform.position,
                         characterRb.linearVelocity.normalized * 0.5f,
                         Color.blue,
                         debugLineDuration);
        }

        // Draw status indicator
        Color statusColor = isEnabled ?
            (attackTimer <= 0 ? Color.green : new Color(0.5f, 1f, 0.5f)) :
            Color.red;

        Debug.DrawRay(characterTransform.position + Vector3.up * 0.5f,
                     Vector2.right * 0.2f,
                     statusColor,
                     debugLineDuration);

        // Draw cooldown indicator
        if (recentHits.Count > 0)
        {
            float yOffset = 0.6f;
            foreach (var hit in recentHits)
            {
                if (hit.enemy != null)
                {
                    float timeSinceHit = Time.time - hit.timestamp;
                    float cooldownProgress = Mathf.Clamp01(timeSinceHit / hitCooldown);

                    Debug.DrawLine(
                        characterTransform.position + Vector3.up * yOffset,
                        characterTransform.position + Vector3.up * yOffset + Vector3.right * cooldownProgress * 0.5f,
                        Color.Lerp(Color.red, Color.green, cooldownProgress),
                        debugLineDuration
                    );
                    yOffset += 0.1f;
                }
            }
        }
    }

    private void DrawCircle(Vector2 center, float radius, int segments, Color color)
    {
        float angleStep = 360f / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i;
            Vector2 nextPoint = center + new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            Debug.DrawLine(prevPoint, nextPoint, color, debugLineDuration);
            prevPoint = nextPoint;
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates if needed
    }

    public string GetDebugInfo()
    {
        if (config == null) return "No config";

        return $"Auto-Attack Weapon:\n" +
               $"Enabled: {isEnabled}\n" +
               $"Status: {(attackTimer <= 0 ? "READY" : $"Charging ({attackTimer:F2}s)")}\n" +
               $"Velocity: {characterRb?.linearVelocity.magnitude:F1}/{config.velocityThreshold}\n" +
               $"Grappling: {owner.IsGrappling()}\n" +
               $"Total Attacks: {totalAttacks}\n" +
               $"Total Hits: {totalHits}\n" +
               $"Recent Hits: {recentHits.Count}\n" +
               $"Hit Cooldown: {hitCooldown}s";
    }

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