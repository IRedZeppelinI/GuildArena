using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Shared.DTOs.Combat;
using GuildArena.Web.Pages.Arena.Components;
using Xunit;

namespace GuildArena.Web.UnitTests;

public class ModifierPresentationTests
{
    [Fact]
    public void CatalogNameTakesPrecedenceAndOlderPayloadsHaveReadableFallback()
    {
        var modifier = new ActiveModifierDto { DefinitionId = "MOD_GARRET_TRAIT", Name = "Slayer's Instinct" };
        Assert.Equal("Slayer's Instinct", ModifierPresentation.Name(modifier));
        modifier.Name = null;
        Assert.Equal("Garret Trait", ModifierPresentation.Name(modifier));
    }

    [Fact]
    public void DisplayPriorityUsesStatusSnapshotNotNameAndDoesNotMutateServerOrder()
    {
        var passive = new ActiveModifierDto { DefinitionId = "MOD_TRAIT", TurnsRemaining = -1 };
        var temporary = new ActiveModifierDto { DefinitionId = "MOD_STUN_IN_NAME_ONLY", TurnsRemaining = 2 };
        var restriction = new ActiveModifierDto { DefinitionId = "MOD_X", TurnsRemaining = -1, ActiveStatusEffects = [StatusEffectType.Silence] };
        var defensive = new ActiveModifierDto { DefinitionId = "MOD_DEF", TurnsRemaining = 1, ActiveStatusEffects = [StatusEffectType.Invulnerable] };
        var serverOrder = new[] { passive, temporary, restriction, defensive };

        Assert.Equal(new[] { restriction, temporary, defensive, passive }, ModifierPresentation.Order(serverOrder));
        Assert.Equal(new[] { passive, temporary, restriction, defensive }, serverOrder);
        Assert.False(ModifierPresentation.IsRestriction(temporary));
        Assert.False(ModifierPresentation.IsRestriction(defensive));
    }

    [Theory]
    [InlineData(-1, "Permanent", "∞")]
    [InlineData(0, "0 turns", "0t")]
    [InlineData(1, "1 turn", "1t")]
    [InlineData(2, "2 turns", "2t")]
    public void DurationRetainsZeroAndDistinguishesPermanent(int turns, string full, string compact)
    {
        Assert.Equal(full, ModifierPresentation.Duration(turns));
        Assert.Equal(compact, ModifierPresentation.CompactDuration(turns));
    }
}
