using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class StatusConditionServiceTests
{
    private readonly StatusConditionService _service;

    public StatusConditionServiceTests()
    {
        _service = new StatusConditionService();
    }

    [Fact]
    public void CheckStatus_NoEffects_ShouldReturnAllowed()
    {
        var source = new Combatant { Id = 1, Name = "Clean", BaseStats = new() };
        var ability = new AbilityDefinition { Id = "A1", Name = "Test" };

        var result = _service.CheckStatusConditions(source, ability);

        result.ShouldBe(ActionStatusResult.Allowed);
    }

    [Fact]
    public void CheckStatus_WithStun_ShouldReturnStunned()
    {
        var source = new Combatant { Id = 1, Name = "Stunned", BaseStats = new() };
        AddStatus(source, StatusEffectType.Stun);

        var ability = new AbilityDefinition { Id = "A1", Name = "Test" };

        var result = _service.CheckStatusConditions(source, ability);

        result.ShouldBe(ActionStatusResult.Stunned);
    }

    [Fact]
    public void CheckStatus_WithSilence_UsingSkill_ShouldReturnSilenced()
    {
        var abilityId = "Fireball";
        var basicAtkId = "Punch";

        var source = new Combatant
        {
            Id = 1,
            Name = "Silenced",
            BaseStats = new(),
            BasicAttack = new AbilityDefinition { Id = basicAtkId, Name = "Punch" } // Configurar Basic Attack
        };
        AddStatus(source, StatusEffectType.Silence);

        var skill = new AbilityDefinition { Id = abilityId, Name = "Fireball" };

        // Ação: Tentar usar Skill
        var result = _service.CheckStatusConditions(source, skill);

        result.ShouldBe(ActionStatusResult.Silenced);
    }

    [Fact]
    public void CheckStatus_WithSilence_UsingBasicAttack_ShouldReturnAllowed()
    {
        var basicAtkId = "Punch";
        var basicAttack = new AbilityDefinition { Id = basicAtkId, Name = "Punch" };

        var source = new Combatant
        {
            Id = 1,
            Name = "Silenced",
            BaseStats = new(),
            BasicAttack = basicAttack // O Básico é o mesmo que vamos usar
        };
        AddStatus(source, StatusEffectType.Silence);

        // Ação: Tentar usar Basic Attack
        var result = _service.CheckStatusConditions(source, basicAttack);

        result.ShouldBe(ActionStatusResult.Allowed);
    }

    [Fact]
    public void CheckStatus_WithDisarm_UsingBasicAttack_ShouldReturnDisarmed()
    {
        var basicAtkId = "Sword";
        var basicAttack = new AbilityDefinition { Id = basicAtkId, Name = "Slash" };

        var source = new Combatant
        {
            Id = 1,
            Name = "Disarmed",
            BaseStats = new(),
            BasicAttack = basicAttack
        };
        AddStatus(source, StatusEffectType.Disarm);

        var result = _service.CheckStatusConditions(source, basicAttack);

        result.ShouldBe(ActionStatusResult.Disarmed);
    }

    [Fact]
    public void CheckStatus_WithDisarm_UsingSkill_ShouldReturnAllowed()
    {
        var basicAtkId = "Sword";
        var skill = new AbilityDefinition { Id = "Shout", Name = "Shout" };

        var source = new Combatant
        {
            Id = 1,
            Name = "Disarmed",
            BaseStats = new(),
            BasicAttack = new AbilityDefinition { Id = basicAtkId, Name = "Slash" }
        };
        AddStatus(source, StatusEffectType.Disarm);

        var result = _service.CheckStatusConditions(source, skill);

        result.ShouldBe(ActionStatusResult.Allowed);
    }

    // Helper para adicionar status facilmente
    private void AddStatus(Combatant combatant, StatusEffectType status)
    {
        combatant.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "TEST_MOD",
            ActiveStatusEffects = new List<StatusEffectType> { status }
        });
    }
}