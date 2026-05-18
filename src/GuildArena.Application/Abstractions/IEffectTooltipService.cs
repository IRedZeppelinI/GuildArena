using GuildArena.Domain.Definitions;
using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Application.Abstractions;

public interface IEffectTooltipService
{
    /// <summary>
    /// Predicts the outcome of an effect (Damage/Heal amount, or Modifier description) 
    /// based on the source's current stats and buffs, formatting it for the UI.
    /// </summary>
    AbilityEffectSummaryDto GeneratePreview(Combatant source, EffectDefinition effectDef);
}