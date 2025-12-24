using System.Collections.Generic;
using UnityEngine;

public class AIBlackboard : MonoBehaviour
{
    private Dictionary<string, object> data = new Dictionary<string, object>();
    private Dictionary<string, float> timers = new Dictionary<string, float>();

    private Vector2 movementInput = Vector2.zero;

    public void Set<T>(string key, T value)
    {
        data[key] = value;
    }

    public T Get<T>(string key)
    {
        if (data.ContainsKey(key))
            return (T)data[key];
        return default;
    }

    public bool Has(string key) => data.ContainsKey(key);

    public void Remove(string key) => data.Remove(key);

    // Timer methods
    public void StartTimer(string key, float duration)
    {
        timers[key] = duration;
    }

    public bool IsTimerRunning(string key)
    {
        return timers.ContainsKey(key) && timers[key] > 0;
    }

    public float GetTimerValue(string key)
    {
        return timers.ContainsKey(key) ? timers[key] : 0;
    }

    public void UpdateTimers(float deltaTime)
    {
        // Create a list of keys to remove to avoid modification during iteration
        List<string> expiredTimers = new List<string>();

        // Update all timers
        var keys = new List<string>(timers.Keys);
        foreach (var key in keys)
        {
            timers[key] -= deltaTime;
            if (timers[key] <= 0)
            {
                expiredTimers.Add(key);
            }
        }

        // Remove expired timers
        foreach (var key in expiredTimers)
        {
            timers.Remove(key);
        }
    }

    public bool IsTimerExpired(string key)
    {
        return !timers.ContainsKey(key) || timers[key] <= 0;
    }

    public void SetMovementInput(Vector2 input)
    {
        movementInput = input;
    }

    public Vector2 GetMovementInput()
    {
        return movementInput;
    }

    public void ClearMovementInput()
    {
        movementInput = Vector2.zero;
    }
}