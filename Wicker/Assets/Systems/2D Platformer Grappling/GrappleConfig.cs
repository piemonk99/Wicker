using UnityEngine;

/// <summary>
/// Complete grapple configuration as a ScriptableObject.
/// Create assets via: Right-click -> Create -> Grapple System -> Grapple Config
/// </summary>
[CreateAssetMenu(fileName = "NewGrappleConfig", menuName = "Grapple System/Grapple Config")]
public class GrappleConfig : ScriptableObject
{
    [Header("Movement Settings")]
    public GrappleMovementState movementState = new GrappleMovementState();

    [Header("Physics Settings")]
    public GrappleSwingPhysicsConfig physicsConfig = new GrappleSwingPhysicsConfig();

    [Header("Reeling Settings")]
    public GrappleReelConfig reelConfig = new GrappleReelConfig();

    [Header("Visual Settings")]
    public GrappleVisualConfig visualConfig = new GrappleVisualConfig();

    [Header("Audio Settings")]
    public GrappleSoundConfig soundConfig = new GrappleSoundConfig();

    // Helper properties for common config access
    public string GrappleName => movementState.name;
    public LayerMask GrappleLayers => physicsConfig.grappleLayers;
    public SoundNode SoundSet => soundConfig.grappleSoundSet;

    /// <summary>
    /// Creates a deep copy of this config (useful for runtime modifications).
    /// </summary>
    public GrappleConfig Clone()
    {
        var clone = CreateInstance<GrappleConfig>();

        // Clone movement state
        clone.movementState = new GrappleMovementState()
        {
            name = movementState.name,
            allowMovement = movementState.allowMovement,
            applyGravity = movementState.applyGravity,
            applyDeceleration = movementState.applyDeceleration,
            canJump = movementState.canJump,
            gravityMultiplier = movementState.gravityMultiplier,
            groundAccelerationMultiplier = movementState.groundAccelerationMultiplier,
            airAccelerationMultiplier = movementState.airAccelerationMultiplier,
            groundDecelerationMultiplier = movementState.groundDecelerationMultiplier,
            airDecelerationMultiplier = movementState.airDecelerationMultiplier,
            jumpForceMultiplier = movementState.jumpForceMultiplier,
            maxSpeedMultiplier = movementState.maxSpeedMultiplier
        };

        // Clone physics config
        clone.physicsConfig = new GrappleSwingPhysicsConfig()
        {
            maxDistance = physicsConfig.maxDistance,
            ropeDamping = physicsConfig.ropeDamping,
            maxGroundDecelerationVelocity = physicsConfig.maxGroundDecelerationVelocity,
            minGroundDecelerationVelocity = physicsConfig.minGroundDecelerationVelocity,
            friction = physicsConfig.friction,
            tangentialFriction = physicsConfig.tangentialFriction,
            boostMultiplier = physicsConfig.boostMultiplier,
            minBoostVelocity = physicsConfig.minBoostVelocity,
            enableStretch = physicsConfig.enableStretch,
            stretchStiffness = physicsConfig.stretchStiffness,
            stretchStiffnessExponent = physicsConfig.stretchStiffnessExponent,
            enableSquash = physicsConfig.enableSquash,
            squashStiffness = physicsConfig.squashStiffness,
            squashStiffnessExponent = physicsConfig.squashStiffnessExponent,
            grappleLayers = physicsConfig.grappleLayers
        };

        // Clone reel config
        clone.reelConfig = new GrappleReelConfig()
        {
            reelSpeed = reelConfig.reelSpeed,
            slackReelMultiplier = reelConfig.slackReelMultiplier,
            minRopeLength = reelConfig.minRopeLength,
            reelSmoothness = reelConfig.reelSmoothness,
            unreelSpeed = reelConfig.unreelSpeed,
            maxRopeLength = reelConfig.maxRopeLength,
            unreelSmoothness = reelConfig.unreelSmoothness
        };

        // Clone visual config (shallow copy for Unity objects)
        clone.visualConfig = new GrappleVisualConfig()
        {
            hookPrefab = visualConfig.hookPrefab,
            hookScale = visualConfig.hookScale,
            ropePrefab = visualConfig.ropePrefab,
            ropeStartAnchorName = visualConfig.ropeStartAnchorName,
            ropeEndAnchorName = visualConfig.ropeEndAnchorName
        };

        // Clone sound config
        clone.soundConfig = new GrappleSoundConfig()
        {
            grappleSoundSet = soundConfig.grappleSoundSet,
            creakMinVolume = soundConfig.creakMinVolume,
            creakMaxVolume = soundConfig.creakMaxVolume,
            creakMinForce = soundConfig.creakMinForce,
            creakMaxForce = soundConfig.creakMaxForce,
            creakSpatialBlend = soundConfig.creakSpatialBlend
        };

        return clone;
    }
}