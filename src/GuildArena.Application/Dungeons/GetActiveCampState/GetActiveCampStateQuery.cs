using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Dungeons;
using MediatR;

namespace GuildArena.Application.Dungeons.GetActiveCampState;

/// <summary>
/// Retrieves the current camp state (active dungeon run) for the authenticated user's guild.
/// </summary>
public class GetActiveCampStateQuery : IRequest<Result<ActiveDungeonCampDto>>
{
    // No properties – user is determined from ICurrentUserService
}