using UnityEngine;

// Player Direction Condition
public class PlayerDirectionCondition : AICondition
{
    public enum DirectionType { InFront, Behind, InView }

    [System.Serializable]
    public class Settings
    {
        public DirectionType directionType = DirectionType.InFront;
        public float maxAngle = 45f; // For InFront/InView types
    }

    public Settings settings = new Settings();

    // Default constructor
    public PlayerDirectionCondition() { }

    // Constructor for code-based settings
    public PlayerDirectionCondition(Settings settings)
    {
        this.settings = settings;
        conditionName = $"PlayerDirection_{settings.directionType}";
    }

    public override bool Evaluate(AIBlackboard blackboard)
    {
        // Get pre-calculated data from blackboard
        float facingDirection = blackboard.Get<float>("facing_direction", 1f);
        float playerAngle = blackboard.Get<float>("player_angle", 180f);
        float playerDirection = blackboard.Get<float>("player_direction", 0f);

        // For InView: just check angle
        if (settings.directionType == DirectionType.InView)
        {
            return playerAngle <= settings.maxAngle;
        }

        // For InFront/Behind: check both angle and direction
        bool withinAngle = playerAngle <= settings.maxAngle;
        bool sameDirection = Mathf.Sign(facingDirection) == Mathf.Sign(playerDirection);

        return settings.directionType == DirectionType.InFront
            ? (sameDirection && withinAngle)
            : (!sameDirection || !withinAngle);
    }
}
