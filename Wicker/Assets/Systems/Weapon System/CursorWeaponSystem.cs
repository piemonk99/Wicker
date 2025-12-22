// CursorWeaponSystem.cs
using UnityEngine;

public class CursorWeaponSystem : WeaponSystem
{
    // Sword physics
    private Rigidbody2D swordRb;
    private Transform swordTransform;
    private Vector2 targetPosition;
    private Vector2 lastPosition;
    private float currentSwordSpeed;

    // State
    private bool isSwinging = false;

    // Config references
    private CursorWeaponMechanicsConfig mechanicsConfig;
    private CursorWeaponVisualConfig visualConfig;
    private CursorWeaponSoundConfig soundConfig;

    // Debug
    private Vector2 lastMousePosition;
    private float debugDisplayTime = 0.1f;

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);

        if (configManager == null) return;

        // Get specific configs
        mechanicsConfig = configManager.GetMechanicsConfig<CursorWeaponMechanicsConfig>();
        visualConfig = configManager.GetVisualConfig<CursorWeaponVisualConfig>();
        soundConfig = configManager.GetSoundConfig<CursorWeaponSoundConfig>();

        if (mechanicsConfig == null || visualConfig == null)
        {
            Debug.LogError($"CursorWeaponSystem requires appropriate configs");
            return;
        }

        // Setup sword GameObject
        swordTransform = transform;

        // Check if Rigidbody2D already exists
        swordRb = GetComponent<Rigidbody2D>();
        if (swordRb == null)
        {
            // Add Rigidbody2D if it doesn't exist
            swordRb = gameObject.AddComponent<Rigidbody2D>();
        }

        // Configure sword rigidbody
        swordRb.mass = mechanicsConfig.swordMass;
        swordRb.linearDamping = mechanicsConfig.swordDrag;
        swordRb.gravityScale = 0f;
        swordRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // Set initial position
        swordTransform.position = character.transform.position + (Vector3)Vector2.right * mechanicsConfig.orbitRadius;
        lastPosition = swordTransform.position;

        // Set debug mode
        showDebugInfo = visualConfig.enableDebugVisualization;

        Debug.Log($"CursorWeaponSystem initialized: {config.weaponName}");
        Debug.Log($"  Orbit Radius: {mechanicsConfig.orbitRadius}");
        Debug.Log($"  Sword Mass: {mechanicsConfig.swordMass}");
        Debug.Log($"  Debug Visualization: {visualConfig.enableDebugVisualization}");
    }

    protected override void TryAttack()
    {
        // For cursor weapon, attacking toggles swinging mode
        isSwinging = !isSwinging;

        if (isSwinging)
        {
            // Play swing sound
            float velocity = rb != null ? rb.linearVelocity.magnitude : 0f;
            if (soundManager != null)
            {
                soundManager.PlaySwingSound(velocity);
            }

            // Apply initial force if needed
            Vector2 swingDirection = (targetPosition - (Vector2)swordTransform.position).normalized;
            if (swordRb != null && swingDirection.magnitude > 0.1f)
            {
                swordRb.AddForce(swingDirection * mechanicsConfig.orbitSpeed, ForceMode2D.Impulse);
            }

            character.RaiseEvent("cursor_weapon_swing_started", currentConfig.weaponName);
            Debug.Log($"Cursor weapon: Started swinging");
        }
        else
        {
            character.RaiseEvent("cursor_weapon_swing_stopped", currentConfig.weaponName);
            Debug.Log("Cursor weapon: Stopped swinging");
        }
    }

    protected override void StopAttack()
    {
        isSwinging = false;
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);

        if (configManager == null || swordRb == null) return;

        // Update target position based on cursor
        UpdateTargetPosition(deltaTime);

        // Calculate current sword speed for damage
        if (swordTransform != null)
        {
            currentSwordSpeed = ((Vector2)swordTransform.position - lastPosition).magnitude / deltaTime;
            lastPosition = swordTransform.position;
        }

        // Apply control forces
        ApplyControlForces(deltaTime);

        // Check for collisions and apply damage
        if (isSwinging && currentSwordSpeed > mechanicsConfig.minimumDamageSpeed)
        {
            CheckCollisions();
        }

        // Play swoosh sound if moving fast
        if (soundManager != null && soundConfig != null)
        {
            soundManager.PlaySwooshSound(currentSwordSpeed);
        }

        // Draw debug info
        if (showDebugInfo)
        {
            DrawDebugInfo();
        }
    }

    private void UpdateTargetPosition(float deltaTime)
    {
        // For player: target is cursor position
        if (character.CompareTag("Player") && Camera.main != null)
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            lastMousePosition = mouseWorldPos;

            Vector2 toCursor = (Vector2)mouseWorldPos - (Vector2)character.transform.position;
            toCursor = Vector2.ClampMagnitude(toCursor, mechanicsConfig.orbitRadius);

            targetPosition = (Vector2)character.transform.position + toCursor;
        }
        else
        {
            // Enemy AI: orbit around character
            float angle = Time.time * mechanicsConfig.orbitSpeed;
            targetPosition = (Vector2)character.transform.position +
                           new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * mechanicsConfig.orbitRadius;
        }
    }

    private void ApplyControlForces(float deltaTime)
    {
        if (swordTransform == null || swordRb == null) return;

        if (!mechanicsConfig.usePhysicsBasedMovement)
        {
            // Simple lerp movement
            swordTransform.position = Vector2.Lerp(
                swordTransform.position,
                targetPosition,
                mechanicsConfig.cursorFollowSpeed * deltaTime
            );
        }
        else
        {
            // Physics-based movement with force
            Vector2 toTarget = targetPosition - (Vector2)swordTransform.position;
            float distance = toTarget.magnitude;

            if (distance > 0.1f)
            {
                // Apply force towards target
                Vector2 forceDirection = toTarget.normalized;
                float forceMagnitude = distance * mechanicsConfig.returnForce;

                // Limit maximum force
                forceMagnitude = Mathf.Min(forceMagnitude, mechanicsConfig.maxSwordSpeed);

                swordRb.AddForce(forceDirection * forceMagnitude);
            }

            // Limit maximum speed
            if (swordRb.linearVelocity.magnitude > mechanicsConfig.maxSwordSpeed)
            {
                swordRb.linearVelocity = swordRb.linearVelocity.normalized * mechanicsConfig.maxSwordSpeed;
            }
        }
    }

    private void CheckCollisions()
    {
        if (swordTransform == null) return;

        float checkRadius = 0.5f;
        var hitColliders = Physics2D.OverlapCircleAll(swordTransform.position, checkRadius);

        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == character.gameObject || hit.gameObject == gameObject)
                continue;

            // Calculate damage based on sword speed
            float speedDamage = (currentSwordSpeed - mechanicsConfig.minimumDamageSpeed) * mechanicsConfig.damagePerSpeedUnit;
            float totalDamage = Mathf.Max(mechanicsConfig.baseDamage, speedDamage);

            // Add velocity bonus from character
            totalDamage = CalculateDamage(totalDamage);

            // Apply damage
            ApplyDamage(hit.gameObject, totalDamage);

            Debug.Log($"Cursor weapon hit {hit.gameObject.name} for {totalDamage:F1} damage (speed: {currentSwordSpeed:F1})");
        }
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        var condition = target.GetComponent<CharacterCondition>();
        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position);

            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "CursorWeapon",
                configName = currentConfig.weaponName
            });

            // Apply knockback based on sword velocity
            var targetRb = target.GetComponent<Rigidbody2D>();
            if (targetRb != null && swordRb != null && swordRb.linearVelocity.magnitude > 0.1f)
            {
                targetRb.AddForce(swordRb.linearVelocity.normalized * currentSwordSpeed * 0.5f, ForceMode2D.Impulse);
            }
        }
    }

    private void DrawDebugInfo()
    {
        if (swordTransform == null || character.transform == null) return;

        // Draw orbit radius
        DrawCircle(character.transform.position, mechanicsConfig.orbitRadius, 32, visualConfig.orbitDebugColor);

        // Draw line to target
        Debug.DrawLine(swordTransform.position, targetPosition, Color.green, debugDisplayTime);

        // Draw line to character
        Debug.DrawLine(swordTransform.position, character.transform.position, Color.cyan, debugDisplayTime);

        // Draw sword velocity
        if (swordRb != null)
        {
            Debug.DrawRay(swordTransform.position, swordRb.linearVelocity.normalized * 0.5f, visualConfig.swordTrailColor, debugDisplayTime);
        }

        // Draw current speed indicator
        Debug.DrawRay(swordTransform.position, Vector2.up * (currentSwordSpeed * 0.1f),
                     currentSwordSpeed > mechanicsConfig.minimumDamageSpeed ? Color.green : Color.gray,
                     debugDisplayTime);
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

            Debug.DrawLine(prevPoint, nextPoint, color, debugDisplayTime);
            prevPoint = nextPoint;
        }
    }

    protected override void CleanupManagers()
    {
        base.CleanupManagers();

        mechanicsConfig = null;
        visualConfig = null;
        soundConfig = null;
    }
}