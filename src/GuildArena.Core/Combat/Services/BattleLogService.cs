using GuildArena.Core.Combat.Abstractions;

namespace GuildArena.Core.Combat.Services;

public class BattleLogService : IBattleLogService
{
    private readonly List<string> _logs = new();

    public void Log(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _logs.Add(message);
        }
    }

    public List<string> GetAndClearLogs()
    {
        var copy = new List<string>(_logs);
        _logs.Clear();
        return copy;
    }
}