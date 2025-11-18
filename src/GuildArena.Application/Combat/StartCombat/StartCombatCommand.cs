using MediatR;

namespace GuildArena.Application.Combat.StartCombat;

// Retorna o CombatId (guid) gerado
public class StartCombatCommand : IRequest<string>
{
    // (No futuro receberia o ID dos jogadores, combatants selecionados, etc.)
}