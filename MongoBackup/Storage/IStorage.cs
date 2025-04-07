using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MongoBackup.Storage;

public interface IStorage
{
    Task InitializeAsync(
        CancellationToken ct);

    Task UploadAsync(string fileName, Stream stream,
        CancellationToken ct);

    Task DeleteAsync(string fileName,
        CancellationToken ct);

    Task<List<StorageObject>> ListObjectsAsync(
        CancellationToken ct);
}

public sealed record StorageObject(string Name, DateTime Created);
