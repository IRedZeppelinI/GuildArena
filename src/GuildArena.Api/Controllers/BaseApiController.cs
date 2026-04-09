using GuildArena.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers;

/// <summary>
/// Base controller providing standard mechanisms to translate domain Results 
/// into standardized HTTP responses (RFC 7807 ProblemDetails).
/// </summary>
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Translates a void Result object into an appropriate ActionResult 
    /// based on its success status and ErrorType.
    /// </summary>
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }

        return CreateProblemDetails(result.Error);
    }

    /// <summary>
    /// Translates a typed Result object into an appropriate ActionResult.
    /// Returns the value wrapped in a 200 OK if successful.
    /// </summary>
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return CreateProblemDetails(result.Error);
    }

    /// <summary>
    /// Maps the internal Error format to the standard ProblemDetails format.
    /// </summary>
    private IActionResult CreateProblemDetails(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            // Defaulting general business logic failures to 400 Bad Request
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: GetTitle(error.Type),
            detail: error.Message,
            // Injecting custom error code into the extensions so the client 
            // can read it programmatically for i18n or UI logic
            extensions: new Dictionary<string, object?>
            {
                { "code", error.Code }
            }
        );
    }

    private static string GetTitle(ErrorType type) => type switch
    {
        ErrorType.Validation => "Bad Request",
        ErrorType.NotFound => "Not Found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        _ => "Bad Request"
    };
}