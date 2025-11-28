namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Abstraction for random number generation to enable deterministic unit testing.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.
    /// </summary>
    double NextDouble();
}