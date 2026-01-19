using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main grapple system controller that manages grapple behavior, physics, and visuals.
/// Now uses unified anchor system for all grapple points.
/// </summary>
public class CharacterGrapple : MonoBehaviour, ICharacterComponent
{
    [Header("References")]
    public Transform grappleOrigin;
    public LineRenderer grappleLine;

    [Header("Input")]
    public string grappleInput = "grapple_pressed";
    public bool useMouseAiming = true;

    [Header("Debug")]
    public bool showRaycastDebug = true;
    public bool showPhysicsDebug = true;
    public Color raycastHitColor = Color.green;
    public Color raycastMissColor = Color.red;

    // Subsystem managers
    private GrappleConfigManager configManager;
    private GrapplePhysicsCalculator physicsCalculator;
    private GrappleVisualManager visualManager;
    private GrappleSoundManager soundManager;

    // References to other components
    private CharacterCore character;
    private CharacterMovement movement;
    private Rigidbody2D rb;
    private CharacterEquipment equipment;
    private Camera mainCamera;

    // Current config (will be set from CharacterEquipment)
    private GrappleConfig currentConfig;

    // State variables
    private bool isGrappling = false;
    private Vector2 grapplePoint;
    private RaycastHit2D grappleHit;
    private float currentRopeLength;

    // Unified anchor system
    private GrappleAnchor currentGrappleAnchor;
    private GameObject anchorTargetObject; // The object we're anchored to

    // Input state
    private bool isJumpHeld = false;
    private bool isDownHeld = false;

    // Physics state
    private SwingArc swingArc;
    private Vector2 swingMomentum;
    private float momentumCaptureTimer = 0f;
    private const float MOMENTUM_CAPTURE_RATE = 0.1f;

    // Current grapple movement state (cached for updates)
    private MovementState currentGrappleMovementState;

    // Computed properties for reeling
    private bool ShouldReel => isGrappling && isJumpHeld && !isDownHeld;
    private bool ShouldUnreel => isGrappling && isDownHeld && !isJumpHeld;

    //////////////////////// Initialization ////////////////////////

