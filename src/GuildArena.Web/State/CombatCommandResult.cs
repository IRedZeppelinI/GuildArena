namespace GuildArena.Web.State;

public enum CombatCommandOutcome
{
    Accepted,
    Rejected,
    Unconfirmed,
    Unavailable
}

/// <summary>Client transport feedback; combat decisions remain on the server.</summary>
public sealed record CombatCommandResult(CombatCommandOutcome Outcome, string Message);
