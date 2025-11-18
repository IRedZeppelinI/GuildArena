using MediatR;

namespace GuildArena.Application.Combat.EndTurn;

/// <summary>
/// Represents the user's intent to end their current turn in a specific combat.
/// </summary>
public class EndTurnCommand : IRequest 
{
    /// <summary>
    /// The ID (GUID) of the combat where the turn is being ended.
    /// </summary>
    public required string CombatId { get; set; }

    //TODO: Adicionar ID do Jogador para validação de segurança
}