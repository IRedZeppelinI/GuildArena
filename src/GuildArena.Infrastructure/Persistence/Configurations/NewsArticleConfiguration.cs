using GuildArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GuildArena.Infrastructure.Persistence.Configurations;

public class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Content)
            .IsRequired();

        builder.Property(a => a.ImageUrl)
            .HasMaxLength(2048);
    }
}