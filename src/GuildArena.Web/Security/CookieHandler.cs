using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace GuildArena.Web.Security;

/// <summary>
/// A DelegatingHandler that ensures the browser sends the authentication cookies 
/// with every HTTP request made by the HttpClient.
/// </summary>
public class CookieHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Força a inclusão de credenciais (Cookies) em chamadas cross-origin/same-origin
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        return base.SendAsync(request, cancellationToken);
    }
}