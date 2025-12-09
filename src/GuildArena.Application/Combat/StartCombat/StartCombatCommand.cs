using GuildArena.Domain.Enums;
using MediatR;

namespace GuildArena.Application.Combat.StartCombat;

/// <summary>
/// Command to initialize a new combat session based on a list of participants.
/// Supports PvE (Player vs AI) and PvP (Player vs Player).
/// </summary>
public class StartCombatCommand : IRequest<string>
{
    /// <summary>
    /// The list of participants entering the combat.
    /// <para>
    /// The Handler expects this list to be fully populated by the caller.
    /// It must contain at least one player (and their opponents) for the combat to start correctly.
    /// </para>
    /// </summary>
    public List<Participant> Participants { get; set; } = new();


    // ==========================================
    // NESTED CLASSES (DTOs)    
    // ==========================================

    /// <summary>
    /// Defines the setup for a single player within the combat initiation.
    /// </summary>
    public class Participant
    {
        public int PlayerId { get; set; }
        public CombatPlayerType Type { get; set; }
        public List<HeroSetup> Team { get; set; } = new();
    }

    /// <summary>
    /// Defines the configuration for a single hero in a team.
    /// </summary>
    public class HeroSetup
    {
        /// <summary>
        /// The ID of the Character Definition (e.g., "HERO_GARRET").
        /// </summary>
        public required string CharacterDefinitionId { get; set; }

        /// <summary>
        /// The level of the hero for this combat instance.
        /// </summary>
        public int InitialLevel { get; set; } = 1;

        /// <summary>
        /// The list of Modifier IDs selected by the player as their Loadout.
        /// </summary>
        public List<string> LoadoutModifierIds { get; set; } = new();
    }
}