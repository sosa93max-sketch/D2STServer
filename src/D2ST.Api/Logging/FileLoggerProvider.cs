using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;

namespace D2ST.Api.Logging;

/// <summary>
/// Minimal daily-rolling file log. Standard log filtering applies through the
/// "File" provider alias (<c>Logging:File:LogLevel</c>), so raising the GC to
/// Debug only affects this sink.
/// </summary>
[Microsoft.Extensions.Logging.ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly FileLoggerOptions _options;
    private readonly string _directory;
    private readonly Lock _writeLock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private bool _writeFailureReported;

    public FileLoggerProvider(IOptions<FileLoggerOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _directory = Path.IsPathRooted(_options.Directory)
            ? _options.Directory
            : Path.Combine(environment.ContentRootPath, _options.Directory);

        if (_options.Enabled)
        {
            Directory.CreateDirectory(_directory);
            PruneOldFiles();
        }
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() => _loggers.Clear();

    private void Write(string line)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            lock (_writeLock)
            {
                File.AppendAllText(CurrentFilePath(), line + Environment.NewLine, Utf8WithoutBom);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Logging must never break the request that produced it; report once.
            if (!_writeFailureReported)
            {
                _writeFailureReported = true;
                Console.Error.WriteLine($"File log disabled: {exception.Message}");
            }
        }
    }

    private string CurrentFilePath() =>
        Path.Combine(_directory, $"{_options.FilePrefix}-{DateTime.UtcNow:yyyyMMdd}.log");

    private void PruneOldFiles()
    {
        if (_options.RetainedFileCount <= 0)
        {
            return;
        }

        var stale = Directory
            .EnumerateFiles(_directory, $"{_options.FilePrefix}-*.log")
            .OrderByDescending(path => path)
            .Skip(_options.RetainedFileCount);

        foreach (var path in stale)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A file someone else holds open is not worth failing startup for.
            }
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => _provider._options.Enabled && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var builder = new StringBuilder()
                .Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffZ"))
                .Append(" [").Append(Level(logLevel)).Append("] ")
                .Append(_category).Append(": ")
                .Append(formatter(state, exception));

            if (exception is not null)
            {
                builder.AppendLine().Append(exception);
            }

            _provider.Write(builder.ToString());
        }

        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "none"
        };
    }
}
