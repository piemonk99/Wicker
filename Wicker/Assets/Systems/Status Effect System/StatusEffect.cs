using System;
using UnityEngine;

public enum EffectCategory
{
    Buff,
    Debuff,
    Neutral
}

// Main Status Effect class - handles all types through optional delegates
public class StatusEffect
{
    // Core Properties
    public string Id { get; private set; }
    public string DisplayName { get; private set; }
    public EffectCategory Category { get; private set; }

    // Duration & Stacking
    public float BaseDuration { get; private set; }
    public float RemainingTime { get; private set; }
    public int MaxStacks { get; private set; }
    public int CurrentStacks { get; private set; } = 1;

    // Visual/UI
    public Color DisplayColor { get; private set; }
    public Sprite Icon { get; private set; }
    public bool ShowInUI { get; private set; }

    // Modifier Value (for simple value modification)
    public float ModifierValue { get; private set; }

    // Execute Delegate (for custom behavior)
    public Action<CharacterCore, CharacterCondition, object> ExecuteAction { get; private set; }

    // State
    public bool IsExpired => RemainingTime <= 0f;
    public bool IsPermanent => BaseDuration <= 0f;

    // Events
    public event Action<StatusEffect> OnEffectApplied;
    public event Action<StatusEffect> OnEffectRemoved;
    public event Action<StatusEffect> OnEffectUpdated;
    public event Action<StatusEffect> OnStacksChanged;

    // Constructor
    public StatusEffect(
        string id,
        string displayName = "",
        float duration = 0f,
        int maxStacks = 1,
        EffectCategory category = EffectCategory.Neutral,
        Color? displayColor = null,
        Sprite icon = null,
        bool showInUI = true,
        float modifierValue = 1f,
        Action<CharacterCore, CharacterCondition, object> executeAction = null)
    {
        Id = id;
        DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
        BaseDuration = duration;
        RemainingTime = duration;
        MaxStacks = maxStacks;
        Category = category;
        DisplayColor = displayColor ?? GetDefaultColor(category);
        Icon = icon;
        ShowInUI = showInUI;
        ModifierValue = modifierValue;
        ExecuteAction = executeAction;
    }

    // Lifecycle Methods
    public virtual void OnApply(CharacterCore character, CharacterCondition condition)
    {
        OnEffectApplied?.Invoke(this);
    }

    public virtual void OnRemove(CharacterCore character, CharacterCondition condition)
    {
        OnEffectRemoved?.Invoke(this);
    }

    // Update duration (call this from CharacterCondition's Tick)
    public virtual void Update(float deltaTime)
    {
        if (!IsPermanent)
        {
            RemainingTime -= deltaTime;
            if (RemainingTime <= 0f)
            {
                RemainingTime = 0f;
            }
        }

        OnEffectUpdated?.Invoke(this);
    }

    // Execute custom behavior (call this manually when needed)
    public virtual void Execute(CharacterCore character, CharacterCondition condition, object data = null)
    {
        ExecuteAction?.Invoke(character, condition, data);
    }

    // Stack Management
    public virtual bool TryAddStack()
    {
        if (CurrentStacks < MaxStacks)
        {
            CurrentStacks++;
            OnStacksChanged?.Invoke(this);
            return true;
        }
        return false;
    }

    public virtual bool TryRemoveStack()
    {
        if (CurrentStacks > 1)
        {
            CurrentStacks--;
            OnStacksChanged?.Invoke(this);
            return true;
        }
        return false;
    }

    public virtual void SetStacks(int stacks)
    {
        CurrentStacks = Mathf.Clamp(stacks, 1, MaxStacks);
        OnStacksChanged?.Invoke(this);
    }

    // Duration Management
    public virtual void Refresh(float newDuration = -1f)
    {
        if (newDuration > 0f)
        {
            BaseDuration = newDuration;
        }
        RemainingTime = BaseDuration;
    }

    public virtual void Extend(float additionalDuration)
    {
        if (!IsPermanent)
        {
            BaseDuration += additionalDuration;
            RemainingTime += additionalDuration;
        }
    }

    // Helper Methods
    private Color GetDefaultColor(EffectCategory category)
    {
        return category switch
        {
            EffectCategory.Buff => new Color(0.2f, 0.8f, 0.2f, 1f), // Green
            EffectCategory.Debuff => new Color(0.8f, 0.2f, 0.2f, 1f), // Red
            _ => new Color(0.5f, 0.5f, 0.5f, 1f) // Gray
        };
    }

    // Factory Methods for common effect types
    public static StatusEffect CreateInvulnerability(float duration)
    {
        return new StatusEffect(
            id: $"invulnerability",
            displayName: "Invulnerable",
            duration: duration,
            category: EffectCategory.Buff,
            displayColor: new Color(0f, 0.8f, 1f, 1f) // Cyan
        );
    }

    public static StatusEffect CreateHitCooldown(float duration)
    {
        return new StatusEffect(
            id: "hit_cooldown",
            displayName: "Hit Cooldown",
            duration: duration,
            category: EffectCategory.Buff,
            displayColor: new Color(0.5f, 0.5f, 1f, 1f) // Light Blue
        );
    }

    public static StatusEffect CreateDamageModifier(float duration, float multiplier, string source = "default")
    {
        return new StatusEffect(
            id: $"damage_mod_{source}",
            displayName: multiplier > 1f ? "Damage Taken +" : "Damage Taken -",
            duration: duration,
            category: multiplier > 1f ? EffectCategory.Debuff : EffectCategory.Buff,
            modifierValue: multiplier
        );
    }

    public static StatusEffect CreatePeriodicEffect(
        string id,
        float duration,
        float tickInterval,
        float damagePerTick = 0f,
        float healPerTick = 0f)
    {
        float nextTickTime = 0f;

        return new StatusEffect(
            id: id,
            displayName: damagePerTick > 0f ? "Burning" : "Regeneration",
            duration: duration,
            category: damagePerTick > 0f ? EffectCategory.Debuff : EffectCategory.Buff,
            executeAction: (character, condition, data) =>
            {
                // Data should be deltaTime
                if (data is float deltaTime)
                {
                    nextTickTime -= deltaTime;
                    if (nextTickTime <= 0f)
                    {
                        if (damagePerTick > 0f)
                        {
                            condition.TakeDamage(damagePerTick);
                        }
                        if (healPerTick > 0f)
                        {
                            condition.Heal(healPerTick);
                        }
                        nextTickTime = tickInterval;
                    }
                }
            }
        );
    }
}