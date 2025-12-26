using UnityEngine;

public class ChaseBehavior : AIBehavior
{
    [System.Serializable]
    public class Settings
    {
        [Header("Movement")]
        public float speedMultiplier = 1.2f;

        [Header("Update Frequency")]
        public float checkInterval = 0.2f; // How often to recalculate direction

        [Header("Debug")]
        public bool drawDebug = true;
    }

    public Settings settings = new Settings();

    private Transform playerTransform;
    private float lastCheckTime;
    private float currentDirection;

    public override void OnActivate(AIBlackboard blackboard)
    {
        behaviorName = "Chase";

        // Get player reference
        playerTransform = blackboard.Get<Transform>("player");
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                blackboard.Set("player", playerTransform);
            }
        }

        lastCheckTime = 0f;
        currentDirection = 0f;

        Debug.Log($"{behaviorName}: Started chasing");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        blackboard.ClearMovementInput();
        currentDirection = 0f;

        Debug.Log($"{behaviorName}: Stopped chasing");
    }

    public override void Tick(AIBlackboard blackboard, float deltaTime)
    {
        // Update direction periodically for performance
        lastCheckTime -= deltaTime;
        if (lastCheckTime <= 0f)
        {
            UpdateChaseDirection(blackboard);
            lastCheckTime = settings.checkInterval;
        }
    }

    public override void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime)
    {
        // Set movement input based on calculated direction
        if (currentDirection != 0f)
        {
            blackboard.SetMovementInput(new Vector2(currentDirection, 0));
        }
    }

    private void UpdateChaseDirection(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        if (self == null || playerTransform == null)
        {
            currentDirection = 0f;
            return;
        }

        // Calculate direction to player
        currentDirection = Mathf.Sign(playerTransform.position.x - self.position.x);

        // Update facing direction in blackboard (for conditions to use)
        blackboard.Set("facing_direction", currentDirection);

        // Actually flip the sprite to face the player
        UpdateFacingDirection(self, currentDirection);

        // Store player distance for conditions
        float distance = Mathf.Abs(playerTransform.position.x - self.position.x);
        blackboard.Set("player_distance", distance);

        // Debug visualization
        if (settings.drawDebug)
        {
            Debug.DrawLine(self.position, playerTransform.position, Color.yellow);
        }
    }

    private void UpdateFacingDirection(Transform self, float direction)
    {
        if (direction == 0f) return;

        // Flip sprite to face movement direction
        self.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(self.localScale.x),
            self.localScale.y,
            self.localScale.z
        );
    }

    // Optional: For debugging in Scene view
    public void DrawGizmos(Transform transform, AIBlackboard blackboard)
    {
        if (!settings.drawDebug || playerTransform == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, playerTransform.position);
        Gizmos.DrawWireSphere(playerTransform.position, 0.3f);
    }
}