using UnityEngine;
using System;

[CreateAssetMenu(fileName = "CharacterConfig", menuName = "Character/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Core Systems")]
    public MovementConfig movement = new MovementConfig();
    public ConditionConfig condition = new ConditionConfig();

    [Header("Abilities")]
    public AttackConfig attack = new AttackConfig();
    public DashConfig dash = new DashConfig();
    public GrappleDashConfig grappleDash = new GrappleDashConfig();
    public LungeConfig lunge = new LungeConfig();
}

[Serializable]
public class MovementConfig
{
    [Header("Speed Settings")]
    public float maxSpeed = 15f;
    public float groundAcceleration = 60f;
    public float airAcceleration = 48f;
    public float groundDeceleration = 48f;
    public float airDeceleration = 4.8f;

    [Header("Jump/Gravity")]
    public float jumpForce = 20f;
    public float gravity = 30f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.05f;

    [Header("Variable Jump Height")]
    public bool enableVariableJump = true;
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("Dropdown Settings")]
    [Tooltip("If true, dropdown mode does not deactivate after a set amount of time and instead waits for the player to release the key. This causes the player to fall straight through platforms without landing first.")]
    public bool smoothDropdown = false;
    [Tooltip("Time after which dropdown disables until re-enable conditions are met. Not used if smoothDropdown = true.")]
    public float maxDropdownTime = .25f;
    [Tooltip("Minimum time the player must be grounded to enter dropdown mode.")]
    public float minGroundedTime = .05f;

    [Header("Turnaround Skid Settings")]
    [Tooltip("Enable or disable the turnaround skid and dash feature.")]
    public bool enableTurnaround = true;
    [Tooltip("Minimum average speed (over past frames) required to trigger turnaround dash. Expressed as fraction of maxSpeed.")]
    [Range(0f, 1f)] public float turnaroundThreshold = 0.5f;
    [Tooltip("Extra velocity boost applied when performing a turnaround dash.")]
    public float turnaroundMagnitude = 8f;
    [Tooltip("Amount of time the character will remain stopped during a turnaround.")]
    public float turnaroundDelay = .35f;
    [Tooltip("Number of frames to track velocity history for average speed calculation.")]
    public int turnaroundHistoryFrames = 8;
    [Tooltip("Number of recent frames not counted for turnaroundHistory.")]
    public int turnaroundHistoryDeadzone = 8;

    [Header("Layers")]
    public LayerMask groundLayer;
    public LayerMask platformLayer;
    public float groundCheckRadius = 0.2f;
}

[Serializable]
public class ConditionConfig
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    [Tooltip("Default cooldown to being hit. Attacks may provide their own overrides when they deal damage.")]
    public float invulnerabilityDuration = 0.5f;

    [Header("Damage Text")]
    public Vector2 textOffset = new Vector2(0, 1f);
    public Color damageColor = Color.red;
    public Color healColor = Color.green;
    public Color critColor = Color.yellow;

    [Header("Death Settings")]
    public bool destroyOnDeath = true;
    public GameObject deathEffect;
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
    public float postDashGroundDecelerationMultilplier = 2f;
    public float postDashAirDecelerationMultilplier = 20f;
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

    [Header("Force Settings")]
    public Vector2 force = new Vector2(20f, 5f);
    public float duration = 0.3f;
    public float cooldown = 2f;

    [Header("Force Application")]
    public bool applyInstantForce = true;
    public bool applyContinuousForce = false;
    public float continuousForceMultiplier = 1f;
    public bool massDependent = false;

    [Header("Velocity Preservation")]
    [Range(0f, 1f)] public float preserveHorizontalVelocity = 0f;
    [Range(0f, 1f)] public float preserveVerticalVelocity = 0f;
    public bool cancelVerticalVelocity = true;

    [Header("Post-Lunge Effects")]
    public bool applyPostLungeDeceleration = false;
    public float postLungeGroundDecelerationMultilplier = 2f;
    public float postLungeAirDecelerationMultilplier = 20f;
    public float postLungeDecelerationDuration = 0.3f;

    [Header("Visual Feedback")]
    public GameObject trailPrefab;
    public ParticleSystem particles;
    public AudioClip sound;
}