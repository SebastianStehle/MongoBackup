using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace MongoBackup
{
    public sealed class Program
    {
        public sealed class MongoDbOptions
        {
            public string Uri { get; set; } = "mongodb://localhost:27017";

            public string DumpBinaryPath { get; set; }
        }

        public sealed class GoogleStorageOptions
        {
            public string BucketName { get; set; }
        }

        public sealed class BackupOptions
        {
            public string FileName { get; set; } = "backup-{0:yyyy-MM-dd-hh-mm-ss}";
        }

        public sealed class RootOptions
        {
            public MongoDbOptions MongoDb { get; } = new MongoDbOptions();

            public BackupOptions Backup { get; } = new BackupOptions();

            public GoogleStorageOptions GoogleStorage { get; } = new GoogleStorageOptions();
        }

        public static void Main(string[] args)
        {
            var options = ConfigureOptions(args);

            var services =
                new ServiceCollection()
                    .AddLogging(builder =>
                    {
                        builder.AddConsole();
                    })
                    .BuildServiceProvider();

            var logger = services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Backup Mongodb {{Uri={}}} started", options.MongoDb.Uri);

            var file = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            try
            {
                if (!DumpDatabases(services, options, file))
                {
                    return;
                }

                var fileName = string.Format(CultureInfo.InvariantCulture, options.Backup.FileName, DateTime.UtcNow);

                var storageClient = StorageClient.Create();

                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    storageClient.UploadObject(options.GoogleStorage.BucketName, fileName, "text/plain", fs);
                }

                logger.LogInformation("Backup Mongodb {{Uri={}}} completed", options.MongoDb.Uri);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Backup Mongodb {{Uri={}}} failed", options.MongoDb.Uri);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private static RootOptions ConfigureOptions(string[] args)
        {
            var options = new RootOptions();

            var configuration =
                new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();

            configuration.Bind(options);

            return options;
        }

        private static bool DumpDatabases(IServiceProvider services, RootOptions options, string file)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Mongodump");

            var process = new Process();

            process.StartInfo.Arguments = $" --archive=\"{file}\" --gzip --uri=\"{options.MongoDb.Uri}\"";
            process.StartInfo.FileName = options.MongoDb.DumpBinaryPath;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    logger.LogInformation(e.Data.Substring(29));
                }
            };
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    logger.LogInformation(e.Data.Substring(29));
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            var exit = process.ExitCode;

            var isSucceess = exit == 0;

            if (!isSucceess)
            {
                logger.LogCritical("Mongodump failed with status code {}", exit);
            }
            else
            {
                logger.LogInformation("Mongodump succeeded");
            }

            return isSucceess;
        }
    }
}
