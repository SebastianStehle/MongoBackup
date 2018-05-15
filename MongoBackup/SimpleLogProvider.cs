using Microsoft.Extensions.Logging;

namespace MongoBackup
{
    public sealed class SimpleLogProvider : ILoggerProvider
    {
        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
        {
            return new SimpleLog(categoryName);
        }
    }
}
