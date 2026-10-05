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

    [Fact]
    public void TheSameCombatantCanBeChosenForIndependentManualRules()
    {
        _targeting.StartTargeting(1, Ability(
            Rule("FIRST", TargetType.Enemy, 1, 2), Rule("SECOND", TargetType.Enemy, 1, 2)));

        Assert.True(_targeting.TrySelectTarget(Hero(2), out var complete));
        Assert.False(complete);
        Assert.Equal("SECOND", _targeting.PendingRule!.RuleId);
        Assert.False(_targeting.IsSelected(2));
        Assert.True(_targeting.IsValidTarget(Hero(2)));
        Assert.True(_targeting.TrySelectTarget(Hero(2), out complete));
        Assert.True(complete);
        Assert.Equal([2], _targeting.SelectedTargets["FIRST"]);
        Assert.Equal([2], _targeting.SelectedTargets["SECOND"]);
    }

    [Fact]
    public void EachRuleUsesItsOwnCandidatesInServerOrder()
    {
        _targeting.StartTargeting(1, Ability(
            Rule("ALLY", TargetType.Friendly, 1, 3), Rule("ENEMY", TargetType.Enemy, 1, 2)));
        Assert.False(_targeting.TrySelectTarget(Hero(2), out _));
        Assert.True(_targeting.TrySelectTarget(Hero(3), out var complete));
        Assert.False(complete);
        Assert.False(_targeting.TrySelectTarget(Hero(3), out _));
        Assert.True(_targeting.TrySelectTarget(Hero(2), out complete));
        Assert.True(complete);
    }

    [Fact]
    public void SnapshotRefreshPrunesUnauthorizedChoicesAndUsesNewCandidates()
    {
        _targeting.StartTargeting(1, Ability(Rule("ENEMY", TargetType.Enemy, 2, 2, 3)));
        _targeting.TrySelectTarget(Hero(2), out _);
        var current = Ability(Rule("ENEMY", TargetType.Enemy, 2, 3, 4));
        var state = Snapshot(current);
        Assert.True(_targeting.Refresh(state, 1));
        Assert.Same(current, _targeting.ActiveAbility);
        Assert.Empty(_targeting.SelectedTargets["ENEMY"]);
        Assert.False(_targeting.TrySelectTarget(Hero(2), out _));
        Assert.True(_targeting.TrySelectTarget(Hero(3), out var complete));
        Assert.False(complete);
        Assert.True(_targeting.TrySelectTarget(Hero(4), out complete));
        Assert.True(complete);
    }

    [Theory]
    [InlineData("turn")]
    [InlineData("fallen")]
    [InlineData("unaffordable")]
    [InlineData("cooldown")]
    [InlineData("removed")]
    public void SnapshotInvalidatingTheSourceCancelsPreparation(string change)
    {
        var ability = Ability(Rule("ENEMY", TargetType.Enemy, 1, 2));
        _targeting.StartTargeting(1, ability);
        var state = Snapshot(ability);
        if (change == "turn") state.CurrentPlayerId = 2;
        if (change == "fallen") state.Combatants[0].CurrentHP = 0;
        if (change == "unaffordable") ability.IsAffordable = false;
        if (change == "cooldown") ability.CurrentCooldownTurns = 1;
        if (change == "removed") state.Combatants.Clear();
        Assert.False(_targeting.Refresh(state, 1));
        Assert.False(_targeting.IsActive);
        Assert.Empty(_targeting.SelectedTargets);
    }

    [Fact]
    public void SnapshotRetainsValidChoicesButDoesNotExecuteANewlyCompletedSelection()
    {
        _targeting.StartTargeting(1, Ability(Rule("ENEMY", TargetType.Enemy, 2, 2, 3)));
        _targeting.TrySelectTarget(Hero(2), out _);
        var ability = Ability(Rule("ENEMY", TargetType.Enemy, 1, 2));
        Assert.True(_targeting.Refresh(Snapshot(ability), 1));
        Assert.Equal([2], _targeting.SelectedTargets["ENEMY"]);
        Assert.True(_targeting.IsComplete);
        Assert.False(_targeting.TrySelectTarget(Hero(2), out _));
    }

    private static GameStateDto Snapshot(AbilitySummaryDto ability) => new()
    {
        CurrentPlayerId = 1,
        Combatants = [new CombatantDto { Id = 1, OwnerId = 1, CurrentHP = 10, Name = "Source", RaceId = "RACE_HUMAN", Abilities = [ability] }, Hero(2), Hero(3), Hero(4)]
    };

    private static CombatantDto Hero(int id) => new() { Id = id, Name = "Hero", RaceId = "RACE_HUMAN" };
    private static AbilitySummaryDto Ability(params TargetingRuleDto[] rules) => new()
    {
        Id = "ABILITY", Name = "Ability", IsAffordable = true, TargetingRules = rules.ToList()
    };
    private static TargetingRuleDto Rule(string id, TargetType type, int count, params int[] candidates) => new()
    {
        RuleId = id, Type = type, Count = count, Strategy = TargetSelectionStrategy.Manual,
        ValidTargetIds = candidates.ToList()
    };
}
