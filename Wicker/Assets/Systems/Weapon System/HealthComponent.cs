// HealthComponent.cs
using UnityEngine;
using TMPro;
using System.Collections;

public class HealthComponent : MonoBehaviour
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

    [Header("Events")]
    public bool destroyOnDeath = true;
    public GameObject deathEffect;

    private float invulnerabilityTimer = 0f;
    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;

        // Create default damage text prefab if none exists
        if (damageTextPrefab == null)
        {
            CreateDefaultDamageTextPrefab();
        }
    }

    void Update()
    {
        if (isInvulnerable && invulnerabilityTimer > 0)
        {
            invulnerabilityTimer -= Time.deltaTime;
            if (invulnerabilityTimer <= 0)
            {
                isInvulnerable = false;
            }
        }
    }

    public void TakeDamage(float damage, Vector3? hitPosition = null)
    {
        if (isInvulnerable || isDead) return;

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
            if (damage > 0)
            {
                StartInvulnerability();
            }
        }

        // Raise event
        SendMessage("OnHealthChanged", currentHealth / maxHealth, SendMessageOptions.DontRequireReceiver);

        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        // Show heal text
        ShowDamageText(amount, transform.position, healColor);

        // Raise event
        SendMessage("OnHealthChanged", currentHealth / maxHealth, SendMessageOptions.DontRequireReceiver);

        Debug.Log($"{gameObject.name} healed {amount}. Health: {currentHealth}/{maxHealth}");
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
        SendMessage("OnDeath", SendMessageOptions.DontRequireReceiver);

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
    public float GetHealthPercentage() => currentHealth / maxHealth;
    public bool IsAlive() => !isDead && currentHealth > 0;
    public bool IsFullHealth() => Mathf.Approximately(currentHealth, maxHealth);

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
    }
}