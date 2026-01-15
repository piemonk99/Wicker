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
    public GrapplePhysicsConfig physicsConfig = new GrapplePhysicsConfig();

    [Header("Mechanics Settings")]
    public GrappleMechanicsConfig mechanicsConfig = new GrappleMechanicsConfig();

    [Header("Reeling Settings")]
    public GrappleReelConfig reelConfig = new GrappleReelConfig();

    [Header("Visual Settings")]
    public GrappleVisualConfig visualConfig = new GrappleVisualConfig();

    [Header("Audio Settings")]
    public GrappleSoundConfig soundConfig = new GrappleSoundConfig();

    // Helper properties for common config access
    public string GrappleName => movementState.name;
    public LayerMask GrappleLayers => mechanicsConfig.grappleLayers;
    public SoundNode SoundSet => soundConfig.grappleSoundSet;

    // TODO later: when the grapple system is more set in stone, make a clone function
}