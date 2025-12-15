using GuildArena.Domain.Definitions;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Entities;

public class Combatant
{
    public int Id { get; set; } // Pode ser o ID do Hero, ou um ID de Mob
    public int OwnerId { get; set; } // ID do Player (ou 0 para "Mundo/AI")
    public required string Name { get; set; }

    /// <summary>
    /// The Race ID of this combatant (e.g. "RACE_HUMAN").
    /// Cached here for quick lookup during targeting and damage calculations.
    /// </summary>
    public required string RaceId { get; set; }


    /// <summary>
    /// The current level of the combatant. Used for hit chance calculations and scaling.
    /// </summary>
    public int Level { get; set; } = 1;

    public int MaxHP { get; set; }
    public int CurrentHP { get; set; }
    public bool IsAlive => CurrentHP > 0;

    // Os stats finais já calculados (Nível + Equipamento) (sem modifiers)
    public required BaseStats BaseStats { get; set; }

    // Habilidades comuns. GuardAbility e FocusAbility são exclusivas, ou uma ou outra
    public AbilityDefinition? BasicAttack { get; set; }

    /// <summary>
    /// The unique utility ability available to this combatant.
    /// <para>
    /// This slot typically holds either a <b>Guard</b> ability (Defensive, grants armor/shield) 
    /// or a <b>Focus</b> ability (Utility, regenerates essence/resources), but never both simultaneously.
    /// </para>
    /// <para>
    /// The specific behavior is determined by the ability's definition and tags.
    /// </para>
    /// </summary>
    public AbilityDefinition? SpecialAbility { get; set; }

    /// <summary>
    /// The list of active abilities/skills this combatant can use in battle.
    /// Does not include Basic Attack, Guard or Focus.
    /// </summary>
    public List<AbilityDefinition> Abilities { get; set; } = new();


    /// <summary>
    /// Tracks how many Action Points this combatant has consumed in the current turn.
    /// Must be reset to 0 at the start of the player's turn.
    /// </summary>
    public int ActionsTakenThisTurn { get; set; }

    public List<ActiveCooldown> ActiveCooldowns { get; set; } = new();
    public List<ActiveModifier> ActiveModifiers { get; set; } = new();
}
