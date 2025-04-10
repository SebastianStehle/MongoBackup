﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using MongoBackup.Storage;
using MongoBackup.Storage.GoogleCloud;
using MongoBackup.Storage.Azure;

namespace MongoBackup;

public sealed class Program
{
    private const int ConnectionTimeout = 10000;

    public static async Task<int> Main(string[] args)
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

            var storage = await GetAssetStoreAsync(options);

            if (options.DeleteAfterMaxDays > 0)
            {
                logger.LogInformation("Deleting backups after {Days} days.", options.DeleteAfterMaxDays);
                var maxTime = DateTime.UtcNow.AddDays(-options.DeleteAfterMaxDays);

                var objects = await storage.ListObjectsAsync(default);

                var toDelete = objects.Where(x => x.Created <= maxTime).ToList();
                var toKeep = objects.Where(x => x.Created > maxTime).ToList();

                try
                {
                    if (!options.Simulate)
                    {
                        foreach (var obj in toDelete)
                        {
                            await storage.DeleteAsync(obj.Name, default);
                        }
                    }

                    logger.LogInformation("Deleted old backups. Deleted: {TotalDeleted}, kept: {TotalKept}, total: {Total}",
                        toDelete.Count,
                        toKeep.Count,
                        objects.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete objects.");
                }
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
                if (options.Backup.Archive)
                {
                    fileName += ".agz";
                }
                else
                {
                    fileName += ".zip";
                }

                logger.LogInformation("Uploading archive to {}/{}", options.GoogleStorage.BucketName, fileName);

                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    await storage.UploadAsync(fileName, fs, default);
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

    private static async Task<IStorage> GetAssetStoreAsync(Options options)
    {
        IStorage store;

        if (string.Equals(options.Storage, "GC", StringComparison.OrdinalIgnoreCase))
        {
            store = new GoogleCloudStorage(options.GoogleStorage.BucketName);
        }
        else
        {
            store = new AzureStorage(options.AzureStorage.ConnectionString, options.AzureStorage.Container);
        }

        await store.InitializeAsync(default);

        return store;
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

        var args = $" --uri=\"{options.MongoDb.Uri}\"";

        if (options.Backup.Archive)
        {
            args += " --archive=\"{file}\" --gzip";
        }
        else
        {
            args += " --out dump";
        }

        process.StartInfo.Arguments = args;
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

        if (isSucceess && !options.Backup.Archive)
        {
            File.Delete(file);

            logger.LogInformation("Archive creating.");
            ZipFile.CreateFromDirectory("dump", file);
            logger.LogInformation("Archive created.");
        }

        return isSucceess;
    }
}
