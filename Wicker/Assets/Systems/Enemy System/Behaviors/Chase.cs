using UnityEngine;

[CreateAssetMenu(fileName = "Chase", menuName = "AI/Behaviors/Chase")]
public class Chase : AIBehavior
{
    [System.Serializable]
    public class ChaseSettings
    {
        [Header("Chase Settings")]
        public float speedMultiplier = 1.2f;
        public float stopDistance = 1f; // Stop when this close to player
        public float maxChaseDistance = 10f; // Give up if player is too far

        [Header("Movement State")]
        public bool applyChaseMovementState = true;
        public string chaseStateName = "Chasing";

        [Header("Detection")]
        public float checkInterval = 0.2f;

        [Header("Debug")]
        public bool drawDebug = true;
    }

    public ChaseSettings settings = new ChaseSettings();

    private float lastCheckTime = 0f;
    private Transform playerTransform;
    private AIBlackboard currentBlackboard; // Store reference for UpdateFacingDirection

    public override void OnActivate(AIBlackboard blackboard)
    {
        base.OnActivate(blackboard);

        currentBlackboard = blackboard; // Store for later use

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

        // Apply chase movement state if configured
        if (settings.applyChaseMovementState)
        {
            CharacterCore character = blackboard.Get<CharacterCore>("character");
            if (character != null)
            {
                character.RaiseEvent("movement_modifier_add",
                    new CharacterMovement.MovementState(
                        name: settings.chaseStateName,
                        type: CharacterMovement.MovementStateType.Modifier,
                        maxSpeedMultiplier: settings.speedMultiplier
                    ));
            }
        }

        // Start a timer for this state (prevents infinite chasing)
        blackboard.StartTimer(behaviorName + "_Timer", 5f); // 5 second max chase

        Debug.Log($"{behaviorName}: Started chasing");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        base.OnDeactivate(blackboard);

        // Remove chase movement state
        if (settings.applyChaseMovementState)
        {
            CharacterCore character = blackboard.Get<CharacterCore>("character");
            if (character != null)
            {
                character.RaiseEvent("movement_modifier_remove", settings.chaseStateName);
            }
        }

        // Clear movement input
        blackboard.ClearMovementInput();

        Debug.Log($"{behaviorName}: Stopped chasing");
    }

    public override void Tick(AIBlackboard blackboard, float deltaTime)
    {
        // Update detection periodically for performance
        lastCheckTime -= deltaTime;
        if (lastCheckTime <= 0f)
        {
            UpdateChaseDirection(blackboard);
            lastCheckTime = settings.checkInterval;
        }
    }

    public override void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime)
    {
        // Movement is handled by setting the blackboard input
        // The EnemyAIController will send it as an event
    }

    private void UpdateChaseDirection(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        if (self == null || playerTransform == null) return;

        // Calculate direction to player
        float directionToPlayer = Mathf.Sign(playerTransform.position.x - self.position.x);
        float distanceToPlayer = Mathf.Abs(playerTransform.position.x - self.position.x);

        // Store for conditions to use
        blackboard.Set("player_distance", distanceToPlayer);
        blackboard.Set("player_direction", directionToPlayer);

        // Check if player is too far to chase
        if (distanceToPlayer > settings.maxChaseDistance)
        {
            // Player is too far, stop chasing
            blackboard.SetMovementInput(Vector2.zero);
            return;
        }

        // Check if we're close enough to stop
        if (distanceToPlayer <= settings.stopDistance)
        {
            // We're close enough, stop moving
            blackboard.SetMovementInput(Vector2.zero);

            // Face the player
            UpdateFacingDirection(self, directionToPlayer, blackboard);
        }
        else
        {
            // Move toward player
            blackboard.SetMovementInput(new Vector2(directionToPlayer, 0));

            // Face the movement direction
            UpdateFacingDirection(self, directionToPlayer, blackboard);
        }

        // Debug drawing
        if (settings.drawDebug)
        {
            Debug.DrawLine(self.position, playerTransform.position,
                         distanceToPlayer <= settings.stopDistance ? Color.green : Color.yellow);
        }
    }

    private void UpdateFacingDirection(Transform self, float direction, AIBlackboard blackboard)
    {
        // Update facing direction in blackboard
        blackboard.Set("facing_direction", direction);

        // Optional: Actually flip the sprite
        self.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(self.localScale.x),
            self.localScale.y,
            self.localScale.z
        );
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (string.IsNullOrEmpty(behaviorName))
            behaviorName = name;
    }
}