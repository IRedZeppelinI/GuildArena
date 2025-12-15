using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Infrastructure.Options;
using GuildArena.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions; // NullLogger
using Microsoft.Extensions.Options;
using Shouldly;
using System.IO;

namespace GuildArena.IntegrationTests.Data;

public class DataIntegrityTests
{
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly ICharacterDefinitionRepository _heroRepo;

    public DataIntegrityTests()
    {         
        var solutionRoot = FindSolutionRoot();
        var dataPath = Path.Combine(solutionRoot, "src", "GuildArena.Api", "Data");

        if (!Directory.Exists(dataPath))
        {
            throw new DirectoryNotFoundException(
                $"Could not find Game Data folder at: {dataPath}. Check path logic.");
        }

        // 2. Configurar as Options manualmente        
        var options = new GameDataOptions
        {
            AbsoluteRootPath = dataPath,
            RootFolder = "",            
            RacesFile = "races.json",
            // Pastas
            CharactersFolder = "Characters", 
            AbilitiesFolder = "Abilities",
            ModifiersFolder = "Modifiers"
        };

        var optionsWrapper = Options.Create(options);

        // 3. Instanciar todos os Repositórios REAIS (Infrastructure)        
        _modifierRepo = new JsonModifierDefinitionRepository(
            optionsWrapper, NullLogger<JsonModifierDefinitionRepository>.Instance);
        _abilityRepo = new JsonAbilityDefinitionRepository(
            optionsWrapper, NullLogger<JsonAbilityDefinitionRepository>.Instance);
        _raceRepo = new JsonRaceDefinitionRepository(
            optionsWrapper, NullLogger<JsonRaceDefinitionRepository>.Instance);
        _heroRepo = new JsonCharacterDefinitionRepository(
            optionsWrapper, NullLogger<JsonCharacterDefinitionRepository>.Instance);
    }

    // --- TESTES DE CARREGAMENTO BÁSICO ---

    [Fact]
    public void Load_ShouldLoadAllDefinitionsWithoutErrors()
    {
        _raceRepo.GetAllDefinitions().ShouldNotBeEmpty("Races repo is empty.");
        _heroRepo.GetAllDefinitions().ShouldNotBeEmpty("Heroes repo is empty.");
        _abilityRepo.GetAllDefinitions().ShouldNotBeEmpty("Abilities repo is empty.");
        _modifierRepo.GetAllDefinitions().ShouldNotBeEmpty("Modifiers repo is empty.");
    }

    // --- TESTES DE INTEGRIDADE RELACIONAL (HERÓIS) ---

    [Fact]
    public void Integrity_Heroes_ShouldHaveValidReferences()
    {
        var heroes = _heroRepo.GetAllDefinitions().Values;
        var races = _raceRepo.GetAllDefinitions();
        var modifiers = _modifierRepo.GetAllDefinitions();
        var abilities = _abilityRepo.GetAllDefinitions();

        var errors = new List<string>();

        foreach (var hero in heroes)
        {
            if (!races.ContainsKey(hero.RaceId))
                errors.Add($"Hero '{hero.Name}' ({hero.Id}) references unknown RaceId: '{hero.RaceId}'");

            if (!string.IsNullOrEmpty(hero.TraitModifierId))
            {
                if (!modifiers.ContainsKey(hero.TraitModifierId))
                    errors.Add($"Hero '{hero.Name}' ({hero.Id}) references unknown TraitModifierId:" +
                        $" '{hero.TraitModifierId}'");
            }

            foreach (var abilId in hero.AbilityIds)
            {
                if (!abilities.ContainsKey(abilId))
                    errors.Add($"Hero '{hero.Name}' ({hero.Id}) references unknown AbilityId:" +
                        $" '{abilId}'");
            }

            if (!abilities.ContainsKey(hero.BasicAttackAbilityId))
                errors.Add($"Hero '{hero.Name}' ({hero.Id}) references unknown BasicAttackAbilityId:" +
                    $" '{hero.BasicAttackAbilityId}'");

            if (!string.IsNullOrEmpty(hero.GuardAbilityId) && !abilities.ContainsKey(hero.GuardAbilityId))
                errors.Add($"Hero '{hero.Name}' ({hero.Id}) references unknown GuardAbilityId:" +
                    $" '{hero.GuardAbilityId}'");

            if (!string.IsNullOrEmpty(hero.FocusAbilityId) && !abilities.ContainsKey(hero.FocusAbilityId))
                errors.Add($"Hero '{hero.Name}' ({hero.Id}) references unknown FocusAbilityId: " +
                    $"'{hero.FocusAbilityId}'");
        }

        errors.ShouldBeEmpty($"Found Integrity Errors in Heroes:\n{string.Join("\n", errors)}");
    }

