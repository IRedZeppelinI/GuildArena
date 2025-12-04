using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Factories;

public class CombatantFactory : ICombatantFactory
{
    private readonly ICharacterDefinitionRepository _charRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly ILogger<CombatantFactory> _logger;

    public CombatantFactory(
        ICharacterDefinitionRepository charRepo,
        IRaceDefinitionRepository raceRepo,
        IAbilityDefinitionRepository abilityRepo,
        IModifierDefinitionRepository modifierRepo,
        ILogger<CombatantFactory> logger)
    {
        _charRepo = charRepo;
        _raceRepo = raceRepo;
        _abilityRepo = abilityRepo;
        _modifierRepo = modifierRepo;
        _logger = logger;
    }

    public Combatant Create(HeroCharacter hero, int ownerId)
    {
        // 1. Carregar Definições
        if (!_charRepo.TryGetDefinition(hero.CharacterDefinitionID, out var charDef))
        {
            throw new KeyNotFoundException($"Character Definition '{hero.CharacterDefinitionID}' not found.");
        }

        if (!_raceRepo.TryGetDefinition(charDef.RaceId, out var raceDef))
        {
            throw new KeyNotFoundException($"Race Definition '{charDef.RaceId}' not found for character '{charDef.Id}'.");
        }

        // 2. Calcular Stats Finais
        var finalStats = CalculateStats(charDef, raceDef, hero.CurrentLevel);

        // 3. Calcular HP Máximo
        // Fórmula: HP Base Char + (HP Base Raça * FatorDefesa * Nível)
        float defenseMultiplier = 1.0f + (finalStats.Defense * 0.05f);
        int maxHp = charDef.BaseHP + (int)(raceDef.BaseHP * defenseMultiplier * hero.CurrentLevel);

        if (maxHp < 1) maxHp = 1;

        // 4. Instanciar Combatant
        var combatant = new Combatant
        {
            Id = hero.Id,
            OwnerId = ownerId,
            Name = charDef.Name,
            Level = hero.CurrentLevel,
            BaseStats = finalStats,
            MaxHP = maxHp,
            CurrentHP = maxHp,
            ActionsTakenThisTurn = 0
        };

        // 5. Configurar Habilidades (Loadout)

        combatant.BasicAttack = ResolveAbility(charDef.BasicAttackAbilityId);
        combatant.GuardAbility = ResolveAbility(charDef.GuardAbilityId); 
        combatant.FocusAbility = ResolveAbility(charDef.FocusAbilityId);

        // aplicar skills
        foreach (var skillId in charDef.SkillIds)
        {
            var ability = ResolveAbility(skillId);
            if (ability != null)
            {
                combatant.Abilities.Add(ability);
            }
        }

        // 6. Aplicar Modificadores Passivos (Raciais + Perks)
        foreach (var modId in raceDef.RacialModifierIds)
        {
            AddPassiveModifier(combatant, modId);
        }

        foreach (var perkId in hero.UnlockedPerkIds)
        {
            AddPassiveModifier(combatant, perkId);
        }

        return combatant;
    }

    private BaseStats CalculateStats(CharacterDefinition cDef, RaceDefinition rDef, int level)
    {
        int levelsToScale = Math.Max(0, level - 1);

        
        return new BaseStats
        {
            Attack = cDef.BaseStats.Attack + rDef.BonusStats.Attack + 
                (cDef.StatsGrowthPerLevel.Attack * levelsToScale),
            Defense = cDef.BaseStats.Defense + rDef.BonusStats.Defense + 
                (cDef.StatsGrowthPerLevel.Defense * levelsToScale),
            Agility = cDef.BaseStats.Agility + rDef.BonusStats.Agility +
                (cDef.StatsGrowthPerLevel.Agility * levelsToScale),
            Magic = cDef.BaseStats.Magic + rDef.BonusStats.Magic + 
                (cDef.StatsGrowthPerLevel.Magic * levelsToScale),
            MagicDefense = cDef.BaseStats.MagicDefense + rDef.BonusStats.MagicDefense +
                (cDef.StatsGrowthPerLevel.MagicDefense * levelsToScale),

            // MaxActions: Base do Char + Bónus da Raça
            MaxActions = cDef.BaseStats.MaxActions + rDef.BonusStats.MaxActions
        };
    }

    private AbilityDefinition? ResolveAbility(string? abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return null;
        if (_abilityRepo.TryGetDefinition(abilityId, out var def)) return def;

        _logger.LogWarning("Ability ID '{Id}' not found in repository.", abilityId);
        return null;
    }

    private void AddPassiveModifier(Combatant combatant, string modifierId)
    {
        if (_modifierRepo.GetAllDefinitions().TryGetValue(modifierId, out var def))
        {
            var activeMod = new ActiveModifier
            {
                DefinitionId = modifierId,
                CasterId = combatant.Id,
                TurnsRemaining = -1,
                CurrentBarrierValue = 0,
                // Garantir cópia da lista de status
                ActiveStatusEffects = def.GrantedStatusEffects.ToList()
            };
            combatant.ActiveModifiers.Add(activeMod);
        }
        else
        {
            _logger.LogWarning("Passive Modifier '{Id}' not found.", modifierId);
        }
    }
}