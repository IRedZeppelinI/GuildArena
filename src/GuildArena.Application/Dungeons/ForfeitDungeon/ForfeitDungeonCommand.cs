using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Dungeons.ForfeitDungeon;

/// <summary>
/// Deletes the active dungeon run for the current guild (forfeits the run).
/// </summary>
public class ForfeitDungeonCommand : IRequest<Result>
{
    // No parameters – uses current user/guild
}