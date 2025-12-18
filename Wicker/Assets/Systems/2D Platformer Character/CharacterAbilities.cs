using UnityEngine;

public class CharacterAbilities : MonoBehaviour, ICharacterComponent
{
    [System.Serializable]
    public class LungeSettings
    {
        public bool canLunge = false;
        public float lungeHorizontalForce = 20f;
        public float lungeVerticalForce = 5f;
        public float lungeDuration = 0.3f;
        public float lungeCooldown = 2f;
        public bool cancelVerticalVelocity = true;

        [Header("Visual Feedback")]
        public ParticleSystem lungeParticles;
        public AudioClip lungeSound;
    }

    [System.Serializable]
    public class DashSettings
    {
        public bool canDash = false;
        public float dashForce = 25f;
        public float dashDuration = 0.2f;
        public float dashCooldown = 1f;
        public bool preserveVerticalVelocity = true;

        [Header("Visual Feedback")]
        public GameObject dashTrail;
        public AudioClip dashSound;
    }

    [System.Serializable]
    public class AttackSettings
    {
        public bool canAttack = false;
        public float attackDamage = 1f;
        public float attackRange = 1f;
        public float attackCooldown = 0.5f;

        [Header("Visual Feedback")]
        public GameObject attackHitboxPrefab;
        public AnimationClip attackAnimation;
    }

    [Header("Ability Settings")]
    public LungeSettings lunge = new LungeSettings();
    public DashSettings dash = new DashSettings();
    public AttackSettings attack = new AttackSettings();

    // References
    private CharacterCore character;
    private PlatformerMovement movement;
    private Rigidbody2D rb;

    // State
    private bool isLunging = false;
    private float lungeTimer = 0f;
    private float lungeCooldownTimer = 0f;

    private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;

    private float attackTimer = 0f;

    public void Initialize(CharacterCore core)
    {
        character = core;

        // Get references to other components
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlatformerMovement>();

        // Subscribe to events
        character.OnEvent += HandleEvent;
    }

    public void Tick(float deltaTime)
    {
        UpdateLunge(deltaTime);
        UpdateDash(deltaTime);
        UpdateAttack(deltaTime);
        UpdateCooldowns(deltaTime);
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics updates for abilities
        if (isLunging)
        {
            ApplyLungePhysics(fixedDeltaTime);
        }
    }

    private void HandleEvent(string type, object data)
    {
        switch (type)
        {
            case "lunge_pressed":
                if (lunge.canLunge && lungeCooldownTimer <= 0)
                {
                    StartLunge();
                }
                break;

            case "dash_pressed":
                if (dash.canDash && dashCooldownTimer <= 0)
                {
                    StartDash();
                }
                break;

            case "attack_pressed":
                if (attack.canAttack && attackTimer <= 0)
                {
                    PerformAttack();
                }
                break;
        }
    }

    #region Lunge Ability
    private void StartLunge()
    {
        if (!lunge.canLunge) return;

        isLunging = true;
        lungeTimer = lunge.lungeDuration;
        lungeCooldownTimer = lunge.lungeCooldown;

        // Get current facing direction
        float facingDirection = movement.GetCurrentXDirection();

        // Calculate lunge force
        Vector2 lungeForce = new Vector2(
            facingDirection * lunge.lungeHorizontalForce,
            lunge.lungeVerticalForce
        );

        // Cancel vertical velocity if configured
        if (lunge.cancelVerticalVelocity && rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        }

        // Apply lunge force
        if (rb != null)
        {
            rb.AddForce(lungeForce, ForceMode2D.Impulse);
        }

        // Apply lunge movement state override
        if (movement != null)
        {
            var lungeState = new PlatformerMovement.MovementState(
                name: "Lunging",
                allowMovement: false, // No control during lunge
                applyGravity: true,
                applyDeceleration: false,
                canJump: false,
                gravityMultiplier: 1f,
                accelerationMultiplier: 0f,
                airAccelerationMultiplier: 0f,
                decelerationMultiplier: 0f,
                airDecelerationMultiplier: 0f,
                jumpForceMultiplier: 0f,
                maxSpeedMultiplier: 1f
            );

            character.RaiseEvent("movement_override_start", lungeState);
        }

        // Visual/audio feedback
        if (lunge.lungeParticles != null)
            lunge.lungeParticles.Play();

        if (lunge.lungeSound != null)
            AudioSource.PlayClipAtPoint(lunge.lungeSound, transform.position);

        character.RaiseEvent("ability_used", "lunge");
        Debug.Log($"Lunge performed! Force: {lungeForce}");
    }

