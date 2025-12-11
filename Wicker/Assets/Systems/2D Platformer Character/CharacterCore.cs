using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterCore : MonoBehaviour
{
    // Simple event system - ALL communication goes through this
    public event Action<string, object> OnEvent;

    // Component management
    private List<ICharacterComponent> components = new();

    void Awake()
    {
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
}

// Base interface for all character systems
public interface ICharacterComponent
{
    void Initialize(CharacterCore character);
    void Tick(float deltaTime);
    void PhysicsTick(float fixedDeltaTime);
}