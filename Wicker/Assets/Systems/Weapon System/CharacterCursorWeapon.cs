using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CharacterCursorWeapon : CharacterWeapon
{
    // Weapon references
    private Transform weaponTransform;
    private GameObject weaponInstance;
    private WeaponCollisionHandler weaponCollisionHandler;
    private CapsuleCollider2D weaponCapsuleCollider;

    // Movement state
    private Vector2 targetOrbitPosition;
    private Vector2 currentOrbitPosition;
    private float currentAngle;
    private float previousAngle;
    private float angularDistance;
    private float currentAngularVelocity;
    private float currentOrbitRadius;

    // Speed tracking
    private float currentWeaponSpeed;
    private float smoothedWeaponSpeed;
    private Vector2 lastPosition;
    private readonly Queue<float> speedHistory = new Queue<float>();
    private float speedSum;

    // Combat state
    private bool isSwinging;
    private readonly HashSet<Collider2D> hitThisFrame = new HashSet<Collider2D>();
    private readonly List<Vector2> ghostColliderPositions = new List<Vector2>();

    // Configs
    private CursorWeaponMechanicsConfig cursorMechanics;
    private CursorWeaponVisualConfig cursorVisual;

    // Weapon dimensions (cached)
    private float weaponCapsuleWidth;
    private float weaponCapsuleHeight;

    // Constants
    private const float DEADZONE_ANGLE = 0.5f;
    private const float MIN_ANGULAR_DISTANCE = 0.1f;

    #region Initialization

    /// <summary>
    /// Loads and initializes the weapon configuration
    /// </summary>
    protected override void LoadConfig(WeaponConfig config)
    {
        base.LoadConfig(config);

        if (currentConfig == null) return;

        // Get specific configs for cursor weapon
        cursorMechanics = currentConfig.MechanicsConfig as CursorWeaponMechanicsConfig;
        cursorVisual = currentConfig.VisualConfig as CursorWeaponVisualConfig;

        if (cursorMechanics == null || cursorVisual == null)
        {
            Debug.LogError("CharacterCursorWeapon requires CursorWeapon configs");
            return;
        }

        CleanupWeapon();
        InitializeSpeedAveraging();
        SpawnWeapon();
        InitializeWeaponPosition();
        showDebugInfo = cursorVisual.enableDebugVisualization;
    }

    /// <summary>
    /// Cleans up any existing weapon instance
    /// </summary>
    private void CleanupWeapon()
    {
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
            weaponInstance = null;
            weaponTransform = null;
            weaponCollisionHandler = null;
            weaponCapsuleCollider = null;
        }
    }

    /// <summary>
    /// Initializes the speed averaging system with default values
    /// </summary>
    private void InitializeSpeedAveraging()
    {
        speedHistory.Clear();
        speedSum = 0f;
        currentWeaponSpeed = 0f;
        smoothedWeaponSpeed = 0f;

        // Pre-fill with zeros for consistent averaging from the start
        for (int i = 0; i < cursorMechanics.speedAverageFrames; i++)
        {
            speedHistory.Enqueue(0f);
        }
    }

    /// <summary>
    /// Spawns the weapon prefab and sets up its components
    /// </summary>
    private void SpawnWeapon()
    {
        if (cursorVisual.weaponPrefab == null)
        {
            Debug.LogError("Weapon prefab required in CursorWeaponVisualConfig");
            return;
        }

        weaponInstance = Instantiate(cursorVisual.weaponPrefab, transform);
        weaponTransform = weaponInstance.transform;
        weaponInstance.SetActive(false);

        InitializeWeaponCollisionHandler();
        InitializeWeaponCollider();
    }

    /// <summary>
    /// Sets up the weapon collision handler component
    /// </summary>
    private void InitializeWeaponCollisionHandler()
    {
        weaponCollisionHandler = weaponInstance.GetComponent<WeaponCollisionHandler>();
        if (weaponCollisionHandler == null)
            weaponCollisionHandler = weaponInstance.AddComponent<WeaponCollisionHandler>();
        weaponCollisionHandler.Initialize(this);
    }

    /// <summary>
    /// Configures the weapon's capsule collider and caches its dimensions
    /// </summary>
    private void InitializeWeaponCollider()
    {
        weaponCapsuleCollider = weaponInstance.GetComponent<CapsuleCollider2D>();
        if (weaponCapsuleCollider == null)
        {
            Debug.LogError("Weapon prefab must have a CapsuleCollider2D!");
            return;
        }

        weaponCapsuleCollider.isTrigger = true;
        weaponCapsuleWidth = weaponCapsuleCollider.size.x;
        weaponCapsuleHeight = weaponCapsuleCollider.size.y;
    }

    /// <summary>
    /// Initializes the weapon's starting position and state
    /// </summary>
    private void InitializeWeaponPosition()
    {
        currentOrbitRadius = cursorMechanics.maxOrbitRadius;
        currentOrbitPosition = Vector2.right * currentOrbitRadius;
        targetOrbitPosition = currentOrbitPosition;
        weaponTransform.position = (Vector2)character.transform.position + currentOrbitPosition;
        lastPosition = weaponTransform.position;
        currentAngle = 0f;
        previousAngle = 0f;
        currentAngularVelocity = 0f;
        angularDistance = 0f;
    }

    #endregion

    #region Attack Methods

    /// <summary>
    /// Toggles weapon swinging on/off when attack is triggered
    /// </summary>
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

    /// <summary>
    /// Stops the current attack
    /// </summary>
    protected override void StopAttack()
    {
        isSwinging = false;
        weaponCollisionHandler?.ClearCollisions();
        if (weaponInstance != null) weaponInstance.SetActive(false);
    }

    #endregion

    #region Update Methods

    /// <summary>
    /// Called every frame for visual updates and direct mode movement
    /// </summary>
    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        if (cursorMechanics == null || weaponTransform == null) return;

        UpdateTargetPosition();

        // Direct mode runs in Update for smooth visuals when not swinging
        if (cursorMechanics.movementMode == MovementMode.Direct && !isSwinging)
        {
            UpdateWeaponState(deltaTime);
        }

        // Always check normal collisions in Update when swinging
        if (isSwinging)
        {
            hitThisFrame.Clear();
            CheckNormalCollisions();
        }

        if (showDebugInfo) DrawDebugInfo();
    }

    /// <summary>
    /// Called every physics frame for acceleration mode and combat calculations
    /// </summary>
    public override void PhysicsTick(float fixedDeltaTime)
    {
        base.PhysicsTick(fixedDeltaTime);
        if (cursorMechanics == null || weaponTransform == null) return;

        // Acceleration mode and combat always run in FixedUpdate for consistency
        if (cursorMechanics.movementMode == MovementMode.Acceleration || isSwinging)
        {
            UpdateWeaponState(fixedDeltaTime);
            UpdateSpeedAverage();

            if (isSwinging && ShouldCheckSweptCollisions())
                CheckSweptCollisions();
        }
    }

    /// <summary>
    /// Updates weapon position, metrics, and rotation
    /// </summary>
    private void UpdateWeaponState(float deltaTime)
    {
        previousAngle = currentAngle;
        UpdateWeaponPosition(deltaTime);
        UpdateWeaponMetrics(deltaTime);
        RotateWeapon();
    }

    #endregion

    #region Movement Logic

    /// <summary>
    /// Updates the target position based on mouse cursor position
    /// </summary>
    private void UpdateTargetPosition()
    {
        if (character == null || Camera.main == null) return;

        // Convert mouse screen position to world position
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Camera.main.nearClipPlane));
        mouseWorldPos.z = 0;

        Vector2 toCursor = (Vector2)mouseWorldPos - (Vector2)character.transform.position;
        float distance = Mathf.Clamp(toCursor.magnitude, cursorMechanics.minOrbitRadius, cursorMechanics.maxOrbitRadius);

        targetOrbitPosition = distance > 0.001f ? toCursor.normalized * distance : Vector2.right * cursorMechanics.minOrbitRadius;
    }

    /// <summary>
    /// Updates the weapon position based on current movement mode
    /// </summary>
    private void UpdateWeaponPosition(float deltaTime)
    {
        if (cursorMechanics.movementMode == MovementMode.Direct)
            UpdateDirectMode(deltaTime);
        else
            UpdateAccelerationMode(deltaTime);

        ApplyRadiusConstraints();
        weaponTransform.position = (Vector2)character.transform.position + currentOrbitPosition;
    }

    /// <summary>
    /// Applies min/max radius constraints to the weapon position
    /// </summary>
    private void ApplyRadiusConstraints()
    {
        float currentDistance = currentOrbitPosition.magnitude;
        if (currentDistance < cursorMechanics.minOrbitRadius)
            currentOrbitPosition = currentOrbitPosition.normalized * cursorMechanics.minOrbitRadius;
        else if (currentDistance > cursorMechanics.maxOrbitRadius)
            currentOrbitPosition = currentOrbitPosition.normalized * cursorMechanics.maxOrbitRadius;

        currentOrbitRadius = currentOrbitPosition.magnitude;
    }

    #region Direct Movement Mode

    /// <summary>
    /// Updates weapon position using direct movement mode (immediate response)
    /// </summary>
    private void UpdateDirectMode(float deltaTime)
    {
        float maxDistance = cursorMechanics.cursorFollowSpeed * deltaTime;
        Vector2 targetDir = targetOrbitPosition - currentOrbitPosition;
        float distance = targetDir.magnitude;

        Vector2 newPosition = CalculateDirectMovement(targetDir, distance, maxDistance, deltaTime);
        currentOrbitPosition = ApplyRadiusConstraintsWithPreservedMovement(newPosition);
        currentAngle = Mathf.Atan2(currentOrbitPosition.y, currentOrbitPosition.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Calculates the new position for direct movement mode
    /// </summary>
    private Vector2 CalculateDirectMovement(Vector2 targetDir, float distance, float maxDistance, float deltaTime)
    {
        // If smoothing is disabled or zero, use direct movement
        if (cursorMechanics.directModeSmoothing <= 0f)
            return distance > maxDistance ? currentOrbitPosition + targetDir.normalized * maxDistance : targetOrbitPosition;

        return CalculateSmoothedMovement(targetDir, distance, maxDistance, deltaTime);
    }

    /// <summary>
    /// Calculates smoothed movement with exponential interpolation
    /// </summary>
    private Vector2 CalculateSmoothedMovement(Vector2 targetDir, float distance, float maxDistance, float deltaTime)
    {
        float smoothFactor = Mathf.Clamp01(cursorMechanics.directModeSmoothing);
        float strengthFactor = distance > maxDistance ? 0.7f : 0.9f;
        float lerpFactor = 1f - Mathf.Exp(-cursorMechanics.cursorFollowSpeed * (1f - smoothFactor * strengthFactor) * deltaTime);
        lerpFactor = Mathf.Min(lerpFactor, 0.98f);

        Vector2 newPosition = Vector2.Lerp(currentOrbitPosition, targetOrbitPosition, lerpFactor);
        Vector2 movement = newPosition - currentOrbitPosition;

        return movement.magnitude > maxDistance ? currentOrbitPosition + movement.normalized * maxDistance : newPosition;
    }

    /// <summary>
    /// Applies radius constraints while preserving movement distance
    /// </summary>
    private Vector2 ApplyRadiusConstraintsWithPreservedMovement(Vector2 newPosition)
    {
        float newRadius = newPosition.magnitude;

        // Handle max radius clamping
        if (newRadius > cursorMechanics.maxOrbitRadius)
            return newPosition.normalized * cursorMechanics.maxOrbitRadius;

        // Handle min radius clamping with intersection calculation
        if (newRadius < cursorMechanics.minOrbitRadius)
            return CalculateMinRadiusIntersection(newPosition);

        return newPosition;
    }

    /// <summary>
    /// Calculates the intersection point on the min radius circle
    /// </summary>
    private Vector2 CalculateMinRadiusIntersection(Vector2 newPosition)
    {
        float desiredDistance = (newPosition - currentOrbitPosition).magnitude;

        // Special cases
        if (desiredDistance < 0.001f || currentOrbitPosition.magnitude < 0.001f)
            return newPosition.normalized * cursorMechanics.minOrbitRadius;

        // Find intersection between two circles:
        // Circle 1: centered at current position with radius = desired distance
        // Circle 2: centered at player (0,0) with radius = min orbit radius
        Vector2 C1 = currentOrbitPosition;
        float r1 = desiredDistance;
        Vector2 C2 = Vector2.zero;
        float r2 = cursorMechanics.minOrbitRadius;
        Vector2 d = C2 - C1;
        float dMag = d.magnitude;

        // Check if circles intersect
        if (dMag > r1 + r2 || dMag < Mathf.Abs(r1 - r2))
            return newPosition.normalized * cursorMechanics.minOrbitRadius;

        // Calculate intersection points using circle-circle intersection formula
        float a = (r1 * r1 - r2 * r2 + dMag * dMag) / (2 * dMag);
        float h = Mathf.Sqrt(r1 * r1 - a * a);
        Vector2 P2 = C1 + (a / dMag) * d;

        Vector2 intersection1 = new Vector2(
            P2.x + (h / dMag) * (C2.y - C1.y),
            P2.y - (h / dMag) * (C2.x - C1.x)
        );

        Vector2 intersection2 = new Vector2(
            P2.x - (h / dMag) * (C2.y - C1.y),
            P2.y + (h / dMag) * (C2.x - C1.x)
        );

        // Choose the intersection point closer to our original target
        return (intersection1 - newPosition).sqrMagnitude < (intersection2 - newPosition).sqrMagnitude
            ? intersection1 : intersection2;
    }

    #endregion

    #region Acceleration Movement Mode

    /// <summary>
    /// Updates weapon position using acceleration-based movement (physics-like)
    /// </summary>
    private void UpdateAccelerationMode(float deltaTime)
    {
        float targetAngle = Mathf.Atan2(targetOrbitPosition.y, targetOrbitPosition.x) * Mathf.Rad2Deg;
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
        float absAngleDiff = Mathf.Abs(angleDiff);

        // Deadzone: if close to target AND moving slowly, snap to target
        if (absAngleDiff < DEADZONE_ANGLE && Mathf.Abs(currentAngularVelocity) < cursorMechanics.maxAngularVelocity * 0.2f)
        {
            currentAngularVelocity = 0f;
            currentAngle = targetAngle;
        }
        else
        {
            UpdateAngularVelocity(angleDiff, absAngleDiff, deltaTime);
            currentAngle += currentAngularVelocity * deltaTime;
            currentAngle = Mathf.Repeat(currentAngle, 360f);
        }

        UpdateOrbitRadius(deltaTime);
        UpdateOrbitPosition();
    }

    /// <summary>
    /// Updates angular velocity with acceleration/deceleration logic
    /// </summary>
    private void UpdateAngularVelocity(float angleDiff, float absAngleDiff, float deltaTime)
    {
        // Calculate braking distance to determine if we should start braking
        float brakingDistance = (currentAngularVelocity * currentAngularVelocity) / (2f * cursorMechanics.angularDeceleration);
        bool shouldBrake = brakingDistance >= absAngleDiff;

        if (shouldBrake)
        {
            ApplyBraking(deltaTime);
        }
        else if (absAngleDiff > 0.1f)
        {
            ApplyAcceleration(angleDiff, deltaTime);
        }
        else
        {
            // Gentle damping when close to target but not in deadzone
            currentAngularVelocity = Mathf.Lerp(currentAngularVelocity, 0f, 5f * deltaTime);
        }
    }

    /// <summary>
    /// Applies braking to reduce angular velocity
    /// </summary>
    private void ApplyBraking(float deltaTime)
    {
        if (currentAngularVelocity > 0)
            currentAngularVelocity = Mathf.Max(0f, currentAngularVelocity - cursorMechanics.angularDeceleration * deltaTime);
        else if (currentAngularVelocity < 0)
            currentAngularVelocity = Mathf.Min(0f, currentAngularVelocity + cursorMechanics.angularDeceleration * deltaTime);
    }

    /// <summary>
    /// Applies acceleration to increase angular velocity toward target
    /// </summary>
    private void ApplyAcceleration(float angleDiff, float deltaTime)
    {
        float desiredDirection = Mathf.Sign(angleDiff);
        currentAngularVelocity += desiredDirection * cursorMechanics.angularAcceleration * deltaTime;
        currentAngularVelocity = Mathf.Clamp(currentAngularVelocity, -cursorMechanics.maxAngularVelocity, cursorMechanics.maxAngularVelocity);
    }

    /// <summary>
    /// Smoothly interpolates the orbit radius toward the target radius
    /// </summary>
    private void UpdateOrbitRadius(float deltaTime)
    {
        float targetRadius = targetOrbitPosition.magnitude;
        currentOrbitRadius = Mathf.Lerp(currentOrbitRadius, targetRadius, cursorMechanics.cursorFollowSpeed * deltaTime);
    }

    /// <summary>
    /// Updates the orbit position based on current angle and radius
    /// </summary>
    private void UpdateOrbitPosition()
    {
        currentOrbitPosition = new Vector2(
            Mathf.Cos(currentAngle * Mathf.Deg2Rad),
            Mathf.Sin(currentAngle * Mathf.Deg2Rad)
        ) * currentOrbitRadius;
    }

    #endregion

    /// <summary>
    /// Updates weapon movement metrics (speed, angular distance)
    /// </summary>
    private void UpdateWeaponMetrics(float deltaTime)
    {
        if (deltaTime <= 0) return;

        Vector2 currentPos = weaponTransform.localPosition;
        currentWeaponSpeed = ((Vector2)currentPos - lastPosition).magnitude / deltaTime;
        lastPosition = currentPos;
        angularDistance = Mathf.DeltaAngle(previousAngle, currentAngle);
    }

    /// <summary>
    /// Updates the smoothed speed average using a moving window
    /// </summary>
    private void UpdateSpeedAverage()
    {
        if (cursorMechanics == null || cursorMechanics.speedAverageFrames <= 1)
        {
            smoothedWeaponSpeed = currentWeaponSpeed;
            return;
        }

        // Maintain fixed-size queue for moving average
        if (speedHistory.Count >= cursorMechanics.speedAverageFrames)
            speedSum -= speedHistory.Dequeue();

        speedHistory.Enqueue(currentWeaponSpeed);
        speedSum += currentWeaponSpeed;

        float simpleAverage = speedSum / speedHistory.Count;
        smoothedWeaponSpeed = CalculateWeightedAverage(simpleAverage);
    }

    /// <summary>
    /// Calculates weighted average between current speed and historical average
    /// </summary>
    private float CalculateWeightedAverage(float simpleAverage)
    {
        if (cursorMechanics.currentFrameWeight >= 1f) return currentWeaponSpeed;
        if (cursorMechanics.currentFrameWeight <= 0f) return simpleAverage;

        return (currentWeaponSpeed * cursorMechanics.currentFrameWeight) +
               (simpleAverage * (1f - cursorMechanics.currentFrameWeight));
    }

    /// <summary>
    /// Rotates the weapon to face outward from the player
    /// </summary>
    private void RotateWeapon()
    {
        Vector2 outwardDirection = currentOrbitPosition.normalized;
        weaponTransform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(outwardDirection.y, outwardDirection.x) * Mathf.Rad2Deg);
    }

    #endregion

    #region Collision Detection

    /// <summary>
    /// Checks for collisions with enemies currently inside the weapon collider
    /// </summary>
    private void CheckNormalCollisions()
    {
        if (weaponCollisionHandler == null) return;

        foreach (var collider in weaponCollisionHandler.GetCurrentCollisions())
        {
            if (collider != null && !hitThisFrame.Contains(collider))
                HitCharacter(collider.gameObject);
        }
    }

    /// <summary>
    /// Determines if swept collisions should be checked this frame
    /// </summary>
    private bool ShouldCheckSweptCollisions()
    {
        return cursorMechanics.alwaysUseSweptCollision || Mathf.Abs(angularDistance) > cursorMechanics.sweptCollisionAngleStep;
    }

    /// <summary>
    /// Checks for collisions along the weapon's movement path using ghost colliders
    /// </summary>
    private void CheckSweptCollisions()
    {
        if (Mathf.Abs(angularDistance) < MIN_ANGULAR_DISTANCE) return;

        ghostColliderPositions.Clear();

        // Calculate optimal sampling for swept collision detection
        float absAngularDistance = Mathf.Abs(angularDistance);
        float angleStep = CalculateOptimalAngleStep(absAngularDistance);
        int steps = CalculateStepCount(absAngularDistance, angleStep);

        // Place ghost colliders along the arc between previous and current positions
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)(steps + 1);
            float intermediateAngle = Mathf.LerpAngle(previousAngle, currentAngle, t);
            Vector2 intermediatePosition = CalculateOrbitPosition(intermediateAngle);

            CheckGhostCollider(intermediatePosition, intermediateAngle);
            ghostColliderPositions.Add(intermediatePosition);
        }

        // Also check at current position
        CheckGhostCollider(weaponTransform.position, currentAngle);
    }

    /// <summary>
    /// Calculates the optimal angle step for ghost collider placement
    /// </summary>
    private float CalculateOptimalAngleStep(float absAngularDistance)
    {
        float angleStep = cursorMechanics.sweptCollisionAngleStep;
        int maxColliders = cursorMechanics.maxGhostCollidersPerFrame;

        return absAngularDistance > angleStep * maxColliders
            ? absAngularDistance / maxColliders
            : angleStep;
    }

    /// <summary>
    /// Calculates the number of ghost collider steps needed
    /// </summary>
    private int CalculateStepCount(float absAngularDistance, float angleStep)
    {
        int maxColliders = cursorMechanics.maxGhostCollidersPerFrame;
        return Mathf.Min(maxColliders, Mathf.CeilToInt(absAngularDistance / angleStep));
    }

    /// <summary>
    /// Calculates position on orbit circle at a given angle
    /// </summary>
    private Vector2 CalculateOrbitPosition(float angle)
    {
        return (Vector2)character.transform.position +
               new Vector2(
                   Mathf.Cos(angle * Mathf.Deg2Rad),
                   Mathf.Sin(angle * Mathf.Deg2Rad)
               ) * currentOrbitRadius;
    }

    /// <summary>
    /// Checks for collisions at a specific ghost collider position
    /// </summary>
    private void CheckGhostCollider(Vector2 position, float intermediateAngle)
    {
        if (cursorMechanics == null || weaponCapsuleCollider == null || character == null) return;

        Vector2 toPosition = position - (Vector2)character.transform.position;
        Vector2 direction = toPosition.normalized;
        float rotationAngle = CalculateCapsuleRotation(toPosition);
        Vector2 scaledSize = GetScaledCapsuleSize();
        Vector2 capsuleCenter = CalculateCapsuleCenter(position, direction, scaledSize);

        // Perform capsule overlap check at ghost position
        Collider2D[] hits = Physics2D.OverlapCapsuleAll(
            capsuleCenter,
            scaledSize,
            weaponCapsuleCollider.direction,
            rotationAngle,
            cursorMechanics.enemyLayers
        );

        ProcessGhostColliderHits(hits);
    }

    /// <summary>
    /// Calculates the rotation angle for the capsule based on its direction
    /// </summary>
    private float CalculateCapsuleRotation(Vector2 toPosition)
    {
        float baseAngle = Mathf.Atan2(toPosition.y, toPosition.x) * Mathf.Rad2Deg;
        return weaponCapsuleCollider.direction == CapsuleDirection2D.Vertical
            ? baseAngle + 90f
            : baseAngle;
    }

    /// <summary>
    /// Gets the scaled capsule size accounting for weapon transform scale
    /// </summary>
    private Vector2 GetScaledCapsuleSize()
    {
        return new Vector2(
            weaponCapsuleWidth * weaponTransform.localScale.x,
            weaponCapsuleHeight * weaponTransform.localScale.y
        );
    }

    /// <summary>
    /// Calculates the center position of the capsule collider
    /// (Accounts for capsule base being at weapon position, not center)
    /// </summary>
    private Vector2 CalculateCapsuleCenter(Vector2 basePosition, Vector2 direction, Vector2 scaledSize)
    {
        float offsetDistance = weaponCapsuleCollider.direction == CapsuleDirection2D.Vertical
            ? scaledSize.y * 0.5f  // For vertical capsule, height extends along direction
            : scaledSize.x * 0.5f; // For horizontal capsule, width extends along direction

        return basePosition + direction * offsetDistance;
    }

    /// <summary>
    /// Processes hits from ghost collider checks
    /// </summary>
    private void ProcessGhostColliderHits(Collider2D[] hits)
    {
        foreach (var hit in hits)
        {
            if (hit == null || hitThisFrame.Contains(hit)) continue;
            if (IsInvalidTarget(hit.gameObject)) continue;

            HitCharacter(hit.gameObject);
        }
    }

    /// <summary>
    /// Checks if a target is invalid (player, weapon, or self)
    /// </summary>
    private bool IsInvalidTarget(GameObject target)
    {
        return target == character.gameObject ||
               target == gameObject ||
               target == weaponInstance;
    }

    #endregion

    #region Damage and Effects

    /// <summary>
    /// Applies damage and effects to a hit character
    /// </summary>
    private void HitCharacter(GameObject target)
    {
        if (IsInvalidTarget(target)) return;

        float damage = CalculateDamage();
        float iFrameDuration = CalculateIFrames();
        var condition = target.GetComponent<CharacterCondition>();

        if (condition == null || condition.HasStatusEffect("hit_cooldown")) return;

        ApplyDamageAndEffects(target, condition, damage, iFrameDuration);
        ApplyKnockback(target);

        hitThisFrame.Add(target.GetComponent<Collider2D>());
    }

    /// <summary>
    /// Applies damage and raises hit events
    /// </summary>
    private void ApplyDamageAndEffects(GameObject target, CharacterCondition condition, float damage, float iFrameDuration)
    {
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
    }

    /// <summary>
    /// Applies knockback force to the hit target
    /// </summary>
    private void ApplyKnockback(GameObject target)
    {
        var targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        float speedForKnockback = cursorMechanics.useAverageSpeedForKnockback ? smoothedWeaponSpeed : currentWeaponSpeed;
        float knockbackForce = Mathf.Min(
            cursorMechanics.baseKnockback + (speedForKnockback * cursorMechanics.speedKnockbackMultiplier),
            cursorMechanics.maxKnockback
        );

        Vector2 knockbackDir = (weaponTransform.position - character.transform.position).normalized;
        if (knockbackDir.magnitude < 0.1f) knockbackDir = currentOrbitPosition.normalized;

        targetRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
    }

    /// <summary>
    /// Calculates damage based on weapon speed and configuration
    /// </summary>
    public float CalculateDamage()
    {
        float damage = cursorMechanics.baseDamage;

        // Apply velocity multiplier from character movement
        if (rb != null)
        {
            float velocity = rb.linearVelocity.magnitude;
            damage *= CalculateVelocityMultiplier(velocity, cursorMechanics);
        }

        // Calculate damage multiplier based on weapon speed
        float maxSpeed = GetMaxSpeed();
        float effectiveSpeed = cursorMechanics.useAverageSpeedForDamage ? smoothedWeaponSpeed : currentWeaponSpeed;
        float speedPercent = Mathf.Clamp01(effectiveSpeed / maxSpeed);

        float damageRange = Mathf.InverseLerp(
            cursorMechanics.minDamageMultiplierSpeedPercent,
            cursorMechanics.maxDamageMultiplierSpeedPercent,
            speedPercent
        );

        return damage * Mathf.Lerp(
            cursorMechanics.minDamageMultiplier,
            cursorMechanics.maxDamageMultiplier,
            damageRange
        );
    }

    /// <summary>
    /// Calculates invincibility duration based on weapon speed
    /// </summary>
    private float CalculateIFrames()
    {
        float maxSpeed = GetMaxSpeed();
        float effectiveSpeed = cursorMechanics.useAverageSpeedForDamage ? smoothedWeaponSpeed : currentWeaponSpeed;
        float speedPercent = Mathf.Clamp01(effectiveSpeed / maxSpeed);

        float invincibilityRange = Mathf.InverseLerp(
            cursorMechanics.minInvincibilitySpeedPercent,
            cursorMechanics.maxInvincibilitySpeedPercent,
            speedPercent
        );

        return Mathf.Lerp(
            cursorMechanics.minInvincibilityDuration,
            cursorMechanics.maxInvincibilityDuration,
            invincibilityRange
        );
    }

    /// <summary>
    /// Gets the maximum possible speed for the current movement mode
    /// </summary>
    private float GetMaxSpeed()
    {
        return cursorMechanics.movementMode switch
        {
            MovementMode.Direct => cursorMechanics.cursorFollowSpeed,
            MovementMode.Acceleration => cursorMechanics.maxAngularVelocity * Mathf.Deg2Rad * cursorMechanics.maxOrbitRadius,
            _ => cursorMechanics.cursorFollowSpeed
        };
    }

    #endregion

    #region Collision Callbacks

    /// <summary>
    /// Called when weapon trigger enters another collider
    /// </summary>
    public void OnWeaponTriggerEnter(Collider2D other) => TryHitCollider(other);

    /// <summary>
    /// Called when weapon trigger stays inside another collider
    /// </summary>
    public void OnWeaponTriggerStay(Collider2D other) => TryHitCollider(other);

    /// <summary>
    /// Attempts to hit a collider if conditions are met
    /// </summary>
    private void TryHitCollider(Collider2D other)
    {
        if (isSwinging && !hitThisFrame.Contains(other))
            HitCharacter(other.gameObject);
    }

    #endregion

    #region Debug Visualization

    /// <summary>
    /// Draws debug information when enabled
    /// </summary>
    private void DrawDebugInfo()
    {
        if (character == null) return;
        DrawOrbitCircles();
        DrawGhostColliders();
    }

    /// <summary>
    /// Draws min and max orbit circles
    /// </summary>
    private void DrawOrbitCircles()
    {
        Vector2 center = character.transform.position;
        DrawCircle(center, cursorMechanics.minOrbitRadius, 24, cursorVisual.minOrbitDebugColor);
        DrawCircle(center, cursorMechanics.maxOrbitRadius, 36, cursorVisual.maxOrbitDebugColor);
    }

    /// <summary>
    /// Draws a circle using line segments
    /// </summary>
    private void DrawCircle(Vector2 center, float radius, int segments, Color color)
    {
        float angleStep = 360f / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i;
            Vector2 nextPoint = center + new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ) * radius;

            Debug.DrawLine(prevPoint, nextPoint, color);
            prevPoint = nextPoint;
        }
    }

    /// <summary>
    /// Draws all ghost collider positions
    /// </summary>
    private void DrawGhostColliders()
    {
        if (ghostColliderPositions.Count == 0) return;

        foreach (Vector2 ghostPos in ghostColliderPositions)
        {
            DrawDebugCross(ghostPos, 0.1f, cursorVisual.sweptCollisionDebugColor);
            DrawDebugCapsule(ghostPos);
        }
    }

    /// <summary>
    /// Draws a cross marker at a position
    /// </summary>
    private void DrawDebugCross(Vector2 position, float size, Color color)
    {
        Debug.DrawLine(position - Vector2.up * size, position + Vector2.up * size, color);
        Debug.DrawLine(position - Vector2.right * size, position + Vector2.right * size, color);
    }

    /// <summary>
    /// Draws a capsule shape at a ghost collider position
    /// </summary>
    private void DrawDebugCapsule(Vector2 ghostPos)
    {
        Vector2 toGhost = ghostPos - (Vector2)character.transform.position;
        float angle = Mathf.Atan2(toGhost.y, toGhost.x) * Mathf.Rad2Deg;
        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * weaponCapsuleHeight * 0.5f;
        Vector2 capsuleStart = ghostPos;
        Vector2 capsuleEnd = ghostPos + direction * weaponCapsuleWidth;

        Debug.DrawLine(capsuleStart - perpendicular, capsuleEnd - perpendicular, cursorVisual.sweptCollisionDebugColor);
        Debug.DrawLine(capsuleStart + perpendicular, capsuleEnd + perpendicular, cursorVisual.sweptCollisionDebugColor);
        Debug.DrawLine(capsuleStart - perpendicular, capsuleStart + perpendicular, cursorVisual.sweptCollisionDebugColor);
        Debug.DrawLine(capsuleEnd - perpendicular, capsuleEnd + perpendicular, cursorVisual.sweptCollisionDebugColor);
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Cleans up weapon instances and collections
    /// </summary>
    protected override void CleanupManagers()
    {
        base.CleanupManagers();
        CleanupWeapon();
        ClearCollections();
    }

    /// <summary>
    /// Clears all collections and resets sums
    /// </summary>
    private void ClearCollections()
    {
        speedHistory?.Clear();
        speedSum = 0f;
        hitThisFrame?.Clear();
        ghostColliderPositions?.Clear();
    }

    #endregion
}