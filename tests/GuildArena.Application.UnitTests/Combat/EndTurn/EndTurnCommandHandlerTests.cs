using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.EndTurn;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.EndTurn;

public class EndTurnCommandHandlerTests
{
    private readonly ITurnManagerService _turnManagerMock;
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly ILogger<EndTurnCommandHandler> _loggerMock;
    private readonly EndTurnCommandHandler _handler;

    public EndTurnCommandHandlerTests()
    {
        _turnManagerMock = Substitute.For<ITurnManagerService>();
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _loggerMock = Substitute.For<ILogger<EndTurnCommandHandler>>();

        _handler = new EndTurnCommandHandler(
            _turnManagerMock,
            _combatRepoMock,
            _loggerMock
        );
    }

    [Fact]
    public async Task Handle_ShouldAdvanceTurn_WhenCombatExists()
    {
        // ARRANGE
        var combatId = Guid.NewGuid().ToString();
        var command = new EndTurnCommand { CombatId = combatId };

        // Mock do Estado existente
        var gameState = new GameState { CurrentTurnNumber = 1 };
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // 1. Deve ter chamado o serviço de domínio para avançar a lógica
        _turnManagerMock.Received(1).AdvanceTurn(gameState);

        // 2. Deve ter guardado o estado alterado no repositório
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenCombatNotFound()
    {
        // ARRANGE
        var combatId = "INVALID_ID";
        var command = new EndTurnCommand { CombatId = combatId };

        // Mock retorna null (não encontrado)
        _combatRepoMock.GetAsync(combatId).Returns((GameState?)null);

        // ACT
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Não deve tentar avançar turno em nulo
        _turnManagerMock.DidNotReceiveWithAnyArgs().AdvanceTurn(default!);

        // Não deve tentar guardar nada
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
                
    }
}