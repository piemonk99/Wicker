using System.Collections;
using UnityEngine.InputSystem; 
using UnityEngine;

public class GrappleSystem : MonoBehaviour, ICharacterComponent
{
    [System.Serializable]
    public class RopePhysicsConfig
    {
        public float maxDistance = 20f;
        public float baseRopeStiffness = 20f;
        public float ropeStiffnessExponent = 2f;
        public float tangentConversionFactor = 0.7f;
        public float ropeDamping = 0.1f;
        public float swingFriction = 0.99f;

        public float boostMultiplier = 1.5f;
        public float minBoostVelocity = 2f;
        
        public LayerMask grappleLayers;
    }

    [System.Serializable]
    public class ReelConfig
    {
        public float reelSpeed = 10f;
        public float slackReelMultiplier = 3f; // Faster reeling when rope has slack
        public float minRopeLength = 1f;
        public float reelSmoothness = 0.1f; // Smoothness when reeling
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
    private bool isReeling = false;
    private bool isJumpHeld = false;
    private SwingArc swingArc;
    private Vector2 swingMomentum;
    private Vector2 previousVelocity;
    private float momentumCaptureTimer = 0f;
    private const float MOMENTUM_CAPTURE_RATE = 0.1f;

    // Debug
    private Vector2 lastAimDirection;
    private float lastRaycastLength;
    private bool lastRaycastHit;

    // Helper: Calculate swing arc geometry
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

    // Helper: Get the closest tangent direction to a given velocity
    private Vector2 GetClosestTangentDirection(Vector2 velocity, Vector2 tangent, Vector2 oppositeTangent)
    {
        float dotTangent = Vector2.Dot(velocity.normalized, tangent);
        float dotOpposite = Vector2.Dot(velocity.normalized, oppositeTangent);

        return dotTangent > dotOpposite ? tangent : oppositeTangent;
    }

    // Helper: Calculate how much rope is stretched beyond its natural length
    private float CalculateStretchRatio(float currentDistance, float ropeLength)
    {
        if (currentDistance <= ropeLength) return 0f;
        return (currentDistance - ropeLength) / ropeLength;
    }

    // Helper: Calculate exponential stiffness based on stretch
    private float CalculateDynamicStiffness(float stretchRatio)
    {
        return physicsConfig.baseRopeStiffness * Mathf.Pow(1f + stretchRatio, physicsConfig.ropeStiffnessExponent);
    }

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

        // Just get the camera reference
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

            if (isReeling)
            {
                UpdateReeling(fixedDeltaTime);
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
                // Check if we should start in reeling mode (jump is held)
                bool shouldStartReeling = isJumpHeld;
                TryStartGrapple(shouldStartReeling);
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

            if (isGrappling)
            {
                StartReeling();
            }
        }
        else if (type == "jump_released")
        {
            isJumpHeld = false;

            if (isGrappling)
            {
                if (isReeling)
                {
                    StopReeling();
                }
            }
        }
    }

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

    private void StartReeling()
    {
        if (!isGrappling || isReeling) return;

        isReeling = true;
        character.RaiseEvent("grapple_reel_started", null);
    }

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

