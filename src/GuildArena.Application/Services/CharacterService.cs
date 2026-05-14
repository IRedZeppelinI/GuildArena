using GuildArena.Application.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Shared.DTOs.Combat;
using GuildArena.Shared.DTOs.GuildAndHeroes;

namespace GuildArena.Application.Services;

public class CharacterService : ICharacterService
{
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly IModifierDefinitionRepository _modifierRepo; 

    public CharacterService(
        ICharacterDefinitionRepository characterRepo,
        IRaceDefinitionRepository raceRepo,
        IAbilityDefinitionRepository abilityRepo,
        IModifierDefinitionRepository modifierRepo)
    {
        _characterRepo = characterRepo;
        _raceRepo = raceRepo;
        _abilityRepo = abilityRepo;
        _modifierRepo = modifierRepo;
    }

    public Result<HeroDetailsDto> GetCharacterDetails(string definitionId)
    {
        if (!_characterRepo.TryGetDefinition(definitionId, out var charDef))
        {
            return Result.Failure<HeroDetailsDto>(new Error("Character.NotFound", "Character definition missing.", ErrorType.NotFound));
        }

        _raceRepo.TryGetDefinition(charDef.RaceId, out var raceDef);

        // Carregar a lista de todos os Modifiers em memória
        var allModifiers = _modifierRepo.GetAllDefinitions();

        // --- LÓGICA DOS TRAITS ---
        var traits = new List<TraitDto>();

        // 1. Passiva da Raça
        if (raceDef != null)
        {
            var linhasRaciais = new List<string>();

            // A. Adiciona a História/Lore
            if (!string.IsNullOrWhiteSpace(raceDef.Description))
            {
                linhasRaciais.Add(raceDef.Description);
            }

            // B. Adiciona os Bónus de Stats
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
                linhasRaciais.Add(string.Empty); // Adiciona uma linha em branco para separar
                linhasRaciais.Add($"Racial Stats: {string.Join(", ", statBonuses)}");
            }

            // C. Adiciona os Modificadores de Combate
            if (raceDef.RacialModifierIds.Any())
            {
                linhasRaciais.Add(string.Empty); // Outra linha em branco
                foreach (var modId in raceDef.RacialModifierIds)
                {
                    if (allModifiers.TryGetValue(modId, out var mod))
                    {
                        linhasRaciais.Add($"• {mod.Name}: {mod.Description}");
                    }
                }
            }

            traits.Add(new TraitDto
            {
                SourceName = raceDef.Name,
                IsRacial = true,
                Name = "Lineage",
                DescriptionLines = linhasRaciais
            });
        }

        // 2. Passiva do Herói
        if (!string.IsNullOrEmpty(charDef.TraitModifierId) && allModifiers.TryGetValue(charDef.TraitModifierId, out var traitMod))
        {
            var linhasHeroi = new List<string>();
            if (!string.IsNullOrWhiteSpace(traitMod.Description))
            {
                linhasHeroi.Add(traitMod.Description);
            }

            traits.Add(new TraitDto
            {
                SourceName = charDef.Name,
                IsRacial = false,
                Name = traitMod.Name,
                DescriptionLines = linhasHeroi
            });
        }

        // --- HABILIDADES E STATS ---
        var abilities = new List<AbilitySummaryDto>();
        void AddAbility(string? abId)
        {
            if (!string.IsNullOrEmpty(abId) && _abilityRepo.TryGetDefinition(abId, out var abDef))
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
                    Tags = abDef.Tags.ToList()
                });
            }
        }

        AddAbility(charDef.GuardAbilityId ?? charDef.FocusAbilityId);
        foreach (var abId in charDef.AbilityIds) AddAbility(abId);

        float GetStat(Func<BaseStats, float> statSelector) => statSelector(charDef.BaseStats) + (raceDef != null ? statSelector(raceDef.BonusStats) : 0);

        return new HeroDetailsDto
        {
            Id = 0,
            DefinitionId = charDef.Id,
            Name = charDef.Name,
            RaceName = raceDef?.Name ?? "Unknown Race",
            CurrentLevel = 1,
            MaxHP = (int)GetStat(s => s.MaxHP),
            Attack = (int)GetStat(s => s.Attack),
            Defense = (int)GetStat(s => s.Defense),
            Agility = (int)GetStat(s => s.Agility),
            Magic = (int)GetStat(s => s.Magic),
            MagicDefense = (int)GetStat(s => s.MagicDefense),
            Abilities = abilities,
            Traits = traits 
        };
    }
}