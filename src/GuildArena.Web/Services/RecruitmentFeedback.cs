using System.Net.Http.Json;
using System.Text.Json;
using GuildArena.Shared.DTOs.Shop;

namespace GuildArena.Web.Services;

public static class RecruitmentFeedback
{
    public static async Task<string> GetFailureAsync(HttpResponseMessage response)
    {
        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
        {
            try
            {
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (document.RootElement.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(detail.GetString()))
                {
                    return detail.GetString()!;
                }
            }
            catch (JsonException) { }
        }

        return "Recruitment could not be completed. Please try again or refresh the page.";
    }

    public static async Task<(bool Success, string? Message)> ReadResultAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return (false, await GetFailureAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<PurchaseHeroResponse>();
        return result?.Success == true
            ? (true, null)
            : (false, result?.Message ?? "Recruitment could not be confirmed. Please try again.");
    }
}
