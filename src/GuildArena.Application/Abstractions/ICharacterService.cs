using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.GuildAndHeroes;

namespace GuildArena.Application.Abstractions;

public interface ICharacterService
{
    Result<HeroDetailsDto> GetCharacterDetails(string definitionId);
}