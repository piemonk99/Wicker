using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// CharacterCore manages all character components for a character - player or enemy.
// Loads config options in from scriptable objects to their corresponding components
// Ticks and PhysicsTicks all components
// Serves as the connection between the event calls from an input script, like PlayerController or some enemy ai script, and the components listening for event calls.
public class CharacterCore : MonoBehaviour
{
    // Config
    [Header("Character Configuration")]
    [SerializeField] private CharacterConfig config;

    // Simple event system - ALL communication goes through this
    public event Action<string, object> OnEvent;

    // Component management
    private List<ICharacterComponent> components = new();
    private List<ICharacterController> controllers = new();


    // This dictionary stores information about the character's current state.
    // The fields are created, managed, and utilized by the character's components.
    // This allows soft referencing of other components that may not always exist.
    public Dictionary<string, object> CharacterContext { get; private set; }


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
        
        // Update all controllers first (generate inputs)
        foreach (var controller in controllers)
        {
            if (controller != null)
                controller.UpdateController(delta);
        }
        
        // Then update all components (react to inputs)
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
        
        // Update all controllers first in FixedUpdate
        foreach (var controller in controllers)
        {
            if (controller != null)
                controller.FixedUpdateController(fixedDelta);
        }
        
        // Then update all components in FixedUpdate
        foreach (var comp in components)
            comp.PhysicsTick(fixedDelta);
    }

    // Public method to raise events
    public void RaiseEvent(string type, object data = null)
    {
        OnEvent?.Invoke(type, data);
    }

    // Initialize all components and controllers (called once at Awake)
    private void InitializeAllComponents()
    {
        // Clear any existing components and controllers
        components.Clear();
        controllers.Clear();

        CharacterContext = new Dictionary<string, object>();

        // Find all ICharacterComponent components on this GameObject
        var foundComponents = GetComponents<ICharacterComponent>();
        components.AddRange(foundComponents);

        // Find all ICharacterController components on this GameObject
        var foundControllers = GetComponents<ICharacterController>();
        controllers.AddRange(foundControllers);

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

        // Initialize each controller
        foreach (var controller in controllers)
        {
            try
            {
                controller.Initialize(this);
                controller.Enable(); // Enable controllers by default
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize controller {controller.GetType().Name}: {e.Message}");
            }
        }
    }

    // Reload all components and controllers (clean up and re-initialize)
    private void ReloadAllComponents()
    {
        if (config == null) return;

#if UNITY_EDITOR
    UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log($"CharacterCore: Starting reload for {gameObject.name}");

        // IMPORTANT: Clear ALL event subscribers before re-initializing
        ClearAllEventSubscribers();

        // Clear the component and controller lists
        components.Clear();
        controllers.Clear();

        CharacterContext = new Dictionary<string, object>();

        // Re-find all components - this includes existing CharacterWeapons
        var foundComponents = GetComponents<ICharacterComponent>();
        components.AddRange(foundComponents);

        Debug.Log($"Found {components.Count} components to reinitialize");

        // Re-find all controllers
        var foundControllers = GetComponents<ICharacterController>();
        controllers.AddRange(foundControllers);

        // Re-initialize all components
        foreach (var comp in components)
        {
            try
            {
                comp.Initialize(this);
                Debug.Log($"Re-initialized component: {comp.GetType().Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to re-initialize component {comp.GetType().Name}: {e.Message}");
            }
        }

        // Re-initialize all controllers
        foreach (var controller in controllers)
        {
            try
            {
                controller.Initialize(this);
                controller.Enable();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to re-initialize controller {controller.GetType().Name}: {e.Message}");
            }
        }

        // Raise config changed event AFTER all components are re-initialized
        RaiseEvent("config_changed", config);

        Debug.Log($"CharacterCore: Reload complete for {gameObject.name}");
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

    // Helper to get controllers
    public T GetCharacterController<T>() where T : class, ICharacterController
    {
        foreach (var controller in controllers)
        {
            if (controller is T typedController)
                return typedController;
        }
        return null;
    }

    // Add controller at runtime
    public void AddController(ICharacterController controller)
    {
        if (controller != null && !controllers.Contains(controller))
        {
            controllers.Add(controller);
            controller.Initialize(this);
            controller.Enable();
        }
    }

    // Remove controller at runtime
    public void RemoveController(ICharacterController controller)
    {
        if (controller != null && controllers.Contains(controller))
        {
            controller.Disable();
            controllers.Remove(controller);
        }
    }

    // Enable/disable specific controller type
    public void EnableController<T>() where T : ICharacterController
    {
        foreach (var controller in controllers)
        {
            if (controller is T)
            {
                controller.Enable();
            }
        }
    }

    public void DisableController<T>() where T : ICharacterController
    {
        foreach (var controller in controllers)
        {
            if (controller is T)
            {
                controller.Disable();
            }
        }
    }

    // Switch controllers (enable one, disable others)
    public void SwitchToController<T>() where T : ICharacterController
    {
        foreach (var controller in controllers)
        {
            if (controller is T)
                controller.Enable();
            else
                controller.Disable();
        }
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
        
        // Disable all controllers
        foreach (var controller in controllers)
        {
            controller?.Disable();
        }
    }
}

// Base interface for all character systems - these listen for event calls from a charactercontroller and apply effects, some of which may call events as well.
public interface ICharacterComponent
{
    void Initialize(CharacterCore character);
    void Tick(float deltaTime);
    void PhysicsTick(float fixedDeltaTime);
}

// Base interface for all character controllers - these generate event calls for the components
public interface ICharacterController
{
    void Initialize(CharacterCore character);
    void UpdateController(float deltaTime);
    void FixedUpdateController(float fixedDeltaTime);
    void Enable();
    void Disable();
}