    private void UpdateLunge(float deltaTime)
    {
        if (!isLunging) return;

        lungeTimer -= deltaTime;
        if (lungeTimer <= 0)
        {
            EndLunge();
        }
    }

    private void ApplyLungePhysics(float fixedDeltaTime)
    {
        // During lunge, we can apply slight directional control if needed
        // For now, just let physics handle it
    }

    private void EndLunge()
    {
        isLunging = false;

        // Restore previous movement state
        if (movement != null)
        {
            character.RaiseEvent("movement_override_end", null);
        }

        character.RaiseEvent("ability_ended", "lunge");
        Debug.Log("Lunge ended");
    }
    #endregion

    #region Dash Ability
    private void StartDash()
    {
        if (!dash.canDash) return;

        isDashing = true;
        dashTimer = dash.dashDuration;
        dashCooldownTimer = dash.dashCooldown;

        // Get current facing direction
        float facingDirection = movement.GetCurrentXDirection();

        // Calculate dash force
        Vector2 dashForce = new Vector2(facingDirection * dash.dashForce, 5f);

        // Preserve vertical velocity if configured
        float preservedVerticalVelocity = dash.preserveVerticalVelocity ? rb.linearVelocity.y : 0f;

        // Apply dash force
        if (rb != null)
        {
            // Clear horizontal velocity for crisp dash
            rb.linearVelocity = new Vector2(0, preservedVerticalVelocity);
            rb.AddForce(dashForce, ForceMode2D.Impulse);
        }

        // Apply dash movement state override
        if (movement != null)
        {
            var dashState = new PlatformerMovement.MovementState(
                name: "Dashing",
                allowMovement: false, // No control during dash
                applyGravity: true,
                applyDeceleration: false,
                canJump: false,
                gravityMultiplier: 1f,
                accelerationMultiplier: 0f,
                airAccelerationMultiplier: 0f,
                decelerationMultiplier: 0f,
                airDecelerationMultiplier: 0f,
                jumpForceMultiplier: 0f,
                maxSpeedMultiplier: 1f
            );

            character.RaiseEvent("movement_override_start", dashState);
        }

        // Visual feedback
        if (dash.dashTrail != null)
            Instantiate(dash.dashTrail, transform.position, Quaternion.identity);

        if (dash.dashSound != null)
            AudioSource.PlayClipAtPoint(dash.dashSound, transform.position);

        character.RaiseEvent("ability_used", "dash");
        Debug.Log($"Dash performed! Force: {dashForce}");
    }

    private void UpdateDash(float deltaTime)
    {
        if (!isDashing) return;

        dashTimer -= deltaTime;
        if (dashTimer <= 0)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        isDashing = false;

        // Restore previous movement state
        if (movement != null)
        {
            character.RaiseEvent("movement_override_end", null);
        }

        character.RaiseEvent("ability_ended", "dash");
        Debug.Log("Dash ended");
    }
    #endregion

    #region Attack Ability
    private void PerformAttack()
    {
        attackTimer = attack.attackCooldown;

        // Spawn attack hitbox in facing direction
        if (attack.attackHitboxPrefab != null)
        {
            float facingDirection = movement.GetCurrentXDirection();
            Vector3 spawnPosition = transform.position +
                new Vector3(attack.attackRange * facingDirection, 0.5f, 0);

            GameObject hitbox = Instantiate(attack.attackHitboxPrefab, spawnPosition, Quaternion.identity);

            // Configure hitbox
            var hitboxComponent = hitbox.GetComponent<DamageHitbox>();
            if (hitboxComponent != null)
            {
                hitboxComponent.SetDamage((int)attack.attackDamage);
                hitboxComponent.SetOwner(gameObject);
            }

            Destroy(hitbox, 0.2f);
        }

        character.RaiseEvent("ability_used", "attack");
        Debug.Log("Attack performed!");
    }

    private void UpdateAttack(float deltaTime)
    {
        if (attackTimer > 0)
            attackTimer -= deltaTime;
    }
    #endregion

    private void UpdateCooldowns(float deltaTime)
    {
        if (lungeCooldownTimer > 0) lungeCooldownTimer -= deltaTime;
        if (dashCooldownTimer > 0) dashCooldownTimer -= deltaTime;
    }

    // Public API for other systems
    public bool CanLunge() => lunge.canLunge && lungeCooldownTimer <= 0;
    public bool CanDash() => dash.canDash && dashCooldownTimer <= 0;
    public bool CanAttack() => attack.canAttack && attackTimer <= 0;

    public bool IsLunging() => isLunging;
    public bool IsDashing() => isDashing;
}