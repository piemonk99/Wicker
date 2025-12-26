using UnityEngine;

// Ability Ready Condition
public class AbilityReadyCondition : AICondition
{
    private string abilityName;

    public AbilityReadyCondition(string abilityName = "lunge")
    {
        this.abilityName = abilityName;
        conditionName = $"AbilityReady_{abilityName}";
    }

    public override bool Evaluate(AIBlackboard blackboard)
    {
        CharacterCore character = blackboard.Get<CharacterCore>("character");
        if (character == null) return false;

        CharacterAbilities abilities = character.GetCharacterComponent<CharacterAbilities>();
        return abilities != null && abilities.CanUseAbility(abilityName);
    }
}
