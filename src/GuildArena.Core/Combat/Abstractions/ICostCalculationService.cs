using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a service responsible for calculating the final resource costs of an ability.
/// </summary>
public interface ICostCalculationService
{
    /// <summary>
    /// Calculates the final essence and HP costs (the "Invoice") to execute an ability against specific targets.
    /// This includes applying caster discounts (CostModifications) and target taxes (Ward).
    /// </summary>
    FinalAbilityCosts CalculateFinalCosts(
        CombatPlayer caster,
        AbilityDefinition ability,
        List<Combatant> targets);
}