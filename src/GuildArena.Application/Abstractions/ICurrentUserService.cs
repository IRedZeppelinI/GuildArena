namespace GuildArena.Application.Abstractions;

// TODO: A implementação atual na Infrastructure é um Fake/Mock que lê de headers ou configuração.
// No futuro, isto deve ser implementado usando IHttpContextAccessor para ler Claims do token JWT.
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the unique identifier (PlayerId) of the currently authenticated user.
    /// </summary>
    /// <value>
    /// The User ID if authenticated; otherwise, null.
    /// </value>
    int? UserId { get; }
}