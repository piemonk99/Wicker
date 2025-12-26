using System.Collections.Generic;
using UnityEngine;

public class LungerStateMachine : AIStateMachine
{
    [Header("Behavior Settings")]
    public PatrolBehavior.Settings patrolSettings = new PatrolBehavior.Settings();
    public IdleBehavior.Settings idleSettings = new IdleBehavior.Settings();
    public ChaseBehavior.Settings chaseSettings = new ChaseBehavior.Settings();
    public LungeBehavior.Settings lungeSettings = new LungeBehavior.Settings();
    
    // Behavior instances (created at runtime)
    private PatrolBehavior patrolBehavior;
    private IdleBehavior idleBehavior;
    private ChaseBehavior chaseBehavior;
    private LungeBehavior lungeBehavior;
    
    // Condition instances
    private TimerExpiredCondition patrolTimer;
    private TimerExpiredCondition idleTimer;
    private TimerExpiredCondition lungeTimer;
    private PlayerDistanceCondition playerInRange;
    private PlayerDistanceCondition playerClose;
    private PlayerDirectionCondition playerInFront;
    private AbilityReadyCondition lungeReady;

    protected override StateMachineData GetStateMachineData()
    {
        // Create behaviors
        patrolBehavior = new PatrolBehavior { settings = patrolSettings };
        idleBehavior = new IdleBehavior { settings = idleSettings };
        chaseBehavior = new ChaseBehavior { settings = chaseSettings };
        lungeBehavior = new LungeBehavior { settings = lungeSettings };
        
        // Create conditions
        patrolTimer = new TimerExpiredCondition("Patrol_Timer");
        idleTimer = new TimerExpiredCondition("Idle_Timer");
        lungeTimer = new TimerExpiredCondition("Lunge_Timer");
        playerInRange = new PlayerDistanceCondition(8f);
        playerClose = new PlayerDistanceCondition(3f);
        playerInFront = new PlayerDirectionCondition(PlayerDirectionCondition.DirectionType.InFront);
        lungeReady = new AbilityReadyCondition("lunge");
        
        // Create state machine data
        return new StateMachineData
        {
            initialState = patrolBehavior,
            logTransitions = true,
            transitions = new List<Transition>
            {
                // Patrol → Idle (when patrol timer expires)
                new Transition
                {
                    fromStates = new List<AIBehavior> { patrolBehavior },
                    toState = idleBehavior,
                    conditions = new List<AICondition> { patrolTimer },
                    priority = 0
                },
                
                // Idle → Patrol (when idle timer expires)
                new Transition
                {
                    fromStates = new List<AIBehavior> { idleBehavior },
                    toState = patrolBehavior,
                    conditions = new List<AICondition> { idleTimer },
                    priority = 0
                },
                
                // Patrol/Idle → Chase (when player in range)
                new Transition
                {
                    fromStates = new List<AIBehavior> { patrolBehavior, idleBehavior },
                    toState = chaseBehavior,
                    conditions = new List<AICondition> { playerInRange },
                    priority = 1
                },
                
                // Chase → Lunge (when player close, in front, and lunge ready)
                new Transition
                {
                    fromStates = new List<AIBehavior> { chaseBehavior },
                    toState = lungeBehavior,
                    conditions = new List<AICondition> 
                    { 
                        playerClose, 
                        playerInFront, 
                        lungeReady 
                    },
                    priority = 2
                },
                
                // Lunge → Idle (when lunge recovery timer expires)
                new Transition
                {
                    fromStates = new List<AIBehavior> { lungeBehavior },
                    toState = idleBehavior,
                    conditions = new List<AICondition> { lungeTimer },
                    priority = 0
                }
            }
        };
    }
}