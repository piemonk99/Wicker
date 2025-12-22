// CharacterCondition.cs
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CharacterCondition : MonoBehaviour, ICharacterComponent
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public bool isInvulnerable = false;
    public float invulnerabilityDuration = 0.5f;

    [Header("Damage Text")]
    public GameObject damageTextPrefab;
    public Vector2 textOffset = new Vector2(0, 1f);
    public float textDuration = 2f;
    public Color damageColor = Color.red;
    public Color healColor = Color.green;

    [Header("Death Settings")]
    public bool destroyOnDeath = true;
    public GameObject deathEffect;

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
        currentHealth = maxHealth;

        // Create default damage text prefab if none exists
        if (damageTextPrefab == null)
        {
            CreateDefaultDamageTextPrefab();
        }

        Debug.Log($"CharacterCondition initialized for {character.gameObject.name}");
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

    // Health Management
    public void TakeDamage(float damage, Vector3? hitPosition = null)
    {
        if (isInvulnerable || isDead || damage <= 0) return;

        // Apply damage modifiers from effects
        damage = CalculateModifiedDamage(damage);

        // Calculate actual hit position
        Vector3 position = hitPosition ?? transform.position;

        // Show damage text
        ShowDamageText(damage, position, damageColor);

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
            ShowDamageText(actualHeal, transform.position, healColor);

            // Raise event
            OnHealthChanged?.Invoke(currentHealth / maxHealth);

            Debug.Log($"{gameObject.name} healed {actualHeal}. Health: {currentHealth}/{maxHealth}");
        }
    }

    private void ShowDamageText(float amount, Vector3 position, Color color)
    {
        if (damageTextPrefab == null) return;

        // Create text at position with offset
        Vector3 textPosition = position + (Vector3)textOffset;
        GameObject textObj = Instantiate(damageTextPrefab, textPosition, Quaternion.identity);

        // Set text
        TextMeshPro textMesh = textObj.GetComponent<TextMeshPro>();
        if (textMesh != null)
        {
            textMesh.text = amount.ToString("F0");
            textMesh.color = color;
        }

        // Start fade/destroy coroutine
        StartCoroutine(DestroyTextAfterDelay(textObj, textDuration));
    }

    private IEnumerator DestroyTextAfterDelay(GameObject textObj, float delay)
    {
        float elapsed = 0f;
        TextMeshPro textMesh = textObj.GetComponent<TextMeshPro>();
        Vector3 startPosition = textObj.transform.position;

        while (elapsed < delay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / delay;

            // Move upward
            textObj.transform.position = startPosition + Vector3.up * (t * 0.5f);

            // Fade out
            if (textMesh != null)
            {
                Color color = textMesh.color;
                color.a = Mathf.Lerp(1f, 0f, t);
                textMesh.color = color;
            }

            yield return null;
        }

        Destroy(textObj);
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

    private void CreateDefaultDamageTextPrefab()
    {
        // Create a simple default text prefab
        GameObject textObj = new GameObject("DefaultDamageText");
        TextMeshPro textMesh = textObj.AddComponent<TextMeshPro>();

        textMesh.text = "0";
        textMesh.fontSize = 3;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.sortingOrder = 1000;

        // Make it a prefab in memory
        damageTextPrefab = textObj;
        textObj.SetActive(false);

        Debug.Log("Created default damage text prefab");
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