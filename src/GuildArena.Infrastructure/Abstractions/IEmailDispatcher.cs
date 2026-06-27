namespace GuildArena.Application.Abstractions;

/// <summary>
/// Defines a provider-agnostic contract for dispatching transactional emails.
/// </summary>
public interface IEmailDispatcher
{
    /// <summary>
    /// Asynchronously dispatches an email to the specified recipient.
    /// </summary>
    /// <param name="toEmail">The recipient's email address.</param>
    /// <param name="subject">The subject line of the email.</param>
    /// <param name="htmlContent">The HTML-formatted body of the email.</param>
    /// <returns>A task that represents the asynchronous dispatch operation.</returns>
    Task DispatchAsync(string toEmail, string subject, string htmlContent);
}