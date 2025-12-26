using UnityEngine;

[CreateAssetMenu(fileName = "PlayerDistanceCondition", menuName = "AI/Conditions/PlayerDistance")]
public class PlayerDistanceCondition : AICondition
{
    public enum ComparisonType
    {
        LessThan,
        GreaterThan,
        WithinRange
    }

    [Header("Distance Check")]
    public ComparisonType comparison = ComparisonType.LessThan;
    public float distance = 5f;

    [Tooltip("For WithinRange type")]
    public float minDistance = 2f;
    [Tooltip("For WithinRange type")]
    public float maxDistance = 5f;

    [Header("Performance")]
    public float checkInterval = 0.1f;

    private float lastCheckTime = 0f;
    private bool cachedResult = false;

    public override bool Evaluate(AIBlackboard blackboard)
    {
        // Check interval for performance
        if (Time.time < lastCheckTime + checkInterval)
            return cachedResult;

        lastCheckTime = Time.time;
        cachedResult = EvaluateDistance(blackboard);
        return cachedResult;
    }

    private bool EvaluateDistance(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        Transform player = blackboard.Get<Transform>("player");

        if (self == null || player == null)
            return false;

        float currentDistance = Vector2.Distance(self.position, player.position);

        // Store for other behaviors to use
        blackboard.Set("player_distance", currentDistance);

        switch (comparison)
        {
            case ComparisonType.LessThan:
                return currentDistance < distance;

            case ComparisonType.GreaterThan:
                return currentDistance > distance;

            case ComparisonType.WithinRange:
                return currentDistance >= minDistance && currentDistance <= maxDistance;

            default:
                return false;
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (string.IsNullOrEmpty(conditionName))
            conditionName = name;
    }
}