            if (showPhysicsDebug)
            {
                Debug.Log($"Reeling with slack: {slackRatio:P0} - Speed: {effectiveReelSpeed:F1}x");
            }
        }

        // Smoothly shorten the rope with variable speed
        float targetLength = Mathf.Max(reelConfig.minRopeLength,
            currentRopeLength - effectiveReelSpeed * fixedDeltaTime);
        currentRopeLength = Mathf.Lerp(currentRopeLength, targetLength, reelConfig.reelSmoothness);

        // Apply stronger inward pull while actively reeling
        if (currentDistance > currentRopeLength)
        {
            float overshoot = currentDistance - currentRopeLength;
            float pullStrength = physicsConfig.baseRopeStiffness * overshoot;
            Vector2 pullDirection = toGrapple.normalized;
            rb.AddForce(pullDirection * pullStrength, ForceMode2D.Force);
        }
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

    private void StopReeling()
    {
        if (!isReeling) return;
        isReeling = false;
        character.RaiseEvent("grapple_reel_ended", null);
    }

    private void TryStartGrapple(bool startReeling = false)
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
            StartGrapple(grappleHit.point, startReeling);
        }
        else
        {
            // Could play "miss" sound/effect
            Debug.Log("Grapple missed - no valid target");
        }
    }

    private void StartGrapple(Vector2 point, bool startReeling = false)
    {
        isGrappling = true;
        grapplePoint = point;
        currentRopeLength = Vector2.Distance(grappleOrigin.position, point);

        // Initialize swing arc
        swingArc = CalculateSwingArc(grappleOrigin.position, point, currentRopeLength);

        // Reset states
        isReeling = false;
        swingMomentum = Vector2.zero;
        momentumCaptureTimer = 0f;
        previousVelocity = rb.linearVelocity;

        // Set movement state
        var grappleState = new PlatformerMovement.MovementState(
            name: "Grappling",
            allowMovement: true,
            applyGravity: true,
            applyDeceleration: true,
            canJump: false,
            gravityMultiplier: 1f,
            accelerationMultiplier: 0.05f,
            decelerationMultiplier: 0.05f,
            jumpForceMultiplier: 0f,
            maxSpeedMultiplier: 1f
        );

        character.RaiseEvent("movement_override_start", grappleState);
        character.RaiseEvent("grapple_started", grapplePoint);

        // Start reeling immediately if jump is held
        if (startReeling)
        {
            isReeling = true;
            character.RaiseEvent("grapple_reel_started", null);
        }
        else
        {
            isReeling = false;
        }

        if (grappleLine != null)
            grappleLine.enabled = true;
    }

    private void StopGrapple()
    {
        if (!isGrappling) return;

        if (isReeling)
        {
            StopReeling();
        }

        isGrappling = false;
        character.RaiseEvent("movement_override_end", null);
        character.RaiseEvent("grapple_ended", grapplePoint);

        if (grappleLine != null)
            grappleLine.enabled = false;
    }

    private void UpdateSwingPhysics(float fixedDeltaTime)
    {
        Vector2 playerPos = grappleOrigin.position;
        Vector2 toGrapple = grapplePoint - playerPos;
        float currentDistance = toGrapple.magnitude;

        // Update swing arc
        swingArc = CalculateSwingArc(playerPos, grapplePoint, currentRopeLength);

        // Calculate stretch
        float stretchRatio = CalculateStretchRatio(currentDistance, currentRopeLength);

        // Apply swing physics based on stretch
        if (stretchRatio > 0f)
        {
            ApplyStretchPhysics(stretchRatio, currentDistance, fixedDeltaTime);
        }

        // Always apply swing friction
        ApplySwingFriction(fixedDeltaTime);

        // Apply tangential motion based on current velocity
        ApplyTangentialMotion(currentDistance, fixedDeltaTime);

        // Apply gravity component along the rope
        ApplyGravityAlongRope(fixedDeltaTime);

        // Check for detachment
        if (currentDistance > physicsConfig.maxDistance * 1.5f)
        {
            StopGrapple();
            return;
        }

        // Store previous velocity for next frame calculations
        previousVelocity = rb.linearVelocity;
    }

    private void ApplyStretchPhysics(float stretchRatio, float currentDistance, float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        Vector2 radialDirection = toGrapple.normalized;

        // Calculate dynamic stiffness based on stretch
        float dynamicStiffness = CalculateDynamicStiffness(stretchRatio);

        // Calculate restoring force (proportional to stretch, with exponential stiffness)
        float stretchAmount = currentDistance - currentRopeLength;
        float restoringForceMagnitude = dynamicStiffness * stretchAmount;

        // Apply restoring force toward grapple point
        Vector2 restoringForce = radialDirection * restoringForceMagnitude;
        rb.AddForce(restoringForce, ForceMode2D.Force);

        // Apply damping to prevent oscillations
        Vector2 radialVelocity = Vector2.Dot(rb.linearVelocity, radialDirection) * radialDirection;
        rb.AddForce(-radialVelocity * physicsConfig.ropeDamping * dynamicStiffness, ForceMode2D.Force);

        // Momentum conversion to tangent
        ConvertRadialMomentumToTangent(radialVelocity.magnitude, radialDirection);

        if (showPhysicsDebug)
        {
            Debug.DrawRay(grappleOrigin.position, restoringForce.normalized * 2f, Color.red, fixedDeltaTime);
            Debug.DrawRay(grappleOrigin.position, radialDirection * stretchAmount, Color.blue, fixedDeltaTime);
        }
    }

    private void ConvertRadialMomentumToTangent(float radialSpeed, Vector2 radialDirection)
    {
        if (radialSpeed < 0.1f) return;

        Vector2 currentVelocity = rb.linearVelocity;
        Vector2 radialVelocity = Vector2.Dot(currentVelocity, radialDirection) * radialDirection;
        Vector2 radialVelDir = radialVelocity.normalized;

        // Get both possible tangent directions
        Vector2 tangent1 = new Vector2(-radialDirection.y, radialDirection.x);
        Vector2 tangent2 = -tangent1;

        // Find which tangent is closer to current velocity direction
        Vector2 closestTangent = GetClosestTangentDirection(currentVelocity, tangent1, tangent2);

        // Calculate dot product (cosine of angle)
        // 1.0 = same direction, 0.0 = perpendicular, -1.0 = opposite
        float cosineAlignment = Vector2.Dot(radialVelDir, closestTangent);

        // Convert to 0-1 factor where:
        // cosine=1.0 (0°) -> factor=1.0
        // cosine=0.0 (90°) -> factor=0.0
        // cosine=-1.0 (180°) -> factor=0.0
        float alignmentFactor = Mathf.Max(0f, cosineAlignment);

        // Add power for sharper falloff (optional)
        alignmentFactor = Mathf.Pow(alignmentFactor, 2f);

        // Base conversion factor
        float conversionFactor = physicsConfig.tangentConversionFactor * alignmentFactor;

        // Convert radial momentum to tangent
        float momentumToConvert = radialSpeed * conversionFactor;
        Vector2 tangentVelocity = closestTangent * momentumToConvert;

        // Apply the conversion
        Vector2 newVelocity = currentVelocity - radialVelocity * conversionFactor + tangentVelocity;
        rb.linearVelocity = newVelocity;

        if (showPhysicsDebug)
        {
            Debug.DrawRay(grappleOrigin.position, closestTangent * 3f, Color.green, 0.1f);
            Debug.DrawRay(grappleOrigin.position, tangentVelocity, Color.yellow, 0.1f);

            float angle = Mathf.Acos(Mathf.Clamp(cosineAlignment, -1f, 1f)) * Mathf.Rad2Deg;
            Debug.Log($"Tangent conversion: Angle={angle:F1}°, " +
                     $"Cosine={cosineAlignment:F2}, " +
                     $"Factor={alignmentFactor:F2}, " +
                     $"Conversion={conversionFactor:P0}");
        }
    }

    // Keep these the same as above (simple versions)
    private void ApplySwingFriction(float fixedDeltaTime)
    {
        // Apply air resistance/friction to swing
        rb.linearVelocity *= physicsConfig.swingFriction;
    }

    private void ApplyTangentialMotion(float currentDistance, float fixedDeltaTime)
    {
        Vector2 toGrapple = grapplePoint - (Vector2)grappleOrigin.position;
        Vector2 radialDirection = toGrapple.normalized;
        Vector2 tangentDirection = new Vector2(-radialDirection.y, radialDirection.x);

        // Get tangential component of velocity
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

    private void UpdateGrappleVisuals()
    {
        if (grappleLine != null)
        {
            grappleLine.SetPosition(0, grappleOrigin.position);
            grappleLine.SetPosition(1, grapplePoint);

            // Visual feedback for reeling
            if (isReeling)
            {
                grappleLine.startColor = Color.yellow;
                grappleLine.endColor = Color.yellow;
                grappleLine.widthMultiplier = 0.15f;
            }
            else
            {
                grappleLine.startColor = Color.white;
                grappleLine.endColor = Color.white;
                grappleLine.widthMultiplier = 0.1f;
            }
        }

        // Physics debug visualization
        if (showPhysicsDebug && isGrappling)
        {
            // Draw swing circle
            DrawSwingCircle();

            // Draw tangent directions
            if (swingArc != null)
            {
                Debug.DrawRay(grappleOrigin.position, swingArc.tangentDirection * 3f, Color.green);
                Debug.DrawRay(grappleOrigin.position, -swingArc.tangentDirection * 3f, Color.green);

                // Draw current angle indicator
                Vector2 angleIndicator = Quaternion.Euler(0, 0, swingArc.currentAngle * Mathf.Rad2Deg) * Vector2.down * currentRopeLength;
                Debug.DrawLine(grapplePoint, grapplePoint + angleIndicator, Color.cyan);
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

        // Default: aim up
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
            GUI.Label(new Rect(10, yPos, 400, 20), $"Grapple Point: {grapplePoint:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Rope Length: {currentRopeLength:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Swing Angle: {swingArc.currentAngle * Mathf.Rad2Deg:F1}°", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Velocity: {rb.linearVelocity.magnitude:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Momentum: {swingMomentum.magnitude:F2}", style); yPos += 20;
            GUI.Label(new Rect(10, yPos, 400, 20), $"Reeling: {isReeling}", style); yPos += 20;
        }
    }

    // Public API
    public bool IsGrappling() => isGrappling;
    public Vector2 GetGrapplePoint() => grapplePoint;
    public float GetRopeLength() => currentRopeLength;
    public SwingArc GetSwingArc() => swingArc;

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
}

// Data structure for grapple boost events
public struct GrappleBoostData
{
    public Vector2 direction;
    public float strength;
    public Vector2 momentum;
}