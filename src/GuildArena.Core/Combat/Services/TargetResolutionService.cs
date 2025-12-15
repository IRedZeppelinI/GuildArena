using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

public class TargetResolutionService : ITargetResolutionService
{
    private readonly ILogger<TargetResolutionService> _logger;
    private readonly IRandomProvider _random;

    public TargetResolutionService(ILogger<TargetResolutionService> logger, IRandomProvider random)
    {
        _logger = logger;   
        _random = random;
    }

    public List<Combatant> ResolveTargets(
        TargetingRule rule,
        Combatant source,
        GameState gameState,
        AbilityTargets playerInput)
    {
        // 1. Obter o universo de candidatos válidos (Filtragem Base)
        var potentialTargets = GetPotentialTargets(rule, source, gameState);

        // 2. Filtrar por Estado de Vida (Vivo/Morto)
        if (rule.CanTargetDead)
            potentialTargets = potentialTargets.Where(c => !c.IsAlive).ToList();
        else
            potentialTargets = potentialTargets.Where(c => c.IsAlive).ToList();

        // Filtrar Untargetable
        //  AoE ignora
        if (rule.Type == TargetType.Enemy)
        {
            // Remove quem tem IsUntargetable
            potentialTargets = potentialTargets.Where(c => !IsCombatantUntargetable(c)).ToList();
        }

        potentialTargets = FilterByRace(potentialTargets, rule);

        // 4. Seleção Final (Manual vs Automática)
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
        // Se não houver restrições, devolve a lista original
        if ((rule.RequiredRaceIds == null || rule.RequiredRaceIds.Count == 0) &&
            (rule.ExcludedRaceIds == null || rule.ExcludedRaceIds.Count == 0))
        {
            return candidates;
        }

        return candidates.Where(c =>
        {
            // 1. Verificação de Inclusão (Whitelist)
            // Se a lista existir, o alvo TEM de estar nela.
            if (rule.RequiredRaceIds != null && rule.RequiredRaceIds.Count > 0)
            {
                if (!rule.RequiredRaceIds.Contains(c.RaceId))
                    return false;
            }

            // 2. Verificação de Exclusão (Blacklist)
            // Se a lista existir, o alvo NÃO PODE estar nela.
            if (rule.ExcludedRaceIds != null && rule.ExcludedRaceIds.Count > 0)
            {
                if (rule.ExcludedRaceIds.Contains(c.RaceId))
                    return false;
            }

            return true;
        }).ToList();
    }


    private List<Combatant> ApplySelectionStrategy(
        TargetingRule rule,
        List<Combatant> potencialTargets,
        AbilityTargets playerInput)
    {
        // Se for AoE, devolve todos os candidatos válidos
        if (IsAreaEffect(rule.Type) || rule.Type == TargetType.Self)
        {
            return potencialTargets;
        }

        switch (rule.Strategy)
        {
            case TargetSelectionStrategy.Manual:
                if (playerInput.SelectedTargets.TryGetValue(rule.RuleId, out var selectedIds))
                {
                    // Interseção: Só aceita IDs que estejam na lista de candidatos válidos
                    return potencialTargets.Where(c => selectedIds.Contains(c.Id)).Take(rule.Count).ToList();
                }
                _logger.LogWarning("No manual selection provided for rule {RuleId}", rule.RuleId);
                return new List<Combatant>();

            case TargetSelectionStrategy.Random:
                return potencialTargets.OrderBy(x => _random.Next(int.MaxValue)).Take(rule.Count).ToList();

            //TODO: Rever tie-break
            case TargetSelectionStrategy.LowestHP:
                return potencialTargets
                    .OrderBy(c => c.CurrentHP)
                    .ThenBy(c => c.Id) // TIE-BREAK (Determinismo)
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.HighestHP:
                return potencialTargets
                    .OrderByDescending(c => c.CurrentHP)
                    .ThenBy(c => c.Id) // TIE-BREAK
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.LowestHPPercent:
                return potencialTargets
                    .OrderBy(c => (float)c.CurrentHP / c.MaxHP)
                    .ThenBy(c => c.Id) // TIE-BREAK
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.HighestHPPercent:
                return potencialTargets
                    .OrderByDescending(c => (float)c.CurrentHP / c.MaxHP)
                    .ThenBy(c => c.Id) // TIE-BREAK
                    .Take(rule.Count)
                    .ToList();

            default:                
                return potencialTargets.Take(rule.Count).ToList();
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