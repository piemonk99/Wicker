using UnityEngine;

[CreateAssetMenu(menuName = "AI/Behaviors/Patrol")]
public class Patrol : AIBehavior
{
    [System.Serializable]
    public class Settings
    {
        public float directionChangeDelay = 0.5f;
        public float forwardRaycastOffset = 3f;
        public float groundCheckDistance = 2f;
        public float wallCheckDistance = 3f;
        public LayerMask groundLayer;
        public bool debugDraw = true;
        public bool rememberLastDirection = false; // Remember last patrol direction
    }

    [Header("Patrol Settings")]
    public Settings settings = new Settings();

    [Header("Patrol Duration")]
    public float minPatrolTime = 5f;
    public float maxPatrolTime = 8f;

    protected override void OnEnable()
    {
        base.OnEnable(); // This sets behaviorName from asset name
    }

    protected override void OnValidate()
    {
        base.OnValidate(); // This sets behaviorName from asset name
    }

    public override void OnActivate(AIBlackboard blackboard)
    {
        // Check if we should remember the last patrol direction
        float initialDirection;

        if (settings.rememberLastDirection)
        {
            // Try to get the last patrol direction from blackboard
            float lastDirection = blackboard.Get<float>("last_patrol_direction");

            if (lastDirection == 1f || lastDirection == -1f)
            {
                // Use the remembered direction
                initialDirection = lastDirection;
                if (settings.debugDraw)
                    Debug.Log($"{blackboard.Get<Transform>("transform").name}: Using remembered direction: {initialDirection}");
            }
            else
            {
                // No valid remembered direction, pick random
                initialDirection = Random.value > 0.5f ? 1f : -1f;
                if (settings.debugDraw)
                    Debug.Log($"{blackboard.Get<Transform>("transform").name}: No remembered direction, picking random: {initialDirection}");
            }
        }
        else
        {
            // Don't remember, always pick random
            initialDirection = Random.value > 0.5f ? 1f : -1f;
            if (settings.debugDraw)
                Debug.Log($"{blackboard.Get<Transform>("transform").name}: Picking random direction: {initialDirection}");
        }

        // Initialize patrol with chosen direction
        blackboard.Set("patrol_direction", initialDirection);
        blackboard.Set("direction_change_timer", 0f);

        // Clear any existing movement input
        blackboard.ClearMovementInput();

        // Start patrol timer using behaviorName (auto-filled from asset name)
        float patrolTime = Random.Range(minPatrolTime, maxPatrolTime);
        blackboard.StartTimer(behaviorName + "_Timer", patrolTime);

        Debug.Log($"{blackboard.Get<Transform>("transform").name}: Starting {behaviorName} for {patrolTime} seconds");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        // Save the current patrol direction if we should remember it
        if (settings.rememberLastDirection)
        {
            float currentDirection = blackboard.Get<float>("patrol_direction");
            blackboard.Set("last_patrol_direction", currentDirection);

            if (settings.debugDraw)
                Debug.Log($"{blackboard.Get<Transform>("transform").name}: Saved patrol direction: {currentDirection}");
        }

        // Clear movement input when leaving patrol
        blackboard.ClearMovementInput();

        if (settings.debugDraw)
            Debug.Log($"{blackboard.Get<Transform>("transform").name}: Clearing movement input");
    }

    public override void Tick(AIBlackboard blackboard, float deltaTime) { }

    public override void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime)
    {
        Transform transform = blackboard.Get<Transform>("transform");
        if (transform == null) return;

        float currentDirection = blackboard.Get<float>("patrol_direction");
        float directionChangeTimer = blackboard.Get<float>("direction_change_timer");

        // Update timer
        directionChangeTimer -= fixedDeltaTime;
        blackboard.Set("direction_change_timer", directionChangeTimer);

        // Check for platform edge and walls
        bool hasGroundAhead = CheckGroundAhead(transform, currentDirection);
        bool hasWallAhead = CheckWallAhead(transform, currentDirection);

        // Debug logging
        if (settings.debugDraw)
        {
            Debug.Log($"[{Time.time:F2}] Patrol Check - Direction: {currentDirection}, " +
                     $"Ground Ahead: {hasGroundAhead}, Wall Ahead: {hasWallAhead}, " +
                     $"Timer: {directionChangeTimer:F2}");
        }

        // If no ground ahead or wall ahead, change direction
        if (!hasGroundAhead || hasWallAhead)
        {
            if (directionChangeTimer <= 0)
            {
                currentDirection *= -1;
                blackboard.Set("patrol_direction", currentDirection);
                blackboard.Set("direction_change_timer", settings.directionChangeDelay);

                // Update facing
                UpdateFacingDirection(transform, currentDirection);

                if (settings.debugDraw)
                    Debug.Log($"[{Time.time:F2}] Changing direction to: {currentDirection}");
            }
        }

        // Set movement input (just direction, speed handled by CharacterMovement)
        Vector2 moveInput = new Vector2(currentDirection, 0);
        blackboard.SetMovementInput(moveInput);
    }

    private bool CheckGroundAhead(Transform transform, float direction)
    {
        Vector2 rayOrigin = (Vector2)transform.position +
            Vector2.right * direction * settings.forwardRaycastOffset;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            settings.groundCheckDistance,
            settings.groundLayer
        );

        // Debug visualization
        if (settings.debugDraw)
        {
            Debug.DrawRay(rayOrigin, Vector2.down * settings.groundCheckDistance,
                         hit.collider ? Color.green : Color.red);
        }

        return hit.collider != null;
    }

    private bool CheckWallAhead(Transform transform, float direction)
    {
        Vector2 rayOrigin = (Vector2)transform.position +
            Vector2.right * direction * 0.2f;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.right * direction,
            settings.wallCheckDistance,
            settings.groundLayer
        );

        // Debug visualization
        if (settings.debugDraw)
        {
            Debug.DrawRay(rayOrigin, Vector2.right * direction * settings.wallCheckDistance,
                         hit.collider ? Color.yellow : Color.blue);
        }

        return hit.collider != null;
    }

    private void UpdateFacingDirection(Transform transform, float direction)
    {
        transform.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(transform.localScale.x),
            transform.localScale.y,
            transform.localScale.z
        );
    }

    // For debug drawing in Scene view
    public void DrawGizmos(Transform transform, AIBlackboard blackboard)
    {
        if (transform == null || !settings.debugDraw) return;

        float currentDirection = blackboard.Get<float>("patrol_direction");

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