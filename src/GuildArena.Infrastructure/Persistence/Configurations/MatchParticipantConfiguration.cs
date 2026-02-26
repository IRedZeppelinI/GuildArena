using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

public class MatchParticipantConfiguration : IEntityTypeConfiguration<MatchParticipant>
{
    public void Configure(EntityTypeBuilder<MatchParticipant> builder)
    {
        builder.ToTable("MatchParticipants");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.IsWinner)
            .IsRequired();

        // Relação N:1 com Match
        builder.HasOne(p => p.Match)
            .WithMany(m => m.Participants)
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relação N:1 Opcional com Guild (SetNull se a guilda for apagada, para manter histórico)
        builder.HasOne(p => p.Guild)
            .WithMany(g => g.MatchHistory)
            .HasForeignKey(p => p.GuildId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Relação 1:N com MatchHeroEntry
        builder.HasMany(p => p.HeroesUsed)
            .WithOne(e => e.MatchParticipant)
            .HasForeignKey(e => e.MatchParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}