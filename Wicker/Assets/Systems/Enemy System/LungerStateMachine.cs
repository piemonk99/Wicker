using System.Collections.Generic;
using UnityEngine;

public class LungerStateMachine : AIStateMachine
{
    [Header("Behavior Settings")]
    public PatrolBehavior.Settings patrolSettings = new PatrolBehavior.Settings();
    public ChaseBehavior.Settings chaseSettings = new ChaseBehavior.Settings();
    public LungeBehavior.Settings lungeSettings = new LungeBehavior.Settings();

    [Header("Timer Settings")]
    public TimerExpiredCondition.Settings patrolTimerSettings = new TimerExpiredCondition.Settings
    {
        timerKey = "Patrol_Timer",
        duration = 8f,
        randomVariance = 5f
    };

    public TimerExpiredCondition.Settings idleTimerSettings = new TimerExpiredCondition.Settings
    {
        timerKey = "Idle_Timer",
        duration = 4f,
        randomVariance = 2f
    };

    public TimerExpiredCondition.Settings lungeTimerSettings = new TimerExpiredCondition.Settings
    {
        timerKey = "Lunge_Timer",
        duration = 1f,
        randomVariance = 0f
    };

    [Header("Distance Conditions")]
    public PlayerDistanceCondition.Settings chaseStartSettings = new PlayerDistanceCondition.Settings
    {
        comparison = PlayerDistanceCondition.ComparisonType.LessThan,
        distance = 25f
    };

    public PlayerDistanceCondition.Settings chaseEndSettings = new PlayerDistanceCondition.Settings
    {
        comparison = PlayerDistanceCondition.ComparisonType.GreaterThan,
        distance = 30f
    };

    public PlayerDistanceCondition.Settings lungeRangeSettings = new PlayerDistanceCondition.Settings
    {
        comparison = PlayerDistanceCondition.ComparisonType.LessThan,
        distance = 18f
    };

    [Header("Direction Conditions")]
    public PlayerDirectionCondition.Settings playerInViewSettings = new PlayerDirectionCondition.Settings
    {
        directionType = PlayerDirectionCondition.DirectionType.InFront,
        maxAngle = 45f
    };

    public PlayerDirectionCondition.Settings playerInFrontSettings = new PlayerDirectionCondition.Settings
    {
        directionType = PlayerDirectionCondition.DirectionType.InFront,
        maxAngle = 10f
    };

    private PatrolBehavior patrolBehavior;
    private IdleBehavior idleBehavior;
    private ChaseBehavior chaseBehavior;
    private LungeBehavior lungeBehavior;

    private TimerExpiredCondition patrolTimer;
    private TimerExpiredCondition idleTimer;
    private TimerExpiredCondition lungeTimer;
    private PlayerDistanceCondition playerInChaseStartRange;
    private PlayerDistanceCondition playerOutOfChaseEndRange;
    private PlayerDistanceCondition playerInLungeRange;
    private PlayerDirectionCondition playerInView;
    private PlayerDirectionCondition playerInFront;
    private AbilityReadyCondition lungeReady;

    protected override StateMachineData GetStateMachineData()
    {
        // Create behaviors
        patrolBehavior = new PatrolBehavior { settings = patrolSettings };
        idleBehavior = new IdleBehavior();
        chaseBehavior = new ChaseBehavior { settings = chaseSettings };
        lungeBehavior = new LungeBehavior { settings = lungeSettings };

        // Create conditions
        patrolTimer = new TimerExpiredCondition(patrolTimerSettings);
        idleTimer = new TimerExpiredCondition(idleTimerSettings);
        lungeTimer = new TimerExpiredCondition(lungeTimerSettings);

        playerInChaseStartRange = new PlayerDistanceCondition(chaseStartSettings);
        playerOutOfChaseEndRange = new PlayerDistanceCondition(chaseEndSettings);
        playerInLungeRange = new PlayerDistanceCondition(lungeRangeSettings);
        playerInView = new PlayerDirectionCondition(playerInViewSettings);
        playerInFront = new PlayerDirectionCondition(playerInFrontSettings);
        lungeReady = new AbilityReadyCondition("lunge");

        // Start initial timer
        patrolTimer.StartTimer(blackboard);

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
                
                // Patrol/Idle -> Chase (when player in range and in view)
                new Transition
                {
                    fromStates = new List<AIBehavior> { patrolBehavior, idleBehavior },
                    toState = chaseBehavior,
                    conditions = new List<AICondition> { playerInChaseStartRange, playerInView },
                    priority = 1
                },
                
                // Chase -> Patrol (when player out of range)
                new Transition
                {
                    fromStates = new List<AIBehavior> { chaseBehavior },
                    toState = patrolBehavior,
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

    // Override state switching to restart timers
    protected override void SwitchToState(AIBehavior newState)
    {
        base.SwitchToState(newState);

        // Restart appropriate timer when entering state
        if (newState == patrolBehavior)
        {
            patrolTimer.StartTimer(blackboard);
        }
        else if (newState == idleBehavior)
        {
            idleTimer.StartTimer(blackboard);
        }
        else if (newState == lungeBehavior)
        {
            lungeTimer.StartTimer(blackboard);
        }
    }
}