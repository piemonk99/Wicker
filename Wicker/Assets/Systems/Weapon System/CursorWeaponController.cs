// CursorWeaponController.cs
using UnityEngine;
using System.Collections;

public class CursorWeaponController : MonoBehaviour, IWeaponController
{
    private CharacterEquipment owner;
    private CursorWeaponConfig config;
    private CharacterCore character;
    private Transform characterTransform;

    // Sword physics
    private Rigidbody2D swordRb;
    private Transform swordTransform;
    private Vector2 targetPosition;
    private Vector2 lastPosition;
    private float currentSwordSpeed;

    // State
    private bool isActive = false;
    private bool isSwinging = false;

    // Debug
    private bool showDebugInfo = false;
    private Vector2 lastMousePosition;
    private float debugDisplayTime = 0.5f;

    public void Initialize(WeaponConfig baseConfig, CharacterCore character, CharacterEquipment owner)
    {
        this.config = baseConfig as CursorWeaponConfig;
        this.character = character;
        this.owner = owner;
        this.characterTransform = character.transform;

        if (this.config == null)
        {
            Debug.LogError($"CursorWeaponController requires CursorWeaponConfig, got {baseConfig.GetType().Name}");
            return;
        }

        // Setup sword GameObject
        swordTransform = transform;

        // Check if Rigidbody2D already exists
        swordRb = GetComponent<Rigidbody2D>();
        if (swordRb == null)
        {
            // Add Rigidbody2D if it doesn't exist
            swordRb = gameObject.AddComponent<Rigidbody2D>();
            Debug.Log("Added Rigidbody2D to cursor weapon");
        }

        // Configure sword rigidbody
        swordRb.mass = config.swordMass;
        swordRb.linearDamping = config.swordDrag;
        swordRb.gravityScale = 0f;
        swordRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        swordRb.isKinematic = false; // Ensure it's not kinematic

        // Set initial position
        swordTransform.position = characterTransform.position + (Vector3)Vector2.right * config.orbitRadius;
        lastPosition = swordTransform.position;

        // Enable debug if configured
        showDebugInfo = config.enableDebugVisualization;

        Debug.Log($"Cursor weapon initialized with mass={config.swordMass}, drag={config.swordDrag}, orbitRadius={config.orbitRadius}");
    }

    public bool TryAttack()
    {
        // For cursor weapon, attacking toggles swinging mode
        isSwinging = !isSwinging;

        if (isSwinging)
        {
            // Apply initial force if needed
            Vector2 swingDirection = (targetPosition - (Vector2)swordTransform.position).normalized;
            if (swordRb != null && swingDirection.magnitude > 0.1f)
            {
                swordRb.AddForce(swingDirection * config.orbitSpeed, ForceMode2D.Impulse);
                Debug.Log($"Cursor weapon: Started swinging with force {config.orbitSpeed}");
            }
        }
        else
        {
            Debug.Log("Cursor weapon: Stopped swinging");
        }

        return true;
    }

    public bool IsAttacking() => isSwinging;

    public void Tick(float deltaTime)
    {
        if (config == null || swordRb == null) return;

        // Update target position based on cursor (for player) or AI (for enemy)
        UpdateTargetPosition(deltaTime);

        // Calculate current sword speed for damage
        if (swordTransform != null)
        {
            currentSwordSpeed = ((Vector2)swordTransform.position - lastPosition).magnitude / deltaTime;
            lastPosition = swordTransform.position;
        }

        // Apply control forces
        ApplyControlForces(deltaTime);

        // Check for collisions and apply damage
        if (isSwinging && currentSwordSpeed > config.minimumDamageSpeed)
        {
            CheckCollisions();
        }

        // Draw debug info
        if (showDebugInfo)
        {
            DrawDebugInfo();
        }
    }

    private void UpdateTargetPosition(float deltaTime)
    {
        // For player: target is cursor position
        if (character.CompareTag("Player"))
        {
            // Get cursor position in world space
            if (Camera.main != null)
            {
                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0;
                lastMousePosition = mouseWorldPos;

                Vector2 toCursor = (Vector2)mouseWorldPos - (Vector2)characterTransform.position;
                toCursor = Vector2.ClampMagnitude(toCursor, config.orbitRadius);

                targetPosition = (Vector2)characterTransform.position + toCursor;
            }
        }
        else
        {
            // Enemy AI: orbit around character
            // Simple orbit for now
            float angle = Time.time * config.orbitSpeed;
            targetPosition = (Vector2)characterTransform.position +
                           new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * config.orbitRadius;
        }
    }

