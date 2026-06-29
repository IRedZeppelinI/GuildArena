namespace GuildArena.Shared.DTOs.Identity;

public class UserInfoDto
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public int? GuildId { get; set; }
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Indicates whether the user has verified their email address.
    /// </summary>
    public bool IsEmailConfirmed { get; set; }
}