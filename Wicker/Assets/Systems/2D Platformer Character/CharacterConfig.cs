using UnityEngine;
using System;

[CreateAssetMenu(fileName = "CharacterConfig", menuName = "Character/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Movement Settings")]
    public float maxSpeed = 15f;
    public float acceleration = 5f;
    public float deceleration = 1f;
    public float jumpForce = 20f;
    public float gravity = 30f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.05f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airDecelerationMultiplier = 0.1f;
    [Range(0f, 1f)] public float airAccelerationMultiplier = 0.8f;

    [Header("Variable Jump Height")]
    public bool enableVariableJump = true;
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.2f;

    [Header("Abilities")]
    public AttackConfig attack = new AttackConfig();
    public DashConfig dash = new DashConfig();
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