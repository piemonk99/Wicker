using UnityEngine;
using System.Collections.Generic;

public class WorldSpaceTextManager : MonoBehaviour
{
    public static WorldSpaceTextManager Instance { get; private set; }

    [Header("Canvas Settings")]
    [SerializeField] private Transform worldSpaceCanvas;

    [Header("Text Pools")]
    [SerializeField] private List<WorldSpaceText> textPrefabs = new List<WorldSpaceText>();
    [SerializeField] private int initialPoolSize = 20;

    [Header("Default Settings")]
    [SerializeField] private float defaultDuration = 2f;
    [SerializeField] private Color defaultColor = Color.white;

    private Dictionary<System.Type, WorldSpaceText> prefabDictionary = new Dictionary<System.Type, WorldSpaceText>();
    private Queue<WorldSpaceText> textPool = new Queue<WorldSpaceText>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Create canvas if none exists
        if (worldSpaceCanvas == null)
        {
            worldSpaceCanvas = CreateWorldSpaceCanvas();
        }

        // Initialize prefab dictionary and pool
        Initialize();
    }

    private Transform CreateWorldSpaceCanvas()
    {
        GameObject canvasGO = new GameObject("WorldSpaceTextCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        return canvasGO.transform;
    }

    private void Initialize()
    {
        // Build dictionary of prefabs by type
        foreach (var prefab in textPrefabs)
        {
            if (prefab != null)
            {
                prefabDictionary[prefab.GetType()] = prefab;
            }
        }

        // Initialize pool with first prefab (or default)
        if (textPrefabs.Count > 0 && textPrefabs[0] != null)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                WorldSpaceText text = CreateTextObject(textPrefabs[0]);
                textPool.Enqueue(text);
            }
        }
        else
        {
            Debug.LogWarning("No text prefabs assigned. Please add at least one prefab.");
        }
    }

    private WorldSpaceText CreateTextObject(WorldSpaceText prefab)
    {
        WorldSpaceText text = Instantiate(prefab, worldSpaceCanvas);
        text.gameObject.SetActive(false);
        return text;
    }

    #region Public API

    // Main generic function - handles all text types
    public T ShowText<T>(string textContent, Vector3 position,
        Color? color = null, float? duration = null,
        Vector3? floatDirection = null, float? floatSpeed = null) where T : WorldSpaceText
    {
        WorldSpaceText textObject = GetTextFromPool(typeof(T));
        if (textObject == null) return null;

        // Configure text object
        textObject.gameObject.SetActive(true);
        textObject.SetPosition(position);

        // Apply optional overrides
        if (floatDirection.HasValue)
        {
            textObject.floatDirection = floatDirection.Value;
        }

        if (floatSpeed.HasValue)
        {
            textObject.floatSpeed = floatSpeed.Value;
        }

        // Initialize with text content
        textObject.Initialize(
            textContent,
            color ?? defaultColor,
            duration ?? defaultDuration
        );

        return textObject as T;
    }

    // Non-generic version using type parameter
    public WorldSpaceText ShowText(System.Type textType, string textContent, Vector3 position,
        Color? color = null, float? duration = null,
        Vector3? floatDirection = null, float? floatSpeed = null)
    {
        WorldSpaceText textObject = GetTextFromPool(textType);
        if (textObject == null) return null;

        // Configure text object
        textObject.gameObject.SetActive(true);
        textObject.SetPosition(position);

        // Apply optional overrides
        if (floatDirection.HasValue)
        {
            textObject.floatDirection = floatDirection.Value;
        }

        if (floatSpeed.HasValue)
        {
            textObject.floatSpeed = floatSpeed.Value;
        }

        // Initialize with text content
        textObject.Initialize(
            textContent,
            color ?? defaultColor,
            duration ?? defaultDuration
        );

        return textObject;
    }

    // Convenience overload for damage numbers
    public DamageText ShowDamage(
        float amount,
        Vector3 position,
        Color? color = null,
        bool isCrit = false)
    {
        string formattedText = amount.ToString("F0");

        if (isCrit)
        {
            formattedText = $"{formattedText}!";
        }

        Color textColor = color ?? (isCrit ? Color.yellow : Color.red);

        return ShowText<DamageText>(
            formattedText,
            position,
            textColor,
            1.5f, // Shorter duration for damage
            Vector3.up,
            1.5f // Faster float speed
        );
    }

    // Convenience overload for healing
    public DamageText ShowHeal(
        float amount,
        Vector3 position,
        Color? color = null)
    {
        return ShowText<DamageText>(
            $"+{amount:F0}",
            position,
            color ?? Color.green,
            2f,
            Vector3.up,
            1f
        );
    }

    // Simple version for any text
    public WorldSpaceText ShowText(string textContent, Vector3 position,
        Color? color = null, float? duration = null)
    {
        // Default to DamageText if available, otherwise first available type
        System.Type textType = prefabDictionary.ContainsKey(typeof(DamageText))
            ? typeof(DamageText)
            : (textPrefabs.Count > 0 ? textPrefabs[0].GetType() : typeof(WorldSpaceText));

        return ShowText(textType, textContent, position, color, duration, Vector3.up, 1f);
    }

    public void ReturnTextToPool(WorldSpaceText text)
    {
        text.gameObject.SetActive(false);

        SetDefaultValues(text);

        // Return to single pool
        textPool.Enqueue(text);
    }

    #endregion

    private WorldSpaceText GetTextFromPool(System.Type textType)
    {
        // First, try to find an available object of the exact type
        foreach (var pooledText in textPool)
        {
            if (!pooledText.gameObject.activeInHierarchy && pooledText.GetType() == textType)
            {
                return pooledText;
            }
        }

        // If not found, check if we can use a base type (polymorphism)
        foreach (var pooledText in textPool)
        {
            if (!pooledText.gameObject.activeInHierarchy && textType.IsInstanceOfType(pooledText))
            {
                return pooledText;
            }
        }

        // Pool empty or wrong type, create new one
        if (prefabDictionary.ContainsKey(textType))
        {
            Debug.Log($"Creating new {textType.Name} instance (pool empty or type mismatch)");
            return CreateTextObject(prefabDictionary[textType]);
        }
        else
        {
            Debug.LogError($"No prefab registered for type: {textType.Name}");
            return null;
        }
    }

    private void SetDefaultValues(WorldSpaceText text)
    {
        text.TextMesh.text = "";
        text.TextMesh.color = Color.white;
        text.TextMesh.alpha = 1f;
        text.TextMesh.fontSize = 36;
        text.TextMesh.fontStyle = TMPro.FontStyles.Normal;
    }

    // Helper method to add prefab to the list
    public void RegisterTextPrefab(WorldSpaceText prefab)
    {
        if (prefab != null && !textPrefabs.Contains(prefab))
        {
            textPrefabs.Add(prefab);
            prefabDictionary[prefab.GetType()] = prefab;
        }
    }
}