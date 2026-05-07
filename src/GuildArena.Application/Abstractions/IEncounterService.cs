using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Encounters;

namespace GuildArena.Application.Abstractions;

public interface IEncounterService
{
    Result<List<EncounterSummaryDto>> GetAvailableEncounters();
}