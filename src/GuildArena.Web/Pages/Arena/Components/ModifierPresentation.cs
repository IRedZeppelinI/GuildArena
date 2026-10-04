using System.Globalization;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Web.Pages.Arena.Components;

/// <summary>Names and display order only; this does not decide what an effect allows a hero to do.</summary>
public static class ModifierPresentation
{
    public static string Name(ActiveModifierDto modifier)
    {
        if (!string.IsNullOrWhiteSpace(modifier.Name)) return modifier.Name.Trim();
        var id = modifier.DefinitionId;
        if (id.StartsWith("MOD_", StringComparison.OrdinalIgnoreCase)) id = id[4..];
        return string.IsNullOrWhiteSpace(id)
            ? "Unknown effect"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ').ToLowerInvariant());
    }

    public static bool IsRestriction(ActiveModifierDto modifier) => modifier.ActiveStatusEffects.Any(status => status is
        StatusEffectType.Stun or StatusEffectType.Silence or StatusEffectType.Disarm or
        StatusEffectType.Blind or StatusEffectType.Taunted or StatusEffectType.Charmed);

    public static IOrderedEnumerable<ActiveModifierDto> Order(IEnumerable<ActiveModifierDto> modifiers) =>
        modifiers.OrderBy(m => IsRestriction(m) ? 0 : m.TurnsRemaining == -1 ? 2 : 1);

    public static string Duration(int turns) => turns == -1 ? "Permanent" : $"{turns} turn{(turns == 1 ? "" : "s")}";
    public static string CompactDuration(int turns) => turns == -1 ? "∞" : $"{turns}t";
}
