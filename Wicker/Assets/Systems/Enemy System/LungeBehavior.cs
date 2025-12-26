using UnityEngine;

public class LungeBehavior : AIBehavior
{
    [System.Serializable]
    public class Settings
    {
        public float windupDuration = 0.3f;
        public float recoveryDuration = 1f;
        public bool stopDuringWindup = true;
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
                CheckLungeComplete(blackboard);
                break;
                
            case LungePhase.Recovery:
                if (phaseTimer <= 0f)
                    currentPhase = LungePhase.Complete;
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
        }
    }

    private void CheckLungeComplete(AIBlackboard blackboard)
    {
        CharacterCore character = blackboard.Get<CharacterCore>("character");
        if (character == null)
        {
            StartRecovery(blackboard);
            return;
        }

        CharacterAbilities abilities = character.GetCharacterComponent<CharacterAbilities>();
        if (abilities == null || !abilities.IsLunging())
        {
            StartRecovery(blackboard);
        }
    }

    private void StartRecovery(AIBlackboard blackboard)
    {
        currentPhase = LungePhase.Recovery;
        phaseTimer = settings.recoveryDuration;
        
        blackboard.SetMovementInput(Vector2.zero);
        blackboard.StartTimer("Lunge_Timer", settings.recoveryDuration);
    }

    private void UpdateFacingDirection(Transform self, float direction)
    {
        self.localScale = new Vector3(
            Mathf.Sign(direction) * Mathf.Abs(self.localScale.x),
            self.localScale.y,
            self.localScale.z
        );
    }
}