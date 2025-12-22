// CursorSwordController.cs
using UnityEngine;

public class CursorSwordController : MonoBehaviour, IWeaponController
{
    private WeaponComponent owner;
    private CursorSwordConfig config;
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

    public void Initialize(WeaponConfig baseConfig, CharacterCore character, WeaponComponent owner)
    {
        this.config = baseConfig as CursorSwordConfig;
        this.character = character;
        this.owner = owner;
        this.characterTransform = character.transform;

        if (this.config == null)
        {
            Debug.LogError($"CursorSwordController requires CursorSwordConfig, got {baseConfig.GetType().Name}");
            return;
        }

        // Setup sword GameObject
        swordTransform = transform;
        swordRb = gameObject.AddComponent<Rigidbody2D>();

        // Configure sword rigidbody
        swordRb.mass = config.swordMass;
        swordRb.linearDamping = config.swordDrag;
        swordRb.gravityScale = 0f;
        swordRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Set initial position
        swordTransform.position = characterTransform.position + (Vector3)Vector2.right * config.orbitRadius;
        lastPosition = swordTransform.position;
    }

    public bool TryAttack()
    {
        // For cursor sword, attacking might mean "swing with force"
        // or it might just be always active. Let's make it toggle swinging.
        isSwinging = !isSwinging;

        if (isSwinging)
        {
            // Apply initial force if needed
            Vector2 swingDirection = (targetPosition - (Vector2)swordTransform.position).normalized;
            swordRb.AddForce(swingDirection * config.orbitSpeed, ForceMode2D.Impulse);
        }

        return true;
    }

    public bool IsAttacking() => isSwinging;

    public void Tick(float deltaTime)
    {
        if (config == null) return;

        // Update target position based on cursor (for player) or AI (for enemy)
        UpdateTargetPosition(deltaTime);

        // Calculate current sword speed for damage
        currentSwordSpeed = ((Vector2)swordTransform.position - lastPosition).magnitude / deltaTime;
        lastPosition = swordTransform.position;

        // Apply control forces
        ApplyControlForces(deltaTime);

        // Check for collisions and apply damage
        if (isSwinging && currentSwordSpeed > config.minimumDamageSpeed)
        {
            CheckCollisions();
        }
    }

    private void UpdateTargetPosition(float deltaTime)
    {
        // For player: target is cursor position
        // For enemy: target is towards player
        if (character.CompareTag("Player"))
        {
            // Get cursor position in world space
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;

            Vector2 toCursor = (Vector2)mouseWorldPos - (Vector2)characterTransform.position;
            toCursor = Vector2.ClampMagnitude(toCursor, config.orbitRadius);

            targetPosition = (Vector2)characterTransform.position + toCursor;
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
        }
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        // Get or add HealthComponent
        var health = target.GetComponent<HealthComponent>();
        if (health == null)
        {
            // For testing, add a health component if none exists
            health = target.AddComponent<HealthComponent>();
        }

        if (health != null)
        {
            health.TakeDamage(damage, target.transform.position);

            // Raise hit event
            character.RaiseEvent("enemy_hit", new
            {
                enemy = target,
                damage = damage,
                position = target.transform.position,
                weaponType = "CursorSword"
            });

            // Apply knockback based on sword velocity
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null && swordRb.linearVelocity.magnitude > 0.1f)
            {
                rb.AddForce(swordRb.linearVelocity.normalized * currentSwordSpeed * 0.5f, ForceMode2D.Impulse);
            }
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates already handled by Rigidbody2D
    }
}