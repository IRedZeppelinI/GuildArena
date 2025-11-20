using MediatR;

namespace GuildArena.Application.Combat.StartCombat;

// Retorna o CombatId (guid) gerado
public class StartCombatCommand : IRequest<string>
{
    // O ID do jogador principal (que iniciou o combate)
    public int PlayerId { get; set; }

    // Opcional: O ID do oponente (se for PvP) ou 0 se for AI
    public int OpponentId { get; set; } = 0;
    // (No futuro receberia o ID dos jogadores, combatants selecionados, etc.)
}