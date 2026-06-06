using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the GuildDungeonRecord entity for the database.
/// </summary>
public class GuildDungeonRecordConfiguration : IEntityTypeConfiguration<GuildDungeonRecord>
{
    public void Configure(EntityTypeBuilder<GuildDungeonRecord> builder)
    {
        builder.ToTable("GuildDungeonRecords");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DungeonDefinitionId)
               .IsRequired()
               .HasMaxLength(100);

        builder.HasOne(e => e.Guild)
               .WithMany(g => g.DungeonRecords)
               .HasForeignKey(e => e.GuildId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}