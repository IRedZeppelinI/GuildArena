using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <inheritdoc />
public class TargetResolutionService : ITargetResolutionService
{
    private readonly ILogger<TargetResolutionService> _logger;
    private readonly IRandomProvider _random;

    public TargetResolutionService(ILogger<TargetResolutionService> logger, IRandomProvider random)
    {
        _logger = logger;
        _random = random;
    }

    /// <inheritdoc />
    public List<Combatant> ResolveTargets(
        TargetingRule rule,
        Combatant source,
        GameState gameState,
        AbilityTargets playerInput)
    {
        // 1. Obter universo de candidatos válidos (Filtragem Base)
        var potentialTargets = GetPotentialTargets(rule, source, gameState);

        // 2. Filtrar por Estado de Vida (Vivo/Morto)
        if (rule.CanTargetDead)
            potentialTargets = potentialTargets.Where(c => !c.IsAlive).ToList();
        else
            potentialTargets = potentialTargets.Where(c => c.IsAlive).ToList();

        // 3. Filtrar Untargetable        
        if (rule.Type == TargetType.Enemy)
        {
            potentialTargets = potentialTargets.Where(c => !IsCombatantUntargetable(c)).ToList();
        }

        // 4. Filtrar por Raça
        potentialTargets = FilterByRace(potentialTargets, rule);

        // --- 5. LÓGICA DE TAUNTED ---        
        if (rule.Type == TargetType.Enemy)
        {
            var tauntDebuff = source.ActiveModifiers
                .FirstOrDefault(m => m.ActiveStatusEffects.Contains(StatusEffectType.Taunted));

            if (tauntDebuff != null)
            {
                var forcedTargetId = tauntDebuff.CasterId;

                // Procurar quem fez o taunt)
                var forcedTarget = potentialTargets.FirstOrDefault(t => t.Id == forcedTargetId);

                if (forcedTarget != null)
                {
                    //  ele torna-se a ÚNICA opção.
                    potentialTargets = new List<Combatant> { forcedTarget };
                }
                else
                {
                    // TODO: Rever opções
                    // Edge Case: O Tanque morreu ou ficou invisível?
                    // Design Decision: O Taunt quebra e o jogador fica livre para escolher outros.
                    // O código prossegue com a lista original.
                }
            }
        }
        // -----------------------------------

        // 6. Seleção Final (Manual vs Automática)
        return ApplySelectionStrategy(rule, potentialTargets, playerInput);
    }

    // --- Helpers ---

    private bool IsCombatantUntargetable(Combatant target)
    {
        foreach (var mod in target.ActiveModifiers)
        {
            if (mod.ActiveStatusEffects.Contains(StatusEffectType.Untargetable) ||
                mod.ActiveStatusEffects.Contains(StatusEffectType.Stealth))
            {
                return true;
            }
        }
        return false;
    }

    private List<Combatant> GetPotentialTargets(TargetingRule rule, Combatant source, GameState state)
    {
        return rule.Type switch
        {
            TargetType.Self => new List<Combatant> { source },
            TargetType.Ally or TargetType.AllAllies =>
                state.Combatants.Where(c => c.OwnerId == source.OwnerId && c.Id != source.Id).ToList(),
            TargetType.Friendly or TargetType.AllFriendlies =>
                state.Combatants.Where(c => c.OwnerId == source.OwnerId).ToList(),
            TargetType.Enemy or TargetType.AllEnemies =>
                state.Combatants.Where(c => c.OwnerId != source.OwnerId).ToList(),
            TargetType.All => state.Combatants.ToList(),
            _ => new List<Combatant>()
        };
    }

    private List<Combatant> FilterByRace(List<Combatant> candidates, TargetingRule rule)
    {
        if ((rule.RequiredRaceIds == null || rule.RequiredRaceIds.Count == 0) &&
            (rule.ExcludedRaceIds == null || rule.ExcludedRaceIds.Count == 0))
        {
            return candidates;
        }

        return candidates.Where(c =>
        {
            if (rule.RequiredRaceIds != null && rule.RequiredRaceIds.Count > 0)
            {
                if (!rule.RequiredRaceIds.Contains(c.RaceId)) return false;
            }
            if (rule.ExcludedRaceIds != null && rule.ExcludedRaceIds.Count > 0)
            {
                if (rule.ExcludedRaceIds.Contains(c.RaceId)) return false;
            }
            return true;
        }).ToList();
    }

    private List<Combatant> ApplySelectionStrategy(
        TargetingRule rule,
        List<Combatant> potentialTargets,
        AbilityTargets playerInput)
    {
        if (IsAreaEffect(rule.Type) || rule.Type == TargetType.Self)
        {
            return potentialTargets;
        }

        switch (rule.Strategy)
        {
            case TargetSelectionStrategy.Manual:
                if (playerInput.SelectedTargets.TryGetValue(rule.RuleId, out var selectedIds))
                {
                    return potentialTargets.Where(c => selectedIds.Contains(c.Id)).Take(rule.Count).ToList();
                }
                _logger.LogWarning("No manual selection provided for rule {RuleId}", rule.RuleId);
                return new List<Combatant>();

            case TargetSelectionStrategy.Random:
                return potentialTargets.OrderBy(x => _random.Next(int.MaxValue))
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.LowestHP:
                return potentialTargets
                    .OrderBy(c => c.CurrentHP)
                    .ThenBy(c => c.Id)
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.HighestHP:
                return potentialTargets
                    .OrderByDescending(c => c.CurrentHP)
                    .ThenBy(c => c.Id)
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.LowestHPPercent:
                return potentialTargets
                    .OrderBy(c => (float)c.CurrentHP / c.MaxHP)
                    .ThenBy(c => c.Id)
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.HighestHPPercent:
                return potentialTargets
                    .OrderByDescending(c => (float)c.CurrentHP / c.MaxHP)
                    .ThenBy(c => c.Id).Take(rule.Count)
                    .ToList();

            default:
                return potentialTargets.Take(rule.Count).ToList();
        }
    }

    private bool IsAreaEffect(TargetType type)
    {
        return type == TargetType.AllAllies ||
               type == TargetType.AllEnemies ||
               type == TargetType.AllFriendlies ||
               type == TargetType.All;
    }
}