using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; 
using static GrappleSystem;

public class GrappleSystem : MonoBehaviour, ICharacterComponent
{
    [System.Serializable]
    public class GrappleMovementState
    {
        public string name = "Grappling";

        // Input control
        public bool allowMovement = true;

        // Physics control
        public bool applyGravity = true;
        public bool applyDeceleration = true;
        public bool canJump = false;

        // Multipliers
        public float gravityMultiplier = 1f;
        public float accelerationMultiplier = 1f;
        public float airAccelerationMultiplier = 0.025f;
        public float decelerationMultiplier = 1f;
        public float airDecelerationMultiplier = 0.025f;
        public float jumpForceMultiplier = 0f;
        public float maxSpeedMultiplier = 1f;

        // Helper method to convert to MovementState
        public PlatformerMovement.MovementState ToMovementState()
        {
            return new PlatformerMovement.MovementState(
                name: name,
                allowMovement: allowMovement,
                applyGravity: applyGravity,
                applyDeceleration: applyDeceleration,
                canJump: canJump,
                gravityMultiplier: gravityMultiplier,
                accelerationMultiplier: accelerationMultiplier,
                airAccelerationMultiplier: airAccelerationMultiplier,
                decelerationMultiplier: decelerationMultiplier,
                airDecelerationMultiplier: airDecelerationMultiplier,
                jumpForceMultiplier: jumpForceMultiplier,
                maxSpeedMultiplier: maxSpeedMultiplier
            );
        }
    }

    [System.Serializable]
    public class RopePhysicsConfig
    {
        public float maxDistance = 20f; // Max distance the player can reach from to begin a grapple
        public float ropeDamping = 0.1f; // Delays/smooths effect of rope on player - too low causes 'bouncing', and too high causes the player to reach farther outside the radius before being affected, so the stiffness exponent shoots them back (different kind of bouncing)
        public float swingFriction = 0.004f; // Ratio of linear velocity lost every physics tick

        public float boostMultiplier = 1.1f; // Multiplier for swing momentum when releasing the grappling hook
        public float minBoostVelocity = 2f; // Minimum velocity to recieve the boost multiplier

        [Header("Stretch Physics (Outside Rope)")]
        public bool enableStretch = true; // Whether the grapple should pull the player inward when too far
        public float stretchStiffness = 200f; // Base strength of force pulling the player inward
        public float stretchStiffnessExponent = 2f; // How quickly the inward force grows as the player goes further outside the current grapple radius
        public float stretchToTangentConversion = 0.7f; // How much energy is conserved when converting/redirecting radial force to tangential when pulling  ///// CURRENTLY HAS NO EFFECT FOR SOME REASON //////

        [Header("Squash Physics (Inside Rope)")]
        public bool enableSquash = false; // Whether the grapple should push the player outward when too close
        public float squashStiffness = 50f; // Base strength of force pushing the player outward
        public float squashStiffnessExponent = 2f; // How quickly the outward force grows as the player goes further inside the current grapple radius
        public float squashToTangentConversion = 0.7f; // How much energy is conserved when converting/redirecting radial force to tangential when pushing  ///// CURRENTLY HAS NO EFFECT FOR SOME REASON //////

        public LayerMask grappleLayers; // All layers the grapple raycast can hit
    }

    [System.Serializable]
    public class ReelConfig
    {
        [Header("Reeling In")]
        public float reelSpeed = 50f;
        public float slackReelMultiplier = 3f;
        public float minRopeLength = 1f;
        public float reelSmoothness = 0.1f;

        [Header("Unreeling Out")]
        public float unreelSpeed = 50f;
        public float maxRopeLength = 20f;
        public float unreelSmoothness = 0.1f;
    }

    [System.Serializable]
    public class SwingArc
    {
        public Vector2 center;
        public float radius;
        public float currentAngle; // Angle from vertical (0 = straight down)
        public Vector2 tangentDirection;
        public Vector2 radialDirection;
    }

    [Header("Configuration")]
    public GrappleMovementState grappleMovementState;
    public RopePhysicsConfig physicsConfig;
    public ReelConfig reelConfig;

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

    // Input references
    private Camera mainCamera;

    // State
    private CharacterCore character;
    private PlatformerMovement movement;
    private Rigidbody2D rb;
    private bool isGrappling = false;
    private Vector2 grapplePoint;
    private RaycastHit2D grappleHit;
    private float currentRopeLength;

