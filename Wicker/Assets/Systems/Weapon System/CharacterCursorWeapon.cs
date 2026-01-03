using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CharacterCursorWeapon : CharacterWeapon
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
    private float currentWeaponSpeed;           // Instantaneous speed
    private float smoothedWeaponSpeed;          // Averaged speed
    private Vector2 lastPosition;
    private float currentTangentialVelocity; // Current angular velocity for tangent movement (rad/s)
    private float lastTangentialAngle; // Last angle for calculating angular velocity
    private float currentOrbitRadius;

    // Speed averaging system
    private Queue<float> speedHistory = new Queue<float>();
    private float speedSum = 0f;

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

    // Oscillation damping
    private const float DEADZONE_ANGLE = 0.5f;

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);

        if (currentConfig == null) return;

        // Get specific configs
        cursorMechanics = currentConfig.MechanicsConfig as CursorWeaponMechanicsConfig;
        cursorVisual = currentConfig.VisualConfig as CursorWeaponVisualConfig;

        if (cursorMechanics == null || cursorVisual == null)
        {
            Debug.LogError("CharacterCursorWeapon requires CursorWeapon configs");
            return;
        }

        // Clean up existing weapon instance before creating new one
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
            weaponInstance = null;
        }

        // Initialize speed averaging system
        InitializeSpeedAveraging();

        SpawnWeapon();
        ExtractWeaponColliderDimensions();
        InitializeWeaponPosition();
        showDebugInfo = cursorVisual.enableDebugVisualization;
    }

    private void InitializeSpeedAveraging()
    {
        speedHistory.Clear();
        speedSum = 0f;
        currentWeaponSpeed = 0f;
        smoothedWeaponSpeed = 0f;

        // Pre-fill with zeros
        for (int i = 0; i < cursorMechanics.speedAverageFrames; i++)
        {
            speedHistory.Enqueue(0f);
        }
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

        // Start inactive - will be activated when swinging starts
        weaponInstance.SetActive(false);

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
            // Reset speed averaging when starting a new swing
            InitializeSpeedAveraging();

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

        // Direct mode runs in Update for smooth visuals when not swinging
        if (cursorMechanics.movementMode == MovementMode.Direct && !isSwinging)
        {
            previousAngle = currentAngle;
            UpdateWeaponPosition(deltaTime);
            UpdateWeaponMetrics(deltaTime);
            RotateWeapon();
        }

        // Always check normal collisions in Update when swinging
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

        // Acceleration mode and combat always run in FixedUpdate for consistency
        if (cursorMechanics.movementMode == MovementMode.Acceleration || isSwinging)
        {
            previousAngle = currentAngle;
            UpdateWeaponPosition(fixedDeltaTime);
            UpdateWeaponMetrics(fixedDeltaTime);
            UpdateSpeedAverage();

            if (isSwinging && ShouldCheckSweptCollisions())
                CheckSweptCollisions();

            RotateWeapon();
        }
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

    private void UpdateWeaponPosition(float deltaTime)
    {
        if (cursorMechanics.movementMode == MovementMode.Direct)
            UpdateDirectMode(deltaTime);
        else
            UpdateAccelerationMode(deltaTime);

        // Apply radius constraints
        float currentDistance = currentOrbitPosition.magnitude;
        if (currentDistance < cursorMechanics.minOrbitRadius)
            currentOrbitPosition = currentOrbitPosition.normalized * cursorMechanics.minOrbitRadius;
        else if (currentDistance > cursorMechanics.maxOrbitRadius)
            currentOrbitPosition = currentOrbitPosition.normalized * cursorMechanics.maxOrbitRadius;

        currentOrbitRadius = currentOrbitPosition.magnitude;
        weaponTransform.position = (Vector2)character.transform.position + currentOrbitPosition;
    }

    private void UpdateDirectMode(float deltaTime)
    {
        if (cursorMechanics == null) return;

        float maxDistance = cursorMechanics.cursorFollowSpeed * deltaTime;
        Vector2 targetDir = targetOrbitPosition - currentOrbitPosition;
        float distance = targetDir.magnitude;

        // Early exit if no movement needed
        if (distance < 0.001f) return;

        float currentRadius = currentOrbitPosition.magnitude;
        float currentAngleRad = Mathf.Atan2(currentOrbitPosition.y, currentOrbitPosition.x);

        // Check if we should use tangent movement
        bool useTangentMovement = false;

        if (currentRadius <= cursorMechanics.minOrbitRadius + 0.01f)
        {
            // Calculate the angle from player to cursor
            float targetAngleRad = Mathf.Atan2(targetOrbitPosition.y, targetOrbitPosition.x);
            float angleDiff = Mathf.DeltaAngle(currentAngleRad * Mathf.Rad2Deg, targetAngleRad * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            // The tangent line direction (90 degrees from radius)
            float tangentDir = Mathf.Sign(angleDiff);

            // Calculate how much of the target direction points along the tangent
            Vector2 tangentVector = new Vector2(-Mathf.Sin(currentAngleRad), Mathf.Cos(currentAngleRad)) * tangentDir;
            float tangentComponent = Vector2.Dot(targetDir.normalized, tangentVector);

            // Calculate how much points inward (toward center)
            Vector2 inwardVector = -currentOrbitPosition.normalized;
            float inwardComponent = Vector2.Dot(targetDir.normalized, inwardVector);

            // Use tangent movement if cursor is mostly pointing behind player (inward)
            // AND we're not trying to move outward (positive inward component means toward center)
            useTangentMovement = inwardComponent > 0f && Mathf.Abs(tangentComponent) > 0.1f;
        }

        Vector2 newPosition;

        if (useTangentMovement)
        {
            // TANGENT MOVEMENT: Move along circle when cursor is behind player
            float targetAngleRad = Mathf.Atan2(targetOrbitPosition.y, targetOrbitPosition.x);
            float angleDiff = Mathf.DeltaAngle(currentAngleRad * Mathf.Rad2Deg, targetAngleRad * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            // Maximum angular velocity to achieve cursorFollowSpeed linear speed
            float maxAngularVelocity = cursorMechanics.cursorFollowSpeed / cursorMechanics.minOrbitRadius; // rad/s
            float maxAnglePerFrame = maxAngularVelocity * deltaTime; // rad per frame

            // Calculate desired angular velocity (full speed toward target)
            float desiredAngularVelocity = Mathf.Clamp(angleDiff / deltaTime, -maxAngularVelocity, maxAngularVelocity);

            // Apply smoothing to angular acceleration
            if (cursorMechanics.directModeSmoothing > 0f)
            {
                float smoothing = Mathf.Clamp01(cursorMechanics.directModeSmoothing);

                // Calculate acceleration factor based on smoothing
                // Higher smoothing = slower acceleration
                float accelerationFactor = 1f - Mathf.Exp(-cursorMechanics.cursorFollowSpeed * (1f - smoothing * 0.7f) * deltaTime);
                accelerationFactor = Mathf.Min(accelerationFactor, 0.95f);

                // Smoothly approach desired angular velocity
                currentTangentialVelocity = Mathf.Lerp(currentTangentialVelocity, desiredAngularVelocity, accelerationFactor);

                // Clamp to max angular velocity
                currentTangentialVelocity = Mathf.Clamp(currentTangentialVelocity, -maxAngularVelocity, maxAngularVelocity);

                // Apply movement based on smoothed velocity
                float angleToMove = currentTangentialVelocity * deltaTime;

                // Additional safety clamp to ensure we don't overshoot
                if (Mathf.Abs(angleDiff) > 0.001f)
                {
                    float maxAllowedMovement = Mathf.Sign(angleDiff) * Mathf.Min(Mathf.Abs(angleToMove), Mathf.Abs(angleDiff));
                    angleToMove = maxAllowedMovement;
                }

                currentAngleRad += angleToMove;
            }
            else
            {
                // No smoothing - directly apply clamped angular movement
                float angleToMove = Mathf.Clamp(angleDiff, -maxAnglePerFrame, maxAnglePerFrame);
                currentAngleRad += angleToMove;

                // Reset tangential velocity for consistency
                currentTangentialVelocity = angleToMove / deltaTime;
            }

            newPosition = new Vector2(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad)) * cursorMechanics.minOrbitRadius;
        }
        else
        {
            // When not using tangent movement, reset tangential velocity
            currentTangentialVelocity = 0f;

            // NORMAL MOVEMENT: Direct toward target
            bool isFarFromTarget = distance > maxDistance;

            if (cursorMechanics.directModeSmoothing > 0f)
            {
                // Smooth movement with frame-rate independent exponential interpolation
                float smoothing = Mathf.Clamp01(cursorMechanics.directModeSmoothing);
                float strength = isFarFromTarget ? 0.7f : 0.9f;
                float lerpFactor = 1f - Mathf.Exp(-cursorMechanics.cursorFollowSpeed * (1f - smoothing * strength) * deltaTime);

                // Clamp to prevent overshoot
                lerpFactor = Mathf.Min(lerpFactor, isFarFromTarget ? 0.95f : 0.98f);

                newPosition = Vector2.Lerp(currentOrbitPosition, targetOrbitPosition, lerpFactor);

                // Ensure we don't exceed max distance
                Vector2 movement = newPosition - currentOrbitPosition;
                if (movement.magnitude > maxDistance)
                {
                    newPosition = currentOrbitPosition + movement.normalized * maxDistance;
                }
            }
            else
            {
                // Direct movement (no smoothing)
                newPosition = isFarFromTarget
                    ? currentOrbitPosition + targetDir.normalized * maxDistance
                    : targetOrbitPosition;
            }

            // Apply radius constraints
            float newRadius = newPosition.magnitude;
            if (newRadius < cursorMechanics.minOrbitRadius)
                newPosition = newPosition.normalized * cursorMechanics.minOrbitRadius;
            else if (newRadius > cursorMechanics.maxOrbitRadius)
                newPosition = newPosition.normalized * cursorMechanics.maxOrbitRadius;
        }

        currentOrbitPosition = newPosition;
        currentOrbitRadius = currentOrbitPosition.magnitude;
        currentAngle = Mathf.Atan2(currentOrbitPosition.y, currentOrbitPosition.x) * Mathf.Rad2Deg;
    }

    private void UpdateAccelerationMode(float deltaTime)
    {
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
        float absAngleDiff = Mathf.Abs(angleDiff);
        float absAngularVelocity = Mathf.Abs(currentAngularVelocity);

        // Deadzone: if close to target AND moving slowly, snap to target
        if (absAngleDiff < DEADZONE_ANGLE && absAngularVelocity < cursorMechanics.maxAngularVelocity * 0.2f)
        {
            currentAngularVelocity = 0f;
            currentAngle = targetAngle;
        }
        else
        {
            float desiredDirection = Mathf.Sign(angleDiff);

            // Calculate braking distance
            float brakingDistance = (currentAngularVelocity * currentAngularVelocity) / (2f * cursorMechanics.angularDeceleration);
            bool shouldBrake = brakingDistance >= absAngleDiff;

            if (shouldBrake)
            {
                // Braking phase
                if (currentAngularVelocity > 0)
                    currentAngularVelocity = Mathf.Max(0f, currentAngularVelocity - cursorMechanics.angularDeceleration * deltaTime);
                else if (currentAngularVelocity < 0)
                    currentAngularVelocity = Mathf.Min(0f, currentAngularVelocity + cursorMechanics.angularDeceleration * deltaTime);
            }
            else
            {
                // Accelerating phase
                if (absAngleDiff > 0.1f)
                {
                    currentAngularVelocity += desiredDirection * cursorMechanics.angularAcceleration * deltaTime;
                    currentAngularVelocity = Mathf.Clamp(currentAngularVelocity, -cursorMechanics.maxAngularVelocity, cursorMechanics.maxAngularVelocity);
                }
                else
                {
                    // Gentle damping when close to target but not in deadzone
                    currentAngularVelocity = Mathf.Lerp(currentAngularVelocity, 0f, 5f * deltaTime);
                }
            }

            // Apply movement
            currentAngle += currentAngularVelocity * deltaTime;
            currentAngle = Mathf.Repeat(currentAngle, 360f);
        }

        // Lerp radius toward target
        float targetRadius = targetOrbitPosition.magnitude;
        currentOrbitRadius = Mathf.Lerp(currentOrbitRadius, targetRadius, cursorMechanics.cursorFollowSpeed * deltaTime);

        currentOrbitPosition = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad)) * currentOrbitRadius;
    }

    private void UpdateWeaponMetrics(float deltaTime)
    {
        if (deltaTime <= 0) return;

        Vector2 currentPos = weaponTransform.localPosition;
        currentWeaponSpeed = ((Vector2)currentPos - lastPosition).magnitude / deltaTime;
        lastPosition = currentPos;
        angularDistance = Mathf.DeltaAngle(previousAngle, currentAngle);

        Debug.Log($"currentWeaponSpeed: {currentWeaponSpeed}");
    }

    private void UpdateSpeedAverage()
    {
        if (cursorMechanics == null || cursorMechanics.speedAverageFrames <= 1)
        {
            smoothedWeaponSpeed = currentWeaponSpeed;
            return;
        }

        // Remove oldest speed
        if (speedHistory.Count >= cursorMechanics.speedAverageFrames)
        {
            speedSum -= speedHistory.Dequeue();
        }

        // Add current speed
        speedHistory.Enqueue(currentWeaponSpeed);
        speedSum += currentWeaponSpeed;

        // Calculate simple average
        float simpleAverage = speedSum / speedHistory.Count;

        // Blend with weighted approach for more control
        if (cursorMechanics.currentFrameWeight >= 1f)
        {
            smoothedWeaponSpeed = currentWeaponSpeed;
        }
        else if (cursorMechanics.currentFrameWeight <= 0f)
        {
            smoothedWeaponSpeed = simpleAverage;
        }
        else
        {
            // Weighted blend: current frame * weight + average * (1 - weight)
            smoothedWeaponSpeed = (currentWeaponSpeed * cursorMechanics.currentFrameWeight) +
                                 (simpleAverage * (1f - cursorMechanics.currentFrameWeight));
        }
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
                HitCharacter(collider.gameObject);
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

    private void CheckGhostCollider(Vector2 position, float intermediateAngle)
    {
        if (cursorMechanics == null || weaponCapsuleCollider == null || character == null) return;

        // Calculate the direction from player to this ghost position
        Vector2 toPosition = position - (Vector2)character.transform.position;

        // The rotation angle should make the capsule face outward from player
        float rotationAngle = Mathf.Atan2(toPosition.y, toPosition.x) * Mathf.Rad2Deg;

        // For a horizontal capsule, this is correct
        // For a vertical capsule, we might need to adjust by 90 degrees
        if (weaponCapsuleCollider.direction == CapsuleDirection2D.Vertical)
        {
            rotationAngle += 90f; // Vertical capsule needs to be rotated 90 degrees
        }

        // Scale the capsule size by the weapon transform's scale
        Vector2 scaledSize = new Vector2(
            weaponCapsuleWidth * weaponTransform.localScale.x,
            weaponCapsuleHeight * weaponTransform.localScale.y
        );

        // Create capsule check at the ghost position with proper rotation
        Collider2D[] hits = Physics2D.OverlapCapsuleAll(
            position,
            scaledSize,
            weaponCapsuleCollider.direction,
            rotationAngle,
            cursorMechanics.enemyLayers
        );

        foreach (var hit in hits)
        {
            if (hit != null && !hitThisFrame.Contains(hit))
            {
                // Skip invalid targets
                if (hit.gameObject == character.gameObject ||
                    hit.gameObject == gameObject ||
                    hit.gameObject == weaponInstance)
                    continue;

                HitCharacter(hit.gameObject);
            }
        }
    }

    private void HitCharacter(GameObject target)
    {
        if (target == character.gameObject || target == gameObject || target == weaponInstance) return;

        // Get the appropriate speed value based on config
        float speedForDamage = cursorMechanics.useAverageSpeedForDamage ? smoothedWeaponSpeed : currentWeaponSpeed;
        float speedForKnockback = cursorMechanics.useAverageSpeedForKnockback ? smoothedWeaponSpeed : currentWeaponSpeed;

        float damage = CalculateDamage();
        float iFrameDuration = CalculateIFrames();

        // Apply to target
        var condition = target.GetComponent<CharacterCondition>();
        if (condition != null)
        {
            // Do not hit character if they have hit cooldown
            if (condition.HasStatusEffect("hit_cooldown")) return;

            condition.TakeDamage(damage, target.transform.position, hitCooldown: iFrameDuration);
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                instantaneousSpeed = currentWeaponSpeed,
                averagedSpeed = smoothedWeaponSpeed,
                position = target.transform.position,
                weaponType = "CursorWeapon",
                configName = currentConfig.weaponName
            });

            // Apply knockback
            var targetRb = target.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                float knockbackForce = Mathf.Min(
                    cursorMechanics.baseKnockback + (speedForKnockback * cursorMechanics.speedKnockbackMultiplier),
                    cursorMechanics.maxKnockback
                );

                Vector2 knockbackDir = (weaponTransform.position - character.transform.position).normalized;
                if (knockbackDir.magnitude < 0.1f) knockbackDir = currentOrbitPosition.normalized;
                targetRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
            }
        }

        hitThisFrame.Add(target.GetComponent<Collider2D>());
    }

    public float CalculateDamage()
    {
        float damage = cursorMechanics.baseDamage;

        float velocity = rb != null ? rb.linearVelocity.magnitude : 0f;
        damage *= CalculateVelocityMultiplier(velocity, cursorMechanics);

        // Get max speed based on movement mode
        float maxSpeed = GetMaxSpeed();

        // Get the appropriate speed value based on config
        float effectiveSpeed = cursorMechanics.useAverageSpeedForDamage ? smoothedWeaponSpeed : currentWeaponSpeed;

        // Calculate speed percentage relative to max
        float speedPercent = Mathf.Clamp01(effectiveSpeed / maxSpeed);

        // Get a float the reflects where within the range of the two percents our current speed percent is
        float damageRange = Mathf.InverseLerp(
            cursorMechanics.minDamageMultiplierSpeedPercent,
            cursorMechanics.maxDamageMultiplierSpeedPercent,
            speedPercent
        );

        // Use the float from the previous calculation to lerp between our min/max damage
        return damage * Mathf.Lerp(
            cursorMechanics.minDamageMultiplier,
            cursorMechanics.maxDamageMultiplier,
            damageRange
        );
    }

    private float CalculateIFrames()
    {
        // Get max speed based on movement mode
        float maxSpeed = GetMaxSpeed();

        // Get the appropriate speed value based on config
        float effectiveSpeed = cursorMechanics.useAverageSpeedForDamage ? smoothedWeaponSpeed : currentWeaponSpeed;

        // Calculate speed percentage relative to max
        float speedPercent = Mathf.Clamp01(effectiveSpeed / maxSpeed);

        // Get a float the reflects where within the range of the two percents our current speed percent is
        float invincibilityRange = Mathf.InverseLerp(
            cursorMechanics.minInvincibilitySpeedPercent,
            cursorMechanics.maxInvincibilitySpeedPercent,
            speedPercent
        );

        //Debug.Log($"Hit giving {Mathf.Lerp(cursorMechanics.minInvincibilityDuration, cursorMechanics.maxInvincibilityDuration, invincibilityRange)} i frames");

        // Use the float from the previous calculation to lerp between our min/max damage
        return Mathf.Lerp(
            cursorMechanics.minInvincibilityDuration,
            cursorMechanics.maxInvincibilityDuration,
            invincibilityRange
        );
    }

    private float GetMaxSpeed()
    {
        // Get max speed based on movement mode
        return cursorMechanics.movementMode switch
        {
            MovementMode.Direct => cursorMechanics.cursorFollowSpeed,
            MovementMode.Acceleration => cursorMechanics.maxAngularVelocity * Mathf.Deg2Rad * cursorMechanics.maxOrbitRadius,
            _ => cursorMechanics.cursorFollowSpeed // default fallback
        };
    }

    // Weapon collision callbacks
    public void OnWeaponTriggerEnter(Collider2D other) => TryHitCollider(other);
    public void OnWeaponTriggerStay(Collider2D other) => TryHitCollider(other);
    private void TryHitCollider(Collider2D other)
    {
        if (isSwinging && !hitThisFrame.Contains(other))
            HitCharacter(other.gameObject);
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

    // Add proper cleanup:
    protected override void CleanupManagers()
    {
        base.CleanupManagers();

        // Properly clean up weapon instance
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
            weaponInstance = null;
        }

        // Clear all references
        weaponTransform = null;
        weaponCollisionHandler = null;
        weaponCapsuleCollider = null;
        cursorMechanics = null;
        cursorVisual = null;

        // Clear collections
        speedHistory?.Clear();
        speedSum = 0f;
        hitThisFrame?.Clear();
        ghostColliderPositions?.Clear();
    }
}