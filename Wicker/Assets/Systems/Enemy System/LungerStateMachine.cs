using System.Collections.Generic;
using UnityEngine;

public class LungerStateMachine : AIStateMachine
{
    [Header("Behavior Settings")]
    public PatrolBehavior.Settings patrolSettings = new PatrolBehavior.Settings();
    public IdleBehavior.Settings idleSettings = new IdleBehavior.Settings();
    public ChaseBehavior.Settings chaseSettings = new ChaseBehavior.Settings();
    public LungeBehavior.Settings lungeSettings = new LungeBehavior.Settings();

    [Header("Condition Settings")]
    public PlayerDistanceCondition.Settings chaseStartSettings = new PlayerDistanceCondition.Settings
    {
        comparison = PlayerDistanceCondition.ComparisonType.LessThan,
        distance = 15f
    };

    public PlayerDistanceCondition.Settings chaseEndSettings = new PlayerDistanceCondition.Settings{
        comparison = PlayerDistanceCondition.ComparisonType.GreaterThan,
        distance = 18f
    };

    public PlayerDistanceCondition.Settings lungeRangeSettings = new PlayerDistanceCondition.Settings
    {
        comparison = PlayerDistanceCondition.ComparisonType.LessThan,
        distance = 10f
    };

    public PlayerDirectionCondition.Settings playerInViewSettings = new PlayerDirectionCondition.Settings
    {
        directionType = PlayerDirectionCondition.DirectionType.InFront,
        maxAngle = 45f  // 45 degree cone
    };

    public PlayerDirectionCondition.Settings playerInFrontSettings = new PlayerDirectionCondition.Settings
    {
        directionType = PlayerDirectionCondition.DirectionType.InFront,
        maxAngle = 10f  // 10 degree cone (more precise)
    };

    private PatrolBehavior patrolBehavior;
    private IdleBehavior idleBehavior;
    private ChaseBehavior chaseBehavior;
    private LungeBehavior lungeBehavior;

    private TimerExpiredCondition patrolTimer;
    private TimerExpiredCondition idleTimer;
    private TimerExpiredCondition lungeTimer;
    private PlayerDistanceCondition playerInChaseStartRange;    // Player < 15f away
    private PlayerDistanceCondition playerOutOfChaseEndRange;   // Player > 18f away
    private PlayerDistanceCondition playerInLungeRange;         // Player < 10f away
    private PlayerDirectionCondition playerInView;      // Player within 45 degree cone of vision
    private PlayerDirectionCondition playerInFront;     // Player within 10 degree cone in front
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

        playerInChaseStartRange = new PlayerDistanceCondition(chaseStartSettings);
        playerOutOfChaseEndRange = new PlayerDistanceCondition(chaseEndSettings);
        playerInLungeRange = new PlayerDistanceCondition(lungeRangeSettings);
        playerInView = new PlayerDirectionCondition(playerInViewSettings);
        playerInFront = new PlayerDirectionCondition(playerInFrontSettings);
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
                    conditions = new List<AICondition> { playerInChaseStartRange, playerInView },
                    priority = 1
                },
                
                // Chase -> Patrol/Idle (when player out of range)
                new Transition
                {
                    fromStates = new List<AIBehavior> { chaseBehavior },
                    toState = patrolBehavior,  // Could also go to idle if preferred
                    conditions = new List<AICondition> { playerOutOfChaseEndRange },
                    priority = 0
                },
                
                // Chase -> Lunge (when conditions met)
                new Transition
                {
                    fromStates = new List<AIBehavior> { chaseBehavior },
                    toState = lungeBehavior,
                    conditions = new List<AICondition>
                    {
                        playerInLungeRange,
                        playerInFront,
                        lungeReady
                    },
                    priority = 2
                },
                
                // Lunge -> Chase (recovery)
                new Transition
                {
                    fromStates = new List<AIBehavior> { lungeBehavior },
                    toState = chaseBehavior,
                    conditions = new List<AICondition> { lungeTimer },
                    priority = 0
                }
            }
        };
    }
}