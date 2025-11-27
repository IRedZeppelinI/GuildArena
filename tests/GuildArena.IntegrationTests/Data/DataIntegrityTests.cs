using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums;
using GuildArena.Infrastructure.Options;
using GuildArena.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging.Abstractions; // NullLogger
using Microsoft.Extensions.Options;
using Shouldly;

namespace GuildArena.IntegrationTests.Data;

public class DataIntegrityTests
{
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;

    public DataIntegrityTests()
    {
        // 1. Encontrar o caminho real da pasta 'Data' no projeto API        
        var solutionRoot = FindSolutionRoot();
        var dataPath = Path.Combine(solutionRoot, "src", "GuildArena.Api", "Data");

        if (!Directory.Exists(dataPath))
        {
            throw new DirectoryNotFoundException($"Could not find Game Data folder at: {dataPath}. Check path logic.");
        }

        // 2. Configurar as Options manualmente (Simulando o appsettings.json)
        var options = new GameDataOptions
        {
            AbsoluteRootPath = dataPath,
            RootFolder = "", // Já estamos dentro da pasta Data
            ModifiersFile = "modifiers.json",
            AbilitiesFolder = "Abilities"
        };

        var optionsWrapper = Options.Create(options);
        var loggerMod = NullLogger<JsonModifierDefinitionRepository>.Instance;
        var loggerAbil = NullLogger<JsonAbilityDefinitionRepository>.Instance;

        // 3. Instanciar os Repositórios REAIS (Infrastructure)
        // Isto vai efetivamente ler os ficheiros do disco. Se o JSON estiver mal formatado, rebenta aqui.
        _modifierRepo = new JsonModifierDefinitionRepository(optionsWrapper, loggerMod);
        _abilityRepo = new JsonAbilityDefinitionRepository(optionsWrapper, loggerAbil);
    }

    // --- TESTES DE INTEGRIDADE ---

    [Fact]
    public void Modifiers_ShouldLoadSuccessfully_AndHaveUniqueIds()
    {
        var modifiers = _modifierRepo.GetAllDefinitions();

        modifiers.ShouldNotBeEmpty();

        // Validação extra: Garantir que campos obrigatórios estão preenchidos
        foreach (var mod in modifiers.Values)
        {
            mod.Name.ShouldNotBeNullOrWhiteSpace($"Modifier {mod.Id} must have a name.");
        }
    }

    [Fact]
    public void Abilities_ShouldLoadSuccessfully_AndHaveUniqueIds()
    {
        var abilities = _abilityRepo.GetAllDefinitions();

        abilities.ShouldNotBeEmpty();

        foreach (var abil in abilities.Values)
        {
            abil.Name.ShouldNotBeNullOrWhiteSpace($"Ability {abil.Id} must have a name.");
        }
    }

    [Fact]
    public void Integrity_AllAbilityModifiers_ShouldExistInModifierRepo()
    {       
        // Verifica se a Fireball aponta para um ModifierID que realmente existe.

        var abilities = _abilityRepo.GetAllDefinitions();
        var modifiers = _modifierRepo.GetAllDefinitions();

        var missingReferences = new List<string>();

        foreach (var ability in abilities.Values)
        {
            foreach (var effect in ability.Effects)
            {
                // verificar efeitos do tipo APPLY_MODIFIER
                if (effect.Type == EffectType.APPLY_MODIFIER)
                {
                    if (string.IsNullOrEmpty(effect.ModifierDefinitionId))
                    {
                        missingReferences.Add($"Ability '{ability.Id}' has an APPLY_MODIFIER effect with null/empty ModifierId.");
                        continue;
                    }

                    if (!modifiers.ContainsKey(effect.ModifierDefinitionId))
                    {
                        missingReferences.Add($"Ability '{ability.Id}' references unknown Modifier: '{effect.ModifierDefinitionId}'");
                    }
                }
            }
        }

        // Se a lista tiver erros, o teste falha e imprime o relatório
        missingReferences.ShouldBeEmpty(
            "Found broken references in Ability Definitions:\n" + string.Join("\n", missingReferences));
    }

    [Fact]
    public void Integrity_AllTriggeredAbilities_ShouldExistInAbilityRepo()
    {
        // se um Modificador dispara uma Habilidade (TriggeredAbilityId),
        // essa habilidade tem de existir.

        var abilities = _abilityRepo.GetAllDefinitions();
        var modifiers = _modifierRepo.GetAllDefinitions();

        var missingReferences = new List<string>();

        foreach (var mod in modifiers.Values)
        {
            if (!string.IsNullOrEmpty(mod.TriggeredAbilityId))
            {
                if (!abilities.ContainsKey(mod.TriggeredAbilityId))
                {
                    missingReferences.Add($"Modifier '{mod.Id}' triggers unknown Ability: '{mod.TriggeredAbilityId}'");
                }
            }
        }

        missingReferences.ShouldBeEmpty("Found broken references in Modifier Definitions:\n" + string.Join("\n", missingReferences));
    }

    // --- HELPER: Navegação de Pastas ---
    private string FindSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dirInfo = new DirectoryInfo(currentDir);

        // Sobe até encontrar o ficheiro .sln ou chegar à raiz
        while (dirInfo != null && !dirInfo.GetFiles("*.sln").Any())
        {
            dirInfo = dirInfo.Parent;
        }

        if (dirInfo == null)
        {
            // Fallback: assumir estrutura padrão ../../../..
            // (bin / Debug / net9.0 / GuildArena.IntegrationTests / tests / ROOT)
            return Path.GetFullPath(Path.Combine(currentDir, "../../../../..")); //TODO: rever smelly hack
        }

        return dirInfo.FullName;
    }
}