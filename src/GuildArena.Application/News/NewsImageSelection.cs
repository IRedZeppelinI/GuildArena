using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;

namespace GuildArena.Application.News;

internal static class NewsImageSelection
{
    public static Result<string?> Resolve(string? illustrationId, bool hasUpload)
    {
        if (string.IsNullOrWhiteSpace(illustrationId)) return Result.Success<string?>(null);

        var imageUrl = NewsIllustrations.GetImageUrl(illustrationId);
        if (imageUrl == null)
        {
            return Result.Failure<string?>(new Error("News.InvalidIllustration", "Choose one of the available news illustrations.", ErrorType.Validation));
        }

        if (hasUpload)
        {
            return Result.Failure<string?>(new Error("News.ConflictingImages", "Choose either a news illustration or an image upload.", ErrorType.Validation));
        }

        return Result.Success<string?>(imageUrl);
    }
}
