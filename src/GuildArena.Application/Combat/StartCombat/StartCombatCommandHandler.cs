using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.StartCombat;

public class StartCombatCommandHandler : IRequestHandler<StartCombatCommand, string>
{
    private readonly ICombatStateRepository _combatRepo;
    private readonly IEssenceService _essenceService;
    private readonly ICombatantFactory _combatantFactory;
    private readonly ILogger<StartCombatCommandHandler> _logger;
    private readonly Random _rng = new();

    public StartCombatCommandHandler(
        ICombatStateRepository combatRepo,
        IEssenceService essenceService,
        ICombatantFactory combatantFactory,
        ILogger<StartCombatCommandHandler> logger)
    {
        _combatRepo = combatRepo;
        _essenceService = essenceService;
        _combatantFactory = combatantFactory;
        _logger = logger;
    }

    public async Task<string> Handle(StartCombatCommand request, CancellationToken cancellationToken)
    {
        var combatId = Guid.NewGuid().ToString();
        _logger.LogInformation(
            "Initializing Combat {CombatId} with {Count} participants.",
            combatId,
            request.Participants.Count);

        // 1. Instanciar Estado Vazio
        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            Combatants = new List<Combatant>(),
            Players = new List<CombatPlayer>()
        };

        // 2. Processar Participantes
        int combatantIdCounter = 1;

        foreach (var setup in request.Participants)
        {
            AddParticipantToCombat(gameState, setup, ref combatantIdCounter);
        }

        // 3. Sortear Quem Começa (Regra de Combate)        
        if (gameState.Players.Count > 0) //check para os testes
        {
            var playerIds = gameState.Players.Select(p => p.PlayerId).ToList();
            gameState.CurrentPlayerId = playerIds[_rng.Next(playerIds.Count)];

            _logger.LogInformation("Random Start: Player {PlayerId} goes first.", gameState.CurrentPlayerId);

            // 4. Inicializar Essence do primeiro jogador
            var startingPlayer = gameState.Players.First(p => p.PlayerId == gameState.CurrentPlayerId);
            _essenceService.GenerateStartOfTurnEssence(startingPlayer, 1);
        }
        else
        {
            _logger.LogWarning("Combat started with 0 players.");
        }

        // 5. Persistir
        await _combatRepo.SaveAsync(combatId, gameState);

        return combatId;
    }

    private void AddParticipantToCombat(
        GameState state,
        StartCombatCommand.Participant setup,
        ref int idCounter)
    {
        // A. Criar o Jogador (Owner)
        var player = new CombatPlayer
        {
            PlayerId = setup.PlayerId,
            Type = setup.Type,
            EssencePool = new Dictionary<EssenceType, int>(),
            MaxTotalEssence = 10,
            ActiveModifiers = new List<ActiveModifier>()
        };
        state.Players.Add(player);

        // B. Criar a Equipa (Combatants)
        foreach (var heroSetup in setup.Team)
        {
            // Nota: Sem SQL, simular a entidade HeroCharacter 
            // O ID é sequencial para este combate.
            var heroEntity = new HeroCharacter
            {
                Id = idCounter++,
                CharacterDefinitionID = heroSetup.CharacterDefinitionId,
                CurrentLevel = heroSetup.InitialLevel                
            };

            // A Factory resolve tudo (Stats, Raça, Trait) e aplica o Loadout específico
            var combatant = _combatantFactory.Create(heroEntity, setup.PlayerId, heroSetup.LoadoutModifierIds);

            state.Combatants.Add(combatant);
        }
    }
}