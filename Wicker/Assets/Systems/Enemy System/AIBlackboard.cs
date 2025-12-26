using System.Collections.Generic;
using UnityEngine;

public class AIBlackboard : MonoBehaviour
{
    // Data storage
    private Dictionary<string, object> data = new Dictionary<string, object>();

    // Timer management
    private Dictionary<string, float> timers = new Dictionary<string, float>();

    // Movement input
    private Vector2 movementInput = Vector2.zero;

    // Cached references
    private Transform cachedTransform;
    private CharacterCore cachedCharacter;
    private Transform cachedPlayer;
    private CharacterMovement cachedMovement;
    private CharacterAbilities cachedAbilities;

    // Update settings
    [Header("Update Settings")]
    [SerializeField] private float playerUpdateInterval = 0.1f;
    [SerializeField] private float movementUpdateInterval = 0.05f;
    [SerializeField] private float abilityUpdateInterval = 0.2f;

    private float playerUpdateTimer = 0f;
    private float movementUpdateTimer = 0f;
    private float abilityUpdateTimer = 0f;

    [Header("Debug")]
    [SerializeField] private bool logUpdates = false;

    void Start()
    {
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        cachedTransform = transform;
        cachedCharacter = GetComponent<CharacterCore>();

        if (cachedCharacter != null)
        {
            cachedMovement = cachedCharacter.GetCharacterComponent<CharacterMovement>();
            cachedAbilities = cachedCharacter.GetCharacterComponent<CharacterAbilities>();
        }

        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayer = playerObj.transform;
            Set("player", cachedPlayer);
        }

        // Set initial data
        Set("transform", cachedTransform);
        Set("character", cachedCharacter);
        Set("facing_direction", GetFacingDirection());
    }

    // Main update method - call this from AIStateMachine
    public void UpdateBlackboard(float deltaTime)
    {
        // Update timers
        UpdateTimers(deltaTime);

        // Update facing direction (always current)
        Set("facing_direction", GetFacingDirection());

        // Update player data periodically
        playerUpdateTimer -= deltaTime;
        if (playerUpdateTimer <= 0f)
        {
            UpdatePlayerData();
            playerUpdateTimer = playerUpdateInterval;
        }

        // Update movement data periodically
        movementUpdateTimer -= deltaTime;
        if (movementUpdateTimer <= 0f)
        {
            UpdateMovementData();
            movementUpdateTimer = movementUpdateInterval;
        }

        // Update ability data periodically
        abilityUpdateTimer -= deltaTime;
        if (abilityUpdateTimer <= 0f)
        {
            UpdateAbilityData();
            abilityUpdateTimer = abilityUpdateInterval;
        }
    }

    private void UpdatePlayerData()
    {
        if (cachedTransform == null || cachedPlayer == null) return;

        Vector2 toPlayer = cachedPlayer.position - cachedTransform.position;
        float distance = Vector2.Distance(cachedTransform.position, cachedPlayer.position);
        float directionToPlayer = Mathf.Sign(toPlayer.x);

        Set("player_distance", distance);
        Set("player_direction", directionToPlayer);
        Set("player_position", (Vector2)cachedPlayer.position);

        // Store raw vector to player
        Set("to_player_vector", toPlayer);

        // Calculate angle between facing direction and player (still useful raw data)
        float facingDirection = GetFacingDirection();
        Vector2 facingVector = new Vector2(facingDirection, 0);
        float angle = Vector2.Angle(facingVector, toPlayer);
        Set("player_angle", angle);

        if (logUpdates)
            Debug.Log($"Player Update - Dist: {distance:F2}, Dir: {directionToPlayer}, Angle: {angle:F1}°");
    }

    private void UpdateMovementData()
    {
        if (cachedMovement == null) return;

        // Get current movement state
        Set("velocity", cachedMovement.GetVelocity());
        Set("horizontal_velocity", cachedMovement.GetHorizontalVelocity());
        Set("vertical_velocity", cachedMovement.GetVerticalVelocity());
        Set("is_moving", cachedMovement.IsMoving());
        Set("current_input_x", cachedMovement.GetCurrentInputX());
        Set("last_input_direction", cachedMovement.GetLastInputDirection());
        Set("has_active_input", cachedMovement.HasActiveInput());

        // Ground check - you might need to add a public method to CharacterMovement
        // For now, approximate with vertical velocity
        bool isGrounded = Mathf.Abs(cachedMovement.GetVerticalVelocity()) < 0.1f;
        Set("is_grounded", isGrounded);

        if (logUpdates && cachedMovement.IsMoving())
            Debug.Log($"Movement Update - Vel: {cachedMovement.GetVelocity()}, InputX: {cachedMovement.GetCurrentInputX()}");
    }

    private void UpdateAbilityData()
    {
        if (cachedAbilities == null) return;

        // Lunge ability
        Set("can_lunge", cachedAbilities.CanLunge());
        Set("is_lunging", cachedAbilities.IsLunging());
        Set("lunge_cooldown_percent", cachedAbilities.GetAbilityCooldownPercent("lunge"));

        // Other abilities
        Set("can_dash", cachedAbilities.CanDash());
        Set("is_dashing", cachedAbilities.IsDashing());
        Set("dash_cooldown_percent", cachedAbilities.GetAbilityCooldownPercent("dash"));

        if (logUpdates && cachedAbilities.CanLunge())
            Debug.Log($"Ability Update - CanLunge: {cachedAbilities.CanLunge()}, IsLunging: {cachedAbilities.IsLunging()}");
    }

    private float GetFacingDirection()
    {
        if (cachedTransform == null) return 1f;
        return Mathf.Sign(cachedTransform.localScale.x);
    }

    // Update facing direction and sprite
    public void SetFacing(float direction)
    {
        if (cachedTransform == null) return;

        cachedTransform.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(cachedTransform.localScale.x),
            cachedTransform.localScale.y,
            cachedTransform.localScale.z
        );

        // Update the cached facing direction
        Set("facing_direction", GetFacingDirection());
    }

    public void UpdateTimers(float deltaTime)
    {
        Dictionary<string, float> newTimers = new Dictionary<string, float>();

        foreach (var kvp in timers)
        {
            float newValue = kvp.Value - deltaTime;
            if (newValue > 0)
            {
                newTimers[kvp.Key] = newValue;
            }
            // If newValue <= 0, we don't add it to newTimers (timer expires)
        }

        // Replace the old dictionary with the new one
        timers = newTimers;
    }

    public void StartTimer(string key, float duration)
    {
        timers[key] = duration;
    }

    public bool IsTimerExpired(string key)
    {
        return !timers.ContainsKey(key) || timers[key] <= 0;
    }

    public float GetTimerRemaining(string key)
    {
        return timers.ContainsKey(key) ? timers[key] : 0f;
    }

    // Existing data methods
    public void Set(string key, object value) => data[key] = value;
    public T Get<T>(string key) => data.ContainsKey(key) ? (T)data[key] : default;
    public bool HasKey(string key) => data.ContainsKey(key);

    // Movement input methods
    public void SetMovementInput(Vector2 input) => movementInput = input;
    public Vector2 GetMovementInput() => movementInput;
    public void ClearMovementInput() => movementInput = Vector2.zero;

    // Helper getter with default
    public T Get<T>(string key, T defaultValue)
    {
        if (!data.ContainsKey(key))
            return defaultValue;

        try
        {
            return (T)data[key];
        }
        catch
        {
            return defaultValue;
        }
    }
}