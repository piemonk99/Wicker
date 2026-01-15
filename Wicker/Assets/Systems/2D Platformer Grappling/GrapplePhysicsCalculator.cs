using UnityEngine;

/// <summary>
/// Performs physics calculations for grapple swing behavior.
/// Contains mathematical operations for rope physics and swing arcs.
/// </summary>
public class GrapplePhysicsCalculator
{
    private GrapplePhysicsConfig physicsConfig;

    public GrapplePhysicsCalculator(GrapplePhysicsConfig physicsConfig)
    {
        this.physicsConfig = physicsConfig;
    }

    /// <summary>
    /// Calculates velocity-based ground deceleration multiplier for sliding.
    /// Returns a multiplier between currentStateValue and airDecelerationMultiplier based on speed.
    /// </summary>
    public float CalculateVelocityBasedGroundDeceleration(
        float currentHorizontalSpeed,
        float currentStateGroundDeceleration,
        float airDecelerationMultiplier)
    {
        float maxVel = physicsConfig.maxGroundDecelerationVelocity;
        float minVel = physicsConfig.minGroundDecelerationVelocity;

        // If we're below the minimum velocity threshold, use the full state value
        if (currentHorizontalSpeed <= maxVel)
            return currentStateGroundDeceleration;

        // If we're above the maximum velocity threshold, use air deceleration
        if (currentHorizontalSpeed >= minVel)
            return airDecelerationMultiplier;

        // Between thresholds: lerp from state value to air deceleration
        float t = Mathf.InverseLerp(maxVel, minVel, currentHorizontalSpeed);

        // Apply smoothstep for better transition
        float smoothT = t * t * (3f - 2f * t);

        return Mathf.Lerp(currentStateGroundDeceleration, airDecelerationMultiplier, smoothT);
    }

    /// <summary>
    /// Calculates the swing arc geometry based on player position, grapple point, and rope length.
    /// </summary>
    public SwingArc CalculateSwingArc(Vector2 playerPos, Vector2 grapplePos, float ropeLength)
    {
        SwingArc arc = new SwingArc();
        arc.center = grapplePos;
        arc.radius = ropeLength;

        Vector2 toPlayer = playerPos - grapplePos;
        arc.currentAngle = Vector2.SignedAngle(Vector2.down, toPlayer) * Mathf.Deg2Rad;
        arc.radialDirection = toPlayer.normalized;
        arc.tangentDirection = new Vector2(-arc.radialDirection.y, arc.radialDirection.x);

        return arc;
    }

    /// <summary>
    /// Calculates dynamic stiffness based on stretch/squash ratio.
    /// </summary>
    public float CalculateDynamicStiffness(float ratio, bool isStretch)
    {
        if (Mathf.Abs(ratio) < 0.0001f) return 0f;

        if (isStretch)
            return physicsConfig.stretchStiffness * Mathf.Pow(1f + ratio, physicsConfig.stretchStiffnessExponent);
        else
            return physicsConfig.squashStiffness * Mathf.Pow(1f + Mathf.Abs(ratio), physicsConfig.squashStiffnessExponent);
    }

    /// <summary>
    /// Performs a raycast for grapple targeting.
    /// </summary>
    public RaycastHit2D PerformGrappleRaycast(Vector2 origin, Vector2 direction, LayerMask layers, float maxDistance)
    {
        return Physics2D.Raycast(origin, direction, maxDistance, layers);
    }

    /// <summary>
    /// Calculates the current rope state based on distance and rope length.
    /// </summary>
    public RopeState GetRopeState(float currentDistance, float ropeLength)
    {
        float slack = ropeLength - currentDistance;
        float ratio = 0f;
        bool isStretch = false;
        bool isSquash = false;

        // Check for stretch (outside rope circle)
        if (currentDistance > ropeLength && physicsConfig.enableStretch)
        {
            isStretch = true;
            ratio = (currentDistance - ropeLength) / ropeLength;
        }
        // Check for squash (inside rope circle beyond threshold)
        else if (slack > 0.01f && physicsConfig.enableSquash)
        {
            isSquash = true;
            ratio = -(slack - 0.01f) / ropeLength;
        }

        return new RopeState(ratio, isStretch, isSquash);
    }
}