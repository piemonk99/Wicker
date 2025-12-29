using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CursorWeaponSystem : WeaponSystem
{
    private Transform weaponTransform;
    private GameObject weaponInstance;
    private WeaponCollisionHandler weaponCollisionHandler;
    private CapsuleCollider2D weaponCapsuleCollider;

    // Orbit state
    private Vector2 targetOrbitPosition;
    private Vector2 currentOrbitPosition;
    private float currentAngle;
    private float previousAngle;
    private float angularDistance;

    // Acceleration mode
    private float currentAngularVelocity;
    private float targetAngle;

    // Movement
    private float currentWeaponSpeed;
    private Vector2 lastPosition;
    private float currentOrbitRadius;

    // Combat
    private bool isSwinging = false;
    private HashSet<Collider2D> hitThisFrame = new HashSet<Collider2D>();
    private List<Vector2> ghostColliderPositions = new List<Vector2>();

    // Configs
    private CursorWeaponMechanicsConfig cursorMechanics;
    private CursorWeaponVisualConfig cursorVisual;

    // Weapon capsule dimensions
    private float weaponCapsuleWidth;
    private float weaponCapsuleHeight;

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);
        if (currentConfig == null) return;

        cursorMechanics = currentConfig.MechanicsConfig as CursorWeaponMechanicsConfig;
        cursorVisual = currentConfig.VisualConfig as CursorWeaponVisualConfig;

        if (cursorMechanics == null || cursorVisual == null)
        {
            Debug.LogError("CursorWeaponSystem requires CursorWeapon configs");
            return;
        }

        SpawnWeapon();
        ExtractWeaponColliderDimensions();
        InitializeWeaponPosition();
        showDebugInfo = cursorVisual.enableDebugVisualization;
    }

    private void SpawnWeapon()
    {
        if (cursorVisual.weaponPrefab == null)
        {
            Debug.LogError("Weapon prefab required in CursorWeaponVisualConfig");
            return;
        }

        if (weaponInstance != null) Destroy(weaponInstance);

        weaponInstance = Instantiate(cursorVisual.weaponPrefab, transform);
        weaponTransform = weaponInstance.transform;

        weaponCollisionHandler = weaponInstance.GetComponent<WeaponCollisionHandler>();
        if (weaponCollisionHandler == null)
            weaponCollisionHandler = weaponInstance.AddComponent<WeaponCollisionHandler>();
        weaponCollisionHandler.Initialize(this);

        // Ensure collider exists and is trigger
        weaponCapsuleCollider = weaponInstance.GetComponent<CapsuleCollider2D>();
        if (weaponCapsuleCollider != null)
        {
            weaponCapsuleCollider.isTrigger = true;
        }
        else
        {
            Debug.LogError("Weapon prefab must have a CapsuleCollider2D!");
            weaponInstance.SetActive(false);
        }
    }

    private void ExtractWeaponColliderDimensions()
    {
        if (weaponCapsuleCollider == null) return;

        weaponCapsuleWidth = weaponCapsuleCollider.size.x;
        weaponCapsuleHeight = weaponCapsuleCollider.size.y;
    }

    private void InitializeWeaponPosition()
    {
        currentOrbitRadius = cursorMechanics.maxOrbitRadius;
        currentOrbitPosition = Vector2.right * currentOrbitRadius;
        targetOrbitPosition = currentOrbitPosition;
        weaponTransform.position = (Vector2)character.transform.position + currentOrbitPosition;
        lastPosition = weaponTransform.position;
        currentAngle = 0f;
        previousAngle = 0f;
        targetAngle = 0f;
        angularDistance = 0f;
        currentAngularVelocity = 0f;
    }

    protected override void TryAttack()
    {
        if (cursorMechanics == null) return;

        isSwinging = !isSwinging;
        hitThisFrame.Clear();
        ghostColliderPositions.Clear();

        if (weaponInstance != null)
            weaponInstance.SetActive(isSwinging);

        if (isSwinging)
        {
            soundManager?.PlaySwingSound(0f);
            character.RaiseEvent("cursor_weapon_swing_started", currentConfig.weaponName);
        }
        else
        {
            weaponCollisionHandler?.ClearCollisions();
            character.RaiseEvent("cursor_weapon_swing_stopped", currentConfig.weaponName);
        }
    }

    protected override void StopAttack()
    {
        isSwinging = false;
        weaponCollisionHandler?.ClearCollisions();
        if (weaponInstance != null) weaponInstance.SetActive(false);
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        if (cursorMechanics == null || weaponTransform == null) return;

        UpdateTargetPosition();

        if (isSwinging)
        {
            hitThisFrame.Clear();
            CheckNormalCollisions();
        }

        if (showDebugInfo) DrawDebugInfo();
    }

    public override void PhysicsTick(float fixedDeltaTime)
    {
        base.PhysicsTick(fixedDeltaTime);
        if (cursorMechanics == null || weaponTransform == null) return;

        previousAngle = currentAngle;
        UpdateWeaponPosition(fixedDeltaTime);
        UpdateWeaponMetrics(fixedDeltaTime);

        if (isSwinging && ShouldCheckSweptCollisions())
            CheckSweptCollisions();

        RotateWeapon();
    }

    private void UpdateTargetPosition()
    {
        if (character == null || Camera.main == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Camera.main.nearClipPlane));
        mouseWorldPos.z = 0;

        Vector2 toCursor = (Vector2)mouseWorldPos - (Vector2)character.transform.position;
        float distance = Mathf.Clamp(toCursor.magnitude, cursorMechanics.minOrbitRadius, cursorMechanics.maxOrbitRadius);

        targetOrbitPosition = distance > 0.001f ? toCursor.normalized * distance : Vector2.right * cursorMechanics.minOrbitRadius;
        targetAngle = Mathf.Atan2(targetOrbitPosition.y, targetOrbitPosition.x) * Mathf.Rad2Deg;
    }

    private void UpdateWeaponPosition(float fixedDeltaTime)
    {
        if (cursorMechanics.movementMode == MovementMode.Direct)
            UpdateDirectMode(fixedDeltaTime);
        else
            UpdateAccelerationMode(fixedDeltaTime);

        // Apply radius constraints
        float currentDistance = currentOrbitPosition.magnitude;
        if (currentDistance < cursorMechanics.minOrbitRadius)
            currentOrbitPosition = currentOrbitPosition.normalized * cursorMechanics.minOrbitRadius;
        else if (currentDistance > cursorMechanics.maxOrbitRadius)
            currentOrbitPosition = currentOrbitPosition.normalized * cursorMechanics.maxOrbitRadius;

        currentOrbitRadius = currentOrbitPosition.magnitude;
        weaponTransform.position = (Vector2)character.transform.position + currentOrbitPosition;
    }

    private void UpdateDirectMode(float fixedDeltaTime)
    {
        currentOrbitPosition = Vector2.Lerp(currentOrbitPosition, targetOrbitPosition, cursorMechanics.cursorFollowSpeed * fixedDeltaTime);
        currentAngle = Mathf.Atan2(currentOrbitPosition.y, currentOrbitPosition.x) * Mathf.Rad2Deg;
    }

    private void UpdateAccelerationMode(float fixedDeltaTime)
    {
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
        float desiredDirection = Mathf.Sign(angleDiff);
        float absAngleDiff = Mathf.Abs(angleDiff);

        // Calculate braking distance and determine if we should brake
        float brakingDistance = (currentAngularVelocity * currentAngularVelocity) / (2f * cursorMechanics.angularDeceleration);
        bool shouldBrake = brakingDistance >= absAngleDiff;

        if (shouldBrake)
        {
            // Braking phase
            float deceleration = cursorMechanics.angularDeceleration;
            if (currentAngularVelocity > 0)
                currentAngularVelocity = Mathf.Max(0f, currentAngularVelocity - deceleration * fixedDeltaTime);
            else if (currentAngularVelocity < 0)
                currentAngularVelocity = Mathf.Min(0f, currentAngularVelocity + deceleration * fixedDeltaTime);
        }
        else
        {
            // Accelerating phase
            if (absAngleDiff > 0.1f)
            {
                currentAngularVelocity += desiredDirection * cursorMechanics.angularAcceleration * fixedDeltaTime;
                currentAngularVelocity = Mathf.Clamp(currentAngularVelocity, -cursorMechanics.maxAngularVelocity, cursorMechanics.maxAngularVelocity);
            }
            else
            {
                currentAngularVelocity = Mathf.Lerp(currentAngularVelocity, 0f, 5f * fixedDeltaTime);
            }
        }

        // Apply movement
        currentAngle += currentAngularVelocity * fixedDeltaTime;
        currentAngle = Mathf.Repeat(currentAngle, 360f);

        // Lerp radius toward target
        float targetRadius = targetOrbitPosition.magnitude;
        currentOrbitRadius = Mathf.Lerp(currentOrbitRadius, targetRadius, cursorMechanics.cursorFollowSpeed * fixedDeltaTime);

        currentOrbitPosition = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad)) * currentOrbitRadius;
    }

    private void UpdateWeaponMetrics(float fixedDeltaTime)
    {
        Vector2 currentPos = weaponTransform.position;
        currentWeaponSpeed = ((Vector2)currentPos - lastPosition).magnitude / fixedDeltaTime;
        lastPosition = currentPos;
        angularDistance = Mathf.DeltaAngle(previousAngle, currentAngle);
    }

    private void RotateWeapon()
    {
        Vector2 outwardDirection = currentOrbitPosition.normalized;
        weaponTransform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(outwardDirection.y, outwardDirection.x) * Mathf.Rad2Deg);
    }

    private void CheckNormalCollisions()
    {
        if (weaponCollisionHandler == null) return;
        foreach (var collider in weaponCollisionHandler.GetCurrentCollisions())
            if (collider != null && !hitThisFrame.Contains(collider))
                ApplyDamage(collider.gameObject);
    }

    private bool ShouldCheckSweptCollisions()
    {
        return cursorMechanics.alwaysUseSweptCollision || Mathf.Abs(angularDistance) > cursorMechanics.sweptCollisionAngleStep;
    }

    private void CheckSweptCollisions()
    {
        if (Mathf.Abs(angularDistance) < 0.1f) return;
        ghostColliderPositions.Clear();

        float absAngularDistance = Mathf.Abs(angularDistance);
        float angleStep = cursorMechanics.sweptCollisionAngleStep;
        int maxColliders = cursorMechanics.maxGhostCollidersPerFrame;

        // If angular distance is too large for our step size, increase step
        if (absAngularDistance > angleStep * maxColliders)
            angleStep = absAngularDistance / maxColliders;

        int steps = Mathf.Min(maxColliders, Mathf.CeilToInt(absAngularDistance / angleStep));

        // Place ghost colliders along the arc between previous and current positions
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)(steps + 1);
            float intermediateAngle = Mathf.LerpAngle(previousAngle, currentAngle, t);

            // Calculate position on orbit circle at this angle
            Vector2 intermediatePosition = (Vector2)character.transform.position +
                new Vector2(Mathf.Cos(intermediateAngle * Mathf.Deg2Rad), Mathf.Sin(intermediateAngle * Mathf.Deg2Rad)) * currentOrbitRadius;

            // Check collision with properly oriented capsule
            CheckGhostCollider(intermediatePosition, intermediateAngle);
            ghostColliderPositions.Add(intermediatePosition);
        }

        // Also check at current position
        CheckGhostCollider(weaponTransform.position, currentAngle);
    }

    private void CheckGhostCollider(Vector2 position, float angle)
    {
        if (cursorMechanics == null || weaponCapsuleCollider == null) return;

        // Create capsule check at the ghost position with proper rotation
        Collider2D[] hits = Physics2D.OverlapCapsuleAll(
            position,
            new Vector2(weaponCapsuleWidth, weaponCapsuleHeight),
            weaponCapsuleCollider.direction,
            angle,
            cursorMechanics.enemyLayers
        );

        foreach (var hit in hits)
            if (hit != null && !hitThisFrame.Contains(hit))
                ApplyDamage(hit.gameObject);
    }

    private void ApplyDamage(GameObject target)
    {
        if (target == character.gameObject || target == gameObject || target == weaponInstance) return;
        if (currentWeaponSpeed < cursorMechanics.minimumDamageSpeed) return;

        // Calculate damage
        float speedDamage = (currentWeaponSpeed - cursorMechanics.minimumDamageSpeed) * cursorMechanics.damagePerSpeedUnit;
        float totalDamage = Mathf.Max(cursorMechanics.baseDamage, speedDamage);
        totalDamage = CalculateDamage(totalDamage);

        // Apply to target
        var condition = target.GetComponent<CharacterCondition>();
        if (condition != null)
        {
            condition.TakeDamage(totalDamage, target.transform.position);
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = totalDamage,
                position = target.transform.position,
                weaponType = "CursorWeapon",
                configName = currentConfig.weaponName
            });

            // Apply knockback
            var targetRb = target.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                float knockbackForce = Mathf.Min(
                    cursorMechanics.baseKnockback + (currentWeaponSpeed * cursorMechanics.speedKnockbackMultiplier),
                    cursorMechanics.maxKnockback
                );

                Vector2 knockbackDir = (weaponTransform.position - character.transform.position).normalized;
                if (knockbackDir.magnitude < 0.1f) knockbackDir = currentOrbitPosition.normalized;
                targetRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
            }
        }

        hitThisFrame.Add(target.GetComponent<Collider2D>());
    }

    // Weapon collision callbacks
    public void OnWeaponTriggerEnter(Collider2D other) => TryHitCollider(other);
    public void OnWeaponTriggerStay(Collider2D other) => TryHitCollider(other);
    private void TryHitCollider(Collider2D other)
    {
        if (isSwinging && !hitThisFrame.Contains(other))
            ApplyDamage(other.gameObject);
    }

    // Debug visualization
    private void DrawDebugInfo()
    {
        if (character == null) return;
        DrawOrbitCircles();
        DrawGhostColliders();
    }

    private void DrawOrbitCircles()
    {
        Vector2 center = character.transform.position;
        DrawCircle(center, cursorMechanics.minOrbitRadius, 24, cursorVisual.minOrbitDebugColor);
        DrawCircle(center, cursorMechanics.maxOrbitRadius, 36, cursorVisual.maxOrbitDebugColor);
    }

    private void DrawCircle(Vector2 center, float radius, int segments, Color color)
    {
        float angleStep = 360f / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i;
            Vector2 nextPoint = center + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            Debug.DrawLine(prevPoint, nextPoint, color);
            prevPoint = nextPoint;
        }
    }

    private void DrawGhostColliders()
    {
        if (ghostColliderPositions.Count == 0) return;

        foreach (Vector2 ghostPos in ghostColliderPositions)
        {
            // Draw center indicator
            float crossSize = 0.1f;
            Debug.DrawLine(ghostPos - Vector2.up * crossSize, ghostPos + Vector2.up * crossSize,
                         cursorVisual.sweptCollisionDebugColor);
            Debug.DrawLine(ghostPos - Vector2.right * crossSize, ghostPos + Vector2.right * crossSize,
                         cursorVisual.sweptCollisionDebugColor);

            // Calculate angle from player to this ghost position
            Vector2 toGhost = ghostPos - (Vector2)character.transform.position;
            float angle = Mathf.Atan2(toGhost.y, toGhost.x) * Mathf.Rad2Deg;

            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );

            // Horizontal capsule: width along direction, height perpendicular
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * weaponCapsuleHeight * 0.5f;
            Vector2 capsuleStart = ghostPos;
            Vector2 capsuleEnd = ghostPos + direction * weaponCapsuleWidth;

            // Draw capsule sides
            Debug.DrawLine(capsuleStart - perpendicular, capsuleEnd - perpendicular,
                         cursorVisual.sweptCollisionDebugColor);
            Debug.DrawLine(capsuleStart + perpendicular, capsuleEnd + perpendicular,
                         cursorVisual.sweptCollisionDebugColor);

            // Draw capsule caps
            Debug.DrawLine(capsuleStart - perpendicular, capsuleStart + perpendicular,
                         cursorVisual.sweptCollisionDebugColor);
            Debug.DrawLine(capsuleEnd - perpendicular, capsuleEnd + perpendicular,
                         cursorVisual.sweptCollisionDebugColor);
        }
    }

    protected override void CleanupManagers()
    {
        base.CleanupManagers();
        if (weaponInstance != null) Destroy(weaponInstance);
        cursorMechanics = null;
        cursorVisual = null;
    }
}