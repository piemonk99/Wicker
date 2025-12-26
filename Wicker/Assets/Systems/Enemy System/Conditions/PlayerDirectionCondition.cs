using UnityEngine;

[CreateAssetMenu(fileName = "PlayerDirectionCondition", menuName = "AI/Conditions/PlayerDirection")]
public class PlayerDirectionCondition : AICondition
{
    public enum DirectionType
    {
        InFront,
        Behind,
        EitherSide
    }

    [Header("Direction Check")]
    public DirectionType directionType = DirectionType.InFront;

    [Header("Angle Check (for InFront)")]
    public float maxAngle = 45f; // Degrees

    public override bool Evaluate(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        Transform player = blackboard.Get<Transform>("player");

        if (self == null || player == null)
            return false;

        // Get current facing direction - default to 1 (right) if not set
        float facingDirection = 1f;

        // Check if facing_direction exists in blackboard
        if (blackboard.HasKey("facing_direction"))
        {
            // Explicitly specify the type as float
            facingDirection = blackboard.Get<float>("facing_direction");
        }

        // Calculate direction to player
        Vector2 toPlayer = player.position - self.position;
        float playerDirection = Mathf.Sign(toPlayer.x);

        // Store for other behaviors to use
        blackboard.Set("player_direction", playerDirection);

        switch (directionType)
        {
            case DirectionType.InFront:
                // Check if player is in front (same sign)
                bool sameDirection = Mathf.Sign(facingDirection) == Mathf.Sign(playerDirection);

                // Optional: Also check angle for more precision
                if (maxAngle < 180f && sameDirection)
                {
                    Vector2 facingVector = new Vector2(facingDirection, 0);
                    float angle = Vector2.Angle(facingVector, toPlayer);
                    return angle <= maxAngle;
                }

                return sameDirection;

            case DirectionType.Behind:
                // Check if player is behind (opposite sign)
                return Mathf.Sign(facingDirection) != Mathf.Sign(playerDirection);

            case DirectionType.EitherSide:
                // Always true as long as player exists
                return true;

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