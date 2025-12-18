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
    [Header("Grapple Configuration")]
    [SerializeField] private GrappleConfig grappleConfig;

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
    private float momentumCaptureTimer = 0f;
    private const float MOMENTUM_CAPTURE_RATE = 0.1f;

    // Computed properties for reeling
    private bool ShouldReel => isGrappling && isJumpHeld && !isDownHeld;
    private bool ShouldUnreel => isGrappling && isDownHeld && !isJumpHeld;

    //////////////////////// Initialization ////////////////////////

    public void Initialize(CharacterCore core)
    {
        character = core;
        movement = character.GetComponent<PlatformerMovement>();
        rb = character.gameObject.GetComponent<Rigidbody2D>();

        // Initialize config manager with ScriptableObject
        configManager = new GrappleConfigManager(grappleConfig);

        // Initialize subsystem managers
        physicsCalculator = new GrapplePhysicsCalculator(grappleConfig.physicsConfig);
        visualManager = new GrappleVisualManager(
            grappleConfig.visualConfig,
            grappleOrigin,
            grappleLine,
            showPhysicsDebug
        );

        // Initialize sound manager
        soundManager = new GrappleSoundManager(
            grappleConfig.soundConfig,
            this
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

    public void Tick(float deltaTime)
    {
        // Draw raycast debug when not grappling
        if (showRaycastDebug && !isGrappling)
        {
            Vector2 aimDir = GetAimDirection();
            visualManager.DrawRaycastDebug(
                grappleOrigin.position,
                aimDir,
                grappleConfig.physicsConfig.grappleLayers,
                grappleConfig.physicsConfig.maxDistance,
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

    private void TryStartGrapple(int initialReelDirection = 0)
    {
        Vector2 aimDir = GetAimDirection();

        // Perform grapple raycast using physics calculator
        grappleHit = physicsCalculator.PerformGrappleRaycast(
            grappleOrigin.position,
            aimDir,
            grappleConfig.physicsConfig.grappleLayers,
            grappleConfig.physicsConfig.maxDistance
        );

        if (grappleHit.collider != null)
        {
            StartGrapple(grappleHit.point, initialReelDirection);
        }
        else
        {
            Debug.Log("Grapple missed - no valid target");
        }
    }

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

        // Apply grapple movement state override
        var movementState = grappleConfig.movementState.ToMovementState();

        // Notify other systems about grapple start
        character.RaiseEvent("movement_override_start", movementState);
        character.RaiseEvent("grapple_started", grapplePoint);

        // Create visual elements
        visualManager.InstantiateGrappleVisuals(point);

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

        // Clean up visual elements
        visualManager.CleanupGrappleVisuals();

        // Stop creak sounds
        if (soundManager != null)
        {
            soundManager.StopCreakSounds();
        }

        // Notify other systems about grapple end
        character.RaiseEvent("movement_override_end", null);
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

        // Apply rope physics if stretching or squashing
        if (ropeState.ratio != 0f)
        {
            ApplySwingPhysics(ropeState.ratio, ropeState.isStretch, currentDistance, fixedDeltaTime);
        }

        // Apply friction (always active)
        ApplyFriction(fixedDeltaTime);

        // Check for detachment (rope too long)
        if (currentDistance > grappleConfig.physicsConfig.maxDistance * 1.5f)
        {
            StopGrapple();
        }
    }

    private void ApplySwingPhysics(float ratio, bool isStretch, float currentDistance, float fixedDeltaTime)
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

        rb.AddForce(restoringForce, ForceMode2D.Force);

        // Update creak volume with simple call
        if (soundManager != null && grappleConfig.soundConfig != null)
        {
            soundManager.UpdateCreakVolume(
                restoringForceMagnitude,
                grappleConfig.soundConfig.creakMinForce,
                grappleConfig.soundConfig.creakMaxForce
            );
        }

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
        bool isSwinging = angularVelocity > 0.3f; // ~17 degrees per second
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
                                  (radialVelocity.magnitude * grappleConfig.physicsConfig.ropeDamping * dynamicStiffness);
            rb.AddForce(dampingForce, ForceMode2D.Force);
        }
    }

    /// <summary>
    /// Applies swing friction to simulate air resistance.
    /// Applies two types of friction:
    /// 1. General swing friction (affects all velocity)
    /// 2. Tangential friction (only affects motion perpendicular to rope direction)
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for friction calculation.</param>
    private void ApplyFriction(float fixedDeltaTime)
    {
        // Apply general swing friction (affects all velocity)
        rb.linearVelocity *= 1 - grappleConfig.physicsConfig.friction;

        // Apply tangential friction (only affects motion perpendicular to rope)
        ApplyTangentialFriction(fixedDeltaTime);
    }

    /// <summary>
    /// Applies friction only to the tangential component of velocity.
    /// This allows controlling swinging motion independently from radial motion.
    /// </summary>
    /// <param name="fixedDeltaTime">Fixed time step for friction calculation.</param>
    private void ApplyTangentialFriction(float fixedDeltaTime)
    {
        // Early return if tangential friction is effectively zero
        if (grappleConfig.physicsConfig.tangentialFriction < 0.001f) return;

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
            Vector2 newTangentialVelocity = tangentialVelocity * (1 - grappleConfig.physicsConfig.tangentialFriction);

            // Recombine: radial velocity stays the same, tangential gets friction
            rb.linearVelocity = radialVelocity + newTangentialVelocity;

            // Debug visualization
            if (showPhysicsDebug && grappleConfig.physicsConfig.tangentialFriction > 0.01f)
            {
                // Show tangential velocity direction
                Debug.DrawRay(grappleOrigin.position, tangentialVelocity.normalized * 2f, Color.cyan, fixedDeltaTime);

                // Show friction force (opposite to tangential velocity)
                Debug.DrawRay(grappleOrigin.position, -tangentialVelocity.normalized * grappleConfig.physicsConfig.tangentialFriction * 3f,
                             new Color(1f, 0.5f, 0f), fixedDeltaTime); // Orange color
            }
        }
    }

    private void ApplyBoost()
    {
        if (swingMomentum.magnitude < grappleConfig.physicsConfig.minBoostVelocity)
        {
            return;
        }

        // Calculate boost strength based on captured momentum
        float momentumMagnitude = swingMomentum.magnitude;
        float boostStrength = momentumMagnitude * (grappleConfig.physicsConfig.boostMultiplier - 1f);
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
        float effectiveReelSpeed = grappleConfig.reelConfig.reelSpeed;

        // Increase reel speed when there's slack
        if (currentDistance < currentRopeLength)
        {
            float slackRatio = 1f - (currentDistance / currentRopeLength);
            effectiveReelSpeed *= Mathf.Lerp(1f, grappleConfig.reelConfig.slackReelMultiplier, slackRatio);
        }

        // Calculate and apply new rope length
        float targetLength = Mathf.Max(grappleConfig.reelConfig.minRopeLength,
            currentRopeLength - effectiveReelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, grappleConfig.reelConfig.reelSmoothness);
    }

    private void UpdateUnreeling(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        float currentDistance = toGrapple.magnitude;

        // Calculate and apply new rope length
        float targetLength = Mathf.Min(grappleConfig.reelConfig.maxRopeLength,
            currentRopeLength + grappleConfig.reelConfig.unreelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, grappleConfig.reelConfig.unreelSmoothness);
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

    //////////////////////// Public API ////////////////////////

    public bool IsGrappling() => isGrappling;
    public Vector2 GetGrapplePoint() => grapplePoint;
    public float GetRopeLength() => currentRopeLength;
    public SwingArc GetSwingArc() => swingArc;

    /// <summary>
    /// Switch to a different grapple configuration at runtime.
    /// </summary>
    public void SwitchGrappleConfig(GrappleConfig newConfig)
    {
        if (newConfig == null) return;

        // Stop current grapple if active
        if (isGrappling)
        {
            StopGrapple();
        }

        // Update configuration
        grappleConfig = newConfig;

        // Reinitialize physics calculator with new config
        physicsCalculator = new GrapplePhysicsCalculator(grappleConfig.physicsConfig);

        // Reinitialize visual manager
        visualManager = new GrappleVisualManager(
            grappleConfig.visualConfig,
            grappleOrigin,
            grappleLine,
            showPhysicsDebug
        );

        // Clean up old sound manager and create new one
        if (soundManager != null)
        {
            soundManager.Cleanup();
        }

        soundManager = new GrappleSoundManager(
            grappleConfig.soundConfig,
            this
        );

        Debug.Log($"Switched to {grappleConfig.movementState.name} grapple configuration");
    }

    //////////////////////// Cleanup ///////////////////////////
    private void OnDestroy()
    {
        if (soundManager != null)
        {
            soundManager.Cleanup();
        }
    }
}