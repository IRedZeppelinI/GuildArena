using Microsoft.Extensions.Configuration;

namespace GuildArena.Web.Services;

public class AssetService : IAssetService 
{
    private readonly string _baseAssetUrl;

    public AssetService(IConfiguration config)
    {
        _baseAssetUrl = config["AssetBaseUrl"] ?? "";
    }

    public string GetBackgroundUrl(string backgroundId) => 
        $"{_baseAssetUrl}images/backgrounds/{backgroundId.ToLower()}.jpg";
    public string GetPortraitUrl(string definitionId) => 
        $"{_baseAssetUrl}images/portraits/{definitionId.ToLower()}.png";
    public string GetAbilityIconUrl(string abilityId) => 
        $"{_baseAssetUrl}images/abilities/{abilityId.ToLower()}.png";
    public string GetModifierIconUrl(string modifierId) => 
        $"{_baseAssetUrl}images/modifiers/{modifierId.ToLower()}.png";
}