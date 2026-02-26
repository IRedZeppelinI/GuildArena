using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Domain.ValueObjects.State;
using Microsoft.Extensions.Logging;
using GuildArena.Domain.Gameplay;

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

    public Combatant Create(Hero hero, int ownerId, List<string>? loadoutModifierIds = null)
    {
        // 1. Carregar Definições
        if (!_charRepo.TryGetDefinition(hero.CharacterDefinitionId, out var charDef))
            throw new KeyNotFoundException($"Character '{hero.CharacterDefinitionId}' not found.");

        if (!_raceRepo.TryGetDefinition(charDef.RaceId, out var raceDef))
            throw new KeyNotFoundException($"Race '{charDef.RaceId}' not found.");

        // 2. Calcular Base Stats (Incluindo MaxHP, sem misturar Defesa)
        // A lógica de scaling é aplicada uniformemente a todos os stats.
        var finalStats = CalculateStandardStats(charDef, raceDef, hero.CurrentLevel);

        // 3. Instanciar Combatant
        // O MaxHP é simplesmente o valor que calculámos no passo anterior.
        // O sistema de stats trata do resto (modifiers passivos, buffs, etc.)
        int maxHp = (int)finalStats.MaxHP;
        if (maxHp < 1) maxHp = 1;

        var combatant = new Combatant
        {
            Id = hero.Id,
            OwnerId = ownerId,
            Name = charDef.Name,
            RaceId = charDef.RaceId,
            Level = hero.CurrentLevel,
            BaseStats = finalStats,
            MaxHP = maxHp,
            CurrentHP = maxHp,
            ActionsTakenThisTurn = 0
        };

        // 4. Configurar Habilidades      
        // Special Ability
        // Verifica se o herói tem Guard ou Focus        
        string? specialId = !string.IsNullOrEmpty(charDef.GuardAbilityId)
            ? charDef.GuardAbilityId
            : charDef.FocusAbilityId;

        combatant.SpecialAbility = ResolveAbility(specialId);


        foreach (var skillId in charDef.AbilityIds)
        {
            var ability = ResolveAbility(skillId);
            if (ability != null) combatant.Abilities.Add(ability);
        }

        
        // Modifiers raça
        foreach (var modId in raceDef.RacialModifierIds)
        {
            AddPassiveModifier(combatant, modId);
        }

        //modifier único de character
        if (!string.IsNullOrEmpty(charDef.TraitModifierId))
        {
            AddPassiveModifier(combatant, charDef.TraitModifierId);
        }

        // Loadout para combate
        if (loadoutModifierIds != null)
        {
            foreach (var modId in loadoutModifierIds)
            {
                AddPassiveModifier(combatant, modId);
            }
        }

        return combatant;
    }

    private BaseStats CalculateStandardStats(CharacterDefinition cDef, RaceDefinition rDef, int level)
    {
        // Nível 1 = Base. Scaling começa ao subir para 2.
        int levelsToScale = Math.Max(0, level - 1);

        return new BaseStats
        {
            Attack = cDef.BaseStats.Attack + rDef.BonusStats.Attack + (cDef.StatsGrowthPerLevel.Attack * levelsToScale),
            Defense = cDef.BaseStats.Defense + rDef.BonusStats.Defense + (cDef.StatsGrowthPerLevel.Defense * levelsToScale),
            Agility = cDef.BaseStats.Agility + rDef.BonusStats.Agility + (cDef.StatsGrowthPerLevel.Agility * levelsToScale),
            Magic = cDef.BaseStats.Magic + rDef.BonusStats.Magic + (cDef.StatsGrowthPerLevel.Magic * levelsToScale),
            MagicDefense = cDef.BaseStats.MagicDefense + rDef.BonusStats.MagicDefense + (cDef.StatsGrowthPerLevel.MagicDefense * levelsToScale),

            // MaxActions: Soma base char + base raça (normalmente Growth é 0)
            MaxActions = cDef.BaseStats.MaxActions + rDef.BonusStats.MaxActions,

            // MaxHP: Tratado exatamente como os outros stats.
            // CharBase + RaceBonus + (CharGrowth * Level)
            MaxHP = cDef.BaseStats.MaxHP + rDef.BonusStats.MaxHP + (cDef.StatsGrowthPerLevel.MaxHP * levelsToScale)
        };
    }

    private AbilityDefinition? ResolveAbility(string? abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return null;
        if (_abilityRepo.TryGetDefinition(abilityId, out var def)) return def;
        _logger.LogWarning("Ability ID '{Id}' not found.", abilityId);
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
                ActiveStatusEffects = def.GrantedStatusEffects.ToList()
            };
            combatant.ActiveModifiers.Add(activeMod);
        }
    }
}