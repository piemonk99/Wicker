// AutoAttackWeaponSystem.cs
using UnityEngine;
using System.Collections.Generic;

public class AutoAttackWeaponSystem : WeaponSystem
{
    // State
    private float attackTimer = 0f;
    private bool isEnabled = true;

    // Recent hits tracking
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
    private float hitCooldown = 1f;

    // Config references
    private AutoAttackWeaponMechanicsConfig mechanicsConfig;
    private AutoAttackWeaponVisualConfig visualConfig;
    private AutoAttackWeaponSoundConfig soundConfig;

    // Debug
    private List<Vector3> lastDetectionPositions = new List<Vector3>();
    private int totalAttacks = 0;
    private int totalHits = 0;
    private float debugLineDuration = 0.1f;

    // Cache
    private int enemyLayerMask;
    private string enemyTag = "Enemy";

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);

        if (configManager == null) return;

        // Get specific configs
        mechanicsConfig = configManager.GetMechanicsConfig<AutoAttackWeaponMechanicsConfig>();
        visualConfig = configManager.GetVisualConfig<AutoAttackWeaponVisualConfig>();
        soundConfig = configManager.GetSoundConfig<AutoAttackWeaponSoundConfig>();

        if (mechanicsConfig == null || visualConfig == null)
        {
            Debug.LogError($"AutoAttackWeaponSystem requires appropriate configs");
            return;
        }

        // Build layer mask from config
        enemyLayerMask = mechanicsConfig.enemyLayers.value;

        // Use attack interval as cooldown
        hitCooldown = Mathf.Max(0.5f, mechanicsConfig.attackInterval * 2f);

        attackTimer = mechanicsConfig.attackInterval;

        // Set debug mode
        showDebugInfo = visualConfig.enableDebugVisualization;

        Debug.Log($"AutoAttackWeaponSystem initialized: {config.weaponName}");
        Debug.Log($"  Detection Radius: {mechanicsConfig.detectionRadius}");
        Debug.Log($"  Attack Interval: {mechanicsConfig.attackInterval}");
        Debug.Log($"  Enemy Layers: {mechanicsConfig.enemyLayers.value}");
        Debug.Log($"  Debug Visualization: {visualConfig.enableDebugVisualization}");
    }

    protected override void TryAttack()
    {
        // Toggle auto-attack on/off
        isEnabled = !isEnabled;

        if (isEnabled)
        {
            Debug.Log("Auto-attack weapon ENABLED");
            attackTimer = 0f; // Attack immediately
        }
        else
        {
            Debug.Log("Auto-attack weapon DISABLED");
        }
    }

    protected override void StopAttack()
    {
        // Auto-attack doesn't have a manual stop, only toggle
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);

        if (mechanicsConfig == null || !isEnabled) return;

        // Update timers
        attackTimer -= deltaTime;
        CleanupRecentHits(deltaTime);

        // Check if we should attack
        if (attackTimer <= 0 && ShouldAttack())
        {
            PerformAutoAttack();
            attackTimer = mechanicsConfig.attackInterval;
        }

        // Draw debug info
        if (showDebugInfo)
        {
            DrawDebugInfo();
        }
    }

    private void CleanupRecentHits(float deltaTime)
    {
        float currentTime = Time.time;

        for (int i = recentHits.Count - 1; i >= 0; i--)
        {
            if (recentHits[i].enemy == null)
            {
                recentHits.RemoveAt(i);
                continue;
            }

            if (currentTime - recentHits[i].timestamp > hitCooldown)
            {
                recentHits.RemoveAt(i);
            }
        }
    }

    private bool ShouldAttack()
    {
        // Check if weapon is only active during grapple
        if (mechanicsConfig.onlyActiveDuringGrapple && !equipment.IsGrappling())
        {
            return false;
        }

        // Check velocity threshold
        if (rb != null && rb.linearVelocity.magnitude < mechanicsConfig.velocityThreshold)
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

        if (character == null || character.transform == null) return validEnemies;

        // Find all colliders in detection radius
        var allColliders = Physics2D.OverlapCircleAll(
            character.transform.position,
            mechanicsConfig.detectionRadius,
            enemyLayerMask
        );

        foreach (var collider in allColliders)
        {
            GameObject obj = collider.gameObject;

            // Skip self
            if (obj == character.gameObject) continue;

            // Check if it has the Enemy tag
            if (!obj.CompareTag(enemyTag)) continue;

            // Check if recently hit
            if (IsRecentlyHit(obj)) continue;

            // Check if enemy is in grapple range if grappling
            if (equipment.IsGrappling())
            {
                float distance = Vector2.Distance(character.transform.position, obj.transform.position);
                if (distance > mechanicsConfig.maxGrappleRange)
                    continue;
            }

            // Check if this is a valid enemy root
            if (IsValidEnemyRoot(obj))
            {
                validEnemies.Add(obj);
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

    private bool IsValidEnemyRoot(GameObject obj)
    {
        bool hasCharacterCore = obj.GetComponent<CharacterCore>() != null;
        bool hasCharacterCondition = obj.GetComponent<CharacterCondition>() != null;
        bool isChildOfEnemy = false;

        if (obj.transform.parent != null)
        {
            isChildOfEnemy = obj.transform.parent.CompareTag(enemyTag);
        }

        if (hasCharacterCore || hasCharacterCondition)
        {
            return true;
        }

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

        // Play swing sound based on current velocity
        float currentVelocity = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (soundManager != null)
        {
            soundManager.PlaySwingSound(currentVelocity);
        }

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            // Calculate damage
            float damage = mechanicsConfig.autoAttackDamage;

            // Apply grapple multiplier if grappling
            if (equipment.IsGrappling())
            {
                damage *= mechanicsConfig.grappleDamageMultiplier;
            }

            // Add velocity bonus
            damage = CalculateDamage(damage);

            // Apply damage
            ApplyDamage(enemy, damage);

            // Add to recent hits with timestamp
            recentHits.Add(new RecentHit(enemy, Time.time));

            totalHits++;

            Debug.Log($"  Hit {enemy.name} for {damage:F1} damage");
        }
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        if (target == null) return;

        var condition = target.GetComponent<CharacterCondition>();
        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position);

            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "AutoAttack",
                configName = currentConfig.weaponName,
                isGrappling = equipment.IsGrappling()
            });
        }
    }

    private void DrawDebugInfo()
    {
        if (character == null || character.transform == null) return;

        // Draw detection radius
        DrawCircle(character.transform.position, mechanicsConfig.detectionRadius, 24, visualConfig.detectionRadiusColor);

        // Draw grapple range if grappling
        if (equipment.IsGrappling())
        {
            DrawCircle(character.transform.position, mechanicsConfig.maxGrappleRange, 24, visualConfig.grappleRangeColor);
        }

        // Draw recent enemy positions
        foreach (var pos in lastDetectionPositions)
        {
            Debug.DrawRay(pos, Vector2.up * 0.2f, Color.green, debugLineDuration);
            Debug.DrawRay(pos, Vector2.right * 0.2f, Color.green, debugLineDuration);
            Debug.DrawLine(character.transform.position, pos, new Color(0, 1, 0, 0.5f), debugLineDuration);
        }

        // Draw status indicator
        Color statusColor = isEnabled ?
            (attackTimer <= 0 ? Color.green : new Color(0.5f, 1f, 0.5f)) :
            Color.red;

        Debug.DrawRay(character.transform.position + Vector3.up * 0.5f,
                     Vector2.right * 0.2f,
                     statusColor,
                     debugLineDuration);
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

    public string GetDebugInfo()
    {
        if (mechanicsConfig == null) return "No config";

        return $"Auto-Attack Weapon:\n" +
               $"Enabled: {isEnabled}\n" +
               $"Status: {(attackTimer <= 0 ? "READY" : $"Charging ({attackTimer:F2}s)")}\n" +
               $"Velocity: {rb?.linearVelocity.magnitude:F1}/{mechanicsConfig.velocityThreshold}\n" +
               $"Grappling: {equipment.IsGrappling()}\n" +
               $"Total Attacks: {totalAttacks}\n" +
               $"Total Hits: {totalHits}\n" +
               $"Recent Hits: {recentHits.Count}";
    }

    protected override void CleanupManagers()
    {
        base.CleanupManagers();

        mechanicsConfig = null;
        visualConfig = null;
        soundConfig = null;
        recentHits.Clear();
        lastDetectionPositions.Clear();
    }
}