using GuildArena.Web.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Web.UnitTests.Services;

public class AssetServiceTests
{
    [Fact]
    public void GetUrls_WithBaseUrlConfigured_ShouldReturnCorrectlyFormattedPaths()
    {
        // ARRANGE
        var configMock = Substitute.For<IConfiguration>();
        // Simulamos o valor que viria do appsettings.json
        configMock["AssetBaseUrl"].Returns("https://cdn.guildarena.com/");

        var service = new AssetService(configMock);

        // ACT
        var bgUrl = service.GetBackgroundUrl("BG_FOREST_01");
        var portraitUrl = service.GetPortraitUrl("HERO_GARRET");
        var abilityUrl = service.GetAbilityIconUrl("ABIL_SLASH");
        var modifierUrl = service.GetModifierIconUrl("MOD_STUN");

        // ASSERT
        // Verifica se formatou em minúsculas e aplicou a extensão .jpg do teu blob storage
        bgUrl.ShouldBe("https://cdn.guildarena.com/backgrounds/bg_forest_01.jpg");
        portraitUrl.ShouldBe("https://cdn.guildarena.com/portraits/hero_garret.jpg");
        abilityUrl.ShouldBe("https://cdn.guildarena.com/abilities/abil_slash.jpg");
        modifierUrl.ShouldBe("https://cdn.guildarena.com/modifiers/mod_stun.jpg");
    }

    [Fact]
    public void GetUrls_WithoutBaseUrlConfigured_ShouldReturnLocalRelativePaths()
    {
        // ARRANGE
        var configMock = Substitute.For<IConfiguration>();
        // Simula o caso em que não há configuração
        configMock["AssetBaseUrl"].Returns((string?)null);

        var service = new AssetService(configMock);

        // ACT
        var portraitUrl = service.GetPortraitUrl("HERO_GARRET");

        // ASSERT
        // Sem base URL, deve devolver apenas o caminho relativo
        portraitUrl.ShouldBe("portraits/hero_garret.jpg");
    }
}