using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

public class TargetResolutionService : ITargetResolutionService
{
    private readonly ILogger<TargetResolutionService> _logger;
    private readonly IModifierDefinitionRepository _modifierRepo; // ver se tem IsUntargetable
    private readonly Random _random = new();

    public TargetResolutionService(
        ILogger<TargetResolutionService> logger,
        IModifierDefinitionRepository modifierRepo)
    {
        _logger = logger;
        _modifierRepo = modifierRepo;
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
        if (rule.Type == TargetType.Enemy || rule.Type == TargetType.AllEnemies)
        {
            // Remove quem tem IsUntargetable
            potentialTargets = potentialTargets.Where(c => !IsCombatantUntargetable(c)).ToList();
        }

        // 4. Seleção Final (Manual vs Automática)
        return ApplySelectionStrategy(rule, potentialTargets, playerInput);
    }

    // --- Helpers ---

    private bool IsCombatantUntargetable(Combatant target)
    {
        var defs = _modifierRepo.GetAllDefinitions();
        foreach (var mod in target.ActiveModifiers)
        {
            if (defs.TryGetValue(mod.DefinitionId, out var def))
            {
                if (def.IsUntargetable) return true;
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

    private List<Combatant> ApplySelectionStrategy(
        TargetingRule rule,
        List<Combatant> candidates,
        AbilityTargets playerInput)
    {
        // Se for AoE, devolve todos os candidatos válidos
        if (IsAreaEffect(rule.Type))
        {
            return candidates;
        }

        switch (rule.Strategy)
        {
            case TargetSelectionStrategy.Manual:
                if (playerInput.SelectedTargets.TryGetValue(rule.RuleId, out var selectedIds))
                {
                    // Interseção: Só aceita IDs que estejam na lista de candidatos válidos
                    return candidates.Where(c => selectedIds.Contains(c.Id)).Take(rule.Count).ToList();
                }
                _logger.LogWarning("No manual selection provided for rule {RuleId}", rule.RuleId);
                return new List<Combatant>();

            case TargetSelectionStrategy.Random:
                return candidates.OrderBy(x => _random.Next()).Take(rule.Count).ToList();

            //TODO: Rever tie-break
            case TargetSelectionStrategy.LowestHP:
                return candidates
                    .OrderBy(c => c.CurrentHP)
                    .ThenBy(c => c.Id) // TIE-BREAK (Determinismo)
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.HighestHP:
                return candidates
                    .OrderByDescending(c => c.CurrentHP)
                    .ThenBy(c => c.Id) // TIE-BREAK
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.LowestHPPercent:
                return candidates
                    .OrderBy(c => (float)c.CurrentHP / c.MaxHP)
                    .ThenBy(c => c.Id) // TIE-BREAK
                    .Take(rule.Count)
                    .ToList();

            case TargetSelectionStrategy.HighestHPPercent:
                return candidates
                    .OrderByDescending(c => (float)c.CurrentHP / c.MaxHP)
                    .ThenBy(c => c.Id) // TIE-BREAK
                    .Take(rule.Count)
                    .ToList();

            default:                
                return candidates.Take(rule.Count).ToList();
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