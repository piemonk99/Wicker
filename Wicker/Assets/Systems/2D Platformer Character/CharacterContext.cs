using System.Collections.Generic;
using UnityEngine;

public class CharacterContext
{
    // Core states (updated automatically)
    public bool IsGrounded { get; set; }
    public Vector2 Velocity { get; set; }
    public bool IsGrappling { get; set; }
    public bool IsDashing { get; set; }

    // Computed properties (no storage needed)
    public bool CanDropThroughPlatforms => IsGrounded && !IsGrappling && !IsDashing;

    // Game-specific flags (optional, rarely used)
    private Dictionary<string, object> _customFlags = new Dictionary<string, object>();

    public void SetCustom(string key, object value) => _customFlags[key] = value;
    public T GetCustom<T>(string key, T defaultValue = default)
    {
        if (_customFlags.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return defaultValue;
    }
}