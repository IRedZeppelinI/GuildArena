using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Shop;
using MediatR;

namespace GuildArena.Application.Tavern.GetTavernHero;

public class GetTavernHeroQuery : IRequest<Result<TavernHeroDto>>
{
    public required string DefinitionId { get; init; }
}