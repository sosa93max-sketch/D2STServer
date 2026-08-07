namespace D2ST.Api.Logging;

/// <summary>
/// Options for the built-in file log, so a capture session does not depend on
/// keeping the console window alive.
/// </summary>
public sealed class FileLoggerOptions
{
    public const string SectionName = "Logging:File";

    public bool Enabled { get; set; } = true;

    /// <summary>Directory the daily log files are written to, relative to the content root.</summary>
    public string Directory { get; set; } = "Logs";

    /// <summary>File name prefix; the UTC date and the .log extension are appended.</summary>
    public string FilePrefix { get; set; } = "d2st";

    /// <summary>How many daily files are kept; older ones are deleted at startup.</summary>
    public int RetainedFileCount { get; set; } = 14;
}
