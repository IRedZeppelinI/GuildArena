using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GuildArena.Application.Abstractions; 
using Microsoft.Extensions.Configuration;

namespace GuildArena.Infrastructure.Services;

public class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AzureBlobStorage")
                               ?? throw new ArgumentNullException(nameof(configuration),
                                   "Missing Azure Blob Storage connection string.");

        var containerName = "news-images";
        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Garante que o contentor existe e que as imagens são lidas publicamente no browser
        _containerClient.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(fileName);

        await blobClient.UploadAsync(
            fileStream,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: ct);

        return blobClient.Uri.AbsoluteUri;
    }
}