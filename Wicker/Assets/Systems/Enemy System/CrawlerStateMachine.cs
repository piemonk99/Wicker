using System.Collections.Generic;
using UnityEngine;

public class CrawlerStateMachine : AIStateMachine
{
    [Header("Crawler Behavior Settings")]
    public PatrolBehavior.Settings patrolSettings = new PatrolBehavior.Settings();
    public IdleBehavior.Settings idleSettings = new IdleBehavior.Settings();

    // Behavior instances (will be created at runtime)
    private PatrolBehavior patrolBehavior;
    private IdleBehavior idleBehavior;

    // Condition instances
    private TimerExpiredCondition patrolTimer;
    private TimerExpiredCondition idleTimer;

    protected override StateMachineData GetStateMachineData()
    {
        // Step 1: Create behavior instances with your settings
        patrolBehavior = new PatrolBehavior
        {
            settings = patrolSettings
        };

        idleBehavior = new IdleBehavior
        {
            settings = idleSettings
        };

        // Step 2: Create condition instances
        patrolTimer = new TimerExpiredCondition("Patrol_Timer");
        idleTimer = new TimerExpiredCondition("Idle_Timer");

        // Step 3: Create and return the state machine configuration
        return new StateMachineData
        {
            initialState = patrolBehavior,  // Start with patrol
            logTransitions = true,          // Log state changes for debugging
            transitions = new List<Transition>
            {
                // Transition 1: Patrol -> Idle (when patrol timer expires)
                new Transition
                {
                    fromStates = new List<AIBehavior> { patrolBehavior },
                    toState = idleBehavior,
                    conditions = new List<AICondition> { patrolTimer },
                    priority = 0,
                    enabled = true
                },
                
                // Transition 2: Idle -> Patrol (when idle timer expires)
                new Transition
                {
                    fromStates = new List<AIBehavior> { idleBehavior },
                    toState = patrolBehavior,
                    conditions = new List<AICondition> { idleTimer },
                    priority = 0,
                    enabled = true
                }
            }
        };
    }

    // Optional: Add custom editor methods for better Inspector experience
#if UNITY_EDITOR
    void OnValidate()
    {
        // Set default values for patrol settings
        if (patrolSettings.groundLayer.value == 0)
            patrolSettings.groundLayer = LayerMask.GetMask("Ground");
        
        if (patrolSettings.minPatrolTime <= 0)
            patrolSettings.minPatrolTime = 3f;
            
        if (patrolSettings.maxPatrolTime <= patrolSettings.minPatrolTime)
            patrolSettings.maxPatrolTime = patrolSettings.minPatrolTime + 2f;
    }
#endif
}