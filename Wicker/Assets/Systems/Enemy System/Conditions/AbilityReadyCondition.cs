using UnityEngine;

[CreateAssetMenu(fileName = "AbilityReadyCondition", menuName = "AI/Conditions/AbilityReady")]
public class AbilityReadyCondition : AICondition
{
    [Header("Ability Settings")]
    public string abilityName = "lunge"; // Could be "lunge", "dash", etc.

    [Header("Additional Requirements")]
    public bool requireGrounded = true;
    public float minDistance = 1f;
    public float maxDistance = 10f;

    public override bool Evaluate(AIBlackboard blackboard)
    {
        CharacterCore character = blackboard.Get<CharacterCore>("character");
        if (character == null)
            return false;

        // Get CharacterAbilities component
        CharacterAbilities abilities = character.GetCharacterComponent<CharacterAbilities>();
        if (abilities == null)
            return false;

        // Check if ability is ready
        bool abilityReady = abilities.CanUseAbility(abilityName);

        // Check additional requirements
        if (!abilityReady)
            return false;

        // Check grounded requirement
        if (requireGrounded)
        {
            CharacterMovement movement = character.GetCharacterComponent<CharacterMovement>();
            if (movement != null)
            {
                // We need to check if grounded - you might need to add a public method
                // For now, we'll use velocity as approximation
                if (Mathf.Abs(movement.GetVerticalVelocity()) > 0.1f)
                    return false;
            }
        }

        // Check distance requirement
        Transform player = blackboard.Get<Transform>("player");
        Transform self = blackboard.Get<Transform>("transform");

        if (player != null && self != null)
        {
            float distance = Vector2.Distance(self.position, player.position);
            if (distance < minDistance || distance > maxDistance)
                return false;
        }

        return true;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (string.IsNullOrEmpty(conditionName))
            conditionName = name;
    }
}