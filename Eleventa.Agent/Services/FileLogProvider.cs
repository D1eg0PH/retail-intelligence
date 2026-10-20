using Microsoft.Extensions.Logging;

namespace Eleventa.Agent.Services;

public sealed class FileLogProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly string _directory;

    public FileLogProvider(string directory)
    {
        _directory = directory;
        if (!Directory.Exists(_directory)) return;
        foreach (var file in Directory.EnumerateFiles(_directory, "agent-*.log"))
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-30))
                try { File.Delete(file); } catch (IOException) { }
    }
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, $"agent-{DateTime.UtcNow:yyyyMMdd}.log");
            using var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            writer.WriteLine($"{DateTimeOffset.UtcNow:O} [{level}] {category}: {message}");
            if (exception is not null) writer.WriteLine(exception);
        }
    }

    private sealed class FileLogger(FileLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
