using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Definitions;

public class ModifierDefinition
{
    public required string Id { get; set; } // "MOD_POISON_WEAK"
    public required string Name { get; set; } // "Veneno Fraco"
    public ModifierType Type { get; set; } // Enum: BUFF, DEBUFF

    // O que este modificador faz?
    // Opção A: Altera Stats passivamente?
    public BaseStats? StatAdjustments { get; set; } // Ex: { Attack: 10, Defense: -5, ... }

    // Opção B: Despoleta uma ação (ex: Dano de Veneno)?
    public ModifierTrigger Trigger { get; set; } // Enum: ON_TURN_START, ON_TAKE_DAMAGE...

    // Se despoleta, qual a "mini-habilidade" que ele executa?
    public string? TriggeredAbilityId { get; set; } // Ex: "INTERNAL_POISON_TICK"
}


