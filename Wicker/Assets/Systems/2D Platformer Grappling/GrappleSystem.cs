using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main grapple system controller that manages grapple behavior, physics, and visuals.
/// Implements ICharacterComponent for integration with character systems.
/// Coordinates between ConfigManager, PhysicsCalculator, and VisualManager.
/// </summary>
public class GrappleSystem : MonoBehaviour, ICharacterComponent
{
    [Header("Configuration")]
    public GrappleMovementState grappleMovementState;
    public GrappleSwingPhysicsConfig grapplePhysicsConfig;
    public GrappleReelConfig grappleReelConfig;
    public GrappleVisualConfig visualConfig;

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

    // Input references
    private Camera mainCamera;

    // State variables
    private CharacterCore character;
    private PlatformerMovement movement;
    private Rigidbody2D rb;
    private bool isGrappling = false;
    private Vector2 grapplePoint;
    private RaycastHit2D grappleHit;
    private float currentRopeLength;

    // Input state
    private bool isJumpHeld = false;
    private bool isDownHeld = false;

    // Physics state
    private SwingArc swingArc;
    private Vector2 swingMomentum;
    private Vector2 previousVelocity;
    private float momentumCaptureTimer = 0f;
    private const float MOMENTUM_CAPTURE_RATE = 0.1f;

    // Computed properties for reeling
    private bool ShouldReel => isGrappling && isJumpHeld && !isDownHeld;
    private bool ShouldUnreel => isGrappling && isDownHeld && !isJumpHeld;

    // Debug state
    private Vector2 lastAimDirection;
    private float lastRaycastLength;
    private bool lastRaycastHit;

    //////////////////////// Initialization ////////////////////////

