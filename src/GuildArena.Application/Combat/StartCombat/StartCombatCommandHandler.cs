using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions; 
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using MediatR;

namespace GuildArena.Application.Combat.StartCombat;

public class StartCombatCommandHandler : IRequestHandler<StartCombatCommand, string>
{
    private readonly ICombatStateRepository _repository;
    private readonly IEssenceService _essenceService; 

    public StartCombatCommandHandler(
        ICombatStateRepository repository,
        IEssenceService essenceService) // Injetar o serviço
    {
        _repository = repository;
        _essenceService = essenceService;
    }

    public async Task<string> Handle(StartCombatCommand request, CancellationToken cancellationToken)
    {
        var combatId = Guid.NewGuid().ToString();

        // Passamos os IDs do request para o método de criação
        var gameState = CreateInitialState(request.PlayerId, request.OpponentId);

        // Inicializar Essence para o jogador que começa (Regra de negócio)
        // Nota: Precisamos de saber quem começa. Por defeito assumimos que é o PlayerId do request.
        gameState.CurrentPlayerId = request.PlayerId;

        var firstPlayer = gameState.Players.First(p => p.PlayerId == gameState.CurrentPlayerId);
        _essenceService.GenerateStartOfTurnEssence(firstPlayer, gameState.CurrentTurnNumber);

        await _repository.SaveAsync(combatId, gameState);

        return combatId;
    }

    private GameState CreateInitialState(int player1Id, int player2Id)
    {
        // Jogadores (Usam os IDs dinâmicos)
        var player = new CombatPlayer
        {
            PlayerId = player1Id,
            Type = CombatPlayerType.Human,
            EssencePool = new(),
            MaxTotalEssence = 10
        };

        var opponentType = (player2Id == 0) ? CombatPlayerType.AI : CombatPlayerType.Human;
        var opponent = new CombatPlayer
        {
            PlayerId = player2Id,
            Type = opponentType,
            EssencePool = new(),
            MaxTotalEssence = 10
        };

        // Combatentes (Associados aos IDs corretos)
        var hero = new Combatant
        {
            Id = 10,
            OwnerId = player1Id, // <-- Dinâmico
            Name = $"Hero of {player1Id}",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new BaseStats { Attack = 10, Defense = 5, Agility = 10, Magic = 10, MagicDefense = 5 }
        };

        var enemy = new Combatant
        {
            Id = 20,
            OwnerId = player2Id, // <-- Dinâmico
            Name = (player2Id == 0) ? "Goblin AI" : $"Opponent {player2Id}",
            MaxHP = 50,
            CurrentHP = 50,
            BaseStats = new BaseStats { Attack = 5, Defense = 2, Agility = 5, Magic = 0, MagicDefense = 0 }
        };

        return new GameState
        {
            Players = new List<CombatPlayer> { player, opponent },
            Combatants = new List<Combatant> { hero, enemy },
            CurrentTurnNumber = 1,
            CurrentPlayerId = player1Id // Começa o jogador que pediu
        };
    }
}