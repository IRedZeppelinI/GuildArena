using System.Net;
using System.Net.Http.Json;
using GuildArena.Shared.DTOs.Identity;
using GuildArena.Web.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace GuildArena.Web.UnitTests.Security;

public class CookieAuthenticationStateProviderTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SignedOutResponse_DoesNotReportConnectionFailure(HttpStatusCode statusCode)
    {
        var provider = CreateProvider(_ => Task.FromResult(new HttpResponseMessage(statusCode)));

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.ShouldBe(false);
        provider.IsCheckUnavailable.ShouldBe(false);
    }

    [Fact]
    public async Task FailedCheck_CanRecoverWithoutTreatingItAsSignedOut()
    {
        var attempt = 0;
        var provider = CreateProvider(_ => Task.FromResult(++attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new UserInfoDto { Id = "player-1", Email = "player@example.test", Roles = ["Admin"] })
            }));

        var failedState = await provider.GetAuthenticationStateAsync();
        failedState.User.Identity?.IsAuthenticated.ShouldBe(false);
        provider.IsCheckUnavailable.ShouldBe(true);

        var recoveredState = await provider.GetAuthenticationStateAsync();
        recoveredState.User.Identity?.IsAuthenticated.ShouldBe(true);
        recoveredState.User.IsInRole("Admin").ShouldBe(true);
        provider.IsCheckUnavailable.ShouldBe(false);
    }

    [Fact]
    public async Task NetworkFailure_ReportsConnectionFailure()
    {
        var provider = CreateProvider(_ => throw new HttpRequestException("Server unreachable"));

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.ShouldBe(false);
        provider.IsCheckUnavailable.ShouldBe(true);
    }

    private static CookieAuthenticationStateProvider CreateProvider(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var client = new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("https://localhost/") };
        return new CookieAuthenticationStateProvider(client, NullLogger<CookieAuthenticationStateProvider>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