    /// <summary>
    /// Initializes the grapple system with character core reference.
    /// Sets up subsystem managers and registers for input events.
    /// </summary>
    /// <param name="core">CharacterCore component for event system integration.</param>
    public void Initialize(CharacterCore core)
    {
        character = core;
        movement = character.GetComponent<PlatformerMovement>();
        rb = character.gameObject.GetComponent<Rigidbody2D>();

        // Initialize subsystem managers
        configManager = new GrappleConfigManager(
            grappleMovementState,
            grapplePhysicsConfig,
            grappleReelConfig,
            visualConfig
        );

        physicsCalculator = new GrapplePhysicsCalculator(grapplePhysicsConfig);
        visualManager = new GrappleVisualManager(
            visualConfig,
            grappleOrigin,
            grappleLine,
            showPhysicsDebug
        );

        // Register for character events
        character.OnEvent += HandleEvent;

        // Cache camera reference for mouse aiming
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("No main camera found for grapple aiming");
        }
    }

    /// <summary>
    /// Called every frame for visual updates and input processing.
    /// Updates debug visuals and grapple momentum capture.
    /// </summary>
    /// <param name="deltaTime">Time since last frame in seconds.</param>
    public void Tick(float deltaTime)
    {
        // Draw raycast debug when not grappling
        if (showRaycastDebug && !isGrappling)
        {
            Vector2 aimDir = GetAimDirection();
            visualManager.DrawRaycastDebug(
                grappleOrigin.position,
                aimDir,
                grapplePhysicsConfig.grappleLayers,
                grapplePhysicsConfig.maxDistance,
                raycastHitColor,
                raycastMissColor
            );
        }

        // Update momentum capture timer
        momentumCaptureTimer += deltaTime;

        // Update visual elements if grappling
        if (isGrappling)
        {
            RopeState ropeState = configManager.GetRopeState(
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
        }
    }

    /// <summary>
    /// Called at fixed time intervals for physics updates.
    /// Handles swing physics, reeling, and momentum updates.
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for physics calculations.</param>
    public void PhysicsTick(float fixedDeltaTime)
    {
        if (isGrappling)
        {
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
    }

    //////////////////////// Input Handling ////////////////////////

    /// <summary>
    /// Handles character events for grapple input and button states.
    /// Processes grapple activation, reeling input, and button holds.
    /// </summary>
    /// <param name="type">Event type identifier.</param>
    /// <param name="data">Event data payload.</param>
    private void HandleEvent(string type, object data)
    {
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

    /// <summary>
    /// Attempts to start a grapple by casting a ray in the aim direction.
    /// If a valid surface is hit, initiates grappling at the hit point.
    /// </summary>
    /// <param name="initialReelDirection">Initial reeling direction (-1: unreel, 0: none, 1: reel).</param>
    private void TryStartGrapple(int initialReelDirection = 0)
    {
        lastAimDirection = GetAimDirection();

        // Perform grapple raycast using physics calculator
        grappleHit = physicsCalculator.PerformGrappleRaycast(
            grappleOrigin.position,
            lastAimDirection,
            grapplePhysicsConfig.grappleLayers,
            grapplePhysicsConfig.maxDistance
        );

        lastRaycastHit = grappleHit.collider != null;
        lastRaycastLength = grappleHit.collider != null ? grappleHit.distance : grapplePhysicsConfig.maxDistance;

        if (grappleHit.collider != null)
        {
            StartGrapple(grappleHit.point, initialReelDirection);
        }
        else
        {
            Debug.Log("Grapple missed - no valid target");
        }
    }

    /// <summary>
    /// Starts grappling at the specified point.
    /// Initializes rope length, swing arc, and visual elements.
    /// </summary>
    /// <param name="point">World position where grapple attaches.</param>
    /// <param name="initialReelDirection">Initial reeling direction.</param>
    private void StartGrapple(Vector2 point, int initialReelDirection = 0)
    {
        isGrappling = true;
        grapplePoint = point;
        currentRopeLength = Vector2.Distance(grappleOrigin.position, point);

        // Initialize swing arc for circular motion calculations
        swingArc = physicsCalculator.CalculateSwingArc(grappleOrigin.position, point, currentRopeLength);

        // Reset physics state
        swingMomentum = Vector2.zero;
        momentumCaptureTimer = 0f;
        previousVelocity = rb.linearVelocity;

        // Apply grapple movement state override
        var movementState = (grappleMovementState != null) ?
            grappleMovementState.ToMovementState() :
            CreateDefaultMovementState();

        // Notify other systems about grapple start
        character.RaiseEvent("movement_override_start", movementState);
        character.RaiseEvent("grapple_started", grapplePoint);

        // Create visual elements
        visualManager.InstantiateGrappleVisuals(point);
    }

    /// <summary>
    /// Stops grappling and cleans up all associated systems.
    /// Removes visual elements and restores normal movement state.
    /// </summary>
    private void StopGrapple()
    {
        if (!isGrappling) return;

        isGrappling = false;

        // Clean up visual elements
        visualManager.CleanupGrappleVisuals();

        // Notify other systems about grapple end
        character.RaiseEvent("movement_override_end", null);
        character.RaiseEvent("grapple_ended", grapplePoint);
    }

    /// <summary>
    /// Creates a default movement state for grappling.
    /// Used when no custom grapple movement state is configured.
    /// </summary>
    /// <returns>Default PlatformerMovement.MovementState for grappling.</returns>
    private PlatformerMovement.MovementState CreateDefaultMovementState()
    {
        return new PlatformerMovement.MovementState(
            name: "Grappling",
            allowMovement: true,
            applyGravity: true,
            applyDeceleration: true,
            canJump: false,
            gravityMultiplier: 1f,
            accelerationMultiplier: 1f,
            airAccelerationMultiplier: 0.025f,
            decelerationMultiplier: 1f,
            airDecelerationMultiplier: 0.025f,
            jumpForceMultiplier: 0f,
            maxSpeedMultiplier: 1f
        );
    }

    //////////////////////// Swing Physics ////////////////////////

    /// <summary>
    /// Updates all swing physics calculations for the current frame.
    /// Handles rope state, restoring forces, and circular motion.
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for physics calculations.</param>
    private void UpdateSwingPhysics(float fixedDeltaTime)
    {
        Vector2 playerPos = grappleOrigin.position;
        Vector2 toGrapple = grapplePoint - playerPos;
        float currentDistance = toGrapple.magnitude;

        // Update swing arc for current position
        swingArc = physicsCalculator.CalculateSwingArc(playerPos, grapplePoint, currentRopeLength);

        // Get current rope state (stretch/squash)
        RopeState ropeState = configManager.GetRopeState(currentDistance, currentRopeLength);

        // Check if rope is taut (either stretching or squashing)
        bool isRopeTaut = ropeState.isStretch || ropeState.isSquash;

        // Apply rope physics if stretching or squashing
        if (ropeState.ratio != 0f)
        {
            ApplyRopePhysics(ropeState.ratio, ropeState.isStretch, currentDistance, fixedDeltaTime);
        }

        // Apply tangential motion and gravity only when rope is taut
        if (isRopeTaut)
        {
            ApplyTangentialMotion(currentDistance, fixedDeltaTime);
            ApplyGravityAlongRope(fixedDeltaTime);
        }

        // Apply swing friction (always active)
        ApplySwingFriction(fixedDeltaTime);

        // Check for detachment (rope too long)
        if (currentDistance > grapplePhysicsConfig.maxDistance * 1.5f)
        {
            StopGrapple();
            return;
        }

        previousVelocity = rb.linearVelocity;
    }

    /// <summary>
    /// Applies rope physics forces based on stretch/squash state.
    /// Calculates restoring forces and applies them to the rigidbody.
    /// </summary>
    /// <param name="ratio">Stretch/squash ratio.</param>
    /// <param name="isStretch">Whether the rope is stretching (true) or squashing (false).</param>
    /// <param name="currentDistance">Current distance to grapple point.</param>
    /// <param name="fixedDeltaTime">Fixed time step for force application.</param>
    private void ApplyRopePhysics(float ratio, bool isStretch, float currentDistance, float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        Vector2 radialDirection = toGrapple.normalized;

        // Calculate dynamic stiffness based on displacement
        float dynamicStiffness = physicsCalculator.CalculateDynamicStiffness(ratio, isStretch);

        // Calculate displacement magnitude
        float displacement = Mathf.Abs(ratio) * currentRopeLength;

        // Calculate and apply restoring force
        float restoringForceMagnitude = dynamicStiffness * displacement;
        Vector2 restoringForce = isStretch ?
            radialDirection * restoringForceMagnitude :      // Pull toward grapple point
            -radialDirection * restoringForceMagnitude;     // Push away from grapple point

        rb.AddForce(restoringForce, ForceMode2D.Force);

        // Apply damping to prevent oscillations
        Vector2 radialVelocity = Vector2.Dot(rb.linearVelocity, radialDirection) * radialDirection;
        rb.AddForce(-radialVelocity * grapplePhysicsConfig.ropeDamping * dynamicStiffness, ForceMode2D.Force);

        // Convert radial momentum to tangential motion
        ConvertRadialMomentumToTangent(radialVelocity.magnitude, radialDirection, ratio, isStretch);

        // Debug visualization
        if (showPhysicsDebug)
        {
            Color forceColor = isStretch ? Color.red : Color.blue;
            Debug.DrawRay(grappleOrigin.position, restoringForce.normalized * 2f, forceColor, fixedDeltaTime);
            Debug.DrawRay(grappleOrigin.position, radialDirection * displacement, forceColor * 0.5f, fixedDeltaTime);
        }
    }

    /// <summary>
    /// Converts radial momentum to tangential motion for smoother swinging.
    /// Helps maintain swing momentum when rope stretches or squashes.
    /// </summary>
    /// <param name="radialSpeed">Magnitude of radial velocity.</param>
    /// <param name="radialDirection">Direction from player to grapple point.</param>
    /// <param name="ratio">Stretch/squash ratio.</param>
    /// <param name="isStretch">Whether rope is stretching.</param>
    private void ConvertRadialMomentumToTangent(float radialSpeed, Vector2 radialDirection, float ratio, bool isStretch)
    {
        if (radialSpeed < 0.1f || Mathf.Abs(ratio) < 0.0001f) return;

        Vector2 currentVelocity = rb.linearVelocity;
        Vector2 radialVelocity = Vector2.Dot(currentVelocity, radialDirection) * radialDirection;
        Vector2 radialVelDir = radialVelocity.normalized;

        // Find closest tangent direction to current velocity
        Vector2 closestTangent = physicsCalculator.GetClosestTangentDirection(currentVelocity, radialDirection);

        // Calculate alignment between radial velocity and tangent
        float cosineAlignment = isStretch ?
            Vector2.Dot(radialVelDir, closestTangent) :      // Convert outward motion to tangent
            Vector2.Dot(radialVelDir, -closestTangent);      // Convert inward motion to tangent

        // Calculate conversion factor
        float alignmentFactor = Mathf.Max(0f, cosineAlignment);
        alignmentFactor = Mathf.Pow(alignmentFactor, 2f);
        float baseConversionFactor = physicsCalculator.GetTangentConversionFactor(ratio, isStretch);
        float conversionFactor = baseConversionFactor * alignmentFactor;

        // Convert radial momentum to tangential velocity
        float momentumToConvert = radialSpeed * conversionFactor;
        Vector2 tangentVelocity = closestTangent * momentumToConvert;

        // Apply velocity conversion
        Vector2 newVelocity = currentVelocity - radialVelocity * conversionFactor + tangentVelocity;
        rb.linearVelocity = newVelocity;

        // Debug visualization
        if (showPhysicsDebug)
        {
            Color debugColor = isStretch ? Color.yellow : Color.magenta;
            Debug.DrawRay(grappleOrigin.position, closestTangent * 3f, debugColor, 0.1f);
            Debug.DrawRay(grappleOrigin.position, tangentVelocity, debugColor * 0.7f, 0.1f);
        }
    }

    /// <summary>
    /// Applies swing friction to simulate air resistance.
    /// Gradually reduces velocity over time for more realistic swinging.
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for friction calculation.</param>
    private void ApplySwingFriction(float fixedDeltaTime)
    {
        rb.linearVelocity *= 1 - grapplePhysicsConfig.swingFriction;
    }

    /// <summary>
    /// Applies tangential motion forces for circular swing behavior.
    /// Maintains centripetal force for pendulum-like motion.
    /// </summary>
    /// <param name="currentDistance">Current distance to grapple point.</param>
    /// <param name="fixedDeltaTime">Fixed time step for force calculation.</param>
    private void ApplyTangentialMotion(float currentDistance, float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        Vector2 radialDirection = toGrapple.normalized;
        Vector2 tangentDirection = new Vector2(-radialDirection.y, radialDirection.x);

        // Calculate tangential velocity component
        Vector2 tangentVelocity = rb.linearVelocity - Vector2.Dot(rb.linearVelocity, radialDirection) * radialDirection;

        // Apply centripetal force to maintain circular motion
        if (tangentVelocity.magnitude > 0.1f && currentDistance > 0.1f)
        {
            float centripetalForce = tangentVelocity.sqrMagnitude / currentDistance;
            rb.AddForce(radialDirection * centripetalForce, ForceMode2D.Force);

            if (showPhysicsDebug)
            {
                Debug.DrawRay(grappleOrigin.position, radialDirection * centripetalForce * 0.1f, Color.magenta, fixedDeltaTime);
            }
        }
    }

    /// <summary>
    /// Applies gravity component along the rope direction.
    /// Only applies gravity that pulls away from the grapple point.
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for force calculation.</param>
    private void ApplyGravityAlongRope(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        if (toGrapple.magnitude < 0.1f) return;

        Vector2 radialDirection = toGrapple.normalized;

        // Project gravity onto radial direction
        float gravityAlongRope = Vector2.Dot(Physics2D.gravity, radialDirection);

        // Only apply gravity that pulls away from grapple point
        if (gravityAlongRope > 0)
        {
            rb.AddForce(radialDirection * gravityAlongRope * rb.mass, ForceMode2D.Force);
        }
    }

    /// <summary>
    /// Applies a boost in the direction of swing momentum when grapple is released.
    /// Momentum must exceed minimum velocity threshold for boost to apply.
    /// </summary>
    private void ApplyBoost()
    {
        if (swingMomentum.magnitude < grapplePhysicsConfig.minBoostVelocity)
        {
            return;
        }

        // Calculate boost strength based on captured momentum
        float momentumMagnitude = swingMomentum.magnitude;
        float boostStrength = momentumMagnitude * (grapplePhysicsConfig.boostMultiplier - 1f);
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

    /// <summary>
    /// Updates reeling-in logic to shorten the rope.
    /// Applies variable speed based on slack amount.
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for length adjustment.</param>
    private void UpdateReeling(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        float currentDistance = toGrapple.magnitude;

        // Base reel speed
        float effectiveReelSpeed = grappleReelConfig.reelSpeed;

        // Increase reel speed when there's slack
        if (currentDistance < currentRopeLength)
        {
            float slackRatio = 1f - (currentDistance / currentRopeLength);
            effectiveReelSpeed *= Mathf.Lerp(1f, grappleReelConfig.slackReelMultiplier, slackRatio);
        }

        // Calculate and apply new rope length
        float targetLength = Mathf.Max(grappleReelConfig.minRopeLength,
            currentRopeLength - effectiveReelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, grappleReelConfig.reelSmoothness);
    }

    /// <summary>
    /// Updates unreeling-out logic to lengthen the rope.
    /// Smoothly increases rope length up to maximum.
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for length adjustment.</param>
    private void UpdateUnreeling(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        float currentDistance = toGrapple.magnitude;

        // Calculate and apply new rope length
        float targetLength = Mathf.Min(grappleReelConfig.maxRopeLength,
            currentRopeLength + grappleReelConfig.unreelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, grappleReelConfig.unreelSmoothness);
    }

    //////////////////////// Helper Methods ////////////////////////

    /// <summary>
    /// Captures swing momentum for boost calculations.
    /// Smoothly updates swingMomentum based on current velocity.
    /// </summary>
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

    /// <summary>
    /// Gets the current aim direction based on input method.
    /// Prioritizes mouse aiming, falls back to default direction.
    /// </summary>
    /// <returns>Normalized aim direction vector.</returns>
    private Vector2 GetAimDirection()
    {
        // Try mouse aiming first
        if (useMouseAiming)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mainCamera != null)
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

    //////////////////////// Debug & Gizmos ////////////////////////

    /// <summary>
    /// Unity GUI callback for debug information display.
    /// Shows grapple state, physics values, and input status.
    /// </summary>
    void OnGUI()
    {
        if (!showRaycastDebug && !showPhysicsDebug) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 12;
        style.normal.textColor = Color.white;

        int yPos = 300;

        if (showRaycastDebug)
        {
            GUI.Label(new Rect(10, yPos, 400, 20), $"Grapple Ready: {!isGrappling}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Aim Direction: {GetAimDirection()}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Max Distance: {grapplePhysicsConfig.maxDistance}", style); yPos += 20;
        }

        if (showPhysicsDebug && isGrappling)
        {
            Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
            float currentDistance = toGrapple.magnitude;
            RopeState ropeState = configManager.GetRopeState(currentDistance, currentRopeLength);

            GUI.Label(new Rect(10, yPos, 400, 20), $"Grapple Point: {grapplePoint:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Rope Length: {currentRopeLength:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Current Dist: {currentDistance:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"State: {(ropeState.isStretch ? "Stretch" : ropeState.isSquash ? "Squash" : "Neutral")}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Ratio: {ropeState.ratio:P1}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Jump Held: {isJumpHeld}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Down Held: {isDownHeld}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Should Reel: {ShouldReel}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Should Unreel: {ShouldUnreel}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Velocity: {rb.linearVelocity.magnitude:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Momentum: {swingMomentum.magnitude:F2}", style); yPos += 20;
        }
    }

    /// <summary>
    /// Unity Gizmos callback for editor visualization.
    /// Draws grapple range and active grapple visualization.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!showRaycastDebug && !showPhysicsDebug) return;

        if (grappleOrigin != null)
        {
            // Draw grapple range sphere
            Gizmos.color = new Color(0, 1, 0, 0.1f);
            Gizmos.DrawWireSphere(grappleOrigin.position, grapplePhysicsConfig.maxDistance);

            // Draw current grapple if active during play mode
            if (Application.isPlaying && isGrappling)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(grapplePoint, 0.3f);
                Gizmos.DrawLine(grappleOrigin.position, grapplePoint);
            }
        }
    }

    //////////////////////// Public API ////////////////////////

    /// <summary>
    /// Gets whether the grapple is currently active.
    /// </summary>
    /// <returns>True if grappling is active, false otherwise.</returns>
    public bool IsGrappling() => isGrappling;

    /// <summary>
    /// Gets the current grapple point position.
    /// </summary>
    /// <returns>World position of the grapple attachment point.</returns>
    public Vector2 GetGrapplePoint() => grapplePoint;

    /// <summary>
    /// Gets the current rope length.
    /// </summary>
    /// <returns>Current configured rope length in world units.</returns>
    public float GetRopeLength() => currentRopeLength;

    /// <summary>
    /// Gets the current swing arc data.
    /// </summary>
    /// <returns>SwingArc object containing swing geometry information.</returns>
    public SwingArc GetSwingArc() => swingArc;
}