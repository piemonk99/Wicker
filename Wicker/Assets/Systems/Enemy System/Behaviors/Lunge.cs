using UnityEngine;

[CreateAssetMenu(fileName = "Lunge", menuName = "AI/Behaviors/Lunge")]
public class Lunge : AIBehavior
{
    [System.Serializable]
    public class LungeSettings
    {
        [Header("Windup")]
        public float windupDuration = 0.3f; // Time before actually lunging

        [Header("Recovery")]
        public float recoveryDuration = 1f; // Time after lunge ends before we can do anything else

        [Header("Movement During Windup")]
        public bool stopDuringWindup = true;

        [Header("Debug")]
        public bool drawDebug = true;
    }

    public LungeSettings settings = new LungeSettings();

    private enum LungePhase
    {
        Windup,
        Lunging,
        Recovery,
        Complete
    }

    private LungePhase currentPhase = LungePhase.Complete;
    private float phaseTimer = 0f;

    public override void OnActivate(AIBlackboard blackboard)
    {
        base.OnActivate(blackboard);

        // Start windup phase
        currentPhase = LungePhase.Windup;
        phaseTimer = settings.windupDuration;

        // Stop movement during windup if configured
        if (settings.stopDuringWindup)
        {
            blackboard.SetMovementInput(Vector2.zero);
        }

        // Face the player if we can
        Transform self = blackboard.Get<Transform>("transform");
        Transform player = blackboard.Get<Transform>("player");

        if (self != null && player != null)
        {
            float directionToPlayer = Mathf.Sign(player.position.x - self.position.x);
            blackboard.Set("facing_direction", directionToPlayer);

            // Actually flip the sprite
            self.localScale = new Vector3(
                Mathf.Sign(directionToPlayer) * Mathf.Abs(self.localScale.x),
                self.localScale.y,
                self.localScale.z
            );
        }

        Debug.Log($"{behaviorName}: Starting windup phase ({settings.windupDuration}s)");
    }

    public override void OnDeactivate(AIBlackboard blackboard)
    {
        base.OnDeactivate(blackboard);

        // Clear any movement input
        blackboard.ClearMovementInput();

        Debug.Log($"{behaviorName}: Deactivated");
    }

    public override void Tick(AIBlackboard blackboard, float deltaTime)
    {
        phaseTimer -= deltaTime;

        switch (currentPhase)
        {
            case LungePhase.Windup:
                UpdateWindup(blackboard, deltaTime);
                break;

            case LungePhase.Lunging:
                UpdateLunging(blackboard, deltaTime);
                break;

            case LungePhase.Recovery:
                UpdateRecovery(blackboard, deltaTime);
                break;
        }
    }

    public override void PhysicsTick(AIBlackboard blackboard, float fixedDeltaTime)
    {
        // Physics updates if needed
    }

    private void UpdateWindup(AIBlackboard blackboard, float deltaTime)
    {
        // Windup complete?
        if (phaseTimer <= 0f)
        {
            ExecuteLunge(blackboard);
        }
    }

    private void ExecuteLunge(AIBlackboard blackboard)
    {
        CharacterCore character = blackboard.Get<CharacterCore>("character");
        if (character == null)
        {
            // Skip to recovery if no character
            StartRecovery(blackboard);
            return;
        }

        // Trigger the lunge ability
        character.RaiseEvent("lunge_pressed", null);

        // Move to lunging phase
        currentPhase = LungePhase.Lunging;

        // We don't know exactly how long the lunge will take,
        // so we'll wait for it to finish via ability events
        Debug.Log($"{behaviorName}: Triggered lunge ability");
    }

    private void UpdateLunging(AIBlackboard blackboard, float deltaTime)
    {
        // Check if lunge is still active
        CharacterCore character = blackboard.Get<CharacterCore>("character");
        if (character == null)
        {
            StartRecovery(blackboard);
            return;
        }

        CharacterAbilities abilities = character.GetCharacterComponent<CharacterAbilities>();
        if (abilities == null || !abilities.IsLunging())
        {
            // Lunge has finished, start recovery
            StartRecovery(blackboard);
        }
    }

    private void StartRecovery(AIBlackboard blackboard)
    {
        currentPhase = LungePhase.Recovery;
        phaseTimer = settings.recoveryDuration;

        // Clear movement input during recovery
        blackboard.SetMovementInput(Vector2.zero);

        // Start a timer so conditions can check when recovery is done
        blackboard.StartTimer(behaviorName + "_Timer", settings.recoveryDuration);

        Debug.Log($"{behaviorName}: Starting recovery phase ({settings.recoveryDuration}s)");
    }

    private void UpdateRecovery(AIBlackboard blackboard, float deltaTime)
    {
        if (phaseTimer <= 0f)
        {
            currentPhase = LungePhase.Complete;
            Debug.Log($"{behaviorName}: Recovery complete");
        }
    }

    // Optional: Call this from EnemyAIController when it receives ability events
    public void OnAbilityEvent(string eventType)
    {
        if (currentPhase == LungePhase.Lunging && eventType == "ability_ended")
        {
            // The ability ended, but we might already know this from UpdateLunging
            // This is just an alternative way to detect it
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (string.IsNullOrEmpty(behaviorName))
            behaviorName = name;
    }
}