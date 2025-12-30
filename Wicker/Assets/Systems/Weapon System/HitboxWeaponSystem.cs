using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CharacterHitboxWeapon : CharacterWeapon
{
    // Visual instance
    private GameObject weaponInstance;

    // Attack state
    private float activeTimer = 0f;
    private List<GameObject> alreadyHit = new List<GameObject>();

    // Config references
    private HitboxWeaponMechanicsConfig hitboxMechanics;
    private HitboxWeaponVisualConfig hitboxVisual;
    private HitboxWeaponSoundConfig hitboxSound;

    // Cursor aiming
    private Camera mainCamera;
    private Vector2 lastAttackDirection;
    private Vector2 lastHitboxPosition;

    private float debugLineDuration = 0f;

    protected override void InitializeWithConfig(WeaponConfig config)
    {
        base.InitializeWithConfig(config);

        if (currentConfig == null) return;

        // Get specific configs from the main config
        hitboxMechanics = currentConfig.MechanicsConfig as HitboxWeaponMechanicsConfig;
        hitboxVisual = currentConfig.VisualConfig as HitboxWeaponVisualConfig;
        hitboxSound = currentConfig.SoundConfig as HitboxWeaponSoundConfig;

        if (hitboxMechanics == null || hitboxVisual == null)
        {
            Debug.LogError($"CharacterHitboxWeapon requires HitboxWeapon configs");
            return;
        }

        // Get main camera reference for cursor aiming
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("No main camera found for cursor aiming");
        }

        // Create visual instance if prefab exists
        if (hitboxVisual.weaponPrefab != null && weaponOrigin != null)
        {
            weaponInstance = Instantiate(
                hitboxVisual.weaponPrefab,
                weaponOrigin.position,
                Quaternion.identity,
                weaponOrigin
            );
        }

        // Set debug mode
        showDebugInfo = hitboxVisual.enableDebugVisualization;
    }

    /// <summary>
    /// Gets the attack direction toward the cursor
    /// </summary>
    private Vector2 GetAttackDirectionTowardCursor()
    {
        // Default to character's facing direction if no camera
        if (mainCamera == null)
        {
            return character.transform.right; // Or whatever your default facing direction is
        }

        // Get mouse position using Input System
        Vector2 mousePosition = Vector2.zero;

        if (Mouse.current != null)
        {
            mousePosition = Mouse.current.position.ReadValue();
        }
        else
        {
            // Fallback if mouse is not available
            return character.transform.right;
        }

        // Get mouse position in world space
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, mainCamera.nearClipPlane));
        mouseWorldPos.z = 0; // Ensure 2D

        // Calculate direction from character to cursor
        Vector2 direction = (mouseWorldPos - character.transform.position).normalized;

        // If direction is too small, use default
        if (direction.magnitude < 0.1f)
        {
            return character.transform.right;
        }

        return direction;
    }

    /// <summary>
    /// Gets the hitbox position relative to character and cursor direction
    /// </summary>
    private Vector2 GetHitboxPosition(Vector2 attackDirection)
    {
        if (character == null) return Vector2.zero;

        // Calculate offset based on attack direction
        Vector2 offset = attackDirection * hitboxMechanics.hitboxOffset.x +
                        Vector2.up * hitboxMechanics.hitboxOffset.y;

        return (Vector2)character.transform.position + offset;
    }

    /// <summary>
    /// Gets the rotation for the weapon visual based on attack direction
    /// </summary>
    private Quaternion GetWeaponRotation(Vector2 attackDirection)
    {
        float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
        return Quaternion.AngleAxis(angle, Vector3.forward);
    }

    protected override void TryAttack()
    {
        if (!CanAttack() || hitboxMechanics == null) return;

        isAttacking = true;
        IsAttacking = isAttacking;
        activeTimer = hitboxMechanics.attackDuration;
        attackCooldownTimer = hitboxMechanics.attackCooldown;
        alreadyHit.Clear();

        // Get attack direction toward cursor
        lastAttackDirection = GetAttackDirectionTowardCursor();

        // Update weapon visual rotation
        if (weaponInstance != null)
        {
            weaponInstance.transform.rotation = GetWeaponRotation(lastAttackDirection);
        }

        // Play swing sound
        float velocity = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (soundManager != null)
        {
            soundManager.PlaySwingSound(velocity);
        }

        // Check initial hits
        CheckHitbox();

        // Raise attack event
        character.RaiseEvent("weapon_attack_started", currentConfig.weaponName);
    }

    protected override void StopAttack() // Add "override" keyword
    {
        if (!isAttacking) return;

        isAttacking = false;
        IsAttacking = isAttacking;
        alreadyHit.Clear();

        // Raise attack ended event
        character.RaiseEvent("weapon_attack_ended", currentConfig.weaponName);
    }

    private void CheckHitbox()
    {
        if (character == null || hitboxMechanics == null) return;

        // Calculate hitbox position based on cursor direction
        lastHitboxPosition = GetHitboxPosition(lastAttackDirection);

        // Draw debug visualization
        if (showDebugInfo && hitboxVisual != null)
        {
            DrawDebugHitbox(lastHitboxPosition, lastAttackDirection);
        }

        // Check for hits
        var hitColliders = Physics2D.OverlapBoxAll(
            lastHitboxPosition,
            hitboxMechanics.hitboxSize,
            GetHitboxAngle(lastAttackDirection),
            hitboxMechanics.hitLayers
        );

        Debug.Log($"Hitbox check found {hitColliders.Length} colliders at position {lastHitboxPosition}");

        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == character.gameObject || alreadyHit.Contains(hit.gameObject))
                continue;

            // Check max hits for non-multi-hit weapons
            if (!hitboxMechanics.multiHit && alreadyHit.Count >= hitboxMechanics.maxHitsPerAttack)
                break;

            // Calculate damage
            float damage = CalculateDamage(hitboxMechanics.baseDamage);

            // Apply damage
            ApplyDamage(hit.gameObject, damage, lastAttackDirection);

            alreadyHit.Add(hit.gameObject);

            // Play hit sound if available
            if (soundManager != null && hitboxSound != null)
            {
                soundManager.PlaySound("Hit");
            }

            Debug.Log($"Hit {hit.gameObject.name} for {damage:F1} damage");
        }
    }

    /// <summary>
    /// Gets the hitbox rotation angle based on attack direction
    /// </summary>
    private float GetHitboxAngle(Vector2 attackDirection)
    {
        return Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
    }

    private void DrawDebugHitbox(Vector2 position, Vector2 direction)
    {
        if (hitboxVisual == null) return;

        // Calculate hitbox angle
        float angle = GetHitboxAngle(direction);

        // Draw the rotated hitbox using Debug.DrawLine for each edge
        Vector2 halfSize = hitboxMechanics.hitboxSize * 0.5f;

        // Calculate the four corners of the rotated box
        Vector2[] corners = new Vector2[4];
        Quaternion rotation = Quaternion.Euler(0, 0, angle);

        corners[0] = position + (Vector2)(rotation * new Vector3(-halfSize.x, -halfSize.y));
        corners[1] = position + (Vector2)(rotation * new Vector3(halfSize.x, -halfSize.y));
        corners[2] = position + (Vector2)(rotation * new Vector3(halfSize.x, halfSize.y));
        corners[3] = position + (Vector2)(rotation * new Vector3(-halfSize.x, halfSize.y));

        // Draw the box edges
        for (int i = 0; i < 4; i++)
        {
            Debug.DrawLine(corners[i], corners[(i + 1) % 4], hitboxVisual.hitboxDebugColor, debugLineDuration);
        }

        // Draw direction indicator
        Debug.DrawRay(position, direction * 0.5f, Color.red, debugLineDuration);

        // Draw hitbox center
        Debug.DrawRay(position, Vector2.up * 0.1f, Color.green, debugLineDuration);
        Debug.DrawRay(position, Vector2.right * 0.1f, Color.green, debugLineDuration);

        // Draw line from character to hitbox
        if (character != null)
        {
            Debug.DrawLine(character.transform.position, position, Color.blue, debugLineDuration);
        }

        // Draw cursor position (optional)
        if (mainCamera != null)
        {
            Vector2 mousePosition = Vector2.zero;

            if (Mouse.current != null)
            {
                mousePosition = Mouse.current.position.ReadValue();
            }

            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, mainCamera.nearClipPlane));
            mouseWorldPos.z = 0;

            Debug.DrawRay(mouseWorldPos, Vector2.up * 0.2f, Color.magenta, debugLineDuration);
            Debug.DrawRay(mouseWorldPos, Vector2.right * 0.2f, Color.magenta, debugLineDuration);
        }
    }

    private void ApplyDamage(GameObject target, float damage, Vector2 direction)
    {
        if (target == null) return;

        // Get or add CharacterCondition
        var condition = target.GetComponent<CharacterCondition>();
        if (condition == null)
        {
            condition = target.GetComponentInParent<CharacterCondition>();
        }

        if (condition == null)
        {
            // For testing, add a CharacterCondition if none exists
            condition = target.AddComponent<CharacterCondition>();
            condition.maxHealth = 100f;
            condition.currentHealth = 100f;
            Debug.Log($"Added CharacterCondition to {target.name} for testing");
        }

        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position);

            // Apply knockback in the attack direction
            var targetRb = target.GetComponent<Rigidbody2D>();
            if (targetRb != null && hitboxMechanics.knockbackForce > 0)
            {
                targetRb.AddForce(direction * hitboxMechanics.knockbackForce, ForceMode2D.Impulse);
                Debug.Log($"Applied {hitboxMechanics.knockbackForce} knockback to {target.name} in direction {direction}");
            }

            // Raise hit event
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "HitboxWeapon",
                configName = currentConfig.weaponName,
                direction = direction
            });
        }
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);

        if (isAttacking)
        {
            activeTimer -= deltaTime;
            if (activeTimer <= 0)
            {
                StopAttack();
            }
            else if (hitboxMechanics.multiHit && activeTimer > 0)
            {
                // Continue checking for multi-hit weapons
                CheckHitbox();
            }

            // Update debug visualization during attack
            if (showDebugInfo && hitboxVisual != null && activeTimer > 0)
            {
                UpdateDebugVisualization();
            }
        }
        else if (!isAttacking && weaponInstance != null)
        {
            // Smoothly rotate weapon to follow cursor when not attacking
            UpdateWeaponIdleRotation();
        }
    }

    private void UpdateDebugVisualization()
    {
        // Update attack direction and hitbox position
        lastAttackDirection = GetAttackDirectionTowardCursor();
        lastHitboxPosition = GetHitboxPosition(lastAttackDirection);

        // Update weapon rotation during attack
        if (weaponInstance != null)
        {
            weaponInstance.transform.rotation = GetWeaponRotation(lastAttackDirection);
        }

        // Redraw debug hitbox
        DrawDebugHitbox(lastHitboxPosition, lastAttackDirection);
    }

    private void UpdateWeaponIdleRotation()
    {
        if (weaponInstance == null) return;

        Vector2 cursorDirection = GetAttackDirectionTowardCursor();
        Quaternion targetRotation = GetWeaponRotation(cursorDirection);

        // Smoothly rotate toward cursor
        weaponInstance.transform.rotation = Quaternion.Slerp(
            weaponInstance.transform.rotation,
            targetRotation,
            Time.deltaTime * 10f // Adjust rotation speed as needed
        );
    }

    public override void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates if needed
    }

    protected override void CleanupManagers()
    {
        base.CleanupManagers();

        // Clean up visual instance
        if (weaponInstance != null)
        {
            Destroy(weaponInstance);
            weaponInstance = null;
        }

        // Clear config references
        hitboxMechanics = null;
        hitboxVisual = null;
        hitboxSound = null;
        mainCamera = null;
    }

    // Optional: Add visual feedback for cursor aiming
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isAttacking || !showDebugInfo) return;

        // Draw a line from character to cursor
        if (mainCamera != null && character != null)
        {
            Vector2 mousePosition = Vector2.zero;

            // Use Input System to get mouse position
            if (Mouse.current != null)
            {
                mousePosition = Mouse.current.position.ReadValue();
            }
            else
            {
                return; // No mouse available
            }

            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, mainCamera.nearClipPlane));
            mouseWorldPos.z = 0;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(character.transform.position, mouseWorldPos);
        }
    }
}