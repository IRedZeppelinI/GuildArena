namespace GuildArena.Shared.Results;

/// <summary>
/// Represents a domain error with a specific code and descriptive message.
/// Provides immutability and value-based equality out of the box.
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    /// <summary>
    /// Represents the absence of an error. Used internally for successful results.
    /// </summary>
    public static readonly Error None = new(
        string.Empty,
        string.Empty,
        ErrorType.Failure);

    /// <summary>
    /// Represents a generic null value error, useful when a requested 
    /// resource unexpectedly returns null.
    /// </summary>
    public static readonly Error NullValue = new(
        "Error.NullValue",
        "The specified result value is null.",
        ErrorType.Failure);
}