using UnityEngine;
using System;

[CreateAssetMenu(fileName = "CharacterConfig", menuName = "Character/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("Movement Settings")]
    public float maxSpeed = 6f;
    public float acceleration = 20f;
    public float deceleration = 15f;
    public float jumpForce = 13f;
    public float gravity = 30f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.1f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airDecelerationMultiplier = 0.5f;
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
    public float force = 25f;
    public float duration = 0.2f;
    public float cooldown = 1f;
    public bool preserveVerticalVelocity = true;

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