using System.Text;
using System.Text.Json;
using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace D2ST.GameCoordinator.Diagnostics;

/// <summary>
/// Appends one JSON object per unhandled GC message to a JSON Lines file. The
/// payload is kept verbatim (base64 and hex) so the protobuf can be decoded
/// away from the machine that ran the client.
/// </summary>
public sealed class JsonlGcMessageRecorder : IGcMessageRecorder
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    // A byte order mark would make the first record unreadable to strict JSON
    // Lines consumers (jq, json.loads).
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly GcDiagnosticsOptions _options;
    private readonly ILogger<JsonlGcMessageRecorder> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _writeLock = new();
    private readonly string _path;
    private bool _writeFailureLogged;

    public JsonlGcMessageRecorder(
        IOptions<GcDiagnosticsOptions> options,
        ILogger<JsonlGcMessageRecorder> logger,
        TimeProvider timeProvider,
        string contentRootPath)
    {
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
        _path = Path.IsPathRooted(_options.UnhandledMessageLogPath)
            ? _options.UnhandledMessageLogPath
            : Path.Combine(contentRootPath, _options.UnhandledMessageLogPath);
    }

    /// <summary>Absolute path of the dump, so an operator can be told where to look.</summary>
    public string LogFilePath => _path;

    public void RecordUnhandled(GcContext context, GcMessage message)
    {
        if (!_options.RecordUnhandledMessages)
        {
            return;
        }

        var body = message.Body ?? Array.Empty<byte>();
        var kept = body.Length > _options.MaxBodyBytes ? body.AsSpan(0, _options.MaxBodyBytes).ToArray() : body;

        var entry = new UnhandledGcMessageEntry(
            _timeProvider.GetUtcNow(),
            message.MessageType,
            GcMsgNames.Describe(message.MessageType),
            context.AccountId,
            context.SteamId,
            context.ClientVersion,
            message.SourceJobId,
            message.TargetJobId,
            body.Length,
            kept.Length != body.Length,
            Convert.ToBase64String(kept),
            Convert.ToHexString(kept));

        Append(JsonSerializer.Serialize(entry, Json));
    }

    private void Append(string line)
    {
        try
        {
            lock (_writeLock)
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_path, line + Environment.NewLine, Utf8WithoutBom);
            }
        }
        catch (IOException ex)
        {
            LogWriteFailure(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogWriteFailure(ex);
        }
    }

    // A diagnostics dump must never take the GC down, and a broken path would
    // otherwise repeat the same error on every message.
    private void LogWriteFailure(Exception exception)
    {
        if (_writeFailureLogged)
        {
            return;
        }

        _writeFailureLogged = true;
        _logger.LogError(exception, "Could not write the unhandled GC message dump to {Path}", _path);
    }

    private sealed record UnhandledGcMessageEntry(
        DateTimeOffset Timestamp,
        uint MessageType,
        string MessageName,
        uint AccountId,
        ulong SteamId,
        int ClientVersion,
        ulong? SourceJobId,
        ulong? TargetJobId,
        int BodyLength,
        bool BodyTruncated,
        string BodyBase64,
        string BodyHex);
}
