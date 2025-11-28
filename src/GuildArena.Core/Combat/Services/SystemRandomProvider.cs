using GuildArena.Core.Combat.Abstractions;

namespace GuildArena.Core.Combat.Services;

/// <summary>
/// Standard implementation using .NET System.Random.
/// </summary>
public class SystemRandomProvider : IRandomProvider
{    
    public double NextDouble() => Random.Shared.NextDouble();
}