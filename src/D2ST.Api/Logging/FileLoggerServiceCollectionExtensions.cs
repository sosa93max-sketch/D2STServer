namespace D2ST.Api.Logging;

public static class FileLoggerServiceCollectionExtensions
{
    /// <summary>Adds the daily file log configured under <see cref="FileLoggerOptions.SectionName"/>.</summary>
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.Services.Configure<FileLoggerOptions>(configuration.GetSection(FileLoggerOptions.SectionName));
        logging.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
        return logging;
    }
}
