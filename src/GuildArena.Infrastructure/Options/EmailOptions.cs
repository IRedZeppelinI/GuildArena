namespace GuildArena.Infrastructure.Options;

/// <summary>
/// Provides configuration settings for the application's email dispatching system.
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// Gets or sets the base URL for the frontend application. 
    /// Used to resolve and rewrite identity verification URLs.
    /// </summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name used as the email sender.
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address used as the sender.
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;
}