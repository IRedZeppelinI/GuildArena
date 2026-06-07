using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Dungeons;
using MediatR;

namespace GuildArena.Application.Dungeons.GetAvailableDungeons;

/// <summary>
/// Retrieves all static dungeon definitions and maps them to summary DTOs.
/// </summary>
public class GetAvailableDungeonsQueryHandler : IRequestHandler<GetAvailableDungeonsQuery, Result<List<DungeonSummaryDto>>>
{
    private readonly IDungeonDefinitionRepository _dungeonRepo;

    public GetAvailableDungeonsQueryHandler(IDungeonDefinitionRepository dungeonRepo)
    {
        _dungeonRepo = dungeonRepo;
    }

    public Task<Result<List<DungeonSummaryDto>>> Handle(GetAvailableDungeonsQuery request, CancellationToken cancellationToken)
    {
        var dtos = _dungeonRepo.GetAllDefinitions().Values
            .Select(d => new DungeonSummaryDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                RequiredGuildLevel = d.RequiredGuildLevel
            })
            .ToList();

        return Task.FromResult<Result<List<DungeonSummaryDto>>>(dtos);
    }
}