using Google.Cloud.Storage.V1;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MongoBackup.Storage.GoogleCloud;

internal class GoogleCloudStorage(string bucketName) : IStorage
{
    private StorageClient storageClient;

    public async Task InitializeAsync(
        CancellationToken ct)
    {
        storageClient = await StorageClient.CreateAsync();

        await storageClient.GetBucketAsync(bucketName, cancellationToken: ct);
    }

    public async Task DeleteAsync(string fileName,
        CancellationToken ct)
    {
        await storageClient.DeleteObjectAsync(bucketName, fileName, cancellationToken: ct);
    }

    public async Task UploadAsync(string fileName, Stream stream,
        CancellationToken ct)
    {
        await storageClient.UploadObjectAsync(bucketName, fileName, "application/octet-stream", stream, null, ct);
    }

    public async Task<List<StorageObject>> ListObjectsAsync(
        CancellationToken ct)
    {
        var result = new List<StorageObject>();

        var items = storageClient.ListObjectsAsync(bucketName);
        await foreach (var item in items.WithCancellation(ct))
        {
            if (item.TimeCreated != null)
            {
                result.Add(new StorageObject(item.Name, item.TimeCreated.Value));
            }
        }

        return result;
    }
}
