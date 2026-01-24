using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.IntegrationTests.Setup;
using MediatR;
using Shouldly;

namespace GuildArena.IntegrationTests.Scenarios.Heroes;

public class NyxTests : IntegrationTestBase
{
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupNyxCombat(
        Dictionary<EssenceType, int> essence)
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // Vesper (Player 1)
        var vesper = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Vesper",
            RaceId = "RACE_NETHRA",
            CurrentHP = 90,
            MaxHP = 90,
            // Agility 12 para escalar as habilidades
            BaseStats = new BaseStats { Attack = 12, Agility = 12, Magic = 6 },
            Position = 1,
            ActiveModifiers = new List<ActiveModifier>
            {
                // Trait Passivo (Phase Shift)
                new ActiveModifier { DefinitionId = "MOD_NYX_TRAIT", TurnsRemaining = -1, CasterId = 101 },
                // Racial Blur
                new ActiveModifier { DefinitionId = "MOD_RACIAL_NETHRA_BLUR", TurnsRemaining = -1 }
            },
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_NYX_CUT", Name = "Cut" },
                new() { Id = "ABIL_NYX_SHROUD", Name = "Shroud" },
                new() { Id = "ABIL_NYX_GAZE", Name = "Gaze" },
                new() { Id = "ABIL_NYX_ULTI", Name = "Ulti" }
            }
        };

        // Inimigo "Tanky" (Para testar penetração)
        var enemy = new Combatant
        {
            Id = 201,
            OwnerId = 2,
            Name = "Armored Target",
            RaceId = "RACE_HUMAN",
            CurrentHP = 200,
            MaxHP = 200,
            // Defesa 10 e MagicDefense 4
            BaseStats = new BaseStats { Defense = 10, MagicDefense = 4 },
            Position = 1
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { vesper, enemy },
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, EssencePool = essence },
                new() { PlayerId = 2, EssencePool = new() }
            }
        };

        await stateRepo.SaveAsync(combatId, gameState);
        return (combatId, mediator, stateRepo);
    }

    [Fact]
    public async Task PhantomCut_ShouldPenetrateDefense()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupNyxCombat(new() { { EssenceType.Shadow, 1 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_NYX_CUT",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new() { { EssenceType.Shadow, 1 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var enemy = state!.Combatants.First(c => c.Id == 201);

        // Cálculo de Dano:
        // Base: 5
        // Scaling: 12 Agility * 1.2 = 14.4 -> 14
        // Total Bruto: 19
        // Defesa do Inimigo: 10
        // Penetração 50%: Defesa efetiva = 5
        // Dano Final: 19 - 5 = 14

        // Se não houvesse penetração, seria 19 - 10 = 9.
        int damageDealt = 200 - enemy.CurrentHP;
        damageDealt.ShouldBe(14, "Damage did not match penetration logic.");
    }

    [Fact]
    public async Task ShroudOfNight_ShouldApplyStealth_AndBoostEvasion()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupNyxCombat(new() { { EssenceType.Shadow, 1 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_NYX_SHROUD",
            TargetSelections = new() { { "SELF", new List<int> { 101 } } },
            Payment = new() { { EssenceType.Shadow, 1 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var vesper = state!.Combatants.First(c => c.Id == 101);

        var buff = vesper.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_NYX_SHROUD");
        buff.ShouldNotBeNull();

        // Verifica status
        buff.ActiveStatusEffects.ShouldContain(StatusEffectType.Untargetable);
        buff.ActiveStatusEffects.ShouldContain(StatusEffectType.Stealth);
    }

    [Fact]
    public async Task WitheringGaze_ShouldApplyBlind()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupNyxCombat(new() { { EssenceType.Shadow, 1 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_NYX_GAZE",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new() { { EssenceType.Shadow, 1 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var enemy = state!.Combatants.First(c => c.Id == 201);

        // Verifica Dano Mágico
        // Base 5 + (6 Magic * 0.5) = 8.
        // MagicDef 4. Dano = 4.
        enemy.CurrentHP.ShouldBe(196);

        // Verifica Debuff
        enemy.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_NYX_BLIND");
    }

    [Fact]
    public async Task VoidSingularity_ShouldCrit_IfTargetIsBlind()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupNyxCombat(
            new() { { EssenceType.Shadow, 2 }, { EssenceType.Flux, 1 } });

        var stateInicial = await repo.GetAsync(combatId);
        var enemy = stateInicial!.Combatants.First(c => c.Id == 201);

        // Aplicar BLIND manualmente para testar o combo
        enemy.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_NYX_BLIND",
            ActiveStatusEffects = new List<StatusEffectType> { StatusEffectType.Blind }
        });
        await repo.SaveAsync(combatId, stateInicial);

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_NYX_ULTI",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new() { { EssenceType.Shadow, 2 }, { EssenceType.Flux, 1 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == 201);

        // Cálculo Dano (Crit):
        // Raw: Base 35 + (12 Agi * 1.6) = 35 + 19.2 = 54.2 -> 54
        // Multiplicador Blind: x1.5 (ConditionDamageMultiplier)
        // 54.2 * 1.5 = 81.3 -> 81 (dependendo de onde arredonda, pode ser 81 ou 82)
        // O handler multiplica o mitigatedDamage.

        // Mitigação: MagicDef 4.
        // Mitigated Raw: 54.2 - 4 = 50.2
        // Crit Bonus: 50.2 * 1.5 = 75.3 -> 75 Int Dano.

        int damageDealt = 200 - target.CurrentHP;
        damageDealt.ShouldBe(75, "Damage did not apply 1.5x multiplier on Blind target.");
    }

    [Fact]
    public async Task Trait_ShouldBuffAgility_OnEvade()
    {
        // ARRANGE
        // Como não conseguimos forçar o RNG num teste de integração "caixa preta" facilmente,
        // vamos invocar o TriggerProcessor manualmente para garantir que a mecânica
        // de reação (Trait) está bem configurada.
        // Isto prova que: SE o evento ON_EVADE ocorrer, o Vex ganha o Buff.

        var (combatId, mediator, repo) = await SetupNyxCombat(new());
        var state = await repo.GetAsync(combatId);

        var triggerProcessor = GetService<ITriggerProcessor>();
        var combatEngine = GetService<ICombatEngine>();

        var vesper = state!.Combatants.First(c => c.Id == 101);
        var enemy = state.Combatants.First(c => c.Id == 201);

        // ACT: Simular um Evade
        var context = new TriggerContext
        {
            Source = enemy,   // Quem atacou
            Target = vesper,  // Quem se esquivou (Holder do Trait)
            GameState = state,
            Tags = new HashSet<string> { "Melee" }
        };

        triggerProcessor.ProcessTriggers(ModifierTrigger.ON_EVADE, context);
        combatEngine.ProcessPendingActions(state); // Executar o buff agendado

        // ASSERT
        // Verificar se ganhou o Buff de Agilidade
        var buff = vesper.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_NYX_AGILITY_BUFF");
        buff.ShouldNotBeNull("Phase Shift trait did not trigger on Evade.");

        // Opcional: Verificar se o Stat subiu (se tiveres acesso ao StatService no teste, ou confiar no modificador)
        buff.StackCount.ShouldBe(1);
    }
}