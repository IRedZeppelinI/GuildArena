using System.Net.Http.Json;
using System.Security.Claims;
using GuildArena.Shared.DTOs.Identity;
using Microsoft.AspNetCore.Components.Authorization;

namespace GuildArena.Web.Security;

/// <summary>
/// Queries the backend API to determine the user's authentication state.
/// </summary>
public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CookieAuthenticationStateProvider> _logger;

    public CookieAuthenticationStateProvider(HttpClient httpClient, ILogger<CookieAuthenticationStateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // Call our new secure endpoint
            var response = await _httpClient.GetAsync("api/account/me");

            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<UserInfoDto>();

                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id),
                        new Claim(ClaimTypes.Name, user.Email),
                        new Claim(ClaimTypes.Email, user.Email)
                    };

                    if (user.GuildId.HasValue)
                    {
                        claims.Add(new Claim("GuildId", user.GuildId.ToString()!));
                    }

                    // "Cookies" authentication type tells the system the user is authenticated
                    var identity = new ClaimsIdentity(claims, "Cookies");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
            }
        }
        catch (Exception ex)
        {
            // Network error or server offline. Fallback to unauthenticated.
            _logger.LogWarning(ex, "Failed to fetch user authentication state. Falling back to anonymous.");
        }

        // Return empty principal = not logged in
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Call this after successful login/logout to trigger a UI refresh globally.
    /// </summary>
    public void NotifyAuthStatusChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}