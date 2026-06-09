using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Quests;
using MediatR;

namespace GuildArena.Application.Quests.GetActiveQuests;

/// <summary>
/// Retrieves all active (and recently completed) quests for the authenticated user's guild.
/// </summary>
public class GetActiveQuestsQuery : IRequest<Result<List<QuestDto>>>
{
}