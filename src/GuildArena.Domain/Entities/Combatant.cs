using GuildArena.Domain.Definitions;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Entities;

public class Combatant
{
    public int Id { get; set; } // Pode ser o ID do Hero, ou um ID de Mob
    public int OwnerId { get; set; } // ID do Player (ou 0 para "Mundo/AI")
    public required string Name { get; set; }

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
    public AbilityDefinition? GuardAbility { get; set; }
    public AbilityDefinition? FocusAbility { get; set; }


    /// <summary>
    /// Tracks how many Action Points this combatant has consumed in the current turn.
    /// Must be reset to 0 at the start of the player's turn.
    /// </summary>
    public int ActionsTakenThisTurn { get; set; }

    public List<ActiveCooldown> ActiveCooldowns { get; set; } = new();
    public List<ActiveModifier> ActiveModifiers { get; set; } = new();
}
