using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the DungeonHeroState entity for the database.
/// </summary>
public class DungeonHeroStateConfiguration : IEntityTypeConfiguration<DungeonHeroState>
{
    public void Configure(EntityTypeBuilder<DungeonHeroState> builder)
    {
        builder.ToTable("DungeonHeroStates");

        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.ActiveDungeonRun)
               .WithMany(r => r.HeroesState)
               .HasForeignKey(e => e.ActiveDungeonRunId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Hero)
               .WithMany()
               .HasForeignKey(e => e.HeroId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}