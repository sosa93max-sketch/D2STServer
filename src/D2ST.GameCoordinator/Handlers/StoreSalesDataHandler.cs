using D2ST.Core.GameCoordinator;
using D2ST.Core.Economy;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the store sale query from the active local catalog. Prices are
/// expressed in the server's local USD dollars and refreshed daily by the client.
/// </summary>
public sealed class StoreSalesDataHandler : IGcMessageHandler
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(1);

    private readonly TimeProvider _timeProvider;
    private readonly IEconomyStore _economy;

    public StoreSalesDataHandler(TimeProvider timeProvider, IEconomyStore economy)
    {
        _timeProvider = timeProvider;
        _economy = economy;
    }

    public uint MessageType => GcMsg.RequestStoreSalesData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgGCRequestStoreSalesData>(request.Body);
        var response = new CMsgGCRequestStoreSalesDataResponse
        {
            Version = 1,
            ExpirationTime = (uint)_timeProvider.GetUtcNow().Add(Validity).ToUnixTimeSeconds()
        };

        var nativePlusSkuPublished = false;

        foreach (var item in _economy.GetCatalog())
        {
            var itemDef = item.DefIndex != 0 ? item.DefIndex : item.ProductId;
            if (itemDef == 0 || item.PriceDollars < 0 || item.PriceDollars > LocalEconomyCurrency.MaxWireDollars)
            {
                continue;
            }

            response.SalePrices.Add(new CMsgGCRequestStoreSalesDataResponse.Price
            {
                ItemDef = itemDef,
                price = LocalEconomyCurrency.ToWireAmount(item.PriceDollars)
            });

            // The native client does not submit the custom local ProductId
            // when the Dota Plus checkout is opened. It submits the built-in
            // subscription definitions instead. Publish the first active
            // local plan under those aliases; custom ProductIds remain
            // advertised for clients that can use them directly.
            if (item.ProductType == StoreProductType.DotaPlusSubscription &&
                !nativePlusSkuPublished)
            {
                foreach (var nativeSku in DotaPlusNativeSkus.All)
                {
                    if (nativeSku == itemDef)
                    {
                        continue;
                    }

                    response.SalePrices.Add(new CMsgGCRequestStoreSalesDataResponse.Price
                    {
                        ItemDef = nativeSku,
                        price = LocalEconomyCurrency.ToWireAmount(item.PriceDollars)
                    });
                }

                nativePlusSkuPublished = true;
            }
        }

        return
        [
            new GcMessage(
                GcMsg.RequestStoreSalesDataResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
