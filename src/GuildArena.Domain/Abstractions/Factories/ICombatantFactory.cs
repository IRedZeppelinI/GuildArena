using GuildArena.Domain.Entities;
using GuildArena.Domain.Gameplay;

namespace GuildArena.Domain.Abstractions.Factories;

/// <summary>
/// Defines the contract for creating a battle-ready Combatant from persistent hero data.
/// Handles stat scaling, racial bonuses, and ability setup.
/// </summary>
public interface ICombatantFactory
{
    /// <summary>
    /// Creates a combatant with Race bonuses, Fixed Trait, and Player Selected Loadout.
    /// </summary>
    /// <param name="hero">The persistent hero data.</param>
    /// <param name="ownerId">The Player ID.</param>
    /// <param name="loadoutModifierIds">Optional list of modifiers selected by the player (Runes/Masteries).</param>
    /// <param name="hpOverride">
    /// If not null, overrides <see cref="Combatant.CurrentHP"/> to the given value (e.g., for dungeon carry‑over HP).
    /// If null, HP is set to the combatant's calculated <see cref="Combatant.MaxHP"/>.
    /// </param>
    Combatant Create(
        Hero hero,
        int ownerId,
        List<string>? loadoutModifierIds = null,
        int? hpOverride = null);
}