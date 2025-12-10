namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Abstraction for random number generation to enable deterministic unit testing.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a random floating-point number between 0.0 and 1.0.
    /// Used for percentage checks (Hit Chance, Critical).
    /// </summary>
    double NextDouble();

    /// <summary>
    /// Returns a non-negative random integer that is less than the specified maximum.
    /// Used for selecting items from lists (Random Target, Random Player).
    /// </summary>
    int Next(int maxValue);
}