using UnityEngine;

/// <summary>
/// Performs physics calculations for grapple swing behavior.
/// Contains mathematical operations for rope physics, swing arcs, and motion calculations.
/// </summary>
public class GrapplePhysicsCalculator
{
    private GrappleSwingPhysicsConfig physicsConfig;

    /// <summary>
    /// Initializes a new instance of GrapplePhysicsCalculator with the provided physics configuration.
    /// </summary>
    /// <param name="physicsConfig">Physics configuration containing stiffness, damping, and other parameters.</param>
    public GrapplePhysicsCalculator(GrappleSwingPhysicsConfig physicsConfig)
    {
        this.physicsConfig = physicsConfig;
    }

    /// <summary>
    /// Calculates the swing arc geometry based on player position, grapple point, and rope length.
    /// Determines the circular motion parameters for pendulum-style swinging.
    /// </summary>
    /// <param name="playerPos">Current player position in world space.</param>
    /// <param name="grapplePos">Grapple point position in world space.</param>
    /// <param name="ropeLength">Current rope length.</param>
    /// <returns>A SwingArc object containing center, radius, and directional vectors.</returns>
    public SwingArc CalculateSwingArc(Vector2 playerPos, Vector2 grapplePos, float ropeLength)
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

    /// <summary>
    /// Calculates dynamic stiffness based on stretch/squash ratio.
    /// Stiffness increases exponentially with displacement to simulate realistic rope behavior.
    /// </summary>
    /// <param name="ratio">Stretch/squash ratio (positive for stretch, negative for squash).</param>
    /// <param name="isStretch">True if rope is stretching, false if squashing.</param>
    /// <returns>Dynamic stiffness value for force calculations.</returns>
    public float CalculateDynamicStiffness(float ratio, bool isStretch)
    {
        if (Mathf.Abs(ratio) < 0.0001f) return 0f;

        if (isStretch)
            return physicsConfig.stretchStiffness * Mathf.Pow(1f + ratio, physicsConfig.stretchStiffnessExponent);
        else
            return physicsConfig.squashStiffness * Mathf.Pow(1f + Mathf.Abs(ratio), physicsConfig.squashStiffnessExponent);
    }

    /// <summary>
    /// Determines which tangent direction is closest to the current velocity vector.
    /// Used for converting radial momentum to tangential motion.
    /// </summary>
    /// <param name="velocity">Current velocity vector of the player.</param>
    /// <param name="radialDirection">Radial direction from grapple point to player.</param>
    /// <returns>The tangent direction most aligned with the current velocity.</returns>
    public Vector2 GetClosestTangentDirection(Vector2 velocity, Vector2 radialDirection)
    {
        Vector2 tangent1 = new Vector2(-radialDirection.y, radialDirection.x);
        Vector2 tangent2 = -tangent1;

        float dotTangent = Vector2.Dot(velocity.normalized, tangent1);
        float dotOpposite = Vector2.Dot(velocity.normalized, tangent2);

        return dotTangent > dotOpposite ? tangent1 : tangent2;
    }

    /// <summary>
    /// Gets the tangent conversion factor based on rope state.
    /// Determines how much radial momentum should be converted to tangential motion.
    /// </summary>
    /// <param name="ratio">Stretch/squash ratio.</param>
    /// <param name="isStretch">True if rope is stretching, false if squashing.</param>
    /// <returns>Conversion factor between 0 and 1.</returns>
    public float GetTangentConversionFactor(float ratio, bool isStretch)
    {
        if (Mathf.Abs(ratio) < 0.0001f) return 0f;
        return isStretch ? physicsConfig.stretchToTangentConversion : physicsConfig.squashToTangentConversion;
    }

    /// <summary>
    /// Performs a raycast for grapple targeting.
    /// Checks for valid grapple points within maximum distance.
    /// </summary>
    /// <param name="origin">Raycast origin point (usually player position).</param>
    /// <param name="direction">Raycast direction (aim direction).</param>
    /// <param name="layers">Layer mask for grapple-able surfaces.</param>
    /// <param name="maxDistance">Maximum grapple distance.</param>
    /// <returns>RaycastHit2D containing hit information.</returns>
    public RaycastHit2D PerformGrappleRaycast(Vector2 origin, Vector2 direction, LayerMask layers, float maxDistance)
    {
        return Physics2D.Raycast(origin, direction, maxDistance, layers);
    }
}