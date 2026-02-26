using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Configuração da relação 1:1 com Guild
        // O User é o principal, a Guild é dependente
        builder.HasOne(u => u.Guild)
            .WithOne(g => g.ApplicationUser)
            .HasForeignKey<Guild>(g => g.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}