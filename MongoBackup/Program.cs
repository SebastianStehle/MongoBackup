using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace MongoBackup
{
    public sealed class Program
    {
        private const int ConnectionTimeout = 10000;

        public static int Main(string[] args)
        {
            var services =
                new ServiceCollection()
                    .AddLogging(builder =>
                    {
                        builder.AddProvider(new SimpleLogProvider());
                    })
                    .BuildServiceProvider();

            using (services)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();

                var options = ConfigureOptions(args, logger);

                if (options == null)
                {
                    return 2;
                }

                logger.LogInformation("Backup Mongodb {{Uri={}}} started", options.MongoDb.Uri);

                var file = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

                try
                {
                    if (!DumpDatabases(services, options, file))
                    {
                        return 2;
                    }

                    var fileName = string.Format(CultureInfo.InvariantCulture, options.Backup.FileName, DateTime.UtcNow);

                    logger.LogInformation("Uploading archive to {}/{}", options.GoogleStorage.BucketName, fileName);

                    var storageClient = StorageClient.Create();

                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        storageClient.UploadObject(options.GoogleStorage.BucketName, fileName, "application/x-gzip", fs);
                    }

                    logger.LogInformation("Backup Mongodb {{Uri={}}} completed", options.MongoDb.Uri);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Backup Mongodb {{Uri={}}} failed", options.MongoDb.Uri);

                    return 2;
                }
                finally
                {
                    services.Dispose();

                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
            }

            return 0;
        }

        private static Options ConfigureOptions(string[] args, ILogger<Program> logger)
        {
            var options = new Options();

            var configuration =
                new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();

            configuration.Bind(options);

            var errors = options.Validate();

            if (errors.Count > 0)
            {
                logger.LogCritical("Options are not valid: {}", string.Join(',', errors));

                return null;
            }
            else
            {
                return options;
            }
        }

        private static bool DumpDatabases(IServiceProvider services, Options options, string file)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(".\\mongodump");

            var process = new Process();
            var processConnected = true;

            var connectTimer = new Timer(x =>
            {
                processConnected = false;
                process.Kill();
            });

            connectTimer.Change(ConnectionTimeout, 0);

            process.StartInfo.Arguments = $" --archive=\"{file}\" --gzip --uri=\"{options.MongoDb.Uri}\"";
            process.StartInfo.FileName = options.MongoDb.DumpBinaryPath;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    connectTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    IEnumerable<string> parts = e.Data.Split('\t');

                    if (parts.Count() > 1)
                    {
                        parts = parts.Skip(1);
                    }

                    foreach (var part in parts)
                    {
                        logger.LogInformation(part);
                    }
                }
            };
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    connectTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    IEnumerable<string> parts = e.Data.Split('\t');

                    if (parts.Count() > 1)
                    {
                        parts = parts.Skip(1);
                    }

                    foreach (var part in parts)
                    {
                        logger.LogInformation(part);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            var exit = process.ExitCode;

            var isSucceess = processConnected && exit == 0;

            if (!processConnected)
            {
                logger.LogCritical("Mongodump could not establish connection to database within {} ms. Exit code: {}", ConnectionTimeout, exit);
            }
            else if (!isSucceess)
            {
                logger.LogCritical("Mongodump failed with exit code {}", exit);
            }
            else
            {
                logger.LogInformation("Mongodump succeeded");
            }

            return isSucceess;
        }
    }
}
