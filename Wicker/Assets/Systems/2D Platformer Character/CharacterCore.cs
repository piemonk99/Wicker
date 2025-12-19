using System;
using System.Collections.Generic;
using UnityEngine;

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
        // Notify components that config changed
        RaiseEvent("config_changed", newConfig);
    }

    void Awake()
    {
        // Create default config if none assigned
        if (config == null)
        {
            config = CreateDefaultConfig();
            Debug.LogWarning($"No CharacterConfig assigned to {gameObject.name}. Created default config.");
        }

        // Find and initialize all components
        var found = GetComponents<ICharacterComponent>();
        components.AddRange(found);

        foreach (var comp in components)
            comp.Initialize(this);
    }

    void Update()
    {
        float delta = Time.deltaTime;
        foreach (var comp in components)
            comp.Tick(delta);
    }

    void FixedUpdate()
    {
        float fixedDelta = Time.fixedDeltaTime;
        foreach (var comp in components)
            comp.PhysicsTick(fixedDelta);
    }

    // ONLY public method - raise events
    public void RaiseEvent(string type, object data = null)
    {
        OnEvent?.Invoke(type, data);
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
}

// Base interface for all character systems
public interface ICharacterComponent
{
    void Initialize(CharacterCore character);
    void Tick(float deltaTime);
    void PhysicsTick(float fixedDeltaTime);
}