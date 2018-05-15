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

            public string DumpBinaryPath { get; set; }

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
            public string BucketName { get; set; }

            public void Validate(ICollection<string> errors)
            {
                if (string.IsNullOrWhiteSpace(BucketName))
                {
                    errors.Add("Bucket name has not been defined");
                }
            }
        }

        public sealed class BackupOptions
        {
            public string FileName { get; set; } = "backup-{0:yyyy-MM-dd-hh-mm-ss}.gzip";

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

        public ICollection<string> Validate()
        {
            var errors = new List<string>();

            MongoDb.Validate(errors);

            Backup.Validate(errors);

            GoogleStorage.Validate(errors);

            return errors;
        }
    }
}
