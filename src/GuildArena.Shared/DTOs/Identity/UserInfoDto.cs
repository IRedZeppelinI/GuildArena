namespace GuildArena.Shared.DTOs.Identity;

public class UserInfoDto
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public int? GuildId { get; set; }
    public List<string> Roles { get; set; } = new();
}