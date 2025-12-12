using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class ActionQueueTests
{
    private readonly ILogger<ActionQueue> _logger;
    private readonly ActionQueue _queue;

    public ActionQueueTests()
    {
        _logger = Substitute.For<ILogger<ActionQueue>>();
        _queue = new ActionQueue(_logger);
    }

    [Fact]
    public void Enqueue_ShouldAddItemToQueue()
    {
        // ARRANGE
        var action = Substitute.For<ICombatAction>();

        // ACT
        _queue.Enqueue(action);

        // ASSERT
        _queue.HasNext().ShouldBeTrue();
    }

    [Fact]
    public void Dequeue_ShouldReturnItem_AndRemoveFromQueue()
    {
        // ARRANGE
        var action = Substitute.For<ICombatAction>();
        _queue.Enqueue(action);

        // ACT
        var result = _queue.Dequeue();

        // ASSERT
        result.ShouldBe(action);
        _queue.HasNext().ShouldBeFalse();
    }

    [Fact]
    public void Dequeue_EmptyQueue_ShouldReturnNull()
    {
        // ACT
        var result = _queue.Dequeue();

        // ASSERT
        result.ShouldBeNull();
    }

    [Fact]
    public void Clear_ShouldRemoveAllItems()
    {
        // ARRANGE
        _queue.Enqueue(Substitute.For<ICombatAction>());
        _queue.Enqueue(Substitute.For<ICombatAction>());

        // ACT
        _queue.Clear();

        // ASSERT
        _queue.HasNext().ShouldBeFalse();
        _queue.Dequeue().ShouldBeNull();
    }
}