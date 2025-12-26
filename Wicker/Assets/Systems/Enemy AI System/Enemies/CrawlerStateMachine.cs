using System.Collections.Generic;
using UnityEngine;

public class CrawlerStateMachine : AIStateMachine
{
    [Header("Behavior Settings")]
    public PatrolBehavior.Settings patrolSettings = new PatrolBehavior.Settings();

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

    // Behavior instances
    private PatrolBehavior patrolBehavior;
    private IdleBehavior idleBehavior;

    // Condition instances
    private TimerExpiredCondition patrolTimer;
    private TimerExpiredCondition idleTimer;

    protected override StateMachineData GetStateMachineData()
    {
        // Create behaviors
        patrolBehavior = new PatrolBehavior { settings = patrolSettings };
        idleBehavior = new IdleBehavior();

        // Create conditions
        patrolTimer = new TimerExpiredCondition(patrolTimerSettings);
        idleTimer = new TimerExpiredCondition(idleTimerSettings);

        // Start initial timer
        patrolTimer.StartTimer(blackboard);

        return new StateMachineData
        {
            initialState = patrolBehavior,
            logTransitions = true,
            transitions = new List<Transition>
            {
                // Patrol -> Idle (when patrol timer expires)
                new Transition
                {
                    fromStates = new List<AIBehavior> { patrolBehavior },
                    toState = idleBehavior,
                    conditions = new List<AICondition> { patrolTimer },
                    priority = 0
                },
                
                // Idle -> Patrol (when idle timer expires)
                new Transition
                {
                    fromStates = new List<AIBehavior> { idleBehavior },
                    toState = patrolBehavior,
                    conditions = new List<AICondition> { idleTimer },
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
    }
}