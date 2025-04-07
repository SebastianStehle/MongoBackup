using Microsoft.Extensions.Logging;
using System;

namespace MongoBackup;

public sealed class SimpleLog(string categoryName) : ILogger
{
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var line = $"[{GetLogLevelString(logLevel)}]: {categoryName,-30}: {formatter(state, exception)}";

        if (exception != null)
        {
            line += $" {exception.Message.Replace(Environment.NewLine, "\\n")}";
        }

        if (logLevel >= LogLevel.Error)
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.Out.WriteLine(line);
        }
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
                return "trce";
            case LogLevel.Debug:
                return "dbug";
            case LogLevel.Information:
                return "info";
            case LogLevel.Warning:
                return "warn";
            case LogLevel.Error:
                return "fail";
            case LogLevel.Critical:
                return "crit";
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel));
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }
}
