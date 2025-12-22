using System.Collections.Generic;
using UnityEngine;

// CharacterAbilities handles all extraneous character actions outside of basic movement and attacking.
// This will include any abilities used by the player or enemies, but what abilities each can use is determined by their configs.
public class CharacterAbilities : MonoBehaviour, ICharacterComponent
{
    [Header("Abilities")]
    public DashAbility dash = new DashAbility();
    public LungeAbility lunge = new LungeAbility();

    // List to easily iterate through all abilities
    private List<CharacterAbility> allAbilities = new List<CharacterAbility>();

    private CharacterCore character;

    public void Initialize(CharacterCore core)
    {
        character = core;

        // Clear the list before adding abilities, in case this is a reload
        allAbilities.Clear();

        // Initialize all abilities (they will load their own config)
        dash.Initialize(core);
        lunge.Initialize(core);

        // Add enabled abilities to the list for updating
        if (dash.IsEnabled) allAbilities.Add(dash);
        if (lunge.IsEnabled) allAbilities.Add(lunge);

        Debug.Log($"CharacterAbilities initialized with {allAbilities.Count} enabled abilities");
    }

    public void Tick(float deltaTime)
    {
        // Update all enabled abilities
        foreach (var ability in allAbilities)
        {
            ability.Tick(deltaTime);
        }
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Physics update for all enabled abilities
        foreach (var ability in allAbilities)
        {
            ability.PhysicsTick(fixedDeltaTime);
        }
    }

    // Helper methods
    public bool CanUseAbility(string abilityName)
    {
        return abilityName.ToLower() switch
        {
            "lunge" => lunge.CanActivate(),
            "dash" => dash.CanActivate(),
            _ => false
        };
    }

    public void UseAbility(string abilityName)
    {
        switch (abilityName.ToLower())
        {
            case "lunge":
                lunge.Activate();
                break;
            case "dash":
                dash.Activate();
                break;
        }
    }

    public bool IsAbilityActive(string abilityName)
    {
        return abilityName.ToLower() switch
        {
            "lunge" => lunge.IsActive,
            "dash" => dash.IsActive,
            _ => false
        };
    }

    public float GetAbilityCooldownPercent(string abilityName)
    {
        return abilityName.ToLower() switch
        {
            "lunge" => lunge.GetCooldownPercent(),
            "dash" => dash.GetCooldownPercent(),
            _ => 0f
        };
    }

    // Public getters for UI
    public bool CanLunge() => lunge.CanActivate();
    public bool CanDash() => dash.CanActivate();

    public bool IsLunging() => lunge.IsActive;
    public bool IsDashing() => dash.IsActive;
}