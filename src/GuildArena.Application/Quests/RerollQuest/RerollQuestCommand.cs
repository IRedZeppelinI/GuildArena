using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Quests.RerollQuest;

/// <summary>
/// Command to re‑roll an active, non‑completed quest for the authenticated guild.
/// </summary>
public class RerollQuestCommand : IRequest<Result>
{
    public int QuestId { get; set; }
}