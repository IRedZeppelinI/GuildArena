using GuildArena.Application.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects; // Para BaseStats
using MediatR;

namespace GuildArena.Application.Combat.StartCombat;

public class StartCombatCommandHandler : IRequestHandler<StartCombatCommand, string>
{
    private readonly ICombatStateRepository _repository;

    public StartCombatCommandHandler(ICombatStateRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> Handle(StartCombatCommand request, CancellationToken cancellationToken)
    {
        // 1. Gerar um ID único para o combate
        var combatId = Guid.NewGuid().ToString();

        // 2. Criar o Estado Inicial (Hardcoded para teste)
        var gameState = CreateInitialState();

        // 3. Guardar no Redis
        await _repository.SaveAsync(combatId, gameState);

        return combatId;
    }

    private GameState CreateInitialState()
    {
        // Jogadores
        var player = new CombatPlayer { PlayerId = 1, Type = CombatPlayerType.Human, CurrentEssence = 1, MaxEssence = 10 };
        var ai = new CombatPlayer { PlayerId = 0, Type = CombatPlayerType.AI, CurrentEssence = 1, MaxEssence = 10 };

        // Combatentes (Dummies)
        var hero = new Combatant
        {
            Id = 10,
            OwnerId = 1,
            Name = "Hero Test",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new BaseStats { Attack = 10, Defense = 5, Agility = 10, Magic = 10, MagicDefense = 5 }
        };
        var mob = new Combatant
        {
            Id = 20,
            OwnerId = 0,
            Name = "Goblin AI",
            MaxHP = 50,
            CurrentHP = 50,
            BaseStats = new BaseStats { Attack = 5, Defense = 2, Agility = 5, Magic = 0, MagicDefense = 0 }
        };

        return new GameState
        {
            Players = new List<CombatPlayer> { player, ai },
            Combatants = new List<Combatant> { hero, mob },
            CurrentTurnNumber = 1,
            CurrentPlayerId = 1 // Começa o humano
        };
    }
}