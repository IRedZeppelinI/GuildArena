namespace GuildArena.Domain.Results;

/// <summary>
/// Defines the category of the error, which helps the Presentation layer 
/// map the domain error to the correct HTTP Status Code without the Domain 
/// knowing anything about HTTP.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// General business rule violation (Maps to HTTP 400 Bad Request).
    /// </summary>
    Failure = 0,

    /// <summary>
    /// Input validation error (Maps to HTTP 400 Bad Request).
    /// </summary>
    Validation = 1,

    /// <summary>
    /// Resource not found (Maps to HTTP 404 Not Found).
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// State conflict, like double-spending or concurrency issues 
    /// (Maps to HTTP 409 Conflict).
    /// </summary>
    Conflict = 3,

    /// <summary>
    /// User is not authenticated (Maps to HTTP 401 Unauthorized).
    /// </summary>
    Unauthorized = 4,

    /// <summary>
    /// User is authenticated but lacks permission to perform the action 
    /// (Maps to HTTP 403 Forbidden).
    /// </summary>
    Forbidden = 5
}