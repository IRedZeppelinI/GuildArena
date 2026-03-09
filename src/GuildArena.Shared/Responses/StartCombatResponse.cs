using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Shared.Responses;

public class StartCombatResponse
{
    public required string CombatId { get; set; }
    public required List<string> InitialLogs { get; set; }

    public required GameStateDto InitialState { get; set; }
}