using UnityEngine;

[System.Serializable]
public class AttackAbility : CharacterAbility
{
    private float damage;
    private float range;
    private float cooldown;
    private GameObject hitboxPrefab;

    private float cooldownTimer = 0f;

    public AttackAbility()
    {
        AbilityName = "Attack";
    }

    protected override void LoadConfig(CharacterConfig config)
    {
        IsEnabled = config.attack.isEnabled;
        damage = config.attack.damage;
        range = config.attack.range;
        cooldown = config.attack.cooldown;
        hitboxPrefab = config.attack.hitboxPrefab;

        if (IsEnabled)
        {
            character.OnEvent += HandleEvent;
            Debug.Log($"Attack ability loaded: Enabled={IsEnabled}, Damage={damage}, Cooldown={cooldown}");
        }
    }

    private void HandleEvent(string type, object data)
    {
        if (type == "attack_pressed" && CanActivate())
        {
            Activate();
        }
    }

    public override bool CanActivate()
    {
        return IsEnabled && cooldownTimer <= 0 && !IsActive;
    }

    public override void Activate()
    {
        IsActive = true;
        cooldownTimer = cooldown;

        // Spawn attack hitbox
        if (hitboxPrefab != null)
        {
            Vector2 facingDirection = movement.GetFacingDirection();
            Vector3 spawnPosition = transform.position +
                new Vector3(range * facingDirection.x, 0.5f, 0);

            GameObject hitbox = GameObject.Instantiate(hitboxPrefab, spawnPosition, Quaternion.identity);

            var hitboxComponent = hitbox.GetComponent<DamageHitbox>();
            if (hitboxComponent != null)
            {
                hitboxComponent.SetDamage((int)damage);
                hitboxComponent.SetOwner(character.gameObject);
            }

            GameObject.Destroy(hitbox, 0.2f);
        }

        character.RaiseEvent("ability_used", AbilityName);
        OnActivated();

        // Attack is instant, deactivate immediately
        IsActive = false;
        OnDeactivated();
    }

    public override void Deactivate()
    {
        // Attack is instant, nothing to clean up
    }

    public override void Tick(float deltaTime)
    {
        if (cooldownTimer > 0) cooldownTimer -= deltaTime;
    }

    public float GetCooldownPercent() => cooldownTimer / cooldown;
}