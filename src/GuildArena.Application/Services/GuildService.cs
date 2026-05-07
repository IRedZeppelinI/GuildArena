using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.GuildAndHeroes;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Services;

public class GuildService : IGuildService
{
    private readonly IGuildRepository _guildRepo;
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly ILogger<GuildService> _logger;

    public GuildService(
        IGuildRepository guildRepo,
        ICharacterDefinitionRepository characterRepo,
        ILogger<GuildService> logger)
    {
        _guildRepo = guildRepo;
        _characterRepo = characterRepo;
        _logger = logger;
    }

    public async Task<Result<List<HeroDto>>> GetGuildRosterAsync(int? guildId)
    {
        if (!guildId.HasValue)
        {
            return Result.Failure<List<HeroDto>>(new Error(
                "Guild.NotFound",
                "User is not associated with any guild.",
                ErrorType.NotFound));
        }

        _logger.LogDebug("Fetching roster for Guild {GuildId}", guildId.Value);

        var heroes = await _guildRepo.GetAllHeroesAsync(guildId.Value);
        var definitions = _characterRepo.GetAllDefinitions();

        var dtos = heroes.Select(h =>
        {
            var hasDef = definitions.TryGetValue(h.CharacterDefinitionId, out var def);

            return new HeroDto
            {
                Id = h.Id,
                DefinitionId = h.CharacterDefinitionId,
                Name = hasDef ? def!.Name : "Unknown Hero",
                CurrentLevel = h.CurrentLevel
            };
        }).ToList();

        return dtos;
    }

    public async Task<Result> CreateGuildAsync(string applicationUserId, int? existingGuildId, string guildName)
    {
        // Validações de negócio agora vivem no Application Layer
        if (existingGuildId.HasValue)
        {
            return Result.Failure(new Error(
                "Guild.AlreadyExists",
                "User already has an active Guild.",
                ErrorType.Conflict));
        }

        if (string.IsNullOrWhiteSpace(guildName) || guildName.Length < 3)
        {
            return Result.Failure(new Error(
                "Guild.InvalidName",
                "Guild name must be at least 3 characters long.",
                ErrorType.Validation));
        }

        _logger.LogInformation("Creating new guild '{GuildName}' for User {UserId}", guildName, applicationUserId);

        await _guildRepo.CreateWithStarterPackAsync(applicationUserId, guildName);

        return Result.Success();
    }
}