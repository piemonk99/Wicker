using UnityEngine;

public class ChaseBehavior : AIBehavior
{
    [System.Serializable]
    public class Settings
    {
        [Header("Movement")]
        public float speedMultiplier = 1.2f;

        [Header("Debug")]
        public bool drawDebug = true;
    }

    public Settings settings = new Settings();

    private float currentDirection;

    public override void OnActivate(AIBlackboard blackboard)
    {
        behaviorName = "Chase";
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
        // Get player direction from blackboard (auto-updated)
        float playerDirection = blackboard.Get<float>("player_direction", 0f);

        // Update our movement direction
        currentDirection = playerDirection;

        // Face the player (blackboard handles sprite flipping)
        blackboard.SetFacing(currentDirection);
    }

    public override void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime)
    {
        // Set movement input based on calculated direction
        if (currentDirection != 0f)
        {
            blackboard.SetMovementInput(new Vector2(currentDirection, 0));
        }
    }
}