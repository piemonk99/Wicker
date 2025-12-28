using UnityEngine;
using UnityEngine.InputSystem;

public class CursorWeaponSystem : WeaponSystem
{
    // Sword references
    private Transform swordTransform;
    private GameObject swordInstance;
    private SpriteRenderer swordSpriteRenderer;

    // Orbit state
    private Vector2 targetPosition;  // Where cursor wants sword to be
    private Vector2 currentOrbitPosition;  // Where sword actually is on orbit circle
    private float currentSwordSpeed;
    private Vector2 lastPosition;

    // Attack state
    private bool isActive = false;  // Sword is visible and active
    private bool isSwinging = false;  // Sword can deal damage

    // Cached config references
    private CursorWeaponMechanicsConfig cursorMechanics;
    private CursorWeaponVisualConfig cursorVisual;

    // Debug
    private Vector2 debugLastMousePosition;
    private float debugDisplayTime = 0.1f;

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);

        if (currentConfig == null) return;

        // Get specific configs
        cursorMechanics = currentConfig.MechanicsConfig as CursorWeaponMechanicsConfig;
        cursorVisual = currentConfig.VisualConfig as CursorWeaponVisualConfig;

        if (cursorMechanics == null || cursorVisual == null)
        {
            Debug.LogError("CursorWeaponSystem requires CursorWeapon configs");
            return;
        }

        // Spawn sword prefab if one is specified
        if (cursorVisual.weaponPrefab != null)
        {
            Debug.Log("Spawning sword");

            swordInstance = Instantiate(cursorVisual.weaponPrefab, transform);
            swordTransform = swordInstance.transform;
            swordSpriteRenderer = swordInstance.GetComponent<SpriteRenderer>();

            if (swordSpriteRenderer == null)
            {
                swordSpriteRenderer = swordInstance.GetComponentInChildren<SpriteRenderer>();
            }

            // Start with sword deactivated
            SetSwordActive(false);
        }
        else
        {
            // If no prefab, use this transform
            swordTransform = transform;
            Debug.LogWarning("No sword prefab assigned in CursorWeaponVisualConfig");
        }

        // Initialize orbit position
        currentOrbitPosition = GetInitialOrbitPosition();
        if (swordTransform != null)
        {
            swordTransform.position = (Vector2)character.transform.position + currentOrbitPosition;
        }

        lastPosition = swordTransform.position;

        // Set debug mode
        showDebugInfo = cursorVisual.enableDebugVisualization;

        // Activate the sword
        SetSwordActive(true);
        isActive = true;
    }

    private Vector2 GetInitialOrbitPosition()
    {
        // Start sword to the right of the player
        return Vector2.right * cursorMechanics.orbitRadius;
    }

    private void SetSwordActive(bool active)
    {
        if (swordInstance != null)
        {
            swordInstance.SetActive(active);
        }

        if (swordSpriteRenderer != null)
        {
            swordSpriteRenderer.enabled = active;
        }
    }

    protected override void TryAttack()
    {
        if (cursorMechanics == null || !isActive) return;

        // Toggle swinging state
        isSwinging = !isSwinging;

        if (isSwinging)
        {
            // Play swing sound
            if (soundManager != null)
            {
                soundManager.PlaySwingSound(0f);
            }

            character.RaiseEvent("cursor_weapon_swing_started", currentConfig.weaponName);
            Debug.Log("Cursor weapon started swinging");
        }
        else
        {
            character.RaiseEvent("cursor_weapon_swing_stopped", currentConfig.weaponName);
            Debug.Log("Cursor weapon stopped swinging");
        }
    }

    protected override void StopAttack()
    {
        isSwinging = false;
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);

        if (cursorMechanics == null || !isActive || swordTransform == null) return;

        // Update target position based on cursor
        UpdateTargetPosition(deltaTime);

        // Update sword position on orbit circle
        UpdateSwordPosition(deltaTime);

        // Calculate current sword speed for damage
        currentSwordSpeed = CalculateSwordSpeed(deltaTime);

        // Rotate sword to face outward
        RotateSword();

        // Check for collisions and apply damage
        if (isSwinging && currentSwordSpeed > cursorMechanics.minimumDamageSpeed)
        {
            CheckCollisions();
        }

        // Play swoosh sound if moving fast
        if (soundManager != null)
        {
            // You might need to add a PlaySwooshSound method to WeaponSoundManager
            // or implement it here
        }

        // Draw debug info
        if (showDebugInfo && cursorVisual != null)
        {
            DrawDebugInfo();
        }
    }

    public override void PhysicsTick(float fixedDeltaTime)
    {
        base.PhysicsTick(fixedDeltaTime);

        // Physics updates could go here for the physics-based version
    }

    private void UpdateTargetPosition(float deltaTime)
    {
        if (cursorMechanics == null || character == null) return;

        Vector2 mouseWorldPos = GetMouseWorldPosition();
        debugLastMousePosition = mouseWorldPos;

        // Calculate direction from player to cursor
        Vector2 toCursor = mouseWorldPos - (Vector2)character.transform.position;

        // Clamp to orbit radius to get target position on circle
        toCursor = Vector2.ClampMagnitude(toCursor, cursorMechanics.orbitRadius);

        // Target position is on the orbit circle
        targetPosition = (Vector2)character.transform.position + toCursor;
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (Camera.main == null) return Vector2.zero;

        // Using Input System
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreenPos.x, mouseScreenPos.y, Camera.main.nearClipPlane)
        );
        mouseWorldPos.z = 0;
        return mouseWorldPos;
    }

    private void UpdateSwordPosition(float deltaTime)
    {
        if (character == null || swordTransform == null) return;

        // Get direction from player to target (cursor position clamped to circle)
        Vector2 playerToTarget = targetPosition - (Vector2)character.transform.position;

        // Calculate the desired orbit position (normalized direction * radius)
        Vector2 desiredOrbitPosition = playerToTarget.normalized * cursorMechanics.orbitRadius;

        // Smoothly move current orbit position toward desired position
        if (!cursorMechanics.usePhysicsBasedMovement)
        {
            // Non-physics version: smooth interpolation
            currentOrbitPosition = Vector2.Lerp(
                currentOrbitPosition,
                desiredOrbitPosition,
                cursorMechanics.cursorFollowSpeed * deltaTime
            );

            // Apply max angle constraint if needed
            float angleDiff = Vector2.SignedAngle(currentOrbitPosition.normalized, desiredOrbitPosition.normalized);
            float maxAngleDelta = cursorMechanics.maxAnglePerSecond * deltaTime;

            if (Mathf.Abs(angleDiff) > maxAngleDelta)
            {
                float targetAngle = Mathf.Atan2(desiredOrbitPosition.y, desiredOrbitPosition.x) * Mathf.Rad2Deg;
                float currentAngle = Mathf.Atan2(currentOrbitPosition.y, currentOrbitPosition.x) * Mathf.Rad2Deg;

                // Move toward target angle with max speed
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxAngleDelta);
                currentOrbitPosition = new Vector2(
                    Mathf.Cos(newAngle * Mathf.Deg2Rad),
                    Mathf.Sin(newAngle * Mathf.Deg2Rad)
                ) * cursorMechanics.orbitRadius;
            }
        }
        else
        {
            // For now, just use lerp. Physics version will be implemented later
            currentOrbitPosition = desiredOrbitPosition;
        }

        // Set sword position (player position + orbit offset)
        swordTransform.position = (Vector2)character.transform.position + currentOrbitPosition;
    }

    private float CalculateSwordSpeed(float deltaTime)
    {
        if (swordTransform == null || deltaTime <= 0) return 0f;

        Vector2 currentPos = swordTransform.position;
        float speed = ((Vector2)currentPos - lastPosition).magnitude / deltaTime;
        lastPosition = currentPos;

        return speed;
    }

    private void RotateSword()
    {
        if (swordTransform == null) return;

        // Face outward from player (point away from center)
        Vector2 outwardDirection = currentOrbitPosition.normalized;

        // Calculate angle in degrees
        float angle = Mathf.Atan2(outwardDirection.y, outwardDirection.x) * Mathf.Rad2Deg;

        // Apply rotation (adjust offset if your sword sprite faces a different direction)
        swordTransform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void CheckCollisions()
    {
        if (swordTransform == null || cursorMechanics == null) return;

        // Use a small circle check at sword position
        float checkRadius = 0.5f; // Adjust based on sword size
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(swordTransform.position, checkRadius);

        foreach (Collider2D hit in hitColliders)
        {
            // Skip self and character
            if (hit.gameObject == gameObject ||
                hit.gameObject == character.gameObject ||
                (swordInstance != null && hit.gameObject == swordInstance))
                continue;

            // Calculate damage based on sword speed
            float speedDamage = (currentSwordSpeed - cursorMechanics.minimumDamageSpeed) *
                               cursorMechanics.damagePerSpeedUnit;
            float totalDamage = Mathf.Max(cursorMechanics.baseDamage, speedDamage);

            // Apply velocity bonus from character movement
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

            // Optional: Apply knockback
            var targetRb = target.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                // Knockback in direction of sword movement
                Vector2 knockbackDir = (swordTransform.position - (Vector3)lastPosition).normalized;
                if (knockbackDir.magnitude < 0.1f)
                {
                    knockbackDir = currentOrbitPosition.normalized; // Fallback to outward direction
                }

                targetRb.AddForce(knockbackDir * currentSwordSpeed * 0.5f, ForceMode2D.Impulse);
            }
        }
    }

    private void DrawDebugInfo()
    {
        if (character == null || cursorVisual == null) return;

        // Draw orbit circle
        DrawCircle(character.transform.position, cursorMechanics.orbitRadius, 32, cursorVisual.orbitDebugColor);

        // Draw line from player to sword
        if (swordTransform != null)
        {
            Debug.DrawLine(character.transform.position, swordTransform.position, Color.cyan, debugDisplayTime);
        }

        // Draw line from player to target (cursor)
        Debug.DrawLine(character.transform.position, targetPosition, Color.green, debugDisplayTime);

        // Draw sword speed indicator
        if (swordTransform != null)
        {
            float speedBarLength = currentSwordSpeed * 0.1f;
            Color speedColor = currentSwordSpeed > cursorMechanics.minimumDamageSpeed ? Color.green : Color.gray;
            Debug.DrawRay(swordTransform.position, Vector2.up * speedBarLength, speedColor, debugDisplayTime);
        }

        // Draw mouse position
        Debug.DrawRay(debugLastMousePosition, Vector2.up * 0.5f, Color.red, debugDisplayTime);
        Debug.DrawRay(debugLastMousePosition, Vector2.right * 0.5f, Color.red, debugDisplayTime);
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

        // Destroy sword instance if we created it
        if (swordInstance != null)
        {
            Destroy(swordInstance);
        }

        cursorMechanics = null;
        cursorVisual = null;
    }
}