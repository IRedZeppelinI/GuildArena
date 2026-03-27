namespace GuildArena.Web.Services;

/// <summary>
/// Defines the contract for resolving UI asset URLs (images, icons, backgrounds).
/// </summary>
public interface IAssetService
{
    string GetBackgroundUrl(string backgroundId);
    string GetPortraitUrl(string definitionId);
    string GetAbilityIconUrl(string abilityId);
    string GetModifierIconUrl(string modifierId);
    string GetEssenceIconUrl(string essenceType);
}