using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Dungeons;
using MediatR;

namespace GuildArena.Application.Dungeons.GetAvailableDungeons;

/// <summary>
/// Returns all dungeons available in the game.
/// </summary>
public class GetAvailableDungeonsQuery : IRequest<Result<List<DungeonSummaryDto>>>
{
}