using UnityEngine;
using System.Collections.Generic;

public class CharacterAutoAttackWeapon : CharacterWeapon
{
    // Component references
    private CharacterGrapple characterGrapple;

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
    private float enemyRehitCooldown = 1f;

    // Config references
    private AutoAttackWeaponMechanicsConfig autoAttackMechanics;
    private AutoAttackWeaponVisualConfig autoAttackVisual;
    private AutoAttackWeaponSoundConfig autoAttackSound;

    // Debug
    private List<Vector3> lastDetectionPositions = new List<Vector3>();
    private int totalAttacks = 0;
    private int totalHits = 0;
    private float debugLineDuration = 0f;

    // Cache
    private int enemyLayerMask;
    private string enemyTag;

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);

        if (currentConfig == null) return;

        // Get grapple system if it exists
        characterGrapple = character.GetComponent<CharacterGrapple>();

        // Get specific configs from the main config
        autoAttackMechanics = currentConfig.MechanicsConfig as AutoAttackWeaponMechanicsConfig;
        autoAttackVisual = currentConfig.VisualConfig as AutoAttackWeaponVisualConfig;
        autoAttackSound = currentConfig.SoundConfig as AutoAttackWeaponSoundConfig;

        if (autoAttackMechanics == null || autoAttackVisual == null)
        {
            Debug.LogError($"CharacterAutoAttackWeapon requires AutoAttackWeapon configs");
            return;
        }

        // Build layer mask and tag mask from config
        enemyLayerMask = autoAttackMechanics.enemyLayers.value;
        enemyTag = autoAttackMechanics.enemyTag;

        // Use attack interval as cooldown
        enemyRehitCooldown = autoAttackMechanics.enemyRehitCooldown;

        attackTimer = autoAttackMechanics.attackInterval;

        // Set debug mode
        showDebugInfo = autoAttackVisual.enableDebugVisualization;
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

        if (autoAttackMechanics == null || !isEnabled) return;

        // Update timers
        attackTimer -= deltaTime;
        CleanupRecentHits(deltaTime);

        // Check if we should attack
        if (attackTimer <= 0 && ShouldAttack())
        {
            PerformAutoAttack();
            attackTimer = autoAttackMechanics.attackInterval;
        }

        // Draw debug info
        if (showDebugInfo && autoAttackVisual != null)
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

            if (currentTime - recentHits[i].timestamp > enemyRehitCooldown)
            {
                recentHits.RemoveAt(i);
            }
        }
    }

    private bool ShouldAttack()
    {
        if (autoAttackMechanics == null) return false;

        // Check if weapon is only active during grapple
        if (characterGrapple != null && autoAttackMechanics.onlyActiveDuringGrapple && !characterGrapple.IsGrappling())
        {
            return false;
        }

        // Check velocity threshold
        if (rb != null && rb.linearVelocity.magnitude < autoAttackMechanics.velocityThreshold)
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

        if (character == null || character.transform == null || autoAttackMechanics == null)
            return validEnemies;

        // Determine the actual detection radius based on grapple state
        float currentDetectionRadius = autoAttackMechanics.detectionRadius;

        if (characterGrapple != null && characterGrapple.IsGrappling())
        {
            // Use the extended range when grappling
            currentDetectionRadius = autoAttackMechanics.maxGrappleRange;
        }

        // Find all colliders in the appropriate detection radius
        var allColliders = Physics2D.OverlapCircleAll(
            character.transform.position,
            currentDetectionRadius,  // Use dynamic radius
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

            // Check if this is a valid enemy root
            if (IsValidEnemyRoot(obj))
            {
                validEnemies.Add(obj);
            }
        }

        // Store for debug visualization
        if (showDebugInfo && autoAttackVisual != null)
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
                return timeSinceHit <= enemyRehitCooldown;
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
        bool isCritical = currentVelocity >= autoAttackMechanics.maxVelocityForMultiplier;
        if (soundManager != null)
        {
            soundManager.PlaySwingSound(currentVelocity, isCritical);
        }

        foreach (var enemy in enemies)
        {
            if (enemy == null || autoAttackMechanics == null) continue;

            // Calculate damage
            float damage = autoAttackMechanics.baseDamage;

            // Apply grapple multiplier if grappling
            if (characterGrapple != null && characterGrapple.IsGrappling())
            {
                damage *= autoAttackMechanics.grappleDamageMultiplier;
            }

            // Add velocity bonus
            damage = CalculateDamage(damage);

            // Round down
            damage = Mathf.Floor(damage);

            // Apply damage
            ApplyDamage(enemy, damage, isCritical);

            // Add to recent hits with timestamp
            recentHits.Add(new RecentHit(enemy, Time.time));

            totalHits++;

            Debug.Log($"  Hit {enemy.name} for {damage:F1} damage!");
        }
    }

    private void ApplyDamage(GameObject target, float damage, bool isCritical)
    {
        if (target == null) return;

        var condition = target.GetComponent<CharacterCondition>();
        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position, isCritical);

            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "AutoAttack",
                configName = currentConfig.weaponName,
                isGrappling = characterGrapple != null && characterGrapple.IsGrappling()
            });
        }
    }

    private void DrawDebugInfo()
    {
        if (character == null || character.transform == null || autoAttackVisual == null) return;

        // Determine which radius to draw
        float drawRadius = autoAttackMechanics.detectionRadius;
        Color drawColor = autoAttackVisual.detectionRadiusColor;

        if (characterGrapple != null && characterGrapple.IsGrappling() && autoAttackMechanics != null)
        {
            drawRadius = autoAttackMechanics.maxGrappleRange;
            drawColor = autoAttackVisual.grappleRangeColor;
        }

        // Draw detection radius
        DrawCircle(character.transform.position, drawRadius, 24, drawColor);

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
        if (autoAttackMechanics == null) return "No config";

        float currentRadius = autoAttackMechanics.detectionRadius;
        if (characterGrapple != null && characterGrapple.IsGrappling())
        {
            currentRadius = autoAttackMechanics.maxGrappleRange;
        }

        return $"Auto-Attack Weapon:\n" +
               $"Enabled: {isEnabled}\n" +
               $"Status: {(attackTimer <= 0 ? "READY" : $"Charging ({attackTimer:F2}s)")}\n" +
               $"Detection Radius: {currentRadius:F1}\n" +
               $"Velocity: {rb?.linearVelocity.magnitude:F1}/{autoAttackMechanics.velocityThreshold}\n" +
               $"Grappling: {characterGrapple != null && characterGrapple.IsGrappling()}\n" +
               $"Total Attacks: {totalAttacks}\n" +
               $"Total Hits: {totalHits}\n" +
               $"Recent Hits: {recentHits.Count}";
    }

    protected override void CleanupManagers()
    {
        base.CleanupManagers();

        autoAttackMechanics = null;
        autoAttackVisual = null;
        autoAttackSound = null;
        recentHits.Clear();
        lastDetectionPositions.Clear();
    }
}