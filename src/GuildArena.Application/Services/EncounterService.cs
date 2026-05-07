using GuildArena.Application.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Encounters;

namespace GuildArena.Application.Services;

public class EncounterService : IEncounterService
{
    private readonly IEncounterDefinitionRepository _encounterRepo;

    public EncounterService(IEncounterDefinitionRepository encounterRepo)
    {
        _encounterRepo = encounterRepo;
    }

    public Result<List<EncounterSummaryDto>> GetAvailableEncounters()
    {
        var allDefinitions = _encounterRepo.GetAllDefinitions();

        var dtos = allDefinitions.Values.Select(e => new EncounterSummaryDto
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            DifficultyRating = e.DifficultyRating
        }).ToList();

        return dtos; // Conversão implícita para Result.Success
    }
}