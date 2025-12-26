using UnityEngine;

// Timer Condition with configurable duration
public class TimerExpiredCondition : AICondition
{
    [System.Serializable]
    public class Settings
    {
        public string timerKey = "";
        public float duration = 5f;
        public float randomVariance = 0f; // +- this amount
        public bool autoRestart = false; // Should timer auto-restart when it expires?
    }

    public Settings settings = new Settings();
    private float currentDuration;

    // Default constructor for inspector
    public TimerExpiredCondition() { }

    // Constructor for code-based settings
    public TimerExpiredCondition(Settings settings)
    {
        this.settings = settings;
        conditionName = $"Timer_{settings.timerKey}";
    }

    public override bool Evaluate(AIBlackboard blackboard)
    {
        bool isExpired = blackboard.IsTimerExpired(settings.timerKey);

        // Auto-restart logic if needed
        if (isExpired && settings.autoRestart)
        {
            StartTimer(blackboard);
        }

        return isExpired;
    }

    // Method to start the timer (call this from behaviors)
    public void StartTimer(AIBlackboard blackboard)
    {
        if (string.IsNullOrEmpty(settings.timerKey))
        {
            Debug.LogError("TimerExpiredCondition: timerKey is empty!");
            return;
        }

        // Calculate duration with random variance
        currentDuration = settings.duration;
        if (settings.randomVariance > 0)
        {
            currentDuration += Random.Range(-settings.randomVariance, settings.randomVariance);
            currentDuration = Mathf.Max(0.1f, currentDuration); // Ensure positive
        }

        blackboard.StartTimer(settings.timerKey, currentDuration);
    }
}
