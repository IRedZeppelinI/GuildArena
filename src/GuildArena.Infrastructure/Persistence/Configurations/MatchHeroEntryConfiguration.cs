using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

public class MatchHeroEntryConfiguration : IEntityTypeConfiguration<MatchHeroEntry>
{
    public void Configure(EntityTypeBuilder<MatchHeroEntry> builder)
    {
        builder.ToTable("MatchHeroes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.HeroDefinitionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.LevelSnapshot)
            .IsRequired();

        // Relação N:1 com MatchParticipant
        builder.HasOne(e => e.MatchParticipant)
            .WithMany(p => p.HeroesUsed)
            .HasForeignKey(e => e.MatchParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}