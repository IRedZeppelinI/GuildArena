namespace GuildArena.Domain.Entities;

public class GameState
{
    // A lista de todos os combatentes na batalha
    public List<Combatant> Combatants { get; set; } = new();

    // (No futuro: public int CurrentTurn { get; set; } etc.)
}