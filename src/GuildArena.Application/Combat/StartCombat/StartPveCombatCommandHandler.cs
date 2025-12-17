using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.StartCombat;

public class StartPveCombatCommandHandler : IRequestHandler<StartPveCombatCommand, string>
{
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IEncounterDefinitionRepository _encounterRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ICombatantFactory _combatantFactory;
    private readonly IEssenceService _essenceService;
    private readonly ILogger<StartPveCombatCommandHandler> _logger;
    private readonly IRandomProvider _rng;

    public StartPveCombatCommandHandler(
        ICombatStateRepository combatStateRepo,
        IPlayerRepository playerRepo,
        IEncounterDefinitionRepository encounterRepo,
        ICurrentUserService currentUser,
        ICombatantFactory combatantFactory,
        IEssenceService essenceService,
        ILogger<StartPveCombatCommandHandler> logger,
        IRandomProvider rng)
    {
        _combatStateRepo = combatStateRepo;
        _playerRepo = playerRepo;
        _encounterRepo = encounterRepo;
        _currentUser = currentUser;
        _combatantFactory = combatantFactory;
        _essenceService = essenceService;
        _logger = logger;
        _rng = rng;
    }

    public async Task<string> Handle(StartPveCombatCommand request, CancellationToken cancellationToken)
    {
        // 1. Identificar o Jogador (Segurança)
        // Se o token for inválido ou não existir, o CurrentUserService devolve null.
        var playerId = _currentUser.UserId;
        if (playerId == null)
        {
            _logger.LogWarning("StartPveCombat attempt without authentication.");
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        // Validação básica de input
        if (request.HeroInstanceIds == null || !request.HeroInstanceIds.Any())
        {
            throw new ArgumentException("You must select at least one hero to enter combat.");
        }

        _logger.LogInformation("Starting PvE Combat for Player {PlayerId} on Encounter {Encounter}",
            playerId, request.EncounterId);

        // 2. Carregar e Validar Heróis do Repositório (SQL/Persistence)
        // Pedimos apenas os heróis específicos que o utilizador indicou.
        // O repositório DEVE filtrar por OwnerId = playerId para garantir segurança.
        var playerTeamEntities = await _playerRepo.GetHeroesAsync(playerId.Value, request.HeroInstanceIds);

        // Se a contagem não bater certo, significa que o jogador pediu IDs que não lhe pertencem ou não existem.
        if (playerTeamEntities.Count != request.HeroInstanceIds.Count)
        {
            _logger.LogWarning("Player {PlayerId} requested invalid heroes. Requested: {Req}, Found: {Found}",
                playerId, request.HeroInstanceIds.Count, playerTeamEntities.Count);
            throw new InvalidOperationException("One or more selected heroes do not exist or do not belong to you.");
        }

        // 3. Carregar Dados do Encontro (JSON/Static Data)
        if (!_encounterRepo.TryGetDefinition(request.EncounterId, out var encounterDef))
        {
            throw new KeyNotFoundException($"Encounter '{request.EncounterId}' not found.");
        }

        // 4. Instanciar o GameState (Estado Volátil)
        var combatId = Guid.NewGuid().ToString();
        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            Combatants = new List<Combatant>(),
            Players = new List<CombatPlayer>()
        };

        // 5. Configurar Participante: JOGADOR (Humano)
        var humanPlayer = new CombatPlayer
        {
            PlayerId = playerId.Value,
            Type = CombatPlayerType.Human,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };
        gameState.Players.Add(humanPlayer);

        // Converter Entidades de Persistência em Combatentes de Jogo (Factory)
        int playerPosIndex = 0;
        foreach (var heroEntity in playerTeamEntities)
        {
            var combatant = _combatantFactory.Create(heroEntity, humanPlayer.PlayerId);
            combatant.Position = playerPosIndex++;
            gameState.Combatants.Add(combatant);
        }

        // 6. Configurar Participante: AI (Monstros do Encontro)
        // Por convenção, o ID do sistema/AI é 0.
        var aiPlayerId = 0;
        var aiPlayer = new CombatPlayer
        {
            PlayerId = aiPlayerId,
            Type = CombatPlayerType.AI,
            MaxTotalEssence = 10,
            EssencePool = new Dictionary<EssenceType, int>()
        };
        gameState.Players.Add(aiPlayer);

        // Gerar IDs negativos temporários para os mobs para não colidir com IDs de BD
        int mobIdCounter = -1;

        foreach (var enemyDef in encounterDef.Enemies)
        {
            // Criamos uma entidade "volátil" apenas para alimentar a Factory.
            // Os mobs não têm persistência (XP, etc), por isso criamos on-the-fly.
            var mobEntity = new HeroCharacter
            {
                Id = mobIdCounter--,
                GuildId = -1,
                CharacterDefinitionID = enemyDef.CharacterDefinitionId,
                CurrentLevel = enemyDef.Level,
                CurrentXP = 0
            };

            var mobCombatant = _combatantFactory.Create(mobEntity, aiPlayerId);

            mobCombatant.Position = enemyDef.Position;

            gameState.Combatants.Add(mobCombatant);
        }

        // 7. Inicialização de Turno (Sorteio)
        var startingPlayerId = _rng.Next(2) == 0 ? playerId.Value : aiPlayerId;
        gameState.CurrentPlayerId = startingPlayerId;

        // Regra de Handicap: O primeiro jogador recebe menos recursos (definido no serviço)
        var startingPlayer = gameState.Players.First(p => p.PlayerId == startingPlayerId);
        _essenceService.GenerateStartOfTurnEssence(startingPlayer, baseAmount: 2);

        // 8. Persistir Estado Inicial no Redis
        await _combatStateRepo.SaveAsync(combatId, gameState);

        _logger.LogInformation("Combat {CombatId} initialized successfully. Starter: {Starter}", combatId, startingPlayerId);

        return combatId;
    }
}