using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Shared.DTOs.Combat;
using GuildArena.Shared.DTOs.GuildAndHeroes;

namespace GuildArena.Application.Services;

/// <summary>
/// Service responsible for aggregating static character definitions, racial traits, 
/// and ability details to generate comprehensive views for the UI.
/// </summary>
public class CharacterService : ICharacterService
{
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly IEffectTooltipService _tooltipService;

    public CharacterService(
        ICharacterDefinitionRepository characterRepo,
        IRaceDefinitionRepository raceRepo,
        IAbilityDefinitionRepository abilityRepo,
        IModifierDefinitionRepository modifierRepo,
        IEffectTooltipService tooltipService)
    {
        _characterRepo = characterRepo;
        _raceRepo = raceRepo;
        _abilityRepo = abilityRepo;
        _modifierRepo = modifierRepo;
        _tooltipService = tooltipService;
    }

    /// <summary>
    /// Retrieves a detailed representation of a character based on its definition ID, 
    /// calculating base attributes and generating ability tooltips.
    /// </summary>
    /// <param name="definitionId">The unique identifier of the character definition.</param>
    /// <returns>A Result containing the mapped HeroDetailsDto.</returns>
    public Result<HeroDetailsDto> GetCharacterDetails(string definitionId)
    {
        if (!_characterRepo.TryGetDefinition(definitionId, out var charDef))
        {
            return Result.Failure<HeroDetailsDto>(new Error(
                "Character.NotFound",
                "Character definition missing.",
                ErrorType.NotFound));
        }

        _raceRepo.TryGetDefinition(charDef.RaceId, out var raceDef);
        var allModifiers = _modifierRepo.GetAllDefinitions();

        var traits = ExtractPassiveTraits(charDef, raceDef, allModifiers);

        // Pre-calculate base stats including racial bonuses
        float GetStat(Func<BaseStats, float> statSelector) =>
            statSelector(charDef.BaseStats) + (raceDef != null ? statSelector(raceDef.BonusStats) : 0);

        // Construct a baseline combatant to evaluate ability outcomes without active combat context
        var baselineCombatant = new Combatant
        {
            Id = 0,
            OwnerId = 1,
            Name = charDef.Name,
            RaceId = charDef.RaceId,
            BaseStats = new BaseStats
            {
                Attack = GetStat(s => s.Attack),
                Defense = GetStat(s => s.Defense),
                Agility = GetStat(s => s.Agility),
                Magic = GetStat(s => s.Magic),
                MagicDefense = GetStat(s => s.MagicDefense),
                MaxHP = GetStat(s => s.MaxHP)
            }
        };

        // Attach innate modifiers (traits) to ensure they are factored into baseline capability calculations
        if (!string.IsNullOrEmpty(charDef.TraitModifierId))
        {
            baselineCombatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = charDef.TraitModifierId });
        }

        if (raceDef != null)
        {
            foreach (var rm in raceDef.RacialModifierIds)
            {
                baselineCombatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = rm });
            }
        }

        var abilities = ExtractAndMapAbilities(charDef, baselineCombatant);

        return new HeroDetailsDto
        {
            Id = 0,
            DefinitionId = charDef.Id,
            Name = charDef.Name,
            RaceName = raceDef?.Name ?? "Unknown Race",
            CurrentLevel = 1,
            MaxHP = (int)GetStat(s => s.MaxHP),
            MaxActions = (int)GetStat(s => s.MaxActions),
            Attack = (int)GetStat(s => s.Attack),
            Defense = (int)GetStat(s => s.Defense),
            Agility = (int)GetStat(s => s.Agility),
            Magic = (int)GetStat(s => s.Magic),
            MagicDefense = (int)GetStat(s => s.MagicDefense),
            Abilities = abilities,
            Traits = traits
        };
    }

    /// <summary>
    /// Formats racial and character-specific traits into displayable DTOs.
    /// </summary>
    private List<TraitDto> ExtractPassiveTraits(
        Domain.Definitions.CharacterDefinition charDef,
        Domain.Definitions.RaceDefinition? raceDef,
        IReadOnlyDictionary<string, Domain.Definitions.ModifierDefinition> allModifiers)
    {
        var traits = new List<TraitDto>();

        if (raceDef != null)
        {
            var racialLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(raceDef.Description))
            {
                racialLines.Add(raceDef.Description);
            }

            var statBonuses = new List<string>();
            if (raceDef.BonusStats.MaxHP != 0) statBonuses.Add($"{raceDef.BonusStats.MaxHP:+#;-#;0} Max HP");
            if (raceDef.BonusStats.MaxActions != 0) statBonuses.Add($"{raceDef.BonusStats.MaxActions:+#;-#;0} Max Actions");
            if (raceDef.BonusStats.Attack != 0) statBonuses.Add($"{raceDef.BonusStats.Attack:+#;-#;0} Attack");
            if (raceDef.BonusStats.Defense != 0) statBonuses.Add($"{raceDef.BonusStats.Defense:+#;-#;0} Defense");
            if (raceDef.BonusStats.Agility != 0) statBonuses.Add($"{raceDef.BonusStats.Agility:+#;-#;0} Agility");
            if (raceDef.BonusStats.Magic != 0) statBonuses.Add($"{raceDef.BonusStats.Magic:+#;-#;0} Magic");
            if (raceDef.BonusStats.MagicDefense != 0) statBonuses.Add($"{raceDef.BonusStats.MagicDefense:+#;-#;0} Magic Def");

            if (statBonuses.Any())
            {
                racialLines.Add(string.Empty);
                racialLines.Add($"Racial Stats: {string.Join(", ", statBonuses)}");
            }

            if (raceDef.RacialModifierIds.Any())
            {
                racialLines.Add(string.Empty);
                foreach (var modId in raceDef.RacialModifierIds)
                {
                    if (allModifiers.TryGetValue(modId, out var mod))
                    {
                        racialLines.Add($"• {mod.Name}: {mod.Description}");
                    }
                }
            }

            traits.Add(new TraitDto
            {
                SourceName = raceDef.Name,
                IsRacial = true,
                Name = "Lineage",
                DescriptionLines = racialLines
            });
        }

        if (!string.IsNullOrEmpty(charDef.TraitModifierId) && allModifiers.TryGetValue(charDef.TraitModifierId, out var traitMod))
        {
            var heroLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(traitMod.Description))
            {
                heroLines.Add(traitMod.Description);
            }

            traits.Add(new TraitDto
            {
                SourceName = charDef.Name,
                IsRacial = false,
                Name = traitMod.Name,
                DescriptionLines = heroLines
            });
        }

        return traits;
    }

    /// <summary>
    /// Maps the character's abilities and generates the expected effect outcomes based on baseline stats.
    /// </summary>
    private List<AbilitySummaryDto> ExtractAndMapAbilities(
        Domain.Definitions.CharacterDefinition charDef,
        Combatant baselineCombatant)
    {
        var abilities = new List<AbilitySummaryDto>();

        void AddAbility(string? abilityId)
        {
            if (!string.IsNullOrEmpty(abilityId) && _abilityRepo.TryGetDefinition(abilityId, out var abDef))
            {
                abilities.Add(new AbilitySummaryDto
                {
                    Id = abDef.Id,
                    Name = abDef.Name,
                    Description = abDef.Description ?? string.Empty,
                    ActionPointCost = abDef.ActionPointCost,
                    BaseCooldown = abDef.BaseCooldown,
                    HPCost = abDef.HPCost,
                    Costs = abDef.Costs.ToDictionary(c => c.Type, c => c.Amount),
                    Tags = abDef.Tags.ToList(),
                    Effects = abDef.Effects.Select(e => _tooltipService.GeneratePreview(baselineCombatant, e)).ToList()
                });
            }
        }

        AddAbility(charDef.GuardAbilityId ?? charDef.FocusAbilityId);
        foreach (var abilityId in charDef.AbilityIds)
        {
            AddAbility(abilityId);
        }

        return abilities;
    }
}