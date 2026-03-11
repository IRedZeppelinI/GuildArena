using GuildArena.Domain.Enums.Resources;
using MediatR;

namespace GuildArena.Application.Combat.ExchangeEssence;

/// <summary>
/// Command to execute an essence transmutation during a combat session.
/// </summary>
public class ExchangeEssenceCommand : IRequest
{
    public required string CombatId { get; set; }
    public Dictionary<EssenceType, int> EssenceToSpend { get; set; } = new();
    public EssenceType EssenceToGain { get; set; }
}