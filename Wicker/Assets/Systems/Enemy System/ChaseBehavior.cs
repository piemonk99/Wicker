using UnityEngine;

public class ChaseBehavior : AIBehavior
{
    [System.Serializable]
    public class Settings
    {
        public float speedMultiplier = 1.2f;
        public float stopDistance = 1f;
        public float maxChaseDistance = 10f;
        public float checkInterval = 0.2f;
        public bool drawDebug = true;
    }

    public Settings settings = new Settings();
    
    private Transform playerTransform;
    private float lastCheckTime;

    public override void OnActivate(AIBlackboard blackboard)
    {
        behaviorName = "Chase";
        
        playerTransform = blackboard.Get<Transform>("player");
        lastCheckTime = 0f;
        
        Debug.Log($"{behaviorName}: Started chasing");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        blackboard.ClearMovementInput();
    }

    public override void Tick(AIBlackboard blackboard, float deltaTime)
    {
        lastCheckTime -= deltaTime;
        if (lastCheckTime <= 0f)
        {
            UpdateChaseDirection(blackboard);
            lastCheckTime = settings.checkInterval;
        }
    }

    private void UpdateChaseDirection(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        if (self == null || playerTransform == null) return;

        float directionToPlayer = Mathf.Sign(playerTransform.position.x - self.position.x);
        float distance = Mathf.Abs(playerTransform.position.x - self.position.x);
        
        blackboard.Set("player_distance", distance);
        blackboard.Set("player_direction", directionToPlayer);

        if (distance > settings.maxChaseDistance)
        {
            blackboard.SetMovementInput(Vector2.zero);
            return;
        }

        if (distance <= settings.stopDistance)
        {
            blackboard.SetMovementInput(Vector2.zero);
            UpdateFacingDirection(self, directionToPlayer, blackboard);
        }
        else
        {
            blackboard.SetMovementInput(new Vector2(directionToPlayer, 0));
            UpdateFacingDirection(self, directionToPlayer, blackboard);
        }
    }

    private void UpdateFacingDirection(Transform self, float direction, AIBlackboard blackboard)
    {
        blackboard.Set("facing_direction", direction);
        self.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(self.localScale.x),
            self.localScale.y,
            self.localScale.z
        );
    }
}