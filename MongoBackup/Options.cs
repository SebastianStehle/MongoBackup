using System;
using System.Collections.Generic;
using Url = System.Uri;

namespace MongoBackup
{
    public sealed class Options
    {
        public sealed class MongoDbOptions
        {
            public string Uri { get; set; } = "mongodb://localhost:27017";

            public string DumpBinaryPath { get; set; } = "mongodump.exe";

            public void Validate(ICollection<string> errors)
            {
                if (string.IsNullOrWhiteSpace(Uri))
                {
                    errors.Add("Uri to mongodb has not been defined");
                }

                if (string.IsNullOrWhiteSpace(DumpBinaryPath))
                {
                    errors.Add("Path to the mongopdump binary has not been defined");
                }
            }
        }

        public sealed class GoogleStorageOptions
        {
            public string BucketName { get; set; } = "my";

            public void Validate(ICollection<string> errors)
            {
                if (string.IsNullOrWhiteSpace(BucketName))
                {
                    errors.Add("Bucket name has not been defined");
                }
            }
        }

        public sealed class AzureStorageOptions
        {
            public string ConnectionString { get; set; } = "DefaultEndpointsProtocol=my-connection-string";
            public string BlobService { get; set; } = "https://mystorageaccount.blob.core.windows.net/";
            public string Container { get; set; } = "my-container";

            public void Validate(ICollection<string> errors)
            {
                if (string.IsNullOrWhiteSpace(ConnectionString))
                {
                    errors.Add("ConnectionString has not been defined");
                }

                if (string.IsNullOrWhiteSpace(BlobService))
                {
                    errors.Add("BlobService has not been defined");
                }

                if (string.IsNullOrWhiteSpace(Container))
                {
                    errors.Add("Container has not been defined");
                }
            }
        }

        public sealed class BackupOptions
        {
            public string FileName { get; set; } = "backup-{0:yyyy-MM-dd-hh-mm-ss}.agz";

            public void Validate(ICollection<string> errors)
            {
                if (string.IsNullOrWhiteSpace(FileName))
                {
                    errors.Add("File name has not been defined");
                }
            }
        }

        public MongoDbOptions MongoDb { get; } = new MongoDbOptions();

        public BackupOptions Backup { get; } = new BackupOptions();

        public GoogleStorageOptions GoogleStorage { get; } = new GoogleStorageOptions();

        public AzureStorageOptions AzureStorage { get; } = new AzureStorageOptions();

        public ICollection<string> Validate()
        {
            var errors = new List<string>();

            if (MongoDb != null)
            {
                MongoDb.Validate(errors);
            }

            if (Backup != null)
            {
                Backup.Validate(errors);
            }

            if (GoogleStorage != null)
            {
                GoogleStorage.Validate(errors);
            }

            if (AzureStorage != null)
            {
                AzureStorage.Validate(errors);
            }

            return errors;
        }
    }
}
