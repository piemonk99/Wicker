using System;
using UnityEngine;

/// <summary>
/// Configuration class for grapple movement state settings.
/// Defines how the character moves while grappling.
/// </summary>
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

    // Ground-only multipliers
    public float groundAccelerationMultiplier = 1f;
    public float groundDecelerationMultiplier = 1f;

    // Air-only multipliers
    public float airAccelerationMultiplier = 0.025f;
    public float airDecelerationMultiplier = 0.025f;

    public float jumpForceMultiplier = 0f;
    public float maxSpeedMultiplier = 1f;

    /// <summary>
    /// Converts this GrappleMovementState to a PlatformerMovement.MovementState.
    /// </summary>
    /// <returns>A MovementState with the configured values.</returns>
    public MovementState ToMovementState()
    {
        return new MovementState(
            name: name,
            allowMovement: allowMovement,
            applyGravity: applyGravity,
            applyDeceleration: applyDeceleration,
            canJump: canJump,
            gravityMultiplier: gravityMultiplier,
            groundAccelerationMultiplier: groundAccelerationMultiplier,
            groundDecelerationMultiplier: groundDecelerationMultiplier,
            airAccelerationMultiplier: airAccelerationMultiplier,
            airDecelerationMultiplier: airDecelerationMultiplier,
            jumpForceMultiplier: jumpForceMultiplier,
            maxSpeedMultiplier: maxSpeedMultiplier
        );
    }
}

/// <summary>
/// Configuration for grapple swing physics parameters.
/// Controls rope behavior, stretch/squash physics, and swing forces.
/// </summary>
[System.Serializable]
public class GrappleSwingPhysicsConfig
{
    public float maxDistance = 20f;
    public float ropeDamping = 0.1f;
    public float boostMultiplier = 1.1f;
    public float minBoostVelocity = 2f;

    [Header("Ground Deceleration Settings")]
    [Tooltip("Horizontal velocity at which full ground deceleration is applied")]
    public float maxGroundDecelerationVelocity = 15f;

    [Tooltip("Horizontal velocity at which minimum ground deceleration is applied")]
    public float minGroundDecelerationVelocity = 25f;


    [Header("Friction Settings")]
    [Tooltip("Friction applied to all velocity. Higher values = less movement.")]
    [Range(0f, 1f)]
    public float friction = 0f;

    [Tooltip("Friction applied only to motion perpendicular to the rope direction. Higher values = less swinging.")]
    [Range(0f, 1f)]
    public float tangentialFriction = 0.003f;

    [Tooltip("Velocity where we reach minimum reeling/unreeling friction")]
    public float minReelingFrictionVelocity = 50f;
    [Tooltip("Velocity where we reach maximum reeling/unreeling friction")]
    public float maxReelingFrictionVelocity = 5f;

    [Tooltip("Minimum added friction when reeling (at high speeds)")]
    [Range(-1f, 1f)] public float minReelingTangentialFriction = 0.002f;
    [Tooltip("Maximum added friction when reeling (at low speeds)")]
    [Range(-1f, 1f)] public float maxReelingTangentialFriction = 0.005f;

    [Tooltip("Minimum added friction when unreeling (at high speeds)")]
    [Range(-1f, 1f)] public float minUnreelingTangentialFriction = -0.002f;
    [Tooltip("Maximum added friction when unreeling (at low speeds)")]
    [Range(-1f, 1f)] public float maxUnreelingTangentialFriction = -0.005f;


    [Header("Stretch Physics (Outside Rope)")]
    public bool enableStretch = true;
    public float stretchStiffness = 200f;
    public float stretchStiffnessExponent = 2f;

    [Header("Squash Physics (Inside Rope)")]
    public bool enableSquash = false;
    public float squashStiffness = 50f;
    public float squashStiffnessExponent = 2f;

    public LayerMask grappleLayers;
}

