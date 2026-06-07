using GuildArena.Application.Combat.StartCombat; // For StartCombatResult
using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Dungeons.EnterDungeonStage;

/// <summary>
/// Initiates the combat for the current stage of the active dungeon run.
/// </summary>
public class EnterDungeonStageCommand : IRequest<Result<StartCombatResult>>
{
    // Uses current user/guild; no extra properties.
}