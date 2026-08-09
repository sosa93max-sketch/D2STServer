using System.Globalization;
using System.Text;
using System.Text.Json;
using D2ST.Api.Contracts;
using D2ST.Core.Economy;
using D2ST.GameCoordinator.Econ;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Economy;

/// <summary>
/// Updates local catalog prices from the public Steam Community Market
/// endpoint. The operation is administrator-triggered, rate-limited and
/// persisted per product so the client never depends on Steam being reachable
/// during a purchase or a GC session.
/// </summary>
public sealed class SteamMarketPriceSync
{
    private const int AppId = 570;
    private const int CurrencyId = 1; // USD
    private const int DefaultMaxItems = 100;
    private const int DefaultMaxAgeMinutes = 60;
    private const int DefaultDelayMilliseconds = 350;
    private const int MaxItemsLimit = 500;
    private const int MaxAgeMinutesLimit = 7 * 24 * 60;
    private const string Source = "steam_market";

    private readonly IHttpClientFactory _httpClients;
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SteamMarketPriceSync(
        IHttpClientFactory httpClients,
        IServiceScopeFactory scopes,
        IConfiguration configuration)
    {
        _httpClients = httpClients;
        _scopes = scopes;
        _configuration = configuration;
    }

    public async Task<MarketPriceSyncResponse> SyncAsync(
        MarketPriceSyncRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxItems = request.MaxItems <= 0
            ? _configuration.GetValue("MarketPricing:DefaultMaxItems", DefaultMaxItems)
            : request.MaxItems;
        var maxAgeMinutes = request.MaxAgeMinutes <= 0
            ? _configuration.GetValue("MarketPricing:DefaultMaxAgeMinutes", DefaultMaxAgeMinutes)
            : request.MaxAgeMinutes;

        if (maxItems is < 1 or > MaxItemsLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MaxItems),
                $"MaxItems debe estar entre 1 y {MaxItemsLimit}.");
        }

        if (maxAgeMinutes is < 1 or > MaxAgeMinutesLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MaxAgeMinutes),
                $"MaxAgeMinutes debe estar entre 1 y {MaxAgeMinutesLimit}.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await SyncCoreAsync(
                request with { MaxItems = maxItems, MaxAgeMinutes = maxAgeMinutes },
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<MarketPriceSyncResponse> SyncCoreAsync(
        MarketPriceSyncRequest request,
        CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var query = db.StoreCatalogItems
            .Where(item => item.ProductType == StoreProductType.Item);
        if (request.ActiveOnly)
        {
            query = query.Where(item => item.Active);
        }

        var products = query
            .OrderBy(item => item.MarketPriceStatus == "matched" ? 1 : 0)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.ProductId)
            .Take(request.MaxItems)
            .ToList();

        var client = _httpClients.CreateClient("SteamMarket");
        var now = DateTimeOffset.UtcNow;
        var rows = new List<MarketPriceSyncItemResponse>(products.Count);
        var matched = 0;
        var cached = 0;
        var noMatch = 0;
        var noData = 0;
        var failed = 0;
        var delay = Math.Clamp(
            _configuration.GetValue("MarketPricing:RequestDelayMilliseconds", DefaultDelayMilliseconds),
            0,
            10_000);

        for (var index = 0; index < products.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var product = products[index];

            if (IsFreshMatch(product, now, request.MaxAgeMinutes))
            {
                cached++;
                rows.Add(ToResult(product, "cached", product.PriceDollars, null));
                continue;
            }

            try
            {
                var marketHashName = product.MarketHashName.Trim();
                if (marketHashName.Length == 0)
                {
                    var searchName = PrepareSearchName(product.Name);
                    if (searchName.Length == 0)
                    {
                        noMatch++;
                        SetStatus(product, Source, "unresolved_name", now);
                        rows.Add(ToResult(product, "unresolved_name", null,
                            "El producto no tiene un nombre visible utilizable para buscarlo en Steam."));
                        continue;
                    }

                    var search = await SearchAsync(client, searchName, cancellationToken);
                    marketHashName = search?.MarketHashName ?? string.Empty;
                    if (marketHashName.Length == 0)
                    {
                        noMatch++;
                        SetStatus(product, Source, "no_match", now);
                        rows.Add(ToResult(product, "no_match", null,
                            $"Steam no devolvió una coincidencia exacta para '{searchName}'."));
                        await DelayBetweenRequestsAsync(index, products.Count, delay, cancellationToken);
                        continue;
                    }
                }

                if (marketHashName.Length > 300)
                {
                    throw new InvalidDataException("El market_hash_name excede 300 caracteres.");
                }

                var overview = await GetOverviewAsync(client, marketHashName, cancellationToken);
                if (!overview.Success)
                {
                    noData++;
                    product.MarketHashName = marketHashName;
                    SetStatus(product, Source, "no_data", now);
                    rows.Add(ToResult(product, "no_data", null,
                        "El artículo no tiene precio disponible en Steam Market."));
                    await DelayBetweenRequestsAsync(index, products.Count, delay, cancellationToken);
                    continue;
                }

                var selectedCents = request.UseMedian
                    ? overview.MedianPriceCents ?? overview.LowestPriceCents
                    : overview.LowestPriceCents ?? overview.MedianPriceCents;
                if (selectedCents is null or <= 0)
                {
                    noData++;
                    product.MarketHashName = marketHashName;
                    SetStatus(product, Source, "no_data", now);
                    rows.Add(ToResult(product, "no_data", null,
                        "Steam no devolvió un precio positivo para el artículo."));
                    await DelayBetweenRequestsAsync(index, products.Count, delay, cancellationToken);
                    continue;
                }

                var appliedDollars = ToLocalDollars(selectedCents.Value);
                if (appliedDollars > LocalEconomyCurrency.MaxWireDollars)
                {
                    throw new InvalidDataException("El precio de Steam excede el límite de la economía local.");
                }

                if (!request.DryRun)
                {
                    product.MarketHashName = marketHashName;
                    product.MarketLowestPriceCents = overview.LowestPriceCents;
                    product.MarketMedianPriceCents = overview.MedianPriceCents;
                    product.MarketVolume = overview.Volume;
                    product.MarketPriceSource = Source;
                    product.MarketPriceStatus = "matched";
                    product.MarketPriceUpdatedAt = now;
                    product.PriceDollars = appliedDollars;
                }

                matched++;
                rows.Add(new MarketPriceSyncItemResponse(
                    product.ProductId,
                    product.DefIndex,
                    product.Name,
                    "matched",
                    marketHashName,
                    overview.LowestPriceCents,
                    overview.MedianPriceCents,
                    overview.Volume,
                    appliedDollars,
                    null));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException
                or InvalidDataException
                or JsonException
                or UriFormatException)
            {
                failed++;
                SetStatus(product, Source, "error", now);
                rows.Add(ToResult(product, "error", null, exception.Message));
            }

            await DelayBetweenRequestsAsync(index, products.Count, delay, cancellationToken);
        }

        if (!request.DryRun)
        {
            db.SaveChanges();
        }

        return new MarketPriceSyncResponse(
            request.MaxItems,
            products.Count,
            matched,
            cached,
            noMatch,
            noData,
            failed,
            request.DryRun,
            rows);
    }

    private static bool IsFreshMatch(
        StoreCatalogItemEntity product,
        DateTimeOffset now,
        int maxAgeMinutes) =>
        product.MarketPriceStatus == "matched"
        && !string.IsNullOrWhiteSpace(product.MarketHashName)
        && product.MarketPriceUpdatedAt is { } updatedAt
        && now - updatedAt < TimeSpan.FromMinutes(maxAgeMinutes)
        && product.MarketLowestPriceCents is > 0;

    private static string PrepareSearchName(string value)
    {
        var candidate = value.Trim();
        if (candidate.Length == 0
            || candidate.StartsWith('#')
            || candidate.StartsWith("def_", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("wearable_", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return candidate.Length <= 160 ? candidate : candidate[..160];
    }

    private static long ToLocalDollars(long cents) =>
        Math.Max(1, cents / 100 + (cents % 100 == 0 ? 0 : 1));

    private static void SetStatus(
        StoreCatalogItemEntity product,
        string source,
        string status,
        DateTimeOffset updatedAt)
    {
        product.MarketPriceSource = source;
        product.MarketPriceStatus = status;
        product.MarketPriceUpdatedAt = updatedAt;
    }

    private static MarketPriceSyncItemResponse ToResult(
        StoreCatalogItemEntity product,
        string status,
        long? appliedDollars,
        string? error) =>
        new(
            product.ProductId,
            product.DefIndex,
            product.Name,
            status,
            product.MarketHashName,
            product.MarketLowestPriceCents,
            product.MarketMedianPriceCents,
            product.MarketVolume,
            appliedDollars,
            error);

    private static async Task DelayBetweenRequestsAsync(
        int index,
        int count,
        int delay,
        CancellationToken cancellationToken)
    {
        if (delay > 0 && index + 1 < count)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static async Task<MarketSearchMatch?> SearchAsync(
        HttpClient client,
        string searchName,
        CancellationToken cancellationToken)
    {
        var path = $"market/search/render/?appid={AppId}&norender=1&count=100&currency={CurrencyId}&query={Uri.EscapeDataString(searchName)}";
        using var document = await GetJsonAsync(client, path, cancellationToken);
        var root = document.RootElement;
        if (!GetBoolean(root, "success") || !root.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var normalizedQuery = NormalizeMarketName(searchName);
        return results.EnumerateArray()
            .Select(result => new MarketSearchMatch(
                GetNestedString(result, "asset_description", "market_hash_name")
                    ?? GetString(result, "hash_name")
                    ?? string.Empty,
                GetLong(result, "sell_listings") ?? 0))
            .Where(result => result.MarketHashName.Length > 0
                && NormalizeMarketName(result.MarketHashName) == normalizedQuery)
            .OrderByDescending(result => result.SellListings)
            .FirstOrDefault();
    }

    private static async Task<MarketOverview> GetOverviewAsync(
        HttpClient client,
        string marketHashName,
        CancellationToken cancellationToken)
    {
        var path = $"market/priceoverview/?appid={AppId}&currency={CurrencyId}&market_hash_name={Uri.EscapeDataString(marketHashName)}";
        using var document = await GetJsonAsync(client, path, cancellationToken);
        var root = document.RootElement;
        return new MarketOverview(
            GetBoolean(root, "success"),
            ParsePriceCents(root, "lowest_price"),
            ParsePriceCents(root, "median_price"),
            ParseVolume(root, "volume"));
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            path,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static bool GetBoolean(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetNestedString(
        JsonElement element,
        string parent,
        string property) =>
        element.TryGetProperty(parent, out var nested)
            && nested.ValueKind == JsonValueKind.Object
            ? GetString(nested, property)
            : null;

    private static long? GetLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String
            ? ParseDigits(value.GetString())
            : null;
    }

    private static long? ParseVolume(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? ParseDigits(value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString())
            : null;

    private static long? ParsePriceCents(JsonElement element, string property)
    {
        var text = GetString(element, property);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = new string(text
            .Where(character => char.IsDigit(character) || character is '.' or ',')
            .ToArray());
        if (value.Length == 0)
        {
            return null;
        }

        if (value.Contains('.') && value.Contains(','))
        {
            value = value.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (!value.Contains('.') && value.LastIndexOf(',') >= 0)
        {
            var comma = value.LastIndexOf(',');
            value = (value.Length - comma - 1) <= 2
                ? value[..comma] + "." + value[(comma + 1)..]
                : value.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        return decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? checked((long)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero))
            : null;
    }

    private static long? ParseDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeMarketName(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private sealed record MarketSearchMatch(string MarketHashName, long SellListings);

    private sealed record MarketOverview(
        bool Success,
        long? LowestPriceCents,
        long? MedianPriceCents,
        long? Volume);
}
