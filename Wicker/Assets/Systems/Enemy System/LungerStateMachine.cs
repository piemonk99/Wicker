using System.Collections.Generic;
using UnityEngine;

public class LungerStateMachine : AIStateMachine
{
    [Header("Behavior Settings")]
    public PatrolBehavior.Settings patrolSettings = new PatrolBehavior.Settings();
    public IdleBehavior.Settings idleSettings = new IdleBehavior.Settings();
    public ChaseBehavior.Settings chaseSettings = new ChaseBehavior.Settings();
    public LungeBehavior.Settings lungeSettings = new LungeBehavior.Settings();

    private PatrolBehavior patrolBehavior;
    private IdleBehavior idleBehavior;
    private ChaseBehavior chaseBehavior;
    private LungeBehavior lungeBehavior;

    private TimerExpiredCondition patrolTimer;
    private TimerExpiredCondition idleTimer;
    private TimerExpiredCondition lungeTimer;
    private PlayerDistanceCondition playerInRange;      // Player < 8f away
    private PlayerDistanceCondition playerOutOfRange;   // Player > 10f away
    private PlayerDistanceCondition playerClose;        // Player < 3f away
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

        playerInRange = new PlayerDistanceCondition(8f, PlayerDistanceCondition.ComparisonType.LessThan);
        playerOutOfRange = new PlayerDistanceCondition(10f, PlayerDistanceCondition.ComparisonType.GreaterThan);
        playerClose = new PlayerDistanceCondition(3f, PlayerDistanceCondition.ComparisonType.LessThan);
        playerInFront = new PlayerDirectionCondition(PlayerDirectionCondition.DirectionType.InFront);
        lungeReady = new AbilityReadyCondition("lunge");

        return new StateMachineData
        {
            initialState = patrolBehavior,
            logTransitions = true,
            transitions = new List<Transition>
            {
                // Patrol <-> Idle cycle
                new Transition
                {
                    fromStates = new List<AIBehavior> { patrolBehavior },
                    toState = idleBehavior,
                    conditions = new List<AICondition> { patrolTimer },
                    priority = 0
                },
                new Transition
                {
                    fromStates = new List<AIBehavior> { idleBehavior },
                    toState = patrolBehavior,
                    conditions = new List<AICondition> { idleTimer },
                    priority = 0
                },
                
                // Patrol/Idle -> Chase (when player in range)
                new Transition
                {
                    fromStates = new List<AIBehavior> { patrolBehavior, idleBehavior },
                    toState = chaseBehavior,
                    conditions = new List<AICondition> { playerInRange },
                    priority = 1
                },
                
                // Chase -> Patrol/Idle (when player out of range)
                new Transition
                {
                    fromStates = new List<AIBehavior> { chaseBehavior },
                    toState = patrolBehavior,  // Could also go to idle if preferred
                    conditions = new List<AICondition> { playerOutOfRange },
                    priority = 0
                },
                
                // Chase -> Lunge (when conditions met)
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
                
                // Lunge -> Idle (recovery)
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