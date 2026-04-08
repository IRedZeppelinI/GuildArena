namespace GuildArena.Application.Combat.AI.BackgroundServices;

/// <summary>
/// Represents a request for the AI to take its turn.
/// Holds the necessary identifiers to reconstruct the combat state safely in a background thread.
/// </summary>
public record AiTurnRequest(string CombatId, int AiPlayerId);