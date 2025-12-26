using UnityEngine;

public class PatrolBehavior : AIBehavior
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
        public bool rememberLastDirection = false;
        
        // Patrol duration
        public float minPatrolTime = 5f;
        public float maxPatrolTime = 8f;
    }

    public Settings settings = new Settings();
    
    private float currentDirection;
    private float directionChangeTimer;
    private float patrolTimer;

    public override void OnActivate(AIBlackboard blackboard)
    {
        behaviorName = "Patrol";
        
        // Determine initial direction
        if (settings.rememberLastDirection)
        {
            float lastDirection = blackboard.Get<float>("last_patrol_direction", 0f);
            currentDirection = (lastDirection != 0) ? lastDirection : (Random.value > 0.5f ? 1f : -1f);
        }
        else
        {
            currentDirection = Random.value > 0.5f ? 1f : -1f;
        }

        blackboard.Set("patrol_direction", currentDirection);
        directionChangeTimer = 0f;
        
        // Set patrol timer
        patrolTimer = Random.Range(settings.minPatrolTime, settings.maxPatrolTime);
        blackboard.StartTimer("Patrol_Timer", patrolTimer);
        
        Debug.Log($"{blackboard.Get<Transform>("transform").name}: Starting Patrol");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        if (settings.rememberLastDirection)
        {
            blackboard.Set("last_patrol_direction", currentDirection);
        }
        
        blackboard.ClearMovementInput();
    }

    public override void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime)
    {
        Transform transform = blackboard.Get<Transform>("transform");
        if (transform == null) return;

        // Update timers
        directionChangeTimer -= fixedDeltaTime;
        
        // Check for obstacles
        bool hasGroundAhead = CheckGroundAhead(transform, currentDirection);
        bool hasWallAhead = CheckWallAhead(transform, currentDirection);

        // Change direction if needed
        if (!hasGroundAhead || hasWallAhead)
        {
            if (directionChangeTimer <= 0)
            {
                currentDirection *= -1;
                blackboard.Set("patrol_direction", currentDirection);
                directionChangeTimer = settings.directionChangeDelay;
                UpdateFacingDirection(transform, currentDirection);
            }
        }

        // Set movement
        blackboard.SetMovementInput(new Vector2(currentDirection, 0));
    }

    private bool CheckGroundAhead(Transform transform, float direction)
    {
        Vector2 rayOrigin = (Vector2)transform.position + 
                           Vector2.right * direction * settings.forwardRaycastOffset;
        
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 
                                           settings.groundCheckDistance, settings.groundLayer);
        
        if (settings.debugDraw)
        {
            Debug.DrawRay(rayOrigin, Vector2.down * settings.groundCheckDistance,
                         hit.collider ? Color.green : Color.red);
        }
        
        return hit.collider != null;
    }

    private bool CheckWallAhead(Transform transform, float direction)
    {
        Vector2 rayOrigin = (Vector2)transform.position + Vector2.right * direction * 0.2f;
        
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * direction,
                                           settings.wallCheckDistance, settings.groundLayer);
        
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
}