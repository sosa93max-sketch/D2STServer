using D2ST.Core.Events;
using D2ST.Core.Steam;
using D2ST.Steam.Lobbies;
using D2ST.Steam.Social;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace D2ST.Steam.Presence;

/// <summary>
/// Reconciles presence with live sessions. A client that crashes or is killed
/// never says goodbye, so without this sweep its friends would keep seeing it
/// online until the session itself expired.
/// </summary>
public sealed class PresenceSweepService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ISessionStore _sessions;
    private readonly IPresenceTracker _presence;
    private readonly ILobbyService _lobbies;
    private readonly IOptions<SteamOptions> _options;
    private readonly ILogger<PresenceSweepService> _logger;
    private HashSet<uint> _online = new();

    public PresenceSweepService(
        IServiceScopeFactory scopes,
        ISessionStore sessions,
        IPresenceTracker presence,
        ILobbyService lobbies,
        IOptions<SteamOptions> options,
        ILogger<PresenceSweepService> logger)
    {
        _scopes = scopes;
        _sessions = sessions;
        _presence = presence;
        _lobbies = lobbies;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Value.PresenceSweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Presence sweep failed");
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var online = _sessions.OnlineAccounts().ToHashSet();
        var transitions = new HashSet<uint>(online);
        transitions.SymmetricExceptWith(_online);
        if (transitions.Count == 0)
        {
            return;
        }

        _online = online;
        using var scope = _scopes.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<SocialEventPublisher>();

        foreach (var accountId in transitions)
        {
            if (!online.Contains(accountId))
            {
                // Stale rich presence outlives the session that set it, so drop
                // it before telling anyone: "in match" must not survive a crash.
                _presence.Clear(accountId);

                // Otherwise the lobby keeps a member that will never answer,
                // and stays "full" for everyone else.
                _lobbies.LeaveAll(accountId);
            }

            await publisher.PublishToAudienceAsync(
                accountId,
                SteamEventTypes.PersonaStateChanged,
                PersonaChange.Status,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
