using System.Net;
using GuildArena.Infrastructure.Options;
using GuildArena.Infrastructure.Services.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GuildArena.Api.UnitTests.Services;

public class BrevoEmailDispatcherTests
{
    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task RejectedDelivery_OnlyBlocksOutsideDevelopment(string environmentName, bool shouldThrow)
    {
        var dispatcher = CreateDispatcher(environmentName, new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        if (shouldThrow)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(dispatcher));
        }
        else
        {
            await SendAsync(dispatcher);
        }
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task TransportFailure_OnlyBlocksOutsideDevelopment(string environmentName, bool shouldThrow)
    {
        var dispatcher = CreateDispatcher(environmentName, new StubHandler((_, _) =>
            throw new HttpRequestException("Brevo unreachable")));

        if (shouldThrow)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(dispatcher));
        }
        else
        {
            await SendAsync(dispatcher);
        }
    }

    [Fact]
    public async Task AcceptedDelivery_CompletesInProduction()
    {
        var dispatcher = CreateDispatcher(Environments.Production, new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created))));

        await SendAsync(dispatcher);
    }

    private static Task SendAsync(BrevoEmailDispatcher dispatcher) =>
        dispatcher.DispatchAsync("recipient@example.test", "Confirmation", "<p>Test</p>");

    private static BrevoEmailDispatcher CreateDispatcher(string environmentName, HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("BrevoClient").Returns(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/")
        });
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new BrevoEmailDispatcher(factory,
            Options.Create(new EmailOptions { SenderName = "GuildArena", SenderEmail = "sender@example.test" }),
            environment, Substitute.For<ILogger<BrevoEmailDispatcher>>());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            response(request, cancellationToken);
    }
}
