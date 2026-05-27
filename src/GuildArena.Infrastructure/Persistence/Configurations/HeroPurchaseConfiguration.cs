using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

public class HeroPurchaseConfiguration : IEntityTypeConfiguration<HeroPurchase>
{
    public void Configure(EntityTypeBuilder<HeroPurchase> builder)
    {
        builder.ToTable("HeroPurchases");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.GuildId, e.CharacterDefinitionId })
               .IsUnique();

        builder.HasOne(e => e.Guild)
               .WithMany()
               .HasForeignKey(e => e.GuildId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.CharacterDefinitionId)
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(e => e.PurchasedAt)
               .IsRequired();
    }
}