    // --- TESTES DE INTEGRIDADE RELACIONAL (RAÇAS) ---

    [Fact]
    public void Integrity_Races_ShouldHaveValidModifiers()
    {
        var races = _raceRepo.GetAllDefinitions().Values;
        var modifiers = _modifierRepo.GetAllDefinitions();
        var errors = new List<string>();

        foreach (var race in races)
        {
            foreach (var modId in race.RacialModifierIds)
            {
                if (!modifiers.ContainsKey(modId))
                    errors.Add($"Race '{race.Name}' ({race.Id}) references unknown RacialModifierId: '{modId}'");
            }
        }

        errors.ShouldBeEmpty($"Found Integrity Errors in Races:\n{string.Join("\n", errors)}");
    }

    // --- TESTES DE INTEGRIDADE RELACIONAL (HABILIDADES) ---

    [Fact]
    public void Integrity_Abilities_ShouldReferenceValidModifiers()
    {
        var abilities = _abilityRepo.GetAllDefinitions().Values;
        var modifiers = _modifierRepo.GetAllDefinitions();
        var errors = new List<string>();

        foreach (var ability in abilities)
        {
            foreach (var effect in ability.Effects)
            {
                if (effect.Type == EffectType.APPLY_MODIFIER)
                {
                    if (string.IsNullOrEmpty(effect.ModifierDefinitionId))
                    {
                        errors.Add($"Ability '{ability.Id}' has APPLY_MODIFIER effect with null ID.");
                        continue;
                    }

                    if (!modifiers.ContainsKey(effect.ModifierDefinitionId))
                        errors.Add($"Ability '{ability.Id}' references unknown ModifierId: '{effect.ModifierDefinitionId}'");
                }
            }
        }

        errors.ShouldBeEmpty($"Found Integrity Errors in Abilities:\n{string.Join("\n", errors)}");
    }

    // --- TESTE DE VALIDAÇÃO DE REGRAS ---

    [Fact]
    public void Integrity_ActiveAbilities_ShouldHaveValidConfiguration()
    {
        var abilities = _abilityRepo.GetAllDefinitions().Values;
        var errors = new List<string>();

        foreach (var abil in abilities)
        {
            // Validae apenas habilidades "User Facing" (prefixo ABIL_)
            // Ignore habilidades internas (INT_) ou de triggers
            if (abil.Id.StartsWith("ABIL_", StringComparison.OrdinalIgnoreCase))
            {
                if (abil.ActionPointCost < 0)
                    errors.Add($"Active Ability '{abil.Id}' has negative ActionPointCost.");

                if (abil.BaseCooldown < 0)
                    errors.Add($"Active Ability '{abil.Id}' has negative BaseCooldown.");

                // Garante que a lista de custos existe (mesmo que vazia) para evitar NRE no motor
                if (abil.Costs == null)
                    errors.Add($"Active Ability '{abil.Id}' has null Essence Costs list.");

                if (abil.TargetingRules == null || abil.TargetingRules.Count == 0)
                    errors.Add($"Active Ability '{abil.Id}' has no Targeting Rules defined.");
            }
        }

        errors.ShouldBeEmpty
            ($"Found Configuration Errors in Active Abilities:" +
            $"\n{string.Join("\n", errors)}");
    }

    // --- TESTES DE INTEGRIDADE RELACIONAL (MODIFIERS) ---

    [Fact]
    public void Integrity_Modifiers_ShouldReferenceValidTriggeredAbilities()
    {
        var modifiers = _modifierRepo.GetAllDefinitions().Values;
        var abilities = _abilityRepo.GetAllDefinitions();
        var errors = new List<string>();

        foreach (var mod in modifiers)
        {
            if (!string.IsNullOrEmpty(mod.TriggeredAbilityId))
            {
                if (!abilities.ContainsKey(mod.TriggeredAbilityId))
                    errors.Add($"Modifier '{mod.Id}' triggers unknown AbilityId: '{mod.TriggeredAbilityId}'");
            }
        }

        errors.ShouldBeEmpty($"Found Integrity Errors in Modifiers:\n{string.Join("\n", errors)}");
    }

    // --- HELPER ---
    private string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new DirectoryNotFoundException("Could not locate the Solution root (looking for *.sln file). Ensure you are running tests within the project structure.");
        }

        return directory.FullName;
    }
}