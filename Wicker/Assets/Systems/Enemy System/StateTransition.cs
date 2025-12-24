using System;
using UnityEngine;

[Serializable]
public class StateTransition
{
    public int fromBehaviorIndex = 0;
    public int toBehaviorIndex = 0;
    public int conditionIndex = 0;
    public int priority = 0;

    // For editor use only - not serialized
    [NonSerialized] public AIBehavior fromBehavior;
    [NonSerialized] public AIBehavior toBehavior;
    [NonSerialized] public AICondition condition;
}