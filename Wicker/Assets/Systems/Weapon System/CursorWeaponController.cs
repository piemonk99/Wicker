// CursorWeaponController.cs
using UnityEngine;

public class CursorWeaponController : MonoBehaviour, IWeaponController
{
    private CharacterEquipment owner;
    private CursorWeaponConfig config;
    private CharacterCore character;
    private Transform characterTransform;

    // Weapon physics
    private Rigidbody2D weaponRb;
    private Transform weaponTransform;
    private Vector2 targetPosition;
    private Vector2 lastPosition;
    private float currentWeaponSpeed;

    // State
    private bool isActive = false;
    private bool isSwinging = false;

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

        // Setup weapon GameObject
        weaponTransform = transform;
        weaponRb = gameObject.AddComponent<Rigidbody2D>();

        // Configure weapon rigidbody
        weaponRb.mass = config.weaponMass;
        weaponRb.linearDamping = config.weaponDrag;
        weaponRb.gravityScale = 0f;
        weaponRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Set initial position
        weaponTransform.position = characterTransform.position + (Vector3)Vector2.right * config.orbitRadius;
        lastPosition = weaponTransform.position;
    }

    public bool TryAttack()
    {
        // For cursor weapon, attacking might mean "swing with force"
        // or it might just be always active. Let's make it toggle swinging.
        isSwinging = !isSwinging;

        if (isSwinging)
        {
            // Apply initial force if needed
            Vector2 swingDirection = (targetPosition - (Vector2)weaponTransform.position).normalized;
            weaponRb.AddForce(swingDirection * config.orbitSpeed, ForceMode2D.Impulse);
        }

        return true;
    }

    public bool IsAttacking() => isSwinging;

    public void Tick(float deltaTime)
    {
        if (config == null) return;

        // Update target position based on cursor (for player) or AI (for enemy)
        UpdateTargetPosition(deltaTime);

        // Calculate current weapon speed for damage
        currentWeaponSpeed = ((Vector2)weaponTransform.position - lastPosition).magnitude / deltaTime;
        lastPosition = weaponTransform.position;

        // Apply control forces
        ApplyControlForces(deltaTime);

        // Check for collisions and apply damage
        if (isSwinging && currentWeaponSpeed > config.minimumDamageSpeed)
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
            weaponTransform.position = Vector2.Lerp(
                weaponTransform.position,
                targetPosition,
                config.cursorFollowSpeed * deltaTime
            );
        }
        else
        {
            // Physics-based movement with force
            Vector2 toTarget = targetPosition - (Vector2)weaponTransform.position;
            float distance = toTarget.magnitude;

            if (distance > 0.1f)
            {
                // Apply force towards target
                Vector2 forceDirection = toTarget.normalized;
                float forceMagnitude = distance * config.returnForce;

                // Limit maximum force to prevent overshooting
                forceMagnitude = Mathf.Min(forceMagnitude, config.maxWeaponSpeed);

                weaponRb.AddForce(forceDirection * forceMagnitude);
            }

            // Limit maximum speed
            if (weaponRb.linearVelocity.magnitude > config.maxWeaponSpeed)
            {
                weaponRb.linearVelocity = weaponRb.linearVelocity.normalized * config.maxWeaponSpeed;
            }
        }
    }

    private void CheckCollisions()
    {
        // Simple collision check - you might want to use a collider instead
        float checkRadius = 0.5f; // Adjust based on weapon size
        var hitColliders = Physics2D.OverlapCircleAll(weaponTransform.position, checkRadius);

        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == character.gameObject || hit.gameObject == gameObject)
                continue;

            // Calculate damage based on weapon speed
            float speedDamage = (currentWeaponSpeed - config.minimumDamageSpeed) * config.damagePerSpeedUnit;
            float totalDamage = Mathf.Max(config.baseDamage, speedDamage);

            // Add velocity bonus from character
            totalDamage = owner.CalculateDamage(totalDamage);

            // Apply damage
            ApplyDamage(hit.gameObject, totalDamage);
        }
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        // Get or add CharacterCondition
        var health = target.GetComponent<CharacterCondition>();
        if (health == null)
        {
            // For testing, add a health component if none exists
            health = target.AddComponent<CharacterCondition>();
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
                weaponType = "CursorWeapon"
            });

            // Apply knockback based on weapon velocity
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null && weaponRb.linearVelocity.magnitude > 0.1f)
            {
                rb.AddForce(weaponRb.linearVelocity.normalized * currentWeaponSpeed * 0.5f, ForceMode2D.Impulse);
            }
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates already handled by Rigidbody2D
    }
}