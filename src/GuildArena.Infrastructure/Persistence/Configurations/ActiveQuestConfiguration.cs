using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ActiveQuest"/> entity.
/// </summary>
public class ActiveQuestConfiguration : IEntityTypeConfiguration<ActiveQuest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ActiveQuest> builder)
    {
        builder.ToTable("ActiveQuests");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedOnAdd();

        builder.Property(q => q.QuestDefinitionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(q => q.Guild)
            .WithMany(g => g.ActiveQuests)
            .HasForeignKey(q => q.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}