using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CharacterCondition : MonoBehaviour, ICharacterComponent
{
    private ConditionConfig config;

    private float maxHealth = 100f;
    private float currentHealth = 100f;
    private Vector2 textOffset = new Vector2(0, 1f);
    private Color damageColor = Color.red;
    private Color healColor = Color.green;
    private Color critColor = Color.yellow;
    private bool destroyOnDeath = true;
    private GameObject deathEffect;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    [Header("Status Effects")]
    private List<StatusEffect> activeEffects = new List<StatusEffect>();
    private Dictionary<string, StatusEffect> effectLookup = new Dictionary<string, StatusEffect>();

    // Private state
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
    public IReadOnlyList<StatusEffect> ActiveEffects => activeEffects.AsReadOnly();

    public void Initialize(CharacterCore character)
    {
        this.character = character;

        // Get the config from CharacterCore
        config = character.GetConfig().condition;
        if (config != null)
        {
            // Initialize values from config
            maxHealth = config.maxHealth;
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
        // Update all status effects
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            
            // Update the effect's duration
            effect.Update(deltaTime);
            
            // Execute the effect with deltaTime as data
            effect.Execute(character, this, deltaTime);
            
            // Remove if expired
            if (effect.IsExpired)
            {
                RemoveStatusEffect(effect);
            }
        }
    }

    public void PhysicsTick(float fixedDeltaTime) { }

    public void TakeDamage(float damage, Vector3? hitPosition = null, bool isCritical = false, 
                          GameObject instigator = null, float? hitCooldown = null)
    {
        if (isDead || damage <= 0) return;

        // Check all effects for damage blocking
        float finalDamage = damage;

        if (HasStatusEffect("invulnerability")) finalDamage = 0;

        // Ensure damage is positive, and always round up
        finalDamage = Mathf.Max(0, finalDamage);
        finalDamage = Mathf.CeilToInt(finalDamage);
        if (finalDamage <= 0) return;

        // Calculate actual hit position
        Vector3 position = hitPosition ?? transform.position;

        // Show damage text with crit indication
        Color textColor = isCritical ? critColor : damageColor;
        ShowDamageText(finalDamage, position, textColor, isCritical);

        // Apply damage
        currentHealth -= finalDamage;

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Apply hit cooldown
            ApplyHitCooldown(hitCooldown);

            // Raise event
            OnHealthChanged?.Invoke(currentHealth / maxHealth);

            Debug.Log($"{gameObject.name} took {finalDamage} damage. Health: {currentHealth}/{maxHealth}");
        }
    }

    private void ApplyHitCooldown(float? customDuration = null)
    {
        // Remove any existing hit cooldown first
        RemoveStatusEffect("hit_cooldown");
        
        // Use custom duration if provided, otherwise use config default
        float duration = customDuration ?? 
                        (config != null ? config.invulnerabilityDuration : 0.5f);
        
        if (duration > 0)
        {
            var hitCooldownEffect = StatusEffect.CreateHitCooldown(duration);
            AddStatusEffect(hitCooldownEffect);
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0) return;

        // Check all effects for healing modification
        float finalHeal = amount;

        foreach (var effect in activeEffects)
        {
            // Execute effect with heal amount as data
            effect.Execute(character, this, amount);
            
            // Apply heal modification based on ModifierValue
            if (effect.ModifierValue != 1f)
            {
                finalHeal *= effect.ModifierValue;
            }
        }

        // Ensure healing is positive
        finalHeal = Mathf.Max(0, finalHeal);
        if (finalHeal <= 0) return;

        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(currentHealth + finalHeal, maxHealth);
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

        // Remove all effects on death
        RemoveAllEffects();

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

        // Check if effect already exists
        if (effectLookup.TryGetValue(effect.Id, out var existingEffect))
        {
            // Try to add stack
            if (!existingEffect.TryAddStack())
            {
                // If can't stack, refresh duration
                existingEffect.Refresh();
            }
            
            OnEffectAdded?.Invoke(existingEffect);
            return;
        }

        // Add new effect
        effect.OnApply(character, this);
        activeEffects.Add(effect);
        effectLookup[effect.Id] = effect;
        
        OnEffectAdded?.Invoke(effect);
        Debug.Log($"Added status effect: {effect.Id}");
    }

    public void RemoveStatusEffect(StatusEffect effect)
    {
        if (effect == null || !activeEffects.Contains(effect)) return;

        effect.OnRemove(character, this);
        activeEffects.Remove(effect);
        effectLookup.Remove(effect.Id);
        
        OnEffectRemoved?.Invoke(effect);
        Debug.Log($"Removed status effect: {effect.Id}");
    }

    public void RemoveStatusEffect(string effectId)
    {
        if (effectLookup.TryGetValue(effectId, out var effect))
        {
            RemoveStatusEffect(effect);
        }
    }

    public void RemoveAllEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            RemoveStatusEffect(effect);
        }
    }

    public bool HasStatusEffect(string effectId)
    {
        return effectLookup.ContainsKey(effectId);
    }

    public StatusEffect GetStatusEffect(string effectId)
    {
        effectLookup.TryGetValue(effectId, out var effect);
        return effect;
    }

    public bool IsInvulnerable
    {
        get
        {
            // Check if any effect has ModifierValue == 0 (blocks damage)
            foreach (var effect in activeEffects)
            {
                if (effect.ModifierValue == 0f)
                {
                    return true;
                }
            }
            return false;
        }
    }

    // Helper methods for common effects
    public void ApplyInvulnerability(float duration)
    {
        var invuln = StatusEffect.CreateInvulnerability(duration);
        AddStatusEffect(invuln);
    }

    public void ApplyDamageModifier(float duration, float multiplier, string source = "default")
    {
        var modifier = StatusEffect.CreateDamageModifier(duration, multiplier, source);
        AddStatusEffect(modifier);
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
}