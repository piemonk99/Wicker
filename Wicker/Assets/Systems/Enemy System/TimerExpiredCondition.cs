using UnityEngine;

[CreateAssetMenu(menuName = "AI/Conditions/TimerExpired")]
public class TimerExpiredCondition : AICondition
{
    [Header("Timer Settings")]
    [HideInInspector] public string timerKey; // Hidden - auto-filled

    public override bool Evaluate(AIBlackboard blackboard)
    {
        return blackboard.IsTimerExpired(timerKey);
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // Sets conditionName

        // Auto-fill timerKey from asset name
        if (string.IsNullOrEmpty(timerKey))
        {
            // Remove "Timer" suffix if present to avoid duplication
            string baseName = name;
            if (baseName.EndsWith("Timer"))
                baseName = baseName.Substring(0, baseName.Length - 5);

            timerKey = baseName + "_Timer";
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate(); // Sets conditionName

        // Also auto-fill in editor
        if (string.IsNullOrEmpty(timerKey))
        {
            // Remove "Timer" suffix if present to avoid duplication
            string baseName = name;
            if (baseName.EndsWith("Timer"))
                baseName = baseName.Substring(0, baseName.Length - 5);

            timerKey = baseName + "_Timer";
        }
    }
}