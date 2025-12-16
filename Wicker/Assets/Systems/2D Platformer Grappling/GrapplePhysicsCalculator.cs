using UnityEngine;

/// <summary>
/// Performs physics calculations for grapple swing behavior.
/// Contains mathematical operations for rope physics and swing arcs.
/// </summary>
public class GrapplePhysicsCalculator
{
    private GrappleSwingPhysicsConfig physicsConfig;

    public GrapplePhysicsCalculator(GrappleSwingPhysicsConfig physicsConfig)
    {
        this.physicsConfig = physicsConfig;
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
}