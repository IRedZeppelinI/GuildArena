namespace GuildArena.Application.Combat.AI;

public interface IAiTurnOrchestrator
{
    /// <summary>
    /// Executes the entire turn for the AI player in a background process.
    /// </summary>
    Task PlayTurnAsync(string combatId, int aiPlayerId);
}