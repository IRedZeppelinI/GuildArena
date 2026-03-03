namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// The complete snapshot of the battlefield sent to clients to render the current turn.
/// Sensitive information (like hidden enemies or opponent's hands in the future) 
/// is stripped out before this object is serialized.
/// </summary>
public class GameStateDto
{
    public int CurrentTurnNumber { get; set; }
    public int CurrentPlayerId { get; set; }

    public List<CombatPlayerDto> Players { get; set; } = new();
    public List<CombatantDto> Combatants { get; set; } = new();
}