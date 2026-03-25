using Microsoft.Extensions.Configuration;

namespace GuildArena.Web.Services;

public class AssetService : IAssetService
{
    private readonly string _baseAssetUrl;

    public AssetService(IConfiguration config)
    {
        var url = config["AssetBaseUrl"] ?? "";

        
        if (!string.IsNullOrEmpty(url) && !url.EndsWith("/"))
        {
            url += "/";
        }

        _baseAssetUrl = url;
    }

    // backgrounds
    public string GetBackgroundUrl(string backgroundId)
        => $"{_baseAssetUrl}backgrounds/{backgroundId.ToLower()}.jpg";

    // portraits
    public string GetPortraitUrl(string definitionId)
        => $"{_baseAssetUrl}portraits/{definitionId.ToLower()}.jpg";

    // abilities
    public string GetAbilityIconUrl(string abilityId)
        => $"{_baseAssetUrl}abilities/{abilityId.ToLower()}.jpg";

    // modifiers
    public string GetModifierIconUrl(string modifierId)
        => $"{_baseAssetUrl}modifiers/{modifierId.ToLower()}.jpg";
}