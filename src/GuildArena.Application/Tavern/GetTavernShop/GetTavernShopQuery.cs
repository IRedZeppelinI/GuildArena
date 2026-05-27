using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Shop;
using MediatR;

namespace GuildArena.Application.Tavern.GetTavernShop;

public class GetTavernShopQuery : IRequest<Result<TavernShopDto>>
{
    // Sem propriedades – a guild é obtida do ICurrentUserService no handler.
}