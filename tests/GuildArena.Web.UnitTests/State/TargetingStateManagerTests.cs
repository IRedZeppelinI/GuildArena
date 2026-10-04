using GuildArena.Domain.Enums.Targeting;
using GuildArena.Shared.DTOs.Combat;
using GuildArena.Web.Pages.Arena.State;
using Xunit;

namespace GuildArena.Web.UnitTests.State;

public class TargetingStateManagerTests
{
    private readonly TargetingStateManager _targeting = new();

    [Theory]
    [InlineData(TargetType.Self)]
    [InlineData(TargetType.All)]
    [InlineData(TargetType.AllAllies)]
    [InlineData(TargetType.AllEnemies)]
    [InlineData(TargetType.AllFriendlies)]
    public void ServerResolvedTargetsDoNotRequireASecondClick(TargetType type)
    {
        _targeting.StartTargeting(1, Ability(Rule("SELF_OR_AREA", type, 1, 1)));

        Assert.False(_targeting.RequiresTargetSelection);
        Assert.False(_targeting.IsValidTarget(Hero(1)));
        Assert.False(_targeting.TrySelectTarget(Hero(1), out _));
        Assert.Empty(_targeting.SelectedTargets);
    }

    [Theory]
    [InlineData(TargetType.Enemy)]
    [InlineData(TargetType.Friendly)]
    public void AManualRuleWithOneCandidateStillRequiresAnExplicitSelection(TargetType type)
    {
        _targeting.StartTargeting(1, Ability(Rule("CHOICE", type, 1, 2)));

        Assert.True(_targeting.RequiresTargetSelection);
        Assert.False(_targeting.IsValidTarget(Hero(3)));
        Assert.False(_targeting.TrySelectTarget(Hero(3), out _));
        Assert.True(_targeting.IsValidTarget(Hero(2)));
        Assert.True(_targeting.TrySelectTarget(Hero(2), out var complete));
        Assert.True(complete);
        Assert.Equal([2], _targeting.SelectedTargets["CHOICE"]);
    }

    [Fact]
    public void SelfPlusEnemyOnlyPromptsForTheEnemyAndCompletesWithoutSelfInput()
    {
        _targeting.StartTargeting(1, Ability(
            Rule("SELF", TargetType.Self, 1, 1),
            Rule("ENEMY", TargetType.Enemy, 1, 2)));

        Assert.True(_targeting.RequiresTargetSelection);
        Assert.False(_targeting.IsValidTarget(Hero(1)));
        Assert.True(_targeting.TrySelectTarget(Hero(2), out var complete));
        Assert.True(complete);
        Assert.Single(_targeting.SelectedTargets);
        Assert.Equal([2], _targeting.SelectedTargets["ENEMY"]);
    }

    [Fact]
    public void AreaAndAutomaticRulesDoNotBlockCompletionOfAManualRule()
    {
        var automatic = Rule("AUTO", TargetType.Enemy, 1, 2);
        automatic.Strategy = TargetSelectionStrategy.Random;
        _targeting.StartTargeting(1, Ability(
            Rule("AREA", TargetType.AllAllies, 99, 1), automatic,
            Rule("ENEMIES", TargetType.Enemy, 2, 2, 3)));

        Assert.True(_targeting.TrySelectTarget(Hero(2), out var complete));
        Assert.False(complete);
        Assert.False(_targeting.TrySelectTarget(Hero(2), out _));
        Assert.True(_targeting.TrySelectTarget(Hero(3), out complete));
        Assert.True(complete);
        Assert.Equal([2, 3], _targeting.SelectedTargets["ENEMIES"]);
    }

    [Fact]
    public void AutomaticSingleTargetAndNoTargetAbilitiesNeedNoManualSelection()
    {
        var rule = Rule("AUTO", TargetType.Enemy, 1, 2);
        rule.Strategy = TargetSelectionStrategy.LowestHP;
        _targeting.StartTargeting(1, Ability(rule));
        Assert.False(_targeting.RequiresTargetSelection);
        _targeting.StartTargeting(1, Ability());
        Assert.False(_targeting.RequiresTargetSelection);
        _targeting.Cancel();
        Assert.False(_targeting.IsActive);
        Assert.False(_targeting.RequiresTargetSelection);
    }

    private static CombatantDto Hero(int id) => new() { Id = id, Name = "Hero", RaceId = "RACE_HUMAN" };
    private static AbilitySummaryDto Ability(params TargetingRuleDto[] rules) => new()
    {
        Id = "ABILITY", Name = "Ability", TargetingRules = rules.ToList()
    };
    private static TargetingRuleDto Rule(string id, TargetType type, int count, params int[] candidates) => new()
    {
        RuleId = id, Type = type, Count = count, Strategy = TargetSelectionStrategy.Manual,
        ValidTargetIds = candidates.ToList()
    };
}