    public void Initialize(CharacterCore core)
    {
        character = core;
        movement = character.GetComponent<CharacterMovement>();
        rb = character.gameObject.GetComponent<Rigidbody2D>();
        equipment = character.GetComponent<CharacterEquipment>();

        // Check if we have a CharacterEquipment component
        if (equipment == null)
        {
            Debug.LogError("CharacterGrapple requires CharacterEquipment component on the same GameObject");
            return;
        }

        // Subscribe to equipment events to get grapple config updates
        equipment.OnGrappleHookChanged += OnGrappleHookChanged;

        // Get initial grapple config from equipment
        currentConfig = equipment.CurrentGrappleHook;
        if (currentConfig != null)
        {
            LoadConfig(currentConfig);
        }

        // Register for character events
        character.OnEvent += HandleEvent;
        character.CharacterContext["grapple_isGrappling"] = false;

        // Cache camera reference for mouse aiming
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Debug.Log($"Grapple system using camera: {mainCamera.name}");
        }
        else
        {
            Debug.LogWarning("No main camera found for grapple aiming");
        }
    }

    private void LoadConfig(GrappleConfig config)
    {
        if (config == null) return;

        currentConfig = config;

        // Initialize config manager with the config
        configManager = new GrappleConfigManager(config);

        // Initialize subsystem managers
        physicsCalculator = new GrapplePhysicsCalculator(config.physicsConfig);
        visualManager = new GrappleVisualManager(
            config.visualConfig,
            grappleOrigin,
            grappleLine,
            showPhysicsDebug
        );

        // Initialize sound manager
        soundManager = new GrappleSoundManager(
            config.soundConfig,
            this
        );
    }

    private void OnGrappleHookChanged(GrappleConfig newConfig)
    {
        if (newConfig == null)
        {
            Debug.Log("Grapple hook unequipped");

            // Stop current grapple if active
            if (isGrappling)
            {
                StopGrapple();
            }

            // Clean up managers
            CleanupManagers();
            currentConfig = null;
            return;
        }

        Debug.Log($"Switching to grapple hook: {newConfig.GrappleName}");

        // Stop current grapple if active
        if (isGrappling)
        {
            StopGrapple();
        }

        // Initialize with new config
        LoadConfig(newConfig);
    }

    private void CleanupManagers()
    {
        // Clean up visual manager
        if (visualManager != null)
        {
            visualManager.CleanupGrappleVisuals();
        }

        // Clean up sound manager
        if (soundManager != null)
        {
            soundManager.Cleanup();
        }

        // Reset managers
        configManager = null;
        physicsCalculator = null;
        visualManager = null;
        soundManager = null;
    }

    public void Tick(float deltaTime)
    {
        // Early exit if no config is equipped
        if (currentConfig == null) return;

        // Update grapple point from anchor
        if (isGrappling && currentGrappleAnchor != null)
        {
            UpdateGrapplePointFromAnchor();
        }

        // Draw raycast debug when not grappling
        if (showRaycastDebug && !isGrappling)
        {
            Vector2 aimDir = GetAimDirection();
            visualManager.DrawRaycastDebug(
                grappleOrigin.position,
                aimDir,
                currentConfig.mechanicsConfig.grappleLayers,
                currentConfig.physicsConfig.maxDistance,
                raycastHitColor,
                raycastMissColor
            );
        }

        // Update momentum capture timer
        momentumCaptureTimer += deltaTime;

        // Update visual elements if grappling
        if (isGrappling)
        {
            RopeState ropeState = physicsCalculator.GetRopeState(
                Vector2.Distance(grappleOrigin.position, grapplePoint),
                currentRopeLength
            );

            visualManager.UpdateGrappleVisuals(
                grapplePoint,
                currentRopeLength,
                isGrappling,
                ShouldReel,
                ShouldUnreel,
                ropeState,
                swingArc
            );

            // Update sound position if grappling
            if (soundManager != null)
            {
                soundManager.UpdateCreakPosition(grappleOrigin.position, grapplePoint);
            }
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Early exit if no config is equipped or not grappling
        if (currentConfig == null || !isGrappling) return;

        // Update dynamic movement state based on current speed
        UpdateDynamicMovementState(fixedDeltaTime);

        // Capture swing momentum at regular intervals
        if (momentumCaptureTimer >= MOMENTUM_CAPTURE_RATE)
        {
            CaptureSwingMomentum();
            momentumCaptureTimer = 0f;
        }

        // Apply swing physics
        UpdateSwingPhysics(fixedDeltaTime);

        // Handle reeling/unreeling based on input
        if (ShouldReel)
        {
            UpdateReeling(fixedDeltaTime);
        }
        else if (ShouldUnreel)
        {
            UpdateUnreeling(fixedDeltaTime);
        }
    }

    //////////////////////// Anchor System ////////////////////////

    private void UpdateGrapplePointFromAnchor()
    {
        if (currentGrappleAnchor != null)
        {
            // Get updated grapple point from anchor
            grapplePoint = currentGrappleAnchor.GetWorldPosition();
        }
    }

    //////////////////////// Input Handling ////////////////////////

    private void HandleEvent(string type, object data)
    {
        // Early exit if no config is equipped
        if (currentConfig == null) return;

        if (type == grappleInput)
        {
            if (!isGrappling)
            {
                // Determine initial reeling direction based on held buttons
                int initialReelDirection = 0;
                if (isJumpHeld) initialReelDirection += 1;
                if (isDownHeld) initialReelDirection -= 1;

                TryStartGrapple(initialReelDirection);
            }
            else
            {
                StopGrapple();
            }
        }
        else if (type == "grapple_released" && isGrappling)
        {
            StopGrapple();
            ApplyBoost();
        }
        else if (type == "jump_pressed")
        {
            isJumpHeld = true;
        }
        else if (type == "jump_released")
        {
            isJumpHeld = false;
        }
        else if (type == "down_pressed")
        {
            isDownHeld = true;
        }
        else if (type == "down_released")
        {
            isDownHeld = false;
        }
    }

    //////////////////////// Grapple Lifecycle ////////////////////////

    private void TryStartGrapple(int initialReelDirection = 0)
    {
        if (currentConfig == null) return;

        Vector2 aimDir = GetAimDirection();

        // Perform grapple raycast using physics calculator
        grappleHit = physicsCalculator.PerformGrappleRaycast(
            grappleOrigin.position,
            aimDir,
            currentConfig.mechanicsConfig.grappleLayers,
            currentConfig.physicsConfig.maxDistance
        );

        if (grappleHit.collider != null)
        {
            // Get or create an anchor on the hit object
            GameObject hitObject = grappleHit.collider.gameObject;

            // Check if we can grapple to this target
            bool canCreateAnchors = currentConfig.mechanicsConfig.createsAnchors;
            currentGrappleAnchor = GrappleAnchorSystem.GetOrCreateAnchor(
                hitObject,
                grappleHit.point,
                canCreateAnchors
            );

            if (currentGrappleAnchor == null)
            {
                // Grapple failed - either no anchor and can't create one,
                // or anchor requires repositioning but grapple can't do it
                Debug.Log("Grapple failed - insufficient anchor capability");

                return;
            }

            anchorTargetObject = hitObject;

            // Start grapple using the anchor
            StartGrapple(initialReelDirection);
        }
        else
        {
            Debug.Log("Grapple missed - no valid target");
        }
    }

    private void StartGrapple(int initialReelDirection = 0)
    {
        if (currentConfig == null || currentGrappleAnchor == null) return;

        isGrappling = true;
        character.CharacterContext["grapple_isGrappling"] = true;

        // Get initial grapple point from anchor
        grapplePoint = currentGrappleAnchor.GetWorldPosition();
        currentRopeLength = Vector2.Distance(grappleOrigin.position, grapplePoint);

        // Initialize swing arc for circular motion calculations
        swingArc = physicsCalculator.CalculateSwingArc(grappleOrigin.position, grapplePoint, currentRopeLength);

        // Reset physics state
        swingMomentum = Vector2.zero;
        momentumCaptureTimer = 0f;

        // Create initial grapple movement state
        float horizontalSpeed = Mathf.Abs(rb.linearVelocity.x);
        currentGrappleMovementState = CreateDynamicGrappleMovementState(horizontalSpeed);

        // Set grapple as base state with high priority
        currentGrappleMovementState.type = MovementStateType.Base;
        currentGrappleMovementState.priority = 30; // Higher than dash and post-dash

        character.RaiseEvent("movement_base_set", currentGrappleMovementState);
        character.RaiseEvent("grapple_started", grapplePoint);

        // Create visual elements
        visualManager.InstantiateGrappleVisuals(grapplePoint);

        if (soundManager != null)
        {
            soundManager.PlayLaunchSound();
            soundManager.StartCreakSounds();
        }
    }

    private void StopGrapple()
    {
        if (!isGrappling) return;

        isGrappling = false;
        character.CharacterContext["grapple_isGrappling"] = false;

        // Clean up auto-created anchor if needed
        if (currentGrappleAnchor != null)
        {
            GrappleAnchorSystem.CleanupAnchor(currentGrappleAnchor);
        }

        // Reset anchor tracking
        currentGrappleAnchor = null;
        anchorTargetObject = null;

        // Clean up visual elements
        visualManager.CleanupGrappleVisuals();

        // Stop creak sounds
        if (soundManager != null)
        {
            soundManager.StopCreakSounds();
        }

        // Clear the grapple base state and notify other systems about grapple end
        character.RaiseEvent("movement_base_clear", currentGrappleMovementState.name);
        character.RaiseEvent("grapple_ended", grapplePoint);
    }

    //////////////////////// Swing Physics //////////////////////////

    private void UpdateSwingPhysics(float fixedDeltaTime)
    {
        Vector2 playerPos = grappleOrigin.position;
        Vector2 toGrapple = grapplePoint - playerPos;
        float currentDistance = toGrapple.magnitude;

        // Update swing arc for current position
        swingArc = physicsCalculator.CalculateSwingArc(playerPos, grapplePoint, currentRopeLength);

        // Get current rope state (stretch/squash)
        RopeState ropeState = physicsCalculator.GetRopeState(currentDistance, currentRopeLength);

        float restoringForceMagnitude = 0;

        // Apply rope physics if stretching or squashing
        if (ropeState.ratio != 0f)
        {
            restoringForceMagnitude = ApplySwingPhysics(ropeState.ratio, ropeState.isStretch, currentDistance, fixedDeltaTime);
        }

        // Update creak volume with simple call
        if (soundManager != null && currentConfig.soundConfig != null)
        {
            soundManager.UpdateCreakVolume(
                restoringForceMagnitude,
                currentConfig.soundConfig.creakMinForce,
                currentConfig.soundConfig.creakMaxForce
            );
        }

        // Apply friction (always active)
        ApplyFriction(fixedDeltaTime);

        // Check for detachment (rope too long)
        if (currentDistance > currentConfig.physicsConfig.maxDistance * 1.5f)
        {
            StopGrapple();
        }
    }

    private float ApplySwingPhysics(float ratio, bool isStretch, float currentDistance, float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        Vector2 radialDirection = toGrapple.normalized;
        Vector2 tangentDirection = new Vector2(-radialDirection.y, radialDirection.x);

        // Calculate dynamic stiffness based on displacement
        float dynamicStiffness = physicsCalculator.CalculateDynamicStiffness(ratio, isStretch);

        // Calculate displacement magnitude
        float displacement = Mathf.Abs(ratio) * currentRopeLength;

        // Calculate and apply restoring force
        float restoringForceMagnitude = dynamicStiffness * displacement;
        Vector2 restoringForce = isStretch ?
            radialDirection * restoringForceMagnitude :      // Pull toward grapple point
            -radialDirection * restoringForceMagnitude;     // Push away from grapple point

        // Apply force to player
        rb.AddForce(restoringForce, ForceMode2D.Force);

        // Apply reaction force to anchored object if applicable
        ApplyReactionForceToAnchor(restoringForce);

        // Calculate velocity components
        Vector2 radialVelocity = Vector2.Dot(rb.linearVelocity, radialDirection) * radialDirection;
        Vector2 tangentVelocity = Vector2.Dot(rb.linearVelocity, tangentDirection) * tangentDirection;

        float radialSpeed = Vector2.Dot(rb.linearVelocity, radialDirection);
        float tangentSpeed = tangentVelocity.magnitude;

        // Calculate angular velocity
        float angularVelocity = 0f;
        if (currentRopeLength > 0.1f)
        {
            angularVelocity = tangentSpeed / currentRopeLength;
        }

        // Only apply damping when either:
        // 1. We have high angular velocity (swinging fast)
        // 2. Radial motion is increasing displacement
        bool shouldApplyDamping = false;
        bool isSwinging = angularVelocity > 0.15f; // ~8.5 degrees per second
        bool isProblematicRadialMotion = false;
        if (isStretch) // Stretching
        {
            isProblematicRadialMotion = radialSpeed < -0.01f; // Moving inward
        }
        else // Squashing
        {
            isProblematicRadialMotion = radialSpeed > 0.01f; // Moving outward
        }
        shouldApplyDamping = isSwinging || isProblematicRadialMotion;

        // Apply damping
        if (shouldApplyDamping && radialVelocity.magnitude > 0.1f)
        {
            Vector2 dampingForce = -radialVelocity.normalized *
                                  (radialVelocity.magnitude * currentConfig.physicsConfig.ropeDamping * dynamicStiffness);
            rb.AddForce(dampingForce, ForceMode2D.Force);

            // Apply reaction force for damping too
            ApplyReactionForceToAnchor(dampingForce);
        }

        return restoringForceMagnitude;
    }

    private void ApplyFriction(float fixedDeltaTime)
    {
        // Apply general swing friction (affects all velocity)
        rb.linearVelocity *= 1 - currentConfig.physicsConfig.friction;

        // Apply tangential friction (only affects motion perpendicular to rope)
        ApplyTangentialFriction(fixedDeltaTime);
    }

    private void ApplyTangentialFriction(float fixedDeltaTime)
    {
        // Calculate base tangential friction
        float effectiveTangentialFriction = currentConfig.physicsConfig.tangentialFriction;

        // Add reeling/unreeling friction based on state
        float reelingFrictionAdjustment = CalculateReelingFrictionAdjustment();
        effectiveTangentialFriction += reelingFrictionAdjustment;

        // Clamp to valid range (0 to 1 for positive, -1 to 0 for negative)
        if (effectiveTangentialFriction > 0)
            effectiveTangentialFriction = Mathf.Clamp(effectiveTangentialFriction, 0f, 1f);
        else
            effectiveTangentialFriction = Mathf.Clamp(effectiveTangentialFriction, -1f, 0f);

        // Early return if tangential friction is effectively zero
        if (Mathf.Abs(effectiveTangentialFriction) < 0.001f) return;

        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;

        // Don't apply if we're very close to the grapple point
        if (toGrapple.magnitude < 0.1f) return;

        Vector2 radialDirection = toGrapple.normalized;

        // Calculate tangential component of velocity
        // This is velocity perpendicular to the rope direction
        float radialSpeed = Vector2.Dot(rb.linearVelocity, radialDirection);
        Vector2 radialVelocity = radialDirection * radialSpeed;
        Vector2 tangentialVelocity = rb.linearVelocity - radialVelocity;

        // Only apply friction if we have significant tangential motion
        if (tangentialVelocity.magnitude > 0.1f)
        {
            // Apply tangential friction (only reduces tangential component)
            Vector2 newTangentialVelocity = tangentialVelocity * (1 - effectiveTangentialFriction);

            // Recombine: radial velocity stays the same, tangential gets friction
            rb.linearVelocity = radialVelocity + newTangentialVelocity;

            // Debug visualization - FIXED: Using effectiveTangentialFriction instead of undefined effectiveFriction
            if (showPhysicsDebug)
            {
                DrawTangentialFrictionDebug(tangentialVelocity, effectiveTangentialFriction, fixedDeltaTime);
            }
        }
    }

    private void DrawTangentialFrictionDebug(Vector2 tangentialVelocity, float effectiveFriction, float fixedDeltaTime)
    {
        // Color code: green for reeling, red for unreeling, orange for normal
        Color frictionColor;
        if (ShouldReel)
            frictionColor = Color.green;
        else if (ShouldUnreel)
            frictionColor = Color.red;
        else
            frictionColor = new Color(1f, 0.5f, 0f); // Orange

        // Show tangential velocity direction
        Debug.DrawRay(grappleOrigin.position, tangentialVelocity.normalized * 2f,
                     Color.cyan, fixedDeltaTime);

        // Show friction force (opposite to tangential velocity)
        float frictionVisualScale = Mathf.Clamp(Mathf.Abs(effectiveFriction) * 5f, 1f, 3f);
        Debug.DrawRay(grappleOrigin.position,
                     -tangentialVelocity.normalized * frictionVisualScale,
                     frictionColor, fixedDeltaTime);
    }

    private float CalculateReelingFrictionAdjustment()
    {
        if (!isGrappling) return 0f;

        // Calculate current tangential speed
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        if (toGrapple.magnitude < 0.1f) return 0f;

        Vector2 radialDirection = toGrapple.normalized;
        Vector2 tangentialDirection = new Vector2(-radialDirection.y, radialDirection.x);
        float tangentialSpeed = Mathf.Abs(Vector2.Dot(rb.linearVelocity, tangentialDirection));

        if (ShouldReel)
        {
            return GetVelocityScaledFriction(
                tangentialSpeed,
                currentConfig.physicsConfig.minReelingTangentialFriction,
                currentConfig.physicsConfig.maxReelingTangentialFriction,
                currentConfig.physicsConfig.maxReelingFrictionVelocity,
                currentConfig.physicsConfig.minReelingFrictionVelocity
            );
        }
        else if (ShouldUnreel)
        {
            return GetVelocityScaledFriction(
                tangentialSpeed,
                currentConfig.physicsConfig.minUnreelingTangentialFriction,
                currentConfig.physicsConfig.maxUnreelingTangentialFriction,
                currentConfig.physicsConfig.maxReelingFrictionVelocity,
                currentConfig.physicsConfig.minReelingFrictionVelocity
            );
        }

        return 0f;
    }

    private float GetVelocityScaledFriction(
        float currentSpeed,
        float minFriction,
        float maxFriction,
        float minSpeedForMaxFriction,   // Lower speed = more friction
        float minSpeedForMinFriction)   // Higher speed = less friction
    {
        // Ensure valid speed range
        if (minSpeedForMinFriction <= minSpeedForMaxFriction)
        {
            // Invalid range, use max friction as default
            return maxFriction;
        }

        // Calculate interpolation factor (0 at low speed, 1 at high speed)
        float t = Mathf.InverseLerp(minSpeedForMaxFriction, minSpeedForMinFriction, currentSpeed);

        // Clamp t to 0-1 range
        t = Mathf.Clamp01(t);

        // Use smoothstep for smoother transitions at boundaries
        float smoothT = t * t * (3f - 2f * t);

        // Interpolate between max and min friction
        return Mathf.Lerp(maxFriction, minFriction, smoothT);
    }

    private void ApplyBoost()
    {
        if (swingMomentum.magnitude < currentConfig.physicsConfig.minBoostVelocity)
        {
            return;
        }

        // Calculate boost strength based on captured momentum
        float momentumMagnitude = swingMomentum.magnitude;
        float boostStrength = momentumMagnitude * (currentConfig.physicsConfig.boostMultiplier - 1f);
        Vector2 boostDirection = swingMomentum.normalized;

        // Apply boost to movement system
        movement.AddExternalVelocity(boostDirection * boostStrength);

        // Notify other systems about boost
        character.RaiseEvent("grapple_boost_applied", new GrappleBoostData
        {
            direction = boostDirection,
            strength = boostStrength,
            momentum = swingMomentum
        });
    }

    //////////////////////// Reeling System ////////////////////////

    private void UpdateReeling(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        float currentDistance = toGrapple.magnitude;

        // Base reel speed
        float effectiveReelSpeed = currentConfig.reelConfig.reelSpeed;

        // Increase reel speed when there's slack
        if (currentDistance < currentRopeLength)
        {
            float slackRatio = 1f - (currentDistance / currentRopeLength);
            effectiveReelSpeed *= Mathf.Lerp(1f, currentConfig.reelConfig.slackReelMultiplier, slackRatio);
        }

        // Calculate and apply new rope length
        float targetLength = Mathf.Max(currentConfig.reelConfig.minRopeLength,
            currentRopeLength - effectiveReelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, currentConfig.reelConfig.reelSmoothness);
    }

    private void UpdateUnreeling(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        float currentDistance = toGrapple.magnitude;

        // Calculate and apply new rope length
        float targetLength = Mathf.Min(currentConfig.reelConfig.maxRopeLength,
            currentRopeLength + currentConfig.reelConfig.unreelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, currentConfig.reelConfig.unreelSmoothness);
    }


    //////////////////////// Dynamic MovementState //////////////////////////

    private MovementState CreateDynamicGrappleMovementState(float horizontalSpeed)
    {
        // Start with the base grapple movement state
        var movementState = currentConfig.movementState.ToMovementState();

        // Override the ground deceleration based on current speed
        movementState.groundDecelerationMultiplier = CalculateDynamicGroundDeceleration(horizontalSpeed);

        return movementState;
    }

    private float CalculateDynamicGroundDeceleration(float horizontalSpeed)
    {
        if (physicsCalculator == null || currentConfig == null)
            return currentConfig.movementState.groundDecelerationMultiplier;

        // Get the air deceleration multiplier from the grapple movement state
        float airDecelerationMultiplier = currentConfig.movementState.airDecelerationMultiplier;
        float currentStateGroundDeceleration = currentConfig.movementState.groundDecelerationMultiplier;

        return physicsCalculator.CalculateVelocityBasedGroundDeceleration(
            horizontalSpeed,
            currentStateGroundDeceleration,
            airDecelerationMultiplier
        );
    }

    private void UpdateDynamicMovementState(float fixedDeltaTime)
    {
        float horizontalSpeed = Mathf.Abs(rb.linearVelocity.x);
        float newGroundDecelerationMultiplier = CalculateDynamicGroundDeceleration(horizontalSpeed);

        if (Mathf.Abs(currentGrappleMovementState.groundDecelerationMultiplier - newGroundDecelerationMultiplier) > 0.001f)
        {
            currentGrappleMovementState.groundDecelerationMultiplier = newGroundDecelerationMultiplier;

            // Update the grapple base state
            character.RaiseEvent("movement_base_set", currentGrappleMovementState);
        }
    }

    //////////////////////// Reaction Force ////////////////////////

    private void ApplyReactionForceToAnchor(Vector2 force)
    {
        // Early exit if not applicable
        if (!CheckCanApplyReactionForces())
            return;

        // Get the anchor's rigidbody
        Rigidbody2D anchorRb = GetAnchorRigidbody();
        if (anchorRb == null)
            return;

        // Calculate reaction force (Newton's 3rd Law with multiplier)
        Vector2 reactionForce = -force * currentConfig.mechanicsConfig.reactionForceMultiplier;

        // Apply to anchor
        anchorRb.AddForce(reactionForce, ForceMode2D.Force);

        // Debug visualization
        if (showPhysicsDebug && currentGrappleAnchor != null)
        {
            GrappleAnchor effectiveAnchor = currentGrappleAnchor.GetEffectiveAnchor();
            Debug.DrawRay(effectiveAnchor.GetWorldPosition(), reactionForce.normalized * 0.5f,
                         Color.red, Time.fixedDeltaTime);
        }
    }

    // Helper method to check if reaction forces are applicable
    private bool CheckCanApplyReactionForces()
    {
        if (currentGrappleAnchor == null || currentConfig == null)
            return false;

        // Check if grapple config allows reaction forces
        if (!currentConfig.mechanicsConfig.enableReactionForces)
            return false;

        // Get effective anchor
        GrappleAnchor effectiveAnchor = currentGrappleAnchor.GetEffectiveAnchor();

        // Check if anchor allows reaction forces
        if (!effectiveAnchor.GetCanReceiveReactionForces())
            return false;

        // Check if anchor has rigidbody
        if (effectiveAnchor.AnchorRigidbody == null)
            return false;

        // Check mass ratio
        Rigidbody2D anchorRb = effectiveAnchor.AnchorRigidbody;
        float massRatio = anchorRb.mass / rb.mass;
        if (massRatio > currentConfig.mechanicsConfig.maxAffectableMassRatio)
            return false;

        return true;
    }

    // Helper method to get anchor's rigidbody
    private Rigidbody2D GetAnchorRigidbody()
    {
        if (currentGrappleAnchor == null)
            return null;

        return currentGrappleAnchor.GetEffectiveAnchor().AnchorRigidbody;
    }

    //////////////////////// Helper Methods ////////////////////////

    private void CaptureSwingMomentum()
    {
        Vector2 currentVelocity = rb.linearVelocity;

        if (currentVelocity.magnitude > 0.1f)
        {
            swingMomentum = Vector2.Lerp(swingMomentum, currentVelocity, 0.3f);

            if (showPhysicsDebug)
            {
                Debug.DrawRay(transform.position, swingMomentum.normalized * 2f, Color.cyan, 0.2f);
            }
        }
    }

    private Vector2 GetAimDirection()
    {
        // Try mouse aiming first
        if (useMouseAiming && mainCamera != null)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mousePos = mouse.position.ReadValue();

                // Only use mouse if position is valid (not default 0,0)
                if (mousePos != Vector2.zero)
                {
                    Vector3 worldPos = mainCamera.ScreenToWorldPoint(
                        new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

                    Vector2 direction = worldPos - grappleOrigin.position;
                    if (direction.magnitude > 0.01f)
                    {
                        return direction.normalized;
                    }
                }
            }
        }

        // Default direction: up and to the right
        return (Vector2.up + Vector2.right).normalized;
    }


    //////////////////////// Public API /////////////////////////

    public bool IsGrappling() => isGrappling;
    public Vector2 GetGrapplePoint() => grapplePoint;
    public float GetRopeLength() => currentRopeLength;
    public SwingArc GetSwingArc() => swingArc;

    public bool IsAnchoredToPlatform() => currentGrappleAnchor != null && currentGrappleAnchor.IsOnMovingPlatform;
    public GrappleAnchor GetCurrentGrappleAnchor() => currentGrappleAnchor;
    public GameObject GetAnchorTargetObject() => anchorTargetObject;

    public RopeState? GetCurrentRopeState()
    {
        if (!isGrappling) return null;

        float currentDistance = Vector2.Distance(grappleOrigin.position, grapplePoint);
        return physicsCalculator.GetRopeState(currentDistance, currentRopeLength);
    }

    public Vector2 GetRadialDirection()
    {
        if (!isGrappling || swingArc == null) return Vector2.zero;
        return swingArc.radialDirection;
    }

    public Vector2 GetTangentDirection()
    {
        if (!isGrappling || swingArc == null) return Vector2.zero;
        return swingArc.tangentDirection;
    }

    public bool IsUnderGrappleForce()
    {
        if (!isGrappling) return false;

        // Check if we have any restoring force from rope physics
        float currentDistance = Vector2.Distance(grappleOrigin.position, grapplePoint);
        RopeState ropeState = physicsCalculator.GetRopeState(currentDistance, currentRopeLength);

        // We're under grapple force if rope is stretched OR squashed beyond threshold
        return (ropeState.isStretch || ropeState.isSquash) && Mathf.Abs(ropeState.ratio) > 0.01f;
    }

    /// <summary>
    /// Directly switch to a different grapple configuration (for compatibility with old system).
    /// Note: Use CharacterEquipment.EquipGrappleHook() instead for the new system.
    /// </summary>
    public void SwitchGrappleConfig(GrappleConfig newConfig)
    {
        if (newConfig == null) return;

        // Stop current grapple if active
        if (isGrappling)
        {
            StopGrapple();
        }

        // Clean up old managers
        CleanupManagers();

        // Initialize with new config
        LoadConfig(newConfig);
    }

    //////////////////////// Cleanup ///////////////////////////
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (equipment != null)
        {
            equipment.OnGrappleHookChanged -= OnGrappleHookChanged;
        }

        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        // Clean up sound manager
        if (soundManager != null)
        {
            soundManager.Cleanup();
        }
    }
}