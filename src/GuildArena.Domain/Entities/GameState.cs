namespace GuildArena.Domain.Entities;

public class GameState
{
    // A lista de todos os combatentes na batalha
    public List<Combatant> Combatants { get; set; } = new();

        
    public int CurrentTurnNumber { get; set; } = 1;

    /// <summary>
    /// Current turn Player Id
    /// </summary>
    public int CurrentPlayerId { get; set; }
}