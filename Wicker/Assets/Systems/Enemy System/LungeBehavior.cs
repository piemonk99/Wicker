using UnityEngine;

public class LungeBehavior : AIBehavior
{
    [System.Serializable]
    public class Settings
    {
        [Header("Windup")]
        public float windupDuration = 0.3f;

        [Header("Lunge Duration")]
        public float lungeDuration = 1.3f; 

        [Header("Recovery")]
        public float recoveryDuration = 1f;

        [Header("Movement During Windup")]
        public bool stopDuringWindup = true;

        [Header("Debug")]
        public bool drawDebug = true;
    }

    public Settings settings = new Settings();

    private enum LungePhase { Windup, Lunging, Recovery, Complete }
    private LungePhase currentPhase = LungePhase.Complete;
    private float phaseTimer = 0f;

    public override void OnActivate(AIBlackboard blackboard)
    {
        behaviorName = "Lunge";

        currentPhase = LungePhase.Windup;
        phaseTimer = settings.windupDuration;

        if (settings.stopDuringWindup)
            blackboard.SetMovementInput(Vector2.zero);

        // Face player
        Transform self = blackboard.Get<Transform>("transform");
        Transform player = blackboard.Get<Transform>("player");

        if (self != null && player != null)
        {
            float direction = Mathf.Sign(player.position.x - self.position.x);
            blackboard.Set("facing_direction", direction);
            UpdateFacingDirection(self, direction);
        }

        // Start the overall lunge timer (windup + lunge duration + recovery)
        float totalLungeTime = settings.windupDuration + settings.lungeDuration + settings.recoveryDuration;
        blackboard.StartTimer("Lunge_Timer", totalLungeTime);

        Debug.Log($"{behaviorName}: Starting lunge (total: {totalLungeTime:F2}s)");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        blackboard.ClearMovementInput();
    }

    public override void Tick(AIBlackboard blackboard, float deltaTime)
    {
        phaseTimer -= deltaTime;

        switch (currentPhase)
        {
            case LungePhase.Windup:
                if (phaseTimer <= 0f)
                    ExecuteLunge(blackboard);
                break;

            case LungePhase.Lunging:
                UpdateLunging(blackboard, deltaTime);
                break;

            case LungePhase.Recovery:
                UpdateRecovery(blackboard, deltaTime);
                break;
        }
    }

    private void ExecuteLunge(AIBlackboard blackboard)
    {
        CharacterCore character = blackboard.Get<CharacterCore>("character");
        if (character != null)
        {
            character.RaiseEvent("lunge_pressed", null);
            currentPhase = LungePhase.Lunging;
            phaseTimer = settings.lungeDuration;  // Set lunge duration timer
        }
    }

    private void UpdateLunging(AIBlackboard blackboard, float deltaTime)
    {
        // Check if lunge duration is complete
        if (phaseTimer <= 0f)
        {
            StartRecovery(blackboard);
        }
    }

    private void StartRecovery(AIBlackboard blackboard)
    {
        currentPhase = LungePhase.Recovery;
        phaseTimer = settings.recoveryDuration;

        blackboard.SetMovementInput(Vector2.zero);
        // Timer was already started in OnActivate, so no need to start it here
    }

    private void UpdateRecovery(AIBlackboard blackboard, float deltaTime)
    {
        if (phaseTimer <= 0f)
        {
            currentPhase = LungePhase.Complete;
        }
    }

    private void UpdateFacingDirection(Transform self, float direction)
    {
        self.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(self.localScale.x),
            self.localScale.y,
            self.localScale.z
        );
    }

    public void OnAbilityEvent(string eventType)
    {
        // Optional: Could handle ability events here
    }
}