    private void ApplyControlForces(float deltaTime)
    {
        if (swordTransform == null || swordRb == null) return;

        if (!config.usePhysicsBasedMovement)
        {
            // Simple lerp movement
            swordTransform.position = Vector2.Lerp(
                swordTransform.position,
                targetPosition,
                config.cursorFollowSpeed * deltaTime
            );
        }
        else
        {
            // Physics-based movement with force
            Vector2 toTarget = targetPosition - (Vector2)swordTransform.position;
            float distance = toTarget.magnitude;

            if (distance > 0.1f)
            {
                // Apply force towards target
                Vector2 forceDirection = toTarget.normalized;
                float forceMagnitude = distance * config.returnForce;

                // Limit maximum force to prevent overshooting
                forceMagnitude = Mathf.Min(forceMagnitude, config.maxSwordSpeed);

                swordRb.AddForce(forceDirection * forceMagnitude);

                if (showDebugInfo && distance > 1f)
                {
                    Debug.DrawRay(swordTransform.position, forceDirection * (forceMagnitude * 0.1f), Color.blue, deltaTime);
                }
            }

            // Limit maximum speed
            if (swordRb.linearVelocity.magnitude > config.maxSwordSpeed)
            {
                swordRb.linearVelocity = swordRb.linearVelocity.normalized * config.maxSwordSpeed;
            }
        }
    }

    private void CheckCollisions()
    {
        if (swordTransform == null) return;

        // Simple collision check - you might want to use a collider instead
        float checkRadius = 0.5f; // Adjust based on sword size
        var hitColliders = Physics2D.OverlapCircleAll(swordTransform.position, checkRadius);

        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == character.gameObject || hit.gameObject == gameObject)
                continue;

            // Calculate damage based on sword speed
            float speedDamage = (currentSwordSpeed - config.minimumDamageSpeed) * config.damagePerSpeedUnit;
            float totalDamage = Mathf.Max(config.baseDamage, speedDamage);

            // Add velocity bonus from character
            totalDamage = owner.CalculateDamage(totalDamage);

            // Apply damage
            ApplyDamage(hit.gameObject, totalDamage);

            Debug.Log($"Cursor weapon hit {hit.gameObject.name} for {totalDamage} damage (speed: {currentSwordSpeed:F1})");
        }
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        // Get or add CharacterCondition
        var condition = target.GetComponent<CharacterCondition>();
        if (condition == null)
        {
            // For testing, add a CharacterCondition if none exists
            condition = target.AddComponent<CharacterCondition>();
            condition.maxHealth = 100f;
            condition.currentHealth = 100f;
        }

        if (condition != null)
        {
            condition.TakeDamage(damage, target.transform.position);

            // Raise hit event
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "CursorWeapon"
            });

            // Apply knockback based on sword velocity
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null && swordRb != null && swordRb.linearVelocity.magnitude > 0.1f)
            {
                rb.AddForce(swordRb.linearVelocity.normalized * currentSwordSpeed * 0.5f, ForceMode2D.Impulse);
            }
        }
    }

    private void DrawDebugInfo()
    {
        if (swordTransform == null || characterTransform == null) return;

        // Draw orbit radius using multiple Debug.DrawLine calls
        DrawCircle(characterTransform.position, config.orbitRadius, 32, Color.yellow);

        // Draw line to target
        Debug.DrawLine(swordTransform.position, targetPosition, Color.green, debugDisplayTime);

        // Draw line to character
        Debug.DrawLine(swordTransform.position, characterTransform.position, Color.cyan, debugDisplayTime);

        // Draw sword velocity
        if (swordRb != null)
        {
            Debug.DrawRay(swordTransform.position, swordRb.linearVelocity.normalized * 0.5f, Color.red, debugDisplayTime);
        }

        // Draw mouse position
        if (character.CompareTag("Player"))
        {
            Debug.DrawRay(lastMousePosition, Vector2.up * 0.2f, Color.magenta, debugDisplayTime);
            Debug.DrawRay(lastMousePosition, Vector2.right * 0.2f, Color.magenta, debugDisplayTime);
        }

        // Draw current speed indicator
        Debug.DrawRay(swordTransform.position, Vector2.up * (currentSwordSpeed * 0.1f),
                     currentSwordSpeed > config.minimumDamageSpeed ? Color.green : Color.gray,
                     debugDisplayTime);
    }

    // Helper method to draw a circle using Debug.DrawLine
    private void DrawCircle(Vector2 center, float radius, int segments, Color color)
    {
        float angleStep = 360f / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i;
            Vector2 nextPoint = center + new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            Debug.DrawLine(prevPoint, nextPoint, color, debugDisplayTime);
            prevPoint = nextPoint;
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates already handled by Rigidbody2D
    }
}