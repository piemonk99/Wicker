using UnityEngine;

// Player Distance Condition
public class PlayerDistanceCondition : AICondition
{
    public enum ComparisonType { LessThan, GreaterThan, WithinRange }

    [System.Serializable]
    public class Settings
    {
        public ComparisonType comparison = ComparisonType.LessThan;
        public float distance = 5f;
        public float minDistance = 2f;
        public float maxDistance = 5f;
    }

    public PlayerDistanceCondition(Settings settings)
    {
        this.settings = settings;
        conditionName = $"PlayerDistance_{settings.comparison}_{settings.distance}";
    }

    public Settings settings = new Settings();

    public override bool Evaluate(AIBlackboard blackboard)
    {
        // Distance is auto-updated in blackboard
        float currentDistance = blackboard.Get<float>("player_distance", Mathf.Infinity);

        switch (settings.comparison)
        {
            case ComparisonType.LessThan:
                return currentDistance < settings.distance;

            case ComparisonType.GreaterThan:
                return currentDistance > settings.distance;

            case ComparisonType.WithinRange:
                return currentDistance >= settings.minDistance && currentDistance <= settings.maxDistance;

            default:
                return false;
        }
    }
}
