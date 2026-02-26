using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

public class GuildConfiguration : IEntityTypeConfiguration<Guild>
{
    public void Configure(EntityTypeBuilder<Guild> builder)
    {
        builder.ToTable("Guilds");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(g => g.Gold)
            .IsRequired();

        // Relação inversa com User (já configurada no User, mas reforça-se aqui o lado do foreign key)
        builder.HasOne(g => g.ApplicationUser)
            .WithOne(u => u.Guild)
            .HasForeignKey<Guild>(g => g.ApplicationUserId)
            .IsRequired();

        // Relação 1:N com Heroes
        builder.HasMany(g => g.Heroes)
            .WithOne(h => h.Guild)
            .HasForeignKey(h => h.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relação 1:N com MatchHistory
        builder.HasMany(g => g.MatchHistory)
            .WithOne(mp => mp.Guild)
            .HasForeignKey(mp => mp.GuildId);
    }
}