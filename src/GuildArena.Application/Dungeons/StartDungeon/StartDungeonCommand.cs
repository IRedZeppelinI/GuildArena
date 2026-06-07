using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Dungeons.StartDungeon;

/// <summary>
/// Command to initialise a new dungeon run.
/// </summary>
public class StartDungeonCommand : IRequest<Result>
{
    /// <summary>
    /// The static dungeon definition identifier (e.g. "DUNGEON_SHADOW_MAW").
    /// </summary>
    public required string DungeonId { get; init; }

    /// <summary>
    /// The IDs of the three heroes selected by the player.
    /// </summary>
    public required List<int> HeroInstanceIds { get; init; }
}