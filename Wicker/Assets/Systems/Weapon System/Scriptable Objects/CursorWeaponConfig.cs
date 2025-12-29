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

	[Header("Acceleration Mode")]
	public float angularAcceleration = 360f;
	public float angularDeceleration = 720f;
	public float maxAngularVelocity = 720f;

	[Header("Combat Settings")]
	public float damagePerSpeedUnit = 0.2f;
	public float minimumDamageSpeed = 2f;
	public float baseKnockback = 5f;
	public float speedKnockbackMultiplier = 0.5f;
	public float maxKnockback = 30f;

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