﻿using Google.Cloud.Storage.V1;
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
        public sealed class Options
        {
            public string Uri { get; set; } = "mongodb://localhost:27017";

            public string FileName { get; set; } = "backup-{0:yyyy-MM-dd-hh-mm-ss}";

            public string BucketName { get; set; } = "mongodb-backups123";

            public string BinaryPath { get; set; } = "mongodump.exe";
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

            logger.LogInformation("Backup Mongodb {{Uri={}}} started", options.Uri);

            var file = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            try
            {
                if (!DumpDatabases(services, options, file))
                {
                    return;
                }

                var fileName = string.Format(CultureInfo.InvariantCulture, options.FileName, DateTime.UtcNow);

                var storageClient = StorageClient.Create();

                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    storageClient.UploadObject(options.BucketName, fileName, "text/plain", fs);
                }

                logger.LogInformation("Backup Mongodb {{Uri={}}} completed", options.Uri);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Backup Mongodb {{Uri={}}} failed", options.Uri);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private static Options ConfigureOptions(string[] args)
        {
            var options = new Options();

            var configuration =
                new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();

            configuration.GetSection("app").Bind(options);

            return options;
        }

        private static bool DumpDatabases(IServiceProvider services, Options options, string file)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Mongodump");

            var process = new Process();

            process.StartInfo.Arguments = $" --archive=\"{file}\" --gzip --uri=\"{options.Uri}\"";
            process.StartInfo.FileName = options.BinaryPath;
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
