// WeaponComponent.cs
using UnityEngine;
using System.Collections.Generic;

public class WeaponComponent : MonoBehaviour, ICharacterComponent
{
    // Reference to core
    private CharacterCore character;
    private Rigidbody2D rb;
    private CharacterMovement movementSystem;
    private GrappleSystem grappleSystem;

    // Weapon Management
    private WeaponConfig currentWeaponConfig;
    private GameObject currentWeaponInstance;
    private IWeaponController currentController;

    // State
    private float attackCooldownTimer = 0f;
    private Vector2 lastAttackDirection = Vector2.right;

    // Velocity tracking for damage calculation
    private Vector2 previousVelocity;
    private float currentVelocityMagnitude;

    // Interface
    public WeaponConfig CurrentWeapon => currentWeaponConfig;
    public bool IsOnCooldown => attackCooldownTimer > 0;
    public bool IsAttacking => currentController?.IsAttacking() ?? false;

    public void Initialize(CharacterCore character)
    {
        this.character = character;
        rb = GetComponent<Rigidbody2D>();
        movementSystem = character.GetComponent<CharacterMovement>();
        grappleSystem = character.GetCharacterComponent<GrappleSystem>();

        // Subscribe to events
        character.OnEvent += HandleEvent;

        Debug.Log($"WeaponComponent initialized for {character.gameObject.name}");
    }

    public void Tick(float deltaTime)
    {
        // Update cooldown
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= deltaTime;
        }

        // Update current velocity for damage calculations
        if (rb != null)
        {
            currentVelocityMagnitude = rb.linearVelocity.magnitude;
        }

        // Tick current weapon controller
        if (currentController != null)
        {
            currentController.Tick(deltaTime);
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Store velocity for next frame
        if (rb != null)
        {
            previousVelocity = rb.linearVelocity;
        }

        // Physics tick for current weapon controller
        if (currentController != null)
        {
            currentController.PhysicsTick(fixedDeltaTime);
        }
    }

    private void HandleEvent(string type, object data)
    {
        switch (type)
        {
            case "attack_pressed":
                Debug.Log("attack was pressed!");

                if (!IsOnCooldown && currentWeaponConfig != null)
                {
                    TryAttack();
                }
                break;

            case "weapon_equipped":
                if (data is WeaponConfig newWeapon)
                {
                    EquipWeapon(newWeapon);
                }
                break;

            case "weapon_unequipped":
                UnequipWeapon();
                break;

            case "config_changed":
                // Re-initialize with current weapon if config changed
                if (currentWeaponConfig != null)
                {
                    EquipWeapon(currentWeaponConfig);
                }
                break;
        }
    }

    public void EquipWeapon(WeaponConfig weaponConfig)
    {
        // Clean up current weapon
        UnequipWeapon();

        // Set new config
        currentWeaponConfig = weaponConfig;

        if (weaponConfig == null)
        {
            Debug.LogWarning("Tried to equip null weapon config");
            return;
        }

        Debug.Log($"Equipping weapon: {weaponConfig.weaponName}");

        // Create weapon instance if prefab exists
        if (weaponConfig.weaponPrefab != null)
        {
            currentWeaponInstance = Instantiate(
                weaponConfig.weaponPrefab,
                transform.position,
                Quaternion.identity,
                transform
            );

            // Get controller interface based on weapon type
            currentController = GetControllerForWeaponType(weaponConfig.weaponType);

            if (currentController != null)
            {
                currentController.Initialize(weaponConfig, character, this);
            }
        }
        else
        {
            // If no prefab, create an empty GameObject for the weapon
            currentWeaponInstance = new GameObject($"{weaponConfig.weaponName}_Instance");
            currentWeaponInstance.transform.SetParent(transform);
            currentWeaponInstance.transform.localPosition = Vector3.zero;

            currentController = GetControllerForWeaponType(weaponConfig.weaponType);
            if (currentController != null)
            {
                currentController.Initialize(weaponConfig, character, this);
            }
        }

        // Raise event
        character.RaiseEvent("weapon_changed", weaponConfig);
    }


    private IWeaponController GetControllerForWeaponType(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Hitbox:
                var hitboxController = currentWeaponInstance.AddComponent<HitboxWeaponController>();
                hitboxController.Initialize(currentWeaponConfig, character, this);
                return hitboxController;

            case WeaponType.CursorSword:
                var cursorController = currentWeaponInstance.AddComponent<CursorSwordController>();
                cursorController.Initialize(currentWeaponConfig, character, this);
                return cursorController;

            case WeaponType.AutoAttack:
                var autoController = currentWeaponInstance.AddComponent<AutoAttackController>();
                autoController.Initialize(currentWeaponConfig, character, this);
                return autoController;

            default:
                Debug.LogWarning($"No controller found for weapon type: {weaponType}");
                return null;
        }
    }

    private void UnequipWeapon()
    {
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
        }

        currentController = null;
        currentWeaponConfig = null;
    }

    private void TryAttack()
    {
        if (currentWeaponConfig == null || IsOnCooldown) return;

        // For different weapon types, delegate to their controllers
        if (currentController != null && currentController.TryAttack())
        {
            // Set cooldown
            attackCooldownTimer = currentWeaponConfig.attackCooldown;

            // Raise attack event
            character.RaiseEvent("attack_performed", currentWeaponConfig);
        }
    }

    // Helper methods for weapons to use
    public float GetCurrentVelocityMagnitude() => currentVelocityMagnitude;
    public bool IsGrappling() => grappleSystem?.IsGrappling() ?? false;
    public Vector2 GetGrappleVelocity() => movementSystem?.GetVelocity() ?? rb?.linearVelocity ?? Vector2.zero;
    public Vector2 GetAttackDirection()
    {
        // For players: towards cursor
        // For enemies: towards target
        // Default to character facing direction
        return lastAttackDirection;
    }

    public void SetAttackDirection(Vector2 direction)
    {
        lastAttackDirection = direction.normalized;
    }

    // Calculate damage with velocity scaling
    public float CalculateDamage(float baseDamage)
    {
        if (!currentWeaponConfig.scalesWithVelocity)
            return baseDamage;

        float velocityBonus = Mathf.Min(
            currentVelocityMagnitude * currentWeaponConfig.velocityDamageMultiplier,
            currentWeaponConfig.maxVelocityBonus
        );

        return baseDamage + velocityBonus;
    }

    // Cleanup
    private void OnDestroy()
    {
        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }
        UnequipWeapon();
    }
}

// Updated IWeaponController interface
public interface IWeaponController
{
    void Initialize(WeaponConfig config, CharacterCore character, WeaponComponent owner);
    bool TryAttack();
    bool IsAttacking();
    void Tick(float deltaTime);
    void PhysicsTick(float fixedDeltaTime);
}