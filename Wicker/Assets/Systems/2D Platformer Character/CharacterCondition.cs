using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CharacterCondition : MonoBehaviour, ICharacterComponent
{
    private ConditionConfig config;

    public float maxHealth = 100f;
    public float currentHealth = 100f;
    private bool isInvulnerable = false;
    private float invulnerabilityDuration = 0.5f;
    private Vector2 textOffset = new Vector2(0, 1f);
    private Color damageColor = Color.red;
    private Color healColor = Color.green;
    private Color critColor = Color.yellow;
    private bool destroyOnDeath = true;
    private GameObject deathEffect;

    [Header("Buffs/Debuffs")]
    public List<StatusEffect> activeEffects = new List<StatusEffect>();

    // Private state
    private float invulnerabilityTimer = 0f;
    private bool isDead = false;
    private CharacterCore character;

    // Events
    public event System.Action<float> OnHealthChanged;
    public event System.Action OnDeath;
    public event System.Action<StatusEffect> OnEffectAdded;
    public event System.Action<StatusEffect> OnEffectRemoved;

    // Public Properties
    public float HealthPercentage => currentHealth / maxHealth;
    public bool IsAlive => !isDead && currentHealth > 0;
    public bool IsFullHealth => Mathf.Approximately(currentHealth, maxHealth);

    public void Initialize(CharacterCore character)
    {
        this.character = character;

        // Get the config from CharacterCore
        config = character.GetConfig().condition;
        if (config != null)
        {
            // Initialize values from config
            maxHealth = config.maxHealth;
            invulnerabilityDuration = config.invulnerabilityDuration;
            textOffset = config.textOffset;
            damageColor = config.damageColor;
            healColor = config.healColor;
            critColor = config.critColor;
            destroyOnDeath = config.destroyOnDeath;
            deathEffect = config.deathEffect;
        }

        currentHealth = maxHealth;
    }

    public void Tick(float deltaTime)
    {
        if (isInvulnerable && invulnerabilityTimer > 0)
        {
            invulnerabilityTimer -= deltaTime;
            if (invulnerabilityTimer <= 0)
            {
                isInvulnerable = false;
            }
        }

        // Update active effects
        UpdateStatusEffects(deltaTime);
    }

    public void PhysicsTick(float fixedDeltaTime) { }

    
    public void TakeDamage(float damage, Vector3? hitPosition = null, bool isCritical = false)
    {
        if (isInvulnerable || isDead || damage <= 0) return;

        // Apply damage modifiers from effects
        damage = CalculateModifiedDamage(damage);

        // Calculate actual hit position
        Vector3 position = hitPosition ?? transform.position;

        // Show damage text with crit indication
        Color textColor = isCritical ? critColor : damageColor;
        ShowDamageText(damage, position, textColor, isCritical);

        // Apply damage
        currentHealth -= damage;

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Start invulnerability if damage was taken
            StartInvulnerability();
        }

        // Raise event
        OnHealthChanged?.Invoke(currentHealth / maxHealth);

        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0) return;

        // Apply heal modifiers from effects
        amount = CalculateModifiedHealing(amount);

        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        float actualHeal = currentHealth - oldHealth;

        if (actualHeal > 0)
        {
            // Show heal text
            if (WorldSpaceTextManager.Instance != null)
            {
                WorldSpaceTextManager.Instance.ShowHeal(actualHeal, transform.position + (Vector3)textOffset, healColor);
            }

            // Raise event
            OnHealthChanged?.Invoke(currentHealth / maxHealth);

            Debug.Log($"{gameObject.name} healed {actualHeal}. Health: {currentHealth}/{maxHealth}");
        }
    }

    private void ShowDamageText(float amount, Vector3 position, Color color, bool isCrit = false)
    {
        if (WorldSpaceTextManager.Instance == null)
        {
            Debug.LogWarning("WorldSpaceTextManager not found. Text will not be displayed.");
            return;
        }

        Vector3 textPosition = position + (Vector3)textOffset;

        WorldSpaceTextManager.Instance.ShowDamage(
            amount,
            textPosition,
            color,
            isCrit
        );
    }

    private void StartInvulnerability()
    {
        isInvulnerable = true;
        invulnerabilityTimer = invulnerabilityDuration;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0;

        Debug.Log($"{gameObject.name} died!");

        // Play death effect
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        // Raise death event
        OnDeath?.Invoke();

        // Destroy or disable
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            // Disable components instead of destroying
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;

            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;

            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;

            enabled = false;
        }
    }

    // Status Effect Management
    public void AddStatusEffect(StatusEffect effect)
    {
        if (effect == null) return;

        // Check if effect already exists (same type)
        var existingEffect = activeEffects.Find(e => e.effectType == effect.effectType);
        if (existingEffect != null)
        {
            // Refresh or stack effect
            existingEffect.Refresh(effect);
        }
        else
        {
            // Add new effect
            effect.OnApply(this);
            activeEffects.Add(effect);
            OnEffectAdded?.Invoke(effect);
        }
    }

    public void RemoveStatusEffect(StatusEffect effect)
    {
        if (effect == null || !activeEffects.Contains(effect)) return;

        effect.OnRemove(this);
        activeEffects.Remove(effect);
        OnEffectRemoved?.Invoke(effect);
    }

    public void RemoveStatusEffect(StatusEffectType effectType)
    {
        var effect = activeEffects.Find(e => e.effectType == effectType);
        if (effect != null)
        {
            RemoveStatusEffect(effect);
        }
    }

    private void UpdateStatusEffects(float deltaTime)
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            effect.Update(deltaTime, this);

            if (effect.IsExpired)
            {
                RemoveStatusEffect(effect);
            }
        }
    }

    private float CalculateModifiedDamage(float baseDamage)
    {
        float modifiedDamage = baseDamage;

        foreach (var effect in activeEffects)
        {
            modifiedDamage = effect.ModifyDamage(modifiedDamage);
        }

        return modifiedDamage;
    }

    private float CalculateModifiedHealing(float baseHealing)
    {
        float modifiedHealing = baseHealing;

        foreach (var effect in activeEffects)
        {
            modifiedHealing = effect.ModifyHealing(modifiedHealing);
        }

        return modifiedHealing;
    }

    // Public API
    public void SetMaxHealth(float newMaxHealth, bool fillHealth = false)
    {
        maxHealth = newMaxHealth;
        if (fillHealth)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }

        OnHealthChanged?.Invoke(currentHealth / maxHealth);
    }

    public bool HasStatusEffect(StatusEffectType effectType)
    {
        return activeEffects.Exists(e => e.effectType == effectType);
    }

    // Simple status effect system for future expansion
    public abstract class StatusEffect
    {
        public StatusEffectType effectType;
        public float duration;
        public float maxStacks = 1;
        public int currentStacks = 1;

        protected float remainingTime;
        public bool IsExpired => remainingTime <= 0;

        public virtual void OnApply(CharacterCondition condition) { remainingTime = duration; }
        public virtual void OnRemove(CharacterCondition condition) { }
        public virtual void Update(float deltaTime, CharacterCondition condition) { remainingTime -= deltaTime; }
        public virtual void Refresh(StatusEffect other) { remainingTime = duration; }
        public virtual float ModifyDamage(float damage) => damage;
        public virtual float ModifyHealing(float healing) => healing;
    }

    public enum StatusEffectType
    {
        Poison,
        Burn,
        Freeze,
        Stun,
        SpeedBoost,
        DamageBoost,
        Invulnerability
    }
}