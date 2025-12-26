using UnityEngine;
using System;
using Random = UnityEngine.Random;

public static class PatrolHelper
{
    public static class BlackboardKeys
    {
        public const string PatrolDirection = "patrol_direction";
        public const string DirectionChangeTimer = "direction_change_timer";
        public const string LastMoveInput = "last_move_input";
    }

    // Initialize patrol state
    public static void InitializePatrol(AIBlackboard blackboard)
    {
        // Start with random direction
        float initialDirection = Random.value > 0.5f ? 1f : -1f;
        blackboard.Set(BlackboardKeys.PatrolDirection, initialDirection);
        blackboard.Set(BlackboardKeys.DirectionChangeTimer, 0f);
        blackboard.Set(BlackboardKeys.LastMoveInput, Vector2.zero);
    }

    // Physics update for patrol - returns movement input
    public static Vector2 UpdatePatrolPhysics(AIBlackboard blackboard, PatrolSettings settings, float fixedDeltaTime)
    {
        Transform transform = blackboard.Get<Transform>("transform");
        if (transform == null) return Vector2.zero;

        float currentDirection = blackboard.Get<float>(BlackboardKeys.PatrolDirection);
        float directionChangeTimer = blackboard.Get<float>(BlackboardKeys.DirectionChangeTimer);

        // Update timer
        directionChangeTimer -= fixedDeltaTime;
        blackboard.Set(BlackboardKeys.DirectionChangeTimer, directionChangeTimer);

        // Check for platform edge and walls
        bool hasGroundAhead = CheckGroundAhead(transform, currentDirection, settings);
        bool hasWallAhead = CheckWallAhead(transform, currentDirection, settings);

        // If no ground ahead or wall ahead, change direction
        if (!hasGroundAhead || hasWallAhead)
        {
            if (directionChangeTimer <= 0)
            {
                currentDirection *= -1;
                blackboard.Set(BlackboardKeys.PatrolDirection, currentDirection);
                blackboard.Set(BlackboardKeys.DirectionChangeTimer, settings.directionChangeDelay);

                // Update facing
                UpdateFacingDirection(transform, currentDirection);
            }
        }

        // Create movement input
        Vector2 moveInput = new Vector2(currentDirection, 0);
        blackboard.Set(BlackboardKeys.LastMoveInput, moveInput);

        return moveInput;
    }

    private static bool CheckGroundAhead(Transform transform, float direction, PatrolSettings settings)
    {
        Vector2 rayOrigin = (Vector2)transform.position +
            Vector2.right * direction * settings.forwardRaycastOffset;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            settings.groundCheckDistance,
            settings.groundLayer
        );

        return hit.collider != null;
    }

    private static bool CheckWallAhead(Transform transform, float direction, PatrolSettings settings)
    {
        Vector2 rayOrigin = (Vector2)transform.position +
            Vector2.right * direction * 0.2f;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.right * direction,
            settings.wallCheckDistance,
            settings.groundLayer
        );

        return hit.collider != null;
    }

    private static void UpdateFacingDirection(Transform transform, float direction)
    {
        transform.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(transform.localScale.x),
            transform.localScale.y,
            transform.localScale.z
        );
    }

    // Debug visualization
    public static void DrawPatrolGizmos(AIBlackboard blackboard, PatrolSettings settings)
    {
        Transform transform = blackboard.Get<Transform>("transform");
        float currentDirection = blackboard.Get<float>(BlackboardKeys.PatrolDirection);

        if (transform == null) return;

        // Draw ground check ray
        Gizmos.color = Color.red;
        Vector3 rayStart = transform.position + Vector3.right * currentDirection * settings.forwardRaycastOffset;
        Gizmos.DrawLine(rayStart, rayStart + Vector3.down * settings.groundCheckDistance);

        // Draw wall check ray
        Gizmos.color = Color.blue;
        Vector3 wallRayStart = transform.position + Vector3.right * currentDirection * 0.2f;
        Gizmos.DrawLine(wallRayStart, wallRayStart + Vector3.right * currentDirection * settings.wallCheckDistance);
    }
}


[Serializable]
public class PatrolSettings
{
    public float directionChangeDelay = 0.5f;
    public float forwardRaycastOffset = 0.5f;
    public float groundCheckDistance = 0.5f;
    public float wallCheckDistance = 0.3f;
    public LayerMask groundLayer;
}