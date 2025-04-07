using Azure.Storage.Blobs;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MongoBackup.Storage.Azure;

public sealed class AzureStorage(string connectionString, string containerName) : IStorage
{
    private BlobContainerClient blobContainer;

    public async Task InitializeAsync(
        CancellationToken ct)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);

        blobContainer = blobServiceClient.GetBlobContainerClient(containerName);
        await blobContainer.CreateIfNotExistsAsync(cancellationToken: ct);
    }

    public async Task DeleteAsync(string fileName,
        CancellationToken ct)
    {
        await blobContainer.DeleteBlobIfExistsAsync(fileName, cancellationToken: ct);
    }

    public async Task UploadAsync(string fileName, Stream stream,
        CancellationToken ct)
    {
        var blob = blobContainer.GetBlobClient(fileName);

        await blob.UploadAsync(stream, null, ct);
    }

    public async Task<List<StorageObject>> ListObjectsAsync(
        CancellationToken ct)
    {
        var result = new List<StorageObject>();

        var blobs = blobContainer.GetBlobsAsync(cancellationToken: ct);
        await foreach (var blob in blobs)
        {
            if (blob.Properties.CreatedOn != null)
            {
                result.Add(new StorageObject(blob.Name, blob.Properties.CreatedOn.Value.UtcDateTime));
            }
        }

        return result;
    }
}
