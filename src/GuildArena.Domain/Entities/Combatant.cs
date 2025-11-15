using GuildArena.Domain.Definitions;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Entities;

public class Combatant
{
    public int Id { get; set; } // Pode ser o ID do Hero, ou um ID de Mob
    public int OwnerId { get; set; } // ID do Player (ou 0 para "Mundo/AI")
    public required string Name { get; set; }
    public int CurrentHP { get; set; }

    // Os stats finais já calculados (Nível + Equipamento)
    public required BaseStats CalculatedStats { get; set; }

    // A "receita" do ataque básico que este combatente usa
    public AbilityDefinition? BasicAttack { get; set; }

    public List<ActiveCooldown> ActiveCooldowns { get; set; } = new();
    public List<ActiveModifier> ActiveModifiers { get; set; } = new();
}
