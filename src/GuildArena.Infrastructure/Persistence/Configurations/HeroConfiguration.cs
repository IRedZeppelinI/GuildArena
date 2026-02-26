using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

public class HeroConfiguration : IEntityTypeConfiguration<Hero>
{
    public void Configure(EntityTypeBuilder<Hero> builder)
    {
        builder.ToTable("Heroes");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.CharacterDefinitionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(h => h.CurrentLevel)
            .IsRequired();

        builder.Property(h => h.CurrentXP)
            .IsRequired();

        // Relação N:1 com Guild
        builder.HasOne(h => h.Guild)
            .WithMany(g => g.Heroes)
            .HasForeignKey(h => h.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index para performance em queries por Definição (ex: Top Zyrus players)
        builder.HasIndex(h => h.CharacterDefinitionId);
    }
}