using System.Net.Http.Json;
using GuildArena.Application.Abstractions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuildArena.Infrastructure.Services.Email;

/// <summary>
/// Implements the <see cref="IEmailDispatcher"/> interface using the Brevo v3 REST API.
/// </summary>
public class BrevoEmailDispatcher : IEmailDispatcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailOptions _emailOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<BrevoEmailDispatcher> _logger;

    public BrevoEmailDispatcher(
        IHttpClientFactory httpClientFactory,
        IOptions<EmailOptions> emailOptions,
        IHostEnvironment environment,
        ILogger<BrevoEmailDispatcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _emailOptions = emailOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(string toEmail, string subject, string htmlContent)
    {
        var payload = new
        {
            sender = new { name = _emailOptions.SenderName, email = _emailOptions.SenderEmail },
            to = new[] { new { email = toEmail } },
            subject,
            htmlContent
        };

        var client = _httpClientFactory.CreateClient("BrevoClient");
        try
        {
            using var response = await client.PostAsJsonAsync("smtp/email", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("Brevo email dispatch unavailable in Development ({StatusCode}). Details: {ErrorDetails}",
                        response.StatusCode, errorBody);
                    return;
                }

                _logger.LogError("Brevo email dispatch failed with status code {StatusCode}. Details: {ErrorDetails}",
                    response.StatusCode, errorBody);

                throw new HttpRequestException($"Failed to dispatch email via Brevo. Status: {response.StatusCode}");
            }

            _logger.LogInformation("Email successfully dispatched to {RecipientEmail}.", toEmail);
        }
        catch (Exception ex) when (_environment.IsDevelopment() && ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Brevo email dispatch unavailable in Development.");
        }
    }
}
