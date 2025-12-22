using UnityEngine;
using System;

[CreateAssetMenu(fileName = "CharacterConfig", menuName = "Character/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Movement Settings")]
    public float maxSpeed = 15f;
    public float groundAcceleration = 5f;
    public float airAcceleration = 0.8f;
    public float groundDeceleration = 1f;
    public float airDeceleration = .5f;

    [Header("Jump/Gravity")]
    public float jumpForce = 20f;
    public float gravity = 30f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.05f;


    [Header("Variable Jump Height")]
    public bool enableVariableJump = true;
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.2f;

    [Header("Abilities")]
    public AttackConfig attack = new AttackConfig();
    public DashConfig dash = new DashConfig();
    public GrappleDashConfig grappleDash = new GrappleDashConfig();
    public LungeConfig lunge = new LungeConfig();
}

[Serializable]
public class AttackConfig
{
    public bool isEnabled = false;
    public float damage = 1f;
    public float range = 1f;
    public float cooldown = 0.5f;
    public GameObject hitboxPrefab;
}

[Serializable]
public class DashConfig
{
    public bool isEnabled = false;

    [Header("Force Settings")]
    public float force = 25f;
    public float duration = 0.2f;
    public float cooldown = 1f;

    [Header("Force Application")]
    public bool applyInstantForce = true;
    public bool applyContinuousForce = false;
    public float continuousForceMultiplier = 1f;
    public bool massDependent = false;

    [Header("Velocity Preservation")]
    [Range(0f, 1f)] public float preserveHorizontalVelocity = 1f;
    [Range(0f, 1f)] public float preserveVerticalVelocity = 1f;

    [Header("Post-Dash Effects")]
    public bool applyPostDashDeceleration = false;
    public float postDashDecelerationMultilplier = 10f;
    public float postDashDecelerationDuration = 0.3f;

    [Header("Visual Feedback")]
    public GameObject trailPrefab;
    public AudioClip sound;
}

[Serializable]
public class GrappleDashConfig
{
    public bool isEnabled = false;

    [Header("Force Settings")]
    public float force = 30f;
    public float duration = 0.15f;
    public float cooldown = 0.8f;

    [Header("Tangential Dash Behavior")]
    [Tooltip("Minimum rope stretch/squash ratio to trigger tangent dash")]
    public float minRopeRatioThreshold = 0.01f;
    [Tooltip("Maximum angle difference for tangent selection (degrees)")]
    public float maxAngleDifference = 90f;

    [Header("Velocity Preservation")]
    [Tooltip("When true, preserves 100% of existing velocity for tangent dashes")]
    public bool preserveAllVelocityOnTangentDash = true;
    [Range(0f, 1f)] public float normalPreserveHorizontalVelocity = 1f;
    [Range(0f, 1f)] public float normalPreserveVerticalVelocity = 1f;

    [Header("Force Application")]
    public bool applyInstantForce = true;
    public bool applyContinuousForce = false;
    public float continuousForceMultiplier = 1f;
    public bool massDependent = false;

    [Header("Visual Feedback")]
    public GameObject trailPrefab;
    public AudioClip sound;
    [Tooltip("Color for grapple dash trail/effects")]
    public Color dashColor = Color.cyan;
}

[Serializable]
public class LungeConfig
{
    public bool isEnabled = false;
    public float horizontalForce = 20f;
    public float verticalForce = 5f;
    public float duration = 0.3f;
    public float cooldown = 2f;
    public bool cancelVerticalVelocity = true;

    [Header("Visual Feedback")]
    public ParticleSystem particles;
    public AudioClip sound;
}