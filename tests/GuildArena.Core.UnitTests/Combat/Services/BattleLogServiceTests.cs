using GuildArena.Core.Combat.Services;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class BattleLogServiceTests
{
    [Fact]
    public void Log_ShouldAddMessage_WhenMessageIsNotEmpty()
    {
        // ARRANGE
        var service = new BattleLogService();

        // ACT
        service.Log("Test Message");
        service.Log(""); // Should be ignored
        service.Log(null!); // Should be ignored

        // ASSERT
        var logs = service.GetAndClearLogs();
        logs.Count.ShouldBe(1);
        logs.First().ShouldBe("Test Message");
    }

    [Fact]
    public void GetAndClearLogs_ShouldReturnCopy_AndClearInternalList()
    {
        // ARRANGE
        var service = new BattleLogService();
        service.Log("Message 1");
        service.Log("Message 2");

        // ACT
        var result = service.GetAndClearLogs();
        var resultAfterClear = service.GetAndClearLogs();

        // ASSERT
        result.Count.ShouldBe(2);
        resultAfterClear.ShouldBeEmpty();
    }
}