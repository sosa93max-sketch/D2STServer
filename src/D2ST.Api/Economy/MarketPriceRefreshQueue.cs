using System.Threading.Channels;
using D2ST.Api.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D2ST.Api.Economy;

public interface IMarketPriceRefreshQueue
{
    int Enqueue(IEnumerable<uint> productIds, bool activateMatched = false);
}

/// <summary>
/// Runs the Steam Market lookup outside the admin HTTP request. A full Dota
/// catalog can contain thousands of definitions and Steam rate-limits the
/// public endpoint, so imports enqueue bounded batches instead of timing out.
/// </summary>
public sealed class MarketPriceRefreshQueue : BackgroundService, IMarketPriceRefreshQueue
{
    private const int BatchSize = 500;

    private readonly Channel<PriceRefreshJob> _jobs =
        Channel.CreateUnbounded<PriceRefreshJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    private readonly SteamMarketPriceSync _marketPrices;
    private readonly ILogger<MarketPriceRefreshQueue> _logger;

    public MarketPriceRefreshQueue(
        SteamMarketPriceSync marketPrices,
        ILogger<MarketPriceRefreshQueue> logger)
    {
        _marketPrices = marketPrices;
        _logger = logger;
    }

    public int Enqueue(IEnumerable<uint> productIds, bool activateMatched = false)
    {
        var ids = productIds
            .Where(productId => productId != 0)
            .Distinct()
            .ToArray();
        foreach (var batch in ids.Chunk(BatchSize))
        {
            _jobs.Writer.TryWrite(new PriceRefreshJob(batch, activateMatched));
        }

        return ids.Length;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var result = await _marketPrices.SyncAsync(
                    new MarketPriceSyncRequest(
                        ActiveOnly: false,
                        MaxItems: BatchSize,
                        MaxAgeMinutes: 60,
                        UseMedian: false,
                        DryRun: false,
                        ProductIds: job.ProductIds,
                        ActivateMatched: job.ActivateMatched),
                    stoppingToken);
                _logger.LogInformation(
                    "Precios Steam en segundo plano: {Processed} procesados, {Matched} encontrados, {NoMatch} sin coincidencia, {NoData} sin datos, {Failed} errores",
                    result.Processed,
                    result.Matched,
                    result.NoMatch,
                    result.NoData,
                    result.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Falló un lote de actualización de precios Steam en segundo plano.");
            }
        }
    }

    private sealed record PriceRefreshJob(IReadOnlyList<uint> ProductIds, bool ActivateMatched);
}
