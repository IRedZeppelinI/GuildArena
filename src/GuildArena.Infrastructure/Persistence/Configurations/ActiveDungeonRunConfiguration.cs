using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the ActiveDungeonRun entity for the database.
/// </summary>
public class ActiveDungeonRunConfiguration : IEntityTypeConfiguration<ActiveDungeonRun>
{
    public void Configure(EntityTypeBuilder<ActiveDungeonRun> builder)
    {
        builder.ToTable("ActiveDungeonRuns");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DungeonDefinitionId)
               .IsRequired()
               .HasMaxLength(100);

        builder.HasOne(e => e.Guild)
               .WithOne(g => g.ActiveDungeonRun)
               .HasForeignKey<ActiveDungeonRun>(e => e.GuildId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.HeroesState)
               .WithOne(h => h.ActiveDungeonRun)
               .HasForeignKey(h => h.ActiveDungeonRunId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}