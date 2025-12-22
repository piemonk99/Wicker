using System.Collections.Generic;
using UnityEngine;

public class CharacterAbilities : MonoBehaviour, ICharacterComponent
{
    [Header("Abilities")]
    public DashAbility dash = new DashAbility();
    public GrappleDashAbility grappleDash = new GrappleDashAbility();
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
        grappleDash.Initialize(core);
        lunge.Initialize(core);

        // Add enabled abilities to the list for updating
        if (dash.IsEnabled) allAbilities.Add(dash);
        if (grappleDash.IsEnabled) allAbilities.Add(grappleDash);
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
            "grappledash" => grappleDash.CanActivate(),
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
            case "grappledash":
                grappleDash.Activate();
                break;
        }
    }

    public bool IsAbilityActive(string abilityName)
    {
        return abilityName.ToLower() switch
        {
            "lunge" => lunge.IsActive,
            "dash" => dash.IsActive,
            "grappledash" => grappleDash.IsActive,
            _ => false
        };
    }

    public float GetAbilityCooldownPercent(string abilityName)
    {
        return abilityName.ToLower() switch
        {
            "lunge" => lunge.GetCooldownPercent(),
            "dash" => dash.GetCooldownPercent(),
            "grappledash" => grappleDash.GetCooldownPercent(),
            _ => 0f
        };
    }

    // Public getters for UI
    public bool CanLunge() => lunge.CanActivate();
    public bool CanDash() => dash.CanActivate();
    public bool CanGrappleDash() => grappleDash.CanActivate();

    public bool IsLunging() => lunge.IsActive;
    public bool IsDashing() => dash.IsActive;
    public bool IsGrappleDashing() => grappleDash.IsActive;
}