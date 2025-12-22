using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

// CharacterCore manages all character components for a character - player or enemy.
// Loads config options in from scriptable objects to their corresponding components
// Ticks and PhysicsTicks all components
// Serves as the connection between the event calls from an input script, like PlayerInput or some enemy ai script, and the components listening for event calls.
public class CharacterCore : MonoBehaviour
{
    // Config
    [Header("Character Configuration")]
    [SerializeField] private CharacterConfig config;

    // Simple event system - ALL communication goes through this
    public event Action<string, object> OnEvent;

    // Component management
    private List<ICharacterComponent> components = new();

    // Public getter for config
    public CharacterConfig GetConfig() => config;

    // Public setter for runtime config changes
    public void SetConfig(CharacterConfig newConfig)
    {
        config = newConfig;
        // Clean up and re-initialize with new config
        ReloadAllComponents();
    }

    void Awake()
    {
        // Create default config if none assigned
        if (config == null)
        {
            config = CreateDefaultConfig();
            Debug.LogWarning($"No CharacterConfig assigned to {gameObject.name}. Created default config.");
        }

        InitializeAllComponents();
    }

    void Update()
    {
        float delta = Time.deltaTime;
        foreach (var comp in components)
            comp.Tick(delta);

#if UNITY_EDITOR
            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                ReloadAllComponents();
            }
#endif
    }

    void FixedUpdate()
    {
        float fixedDelta = Time.fixedDeltaTime;
        foreach (var comp in components)
            comp.PhysicsTick(fixedDelta);
    }

    // Public method to raise events
    public void RaiseEvent(string type, object data = null)
    {
        OnEvent?.Invoke(type, data);
    }

    // Initialize all components (called once at Awake)
    private void InitializeAllComponents()
    {
        // Clear any existing components
        components.Clear();

        // Find all ICharacterComponent components on this GameObject
        var foundComponents = GetComponents<ICharacterComponent>();
        components.AddRange(foundComponents);

        // Initialize each component
        foreach (var comp in components)
        {
            try
            {
                comp.Initialize(this);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize component {comp.GetType().Name}: {e.Message}");
            }
        }

        Debug.Log($"CharacterCore: Initialized {components.Count} components");
    }

    // Reload all components (clean up and re-initialize)
    private void ReloadAllComponents()
    {
        if (config == null) return;

        Debug.Log($"CharacterCore: Reloading all components with config: {config.name}");

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

        // IMPORTANT: Clear ALL event subscribers before re-initializing
        // This prevents duplicate event handlers
        ClearAllEventSubscribers();

        // Clear the component list and re-find all components
        components.Clear();
        var foundComponents = GetComponents<ICharacterComponent>();
        components.AddRange(foundComponents);

        // Re-initialize all components
        foreach (var comp in components)
        {
            try
            {
                comp.Initialize(this);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to re-initialize component {comp.GetType().Name}: {e.Message}");
            }
        }

        // Raise config changed event AFTER all components are re-initialized
        RaiseEvent("config_changed", config);

        Debug.Log($"CharacterCore: Reloaded {components.Count} components");
    }

    // Clear all event subscribers to prevent accumulation
    private void ClearAllEventSubscribers()
    {
        if (OnEvent != null)
        {
            // Get all delegates and remove them one by one
            var invocationList = OnEvent.GetInvocationList();
            foreach (var handler in invocationList)
            {
                OnEvent -= (Action<string, object>)handler;
            }
        }

        Debug.Log("CharacterCore: Cleared all event subscribers");
    }

    // Helper to get components
    public T GetCharacterComponent<T>() where T : class, ICharacterComponent
    {
        foreach (var comp in components)
        {
            if (comp is T typedComp)
                return typedComp;
        }
        return null;
    }

    // Create a default config for testing
    private CharacterConfig CreateDefaultConfig()
    {
        CharacterConfig defaultConfig = ScriptableObject.CreateInstance<CharacterConfig>();
        defaultConfig.name = "DefaultConfig";
        return defaultConfig;
    }

    // Clean up when destroyed
    void OnDestroy()
    {
        ClearAllEventSubscribers();
    }
}

// Base interface for all character systems
public interface ICharacterComponent
{
    void Initialize(CharacterCore character);
    void Tick(float deltaTime);
    void PhysicsTick(float fixedDeltaTime);
}