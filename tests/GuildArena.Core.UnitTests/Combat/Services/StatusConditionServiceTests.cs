using GuildArena.Core.Combat.Enums;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.ValueObjects.State;
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class StatusConditionServiceTests
{
    private readonly StatusConditionService _service;

    public StatusConditionServiceTests()
    {
        _service = new StatusConditionService();
    }

    [Fact]
    public void CheckStatus_WithStun_ShouldReturnStunned()
    {
        var source = CreateCombatantWithStatus(StatusEffectType.Stun);
        var ability = new AbilityDefinition { Id = "A1", Name = "Any" };

        var result = _service.CheckStatusConditions(source, ability);

        result.ShouldBe(ActionStatusResult.Stunned);
    }

    [Fact]
    public void CheckStatus_WithSilence_UsingSpell_ShouldReturnSilenced()
    {
        var source = CreateCombatantWithStatus(StatusEffectType.Silence);

        // Habilidade tem a tag "Spell"
        var spell = new AbilityDefinition
        {
            Id = "Fireball",
            Name = "Fireball",
            Tags = new List<string> { "Spell", "Fire" }
        };

        var result = _service.CheckStatusConditions(source, spell);

        result.ShouldBe(ActionStatusResult.Silenced);
    }

    [Fact]
    public void CheckStatus_WithSilence_UsingPhysical_ShouldReturnAllowed()
    {
        var source = CreateCombatantWithStatus(StatusEffectType.Silence);

        // Habilidade Física (Melee) - Não deve ser afetada pelo Silence
        var slash = new AbilityDefinition
        {
            Id = "Slash",
            Name = "Slash",
            Tags = new List<string> { "Melee", "Physical" }
        };

        var result = _service.CheckStatusConditions(source, slash);

        result.ShouldBe(ActionStatusResult.Allowed);
    }

    [Fact]
    public void CheckStatus_WithDisarm_UsingMelee_ShouldReturnDisarmed()
    {
        var source = CreateCombatantWithStatus(StatusEffectType.Disarm);

        // Habilidade Melee - Deve ser bloqueada pelo Disarm
        var slash = new AbilityDefinition
        {
            Id = "Slash",
            Name = "Slash",
            Tags = new List<string> { "Melee", "Physical" }
        };

        var result = _service.CheckStatusConditions(source, slash);

        result.ShouldBe(ActionStatusResult.Disarmed);
    }

    [Fact]
    public void CheckStatus_WithDisarm_UsingRanged_ShouldReturnDisarmed()
    {
        var source = CreateCombatantWithStatus(StatusEffectType.Disarm);

        // Disarm também afeta Ranged (armas)
        var shot = new AbilityDefinition
        {
            Id = "Shot",
            Name = "Shot",
            Tags = new List<string> { "Ranged", "Physical" }
        };

        var result = _service.CheckStatusConditions(source, shot);

        result.ShouldBe(ActionStatusResult.Disarmed);
    }

    [Fact]
    public void CheckStatus_WithDisarm_UsingSpell_ShouldReturnAllowed()
    {
        var source = CreateCombatantWithStatus(StatusEffectType.Disarm);

        // Habilidade Mágica (pode ser castada mesmo sem arma)
        var shout = new AbilityDefinition
        {
            Id = "Shout",
            Name = "Shout",
            Tags = new List<string> { "Spell", "Sonic" }
        };

        var result = _service.CheckStatusConditions(source, shout);

        result.ShouldBe(ActionStatusResult.Allowed);
    }

    // Helper
    private Combatant CreateCombatantWithStatus(StatusEffectType status)
    {
        var c = new Combatant 
        { 
            Id = 1,
            Name = "Test",
            RaceId = "X",
            BaseStats = new(),
            MaxHP = 100,
            CurrentHP = 100
        };

        c.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "TEST_MOD",
            ActiveStatusEffects = new List<StatusEffectType> { status }
        });
        return c;
    }
}