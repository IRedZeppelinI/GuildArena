using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Combat.Surrender;

/// <summary>
/// Command to voluntarily end the combat as a loss.
/// </summary>
public record SurrenderCommand(string CombatId) : IRequest<Result>;