/// <summary>
/// Configuration for grapple reeling and unreeling behavior.
/// Controls rope length adjustment speeds and limits.
/// </summary>
[System.Serializable]
public class GrappleReelConfig
{
    [Header("Reeling In")]
    public float reelSpeed = 50f;
    public float slackReelMultiplier = 10f;
    public float minRopeLength = 1f;
    public float reelSmoothness = 0.1f;

    [Header("Unreeling Out")]
    public float unreelSpeed = 50f;
    public float maxRopeLength = 20f;
    public float unreelSmoothness = 0.1f;
}

/// <summary>
/// Configuration for grapple visual elements.
/// Controls hook and rope prefabs and visual settings.
/// </summary>
[System.Serializable]
public class GrappleVisualConfig
{
    [Header("Hook Visual")]
    public GameObject hookPrefab; // Prefab to instantiate at grapple point
    public Vector2 hookScale = Vector2.one;

    [Header("Rope Visual")]
    public GameObject ropePrefab; // Complete rope prefab with two anchor points
    public string ropeStartAnchorName = "StartAnchor"; // Anchor at player end
    public string ropeEndAnchorName = "EndAnchor";     // Anchor at hook end
}

/// <summary>
/// Configuration for grapple sound behavior.
/// Contains references to sound nodes and audio parameters.
/// </summary>
[System.Serializable]
public class GrappleSoundConfig
{
    [Header("Sound References")]
    [Tooltip("Root SoundNode container for this grapple type (e.g., Rope, Metal, Energy)")]
    public SoundNode grappleSoundSet;

    [Header("Creak Sounds")]
    [Tooltip("Minimum volume when rope is loose")]
    [Range(0f, 0.3f)]
    public float creakMinVolume = 0f;

    [Tooltip("Maximum volume when rope is taut")]
    [Range(0.3f, 1f)]
    public float creakMaxVolume = 0.5f;

    [Tooltip("Force at which minimum creaking volume is reached")]
    public float creakMinForce = 30f;

    [Tooltip("Force at which maximum creaking volume is reached")]
    public float creakMaxForce = 100f;

    [Tooltip("Percentage of creak sound made in world vs 2D")]
    public float creakSpatialBlend = .8f;
}

/// <summary>
/// Data structure representing the swing arc of a grapple.
/// Contains information about the circular motion around the grapple point.
/// </summary>
[System.Serializable]
public class SwingArc
{
    public Vector2 center;
    public float radius;
    public float currentAngle;
    public Vector2 tangentDirection;
    public Vector2 radialDirection;
}

/// <summary>
/// Data structure for grapple boost events.
/// Contains information about the boost direction and strength.
/// </summary>
public struct GrappleBoostData
{
    public Vector2 direction;
    public float strength;
    public Vector2 momentum;
}

/// <summary>
/// Represents the current state of the grapple rope.
/// Contains information about stretch/squash and ratio calculations.
/// </summary>
public struct RopeState
{
    public float ratio;
    public bool isStretch;
    public bool isSquash;

    public RopeState(float ratio, bool isStretch, bool isSquash)
    {
        this.ratio = ratio;
        this.isStretch = isStretch;
        this.isSquash = isSquash;
    }
}

/// <summary>
/// Manages all grapple configuration data and provides helper methods.
/// Now works with GrappleConfig ScriptableObject.
/// </summary>
public class GrappleConfigManager
{
    private GrappleConfig config;

    /// <summary>
    /// Initializes a new instance of GrappleConfigManager with a GrappleConfig ScriptableObject.
    /// </summary>
    /// <param name="config">The grapple configuration ScriptableObject.</param>
    public GrappleConfigManager(GrappleConfig config)
    {
        this.config = config ?? CreateDefaultConfig();
    }

    /// <summary>
    /// Creates a default configuration for safety.
    /// </summary>
    private GrappleConfig CreateDefaultConfig()
    {
        Debug.LogWarning("No grapple config provided, using defaults");
        return ScriptableObject.CreateInstance<GrappleConfig>();
    }

    /// <summary>
    /// Gets the grapple name for UI or debugging.
    /// </summary>
    public string GetGrappleName()
    {
        return config.GrappleName;
    }
}