namespace GuildArena.Infrastructure.Options;

/// <summary>
/// Provides configuration settings specific to the Brevo API integration.
/// </summary>
public class BrevoOptions
{
    /// <summary>
    /// Gets or sets the API key required for Brevo authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}