    // Physics state
    private bool isJumpHeld = false;
    private bool isDownHeld = false;
    private SwingArc swingArc;
    private Vector2 swingMomentum;
    private Vector2 previousVelocity;
    private float momentumCaptureTimer = 0f;
    private const float MOMENTUM_CAPTURE_RATE = 0.1f;

    // Computed properties for reeling
    private bool ShouldReel => isGrappling && isJumpHeld && !isDownHeld;
    private bool ShouldUnreel => isGrappling && isDownHeld && !isJumpHeld;

    // Debug
    private Vector2 lastAimDirection;
    private float lastRaycastLength;
    private bool lastRaycastHit;
    

    

    //////////////////////  Macro and Grapple State Handling  ////////////////////////
    
    // Listeners and setup

    public void Initialize(CharacterCore core)
    {
        character = core;
        movement = character.GetComponent<PlatformerMovement>();
        rb = character.gameObject.GetComponent<Rigidbody2D>();

        character.OnEvent += HandleEvent;

        if (grappleLine != null)
        {
            grappleLine.enabled = false;
            grappleLine.positionCount = 2;
        }

        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("No main camera found for grapple aiming");
        }
    }

    public void Tick(float deltaTime)
    {
        if (showRaycastDebug && !isGrappling)
        {
            DrawRaycastDebug();
        }

        momentumCaptureTimer += deltaTime;
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        if (isGrappling)
        {
            if (momentumCaptureTimer >= MOMENTUM_CAPTURE_RATE)
            {
                CaptureSwingMomentum();
                momentumCaptureTimer = 0f;
            }

            UpdateSwingPhysics(fixedDeltaTime);

            // Use computed properties instead of separate states
            if (ShouldReel)
            {
                UpdateReeling(fixedDeltaTime);
            }
            else if (ShouldUnreel)
            {
                UpdateUnreeling(fixedDeltaTime);
            }

            UpdateGrappleVisuals();
        }
    }

    private void HandleEvent(string type, object data)
    {
        if (type == grappleInput)
        {
            if (!isGrappling)
            {
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
            // No need to call StartReeling() - it's now computed
        }
        else if (type == "jump_released")
        {
            isJumpHeld = false;
            // No need to call StopReeling() - it's now computed
        }
        else if (type == "down_pressed")
        {
            isDownHeld = true;
            // No need to call StartUnreeling() - it's now computed
        }
        else if (type == "down_released")
        {
            isDownHeld = false;
            // No need to call StopUnreeling() - it's now computed
        }
    }


    // Grapple state handling

    private void TryStartGrapple(int initialReelDirection = 0)
    {
        lastAimDirection = GetAimDirection();
        grappleHit = Physics2D.Raycast(
            grappleOrigin.position,
            lastAimDirection,
            physicsConfig.maxDistance,
            physicsConfig.grappleLayers
        );

        lastRaycastHit = grappleHit.collider != null;
        lastRaycastLength = grappleHit.collider != null ? grappleHit.distance : physicsConfig.maxDistance;

        if (showRaycastDebug)
        {
            DrawRaycastDebug();
        }

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

        // Initialize swing arc
        swingArc = CalculateSwingArc(grappleOrigin.position, point, currentRopeLength);

        // Reset states
        swingMomentum = Vector2.zero;
        momentumCaptureTimer = 0f;
        previousVelocity = rb.linearVelocity;

        // Set movement state
        var movementState = (grappleMovementState != null) ?
            grappleMovementState.ToMovementState() :
            new PlatformerMovement.MovementState(
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

        character.RaiseEvent("movement_override_start", movementState);
        character.RaiseEvent("grapple_started", grapplePoint);

        if (grappleLine != null)
            grappleLine.enabled = true;
    }

    private void StopGrapple()
    {
        if (!isGrappling) return;

        isGrappling = false;
        character.RaiseEvent("movement_override_end", null);
        character.RaiseEvent("grapple_ended", grapplePoint);

        if (grappleLine != null)
            grappleLine.enabled = false;
    }

    private void ApplyBoost()
    {
        if (swingMomentum.magnitude < physicsConfig.minBoostVelocity)
        {
            return;
        }

        // Boost in the direction of swing momentum
        float momentumMagnitude = swingMomentum.magnitude;
        float boostStrength = momentumMagnitude * (physicsConfig.boostMultiplier - 1f);
        Vector2 boostDirection = swingMomentum.normalized;

        movement.AddExternalVelocity(boostDirection * boostStrength);

        character.RaiseEvent("grapple_boost_applied", new GrappleBoostData
        {
            direction = boostDirection,
            strength = boostStrength,
            momentum = swingMomentum
        });
    }



    //////////////////////  Grapple Physics Handling  /////////////////////////

    // Swinging
    private void UpdateSwingPhysics(float fixedDeltaTime)
    {
        Vector2 playerPos = grappleOrigin.position;
        Vector2 toGrapple = grapplePoint - playerPos;
        float currentDistance = toGrapple.magnitude;

        // Update swing arc
        swingArc = CalculateSwingArc(playerPos, grapplePoint, currentRopeLength);

        // Get rope state (stretch or squash)
        var ropeState = GetRopeState(currentDistance, currentRopeLength);

        // Check if rope is taut (either stretching or squashing)
        bool isRopeTaut = ropeState.isStretch || ropeState.isSquash;

        // Apply physics if we have stretch or squash
        if (ropeState.ratio != 0f)
        {
            ApplyRopePhysics(ropeState.ratio, ropeState.isStretch, currentDistance, fixedDeltaTime);
        }

        // Only apply these forces when rope is taut
        if (isRopeTaut)
        {
            ApplyTangentialMotion(currentDistance, fixedDeltaTime);
            ApplyGravityAlongRope(fixedDeltaTime);
        }

        // Swing friction is general air resistance - apply always
        ApplySwingFriction(fixedDeltaTime);

        // Check for detachment
        if (currentDistance > physicsConfig.maxDistance * 1.5f)
        {
            StopGrapple();
            return;
        }

        previousVelocity = rb.linearVelocity;
    }

    private SwingArc CalculateSwingArc(Vector2 playerPos, Vector2 grapplePos, float ropeLength)
    {
        SwingArc arc = new SwingArc();
        arc.center = grapplePos;
        arc.radius = ropeLength;

        Vector2 toPlayer = playerPos - grapplePos;
        arc.currentAngle = Vector2.SignedAngle(Vector2.down, toPlayer) * Mathf.Deg2Rad;

        // Radial direction (from grapple point to player)
        arc.radialDirection = toPlayer.normalized;

        // Tangent direction (perpendicular to radial, 90 degrees clockwise)
        arc.tangentDirection = new Vector2(-arc.radialDirection.y, arc.radialDirection.x);

        return arc;
    }

    private (float ratio, bool isStretch, bool isSquash) GetRopeState(float currentDistance, float ropeLength)
    {
        float slack = ropeLength - currentDistance;
        float ratio = 0f;
        bool isStretch = false;
        bool isSquash = false;

        // Check for stretch (outside rope circle)
        if (currentDistance > ropeLength && physicsConfig.enableStretch)
        {
            isStretch = true;
            ratio = (currentDistance - ropeLength) / ropeLength; // Positive
        }
        // Check for squash (inside rope circle beyond threshold)
        else if (slack > 0.01f && physicsConfig.enableSquash)
        {
            isSquash = true;
            ratio = -(slack - 0.01f) / ropeLength; // Negative
        }

        return (ratio, isStretch, isSquash);
    }

    private void ApplyRopePhysics(float ratio, bool isStretch, float currentDistance, float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        Vector2 radialDirection = toGrapple.normalized;

        // Calculate dynamic stiffness
        float dynamicStiffness = CalculateDynamicStiffness(ratio, isStretch);

        // Calculate displacement magnitude (always positive)
        float displacement = Mathf.Abs(ratio) * currentRopeLength;

        // Calculate restoring force magnitude
        float restoringForceMagnitude = dynamicStiffness * displacement;

        // Apply restoring force (direction depends on stretch/squash)
        Vector2 restoringForce;
        if (isStretch)
        {
            // Pull toward grapple point when stretching
            restoringForce = radialDirection * restoringForceMagnitude;
        }
        else
        {
            // Push away from grapple point when squashing
            restoringForce = -radialDirection * restoringForceMagnitude;
        }

        rb.AddForce(restoringForce, ForceMode2D.Force);

        // Apply damping to prevent oscillations
        Vector2 radialVelocity = Vector2.Dot(rb.linearVelocity, radialDirection) * radialDirection;
        rb.AddForce(-radialVelocity * physicsConfig.ropeDamping * dynamicStiffness, ForceMode2D.Force);

        // Convert radial momentum to tangent if applicable
        ConvertRadialMomentumToTangent(radialVelocity.magnitude, radialDirection, ratio, isStretch);

        if (showPhysicsDebug)
        {
            Color forceColor = isStretch ? Color.red : Color.blue;
            string type = isStretch ? "Stretch" : "Squash";

            Debug.DrawRay(grappleOrigin.position, restoringForce.normalized * 2f, forceColor, fixedDeltaTime);
            Debug.DrawRay(grappleOrigin.position, radialDirection * displacement, forceColor * 0.5f, fixedDeltaTime);
        }
    }

    private float CalculateDynamicStiffness(float ratio, bool isStretch)
    {
        if (Mathf.Abs(ratio) < 0.0001f) return 0f;

        if (isStretch)
            return physicsConfig.stretchStiffness * Mathf.Pow(1f + ratio, physicsConfig.stretchStiffnessExponent);
        else
            return physicsConfig.squashStiffness * Mathf.Pow(1f + Mathf.Abs(ratio), physicsConfig.squashStiffnessExponent);
    }

    private void ConvertRadialMomentumToTangent(float radialSpeed, Vector2 radialDirection, float ratio, bool isStretch)
    {
        if (radialSpeed < 0.1f || Mathf.Abs(ratio) < 0.0001f) return;

        Vector2 currentVelocity = rb.linearVelocity;
        Vector2 radialVelocity = Vector2.Dot(currentVelocity, radialDirection) * radialDirection;
        Vector2 radialVelDir = radialVelocity.normalized;

        // Get both possible tangent directions
        Vector2 tangent1 = new Vector2(-radialDirection.y, radialDirection.x);
        Vector2 tangent2 = -tangent1;

        // Find which tangent is closer to current velocity direction
        Vector2 closestTangent = GetClosestTangentDirection(currentVelocity, tangent1, tangent2);

        // Calculate dot product for alignment
        float cosineAlignment;
        if (isStretch)
        {
            // For stretch: convert outward motion (away from center) to tangent
            cosineAlignment = Vector2.Dot(radialVelDir, closestTangent);
        }
        else
        {
            // For squash: convert inward motion (toward center) to tangent
            cosineAlignment = Vector2.Dot(radialVelDir, -closestTangent);
        }

        // Convert to 0-1 factor
        float alignmentFactor = Mathf.Max(0f, cosineAlignment);
        alignmentFactor = Mathf.Pow(alignmentFactor, 2f);

        // Get appropriate conversion factor
        float baseConversionFactor = GetTangentConversionFactor(ratio, isStretch);
        float conversionFactor = baseConversionFactor * alignmentFactor;

        // Convert radial momentum to tangent
        float momentumToConvert = radialSpeed * conversionFactor;
        Vector2 tangentVelocity = closestTangent * momentumToConvert;

        // Apply the conversion
        Vector2 newVelocity = currentVelocity - radialVelocity * conversionFactor + tangentVelocity;
        rb.linearVelocity = newVelocity;

        if (showPhysicsDebug)
        {
            Color debugColor = isStretch ? Color.yellow : Color.magenta;
            string type = isStretch ? "Stretch" : "Squash";

            Debug.DrawRay(grappleOrigin.position, closestTangent * 3f, debugColor, 0.1f);
            Debug.DrawRay(grappleOrigin.position, tangentVelocity, debugColor * 0.7f, 0.1f);

            float angle = Mathf.Acos(Mathf.Clamp(cosineAlignment, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }

    private Vector2 GetClosestTangentDirection(Vector2 velocity, Vector2 tangent, Vector2 oppositeTangent)
    {
        float dotTangent = Vector2.Dot(velocity.normalized, tangent);
        float dotOpposite = Vector2.Dot(velocity.normalized, oppositeTangent);

        return dotTangent > dotOpposite ? tangent : oppositeTangent;
    }

    private float GetTangentConversionFactor(float ratio, bool isStretch)
    {
        if (Mathf.Abs(ratio) < 0.0001f) return 0f;
        return isStretch ? physicsConfig.stretchToTangentConversion : physicsConfig.squashToTangentConversion;
    }

    private void ApplySwingFriction(float fixedDeltaTime)
    {
        // Apply air resistance/friction to swing
        rb.linearVelocity *= 1 - physicsConfig.swingFriction;
    }

    private void ApplyTangentialMotion(float currentDistance, float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        Vector2 radialDirection = toGrapple.normalized;
        Vector2 tangentDirection = new Vector2(-radialDirection.y, radialDirection.x);

        // Get tangential component of velocity
        Vector2 tangentVelocity = rb.linearVelocity - Vector2.Dot(rb.linearVelocity, radialDirection) * radialDirection;

        // Apply centripetal force to maintain circular motion (only called when rope is taut)
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

    private void ApplyGravityAlongRope(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        if (toGrapple.magnitude < 0.1f) return;

        Vector2 radialDirection = toGrapple.normalized;

        // Project gravity onto radial direction
        float gravityAlongRope = Vector2.Dot(Physics2D.gravity, radialDirection);

        // Apply gravity component along the rope
        if (gravityAlongRope > 0)
        {
            rb.AddForce(radialDirection * gravityAlongRope * rb.mass, ForceMode2D.Force);
        }
    }

    // Reeling
    private void UpdateReeling(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        float currentDistance = toGrapple.magnitude;

        // Calculate effective reel speed based on slack
        float effectiveReelSpeed = reelConfig.reelSpeed;

        // Check if we have slack (player is inside the rope circle)
        if (currentDistance < currentRopeLength)
        {
            // Calculate how much slack we have (0-1 where 1 = maximum slack)
            float slackRatio = 1f - (currentDistance / currentRopeLength);

            // Apply multiplier based on slack amount
            effectiveReelSpeed *= Mathf.Lerp(1f, reelConfig.slackReelMultiplier, slackRatio);
        }

        // Smoothly shorten the rope with variable speed
        float targetLength = Mathf.Max(reelConfig.minRopeLength,
            currentRopeLength - effectiveReelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, reelConfig.reelSmoothness);
    }

    private void UpdateUnreeling(float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        float currentDistance = toGrapple.magnitude;

        // Calculate target length (increase rope length)
        float targetLength = Mathf.Min(reelConfig.maxRopeLength,
            currentRopeLength + reelConfig.unreelSpeed * fixedDeltaTime);

        // Smoothly lengthen the rope
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, reelConfig.unreelSmoothness);
    }

    //////////////////////////  Debug Visuals  ///////////////////////////

    // Draw debug
    private void UpdateGrappleVisuals()
    {
        if (grappleLine != null)
        {
            grappleLine.SetPosition(0, grappleOrigin.position);
            grappleLine.SetPosition(1, grapplePoint);

            // Calculate rope state for visual feedback
            Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
            float currentDistance = toGrapple.magnitude;
            var ropeState = GetRopeState(currentDistance, currentRopeLength);

            // Visual feedback - use computed properties
            if (ShouldReel)
            {
                grappleLine.startColor = Color.yellow;
                grappleLine.endColor = Color.yellow;
                grappleLine.widthMultiplier = 0.15f;
            }
            else if (ShouldUnreel) // Use computed property
            {
                grappleLine.startColor = Color.green;
                grappleLine.endColor = Color.green;
                grappleLine.widthMultiplier = 0.15f;
            }
            else if (ropeState.isStretch && ropeState.ratio > 0)
            {
                // Stretching - red with intensity based on ratio
                float intensity = Mathf.Clamp01(ropeState.ratio * 5f);
                grappleLine.startColor = Color.Lerp(Color.white, Color.red, intensity);
                grappleLine.endColor = Color.Lerp(Color.white, Color.red, intensity);
                grappleLine.widthMultiplier = 0.1f + (0.1f * intensity);
            }
            else if (ropeState.isSquash)
            {
                // Squashing - blue with intensity based on ratio
                float intensity = Mathf.Clamp01(Mathf.Abs(ropeState.ratio) * 5f);
                grappleLine.startColor = Color.Lerp(Color.white, Color.blue, intensity);
                grappleLine.endColor = Color.Lerp(Color.white, Color.blue, intensity);
                grappleLine.widthMultiplier = 0.1f + (0.1f * intensity);
            }
            else
            {
                // Normal or no stretch/squash
                grappleLine.startColor = Color.white;
                grappleLine.endColor = Color.white;
                grappleLine.widthMultiplier = 0.1f;
            }
        }

        // Physics debug visualization
        if (showPhysicsDebug && isGrappling)
        {
            DrawSwingCircle();

            if (swingArc != null)
            {
                Debug.DrawRay(grappleOrigin.position, swingArc.tangentDirection * 3f, Color.green);
                Debug.DrawRay(grappleOrigin.position, -swingArc.tangentDirection * 3f, Color.green);
            }
        }
    }

    private void DrawSwingCircle()
    {
        const int segments = 32;
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

            Vector2 point1 = grapplePoint + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * currentRopeLength;
            Vector2 point2 = grapplePoint + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * currentRopeLength;

            Debug.DrawLine(point1, point2, new Color(1, 1, 1, 0.3f));
        }
    }

    private void DrawRaycastDebug()
    {
        // ... (keep existing DrawRaycastDebug implementation)
        if (grappleOrigin == null) return;

        Vector2 aimDir = GetAimDirection();
        RaycastHit2D debugHit = Physics2D.Raycast(
            grappleOrigin.position,
            aimDir,
            physicsConfig.maxDistance,
            physicsConfig.grappleLayers
        );

        bool hit = debugHit.collider != null;
        float rayLength = hit ? debugHit.distance : physicsConfig.maxDistance;
        Color rayColor = hit ? raycastHitColor : raycastMissColor;

        Vector3 start = grappleOrigin.position;
        Vector3 end = start + (Vector3)aimDir * rayLength;

        Debug.DrawLine(start, end, rayColor);

        if (hit)
        {
            Debug.DrawRay(debugHit.point, Vector2.up * 0.2f, Color.yellow);
            Debug.DrawRay(debugHit.point, Vector2.down * 0.2f, Color.yellow);
            Debug.DrawRay(debugHit.point, Vector2.left * 0.2f, Color.yellow);
            Debug.DrawRay(debugHit.point, Vector2.right * 0.2f, Color.yellow);
        }
    }

    // Helpers to get info for debug
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
        // Try mouse first if enabled
        if (useMouseAiming)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mainCamera != null)
            {
                Vector2 mousePos = mouse.position.ReadValue();

                // Only use mouse if it's not at (0,0) - Input System default
                if (mousePos != Vector2.zero)
                {
                    Vector3 worldPos = mainCamera.ScreenToWorldPoint(
                        new Vector3(mousePos.x, mousePos.y,
                                   mainCamera.nearClipPlane));

                    Vector2 direction = worldPos - grappleOrigin.position;
                    if (direction.magnitude > 0.01f)
                    {
                        return direction.normalized;
                    }
                }
            }
        }

        // Default: aim up and to the right
        return Vector2.up + Vector2.right;
    }

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
            GUI.Label(new Rect(10, yPos, 400, 20), $"Max Distance: {physicsConfig.maxDistance}", style); yPos += 20;
        }

        if (showPhysicsDebug && isGrappling)
        {
            Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
            float currentDistance = toGrapple.magnitude;
            var ropeState = GetRopeState(currentDistance, currentRopeLength);

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

    void OnDrawGizmosSelected()
    {
        if (!showRaycastDebug && !showPhysicsDebug) return;

        if (grappleOrigin != null)
        {
            // Draw grapple range
            Gizmos.color = new Color(0, 1, 0, 0.1f);
            Gizmos.DrawWireSphere(grappleOrigin.position, physicsConfig.maxDistance);

            // Draw current grapple if active
            if (Application.isPlaying && isGrappling)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(grapplePoint, 0.3f);
                Gizmos.DrawLine(grappleOrigin.position, grapplePoint);
            }
        }
    }

    // Public API
    public bool IsGrappling() => isGrappling;
    public Vector2 GetGrapplePoint() => grapplePoint;
    public float GetRopeLength() => currentRopeLength;
    public SwingArc GetSwingArc() => swingArc; 
}

// Data structure for grapple boost events
public struct GrappleBoostData
{
    public Vector2 direction;
    public float strength;
    public Vector2 momentum;
}