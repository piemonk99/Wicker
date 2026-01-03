using UnityEngine;

[CreateAssetMenu(fileName = "NewCursorWeaponConfig", menuName = "Weapons/Cursor Weapon Config")]
public class CursorWeaponConfig : ScriptableObject
{
	public CursorWeaponMechanicsConfig mechanics;
	public CursorWeaponVisualConfig visual;
	public CursorWeaponSoundConfig sound;
}

[System.Serializable]
public class CursorWeaponMechanicsConfig : WeaponMechanicsConfig
{
	[Header("Orbit Settings")]
	public float minOrbitRadius = 1f;
	public float maxOrbitRadius = 3f;

	[Header("Movement Mode")]
	public MovementMode movementMode = MovementMode.Direct;

	[Header("Direct Mode")]
	public float cursorFollowSpeed = 15f;
    [Tooltip("How smoothly the sword follows the cursor (0 = instant, 1 = very smooth)")]
    [Range(0f, 1f)]
    public float directModeSmoothing = 0.2f;

    [Header("Acceleration Mode")]
	public float angularAcceleration = 360f;
	public float angularDeceleration = 720f;
	public float maxAngularVelocity = 720f;

	[Header("Damage Settings")]
	public float minDamageMultiplier = 1f;
	public float maxDamageMultiplier = 2f;
	public float minDamageMultiplierSpeedPercent = .5f;
	public float maxDamageMultiplierSpeedPercent = .95f;

	[Header("Knockback Settings")]
	public float baseKnockback = 5f;
	public float speedKnockbackMultiplier = 0.5f;
	public float maxKnockback = 30f;

	[Header("I-Frame settings")]
    [Tooltip("Minimum duration of invincibility frames on a character hit by this weapon.")]
    public float minInvincibilityDuration = .33f;
    [Tooltip("Maximum duration of invincibility frames on a character hit by this weapon.")]
	public float maxInvincibilityDuration = .5f;
	[Tooltip("Percentage of max speed at which hit character gets minimum invincibility duration.")]
	public float minInvincibilitySpeedPercent = .5f;
    [Tooltip("Percentage of max speed at which hit character gets maximum invincibility duration.")]
    public float maxInvincibilitySpeedPercent = .9f;

    [Header("Speed Averaging Settings")]
    [Tooltip("Number of frames to average speed over (higher = smoother but more lag)")]
    [Range(1, 60)]
    public int speedAverageFrames = 10;
    [Tooltip("Weight of current frame in average (0-1, higher = more responsive)")]
    [Range(0.1f, 1f)]
    public float currentFrameWeight = 0.5f;
    [Tooltip("If true, use average speed for damage calculation instead of instantaneous speed")]
    public bool useAverageSpeedForDamage = true;
    [Tooltip("If true, use average speed for knockback calculation")]
    public bool useAverageSpeedForKnockback = true;

    [Header("Swept Collision Detection")]
	public LayerMask enemyLayers = ~0;
	public float sweptCollisionAngleStep = 5f;
	public int maxGhostCollidersPerFrame = 5;
	public bool alwaysUseSweptCollision = false;
}

public enum MovementMode
{
	Direct,
	Acceleration
}

[System.Serializable]
public class CursorWeaponVisualConfig : WeaponVisualConfig
{
	[Header("Debug Visualization")]
	public Color minOrbitDebugColor = Color.red;
	public Color maxOrbitDebugColor = Color.yellow;
	public Color sweptCollisionDebugColor = new Color(1f, 0.5f, 0f, 0.7f);
}

[System.Serializable]
public class CursorWeaponSoundConfig : WeaponSoundConfig
{
	[Header("Cursor Weapon Specific Sounds")]
	public SoundNode swooshSound;
	public float swooshVelocityThreshold = 10f;
	public float swooshVolume = 1.0f;
}