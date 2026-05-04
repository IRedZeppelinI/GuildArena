//using GuildArena.Web;
//using GuildArena.Web.Services;
//using GuildArena.Web.State;
//using Microsoft.AspNetCore.Components.Web;
//using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

//var builder = WebAssemblyHostBuilder.CreateDefault(args);
//builder.RootComponents.Add<App>("#app");
//builder.RootComponents.Add<HeadOutlet>("head::after");

//var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
//if (string.IsNullOrEmpty(apiBaseUrl))
//{
//    throw new InvalidOperationException("ApiBaseUrl is missing in configuration.");
//}

////builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

//// Forma correta e robusta
//builder.Services.AddSingleton<IAssetService, AssetService>();

//builder.Services.AddScoped<ICombatStateService, CombatStateService>();

//await builder.Build().RunAsync();


using GuildArena.Web;
using GuildArena.Web.Security;
using GuildArena.Web.Services;
using GuildArena.Web.State;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrEmpty(apiBaseUrl))
{
    throw new InvalidOperationException("ApiBaseUrl is missing in configuration.");
}

// 1. Register the CookieHandler
builder.Services.AddTransient<CookieHandler>();

// 2. Configure the main HttpClient to use the CookieHandler
builder.Services.AddHttpClient("ApiClient", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<CookieHandler>();

// 3. Register a scoped HttpClient that resolves to the pre-configured "ApiClient"
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiClient"));

// 4. Authentication State wiring
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();

// 5. App Services
builder.Services.AddSingleton<IAssetService, AssetService>();
builder.Services.AddScoped<ICombatStateService, CombatStateService>();

await builder.Build().RunAsync();
