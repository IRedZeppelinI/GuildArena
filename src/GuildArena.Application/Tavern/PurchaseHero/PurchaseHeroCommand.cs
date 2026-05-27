using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Shop;
using MediatR;

namespace GuildArena.Application.Tavern.PurchaseHero;

public class PurchaseHeroCommand : IRequest<Result<PurchaseHeroResponse>>
{
    public string HeroId { get; set; } = string.Empty; // DefinitionId
}