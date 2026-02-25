using Microsoft.AspNetCore.Identity;
using System;

namespace GuildArena.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    // Relação 1:1 - Cada Utilizador tem uma Guilda (Perfil de Jogo)
    // Usamos 'Guild?' porque no momento do registo a guild ainda pode não existir por milisegundos.
    public Guild? Guild { get; set; }
}


