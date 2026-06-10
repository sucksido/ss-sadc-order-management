using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SadcOms.Application.Abstractions;
using SadcOms.Domain.Regional;

namespace SadcOms.Infrastructure.Fx;

/// <summary>
/// Stand-in for a real FX rates feed. Rates are illustrative and fixed; a production
/// implementation would call a provider (bank/ECB/commercial feed) behind the same
/// interface. Each (from,to) rate is cached so a burst of report requests results in at
/// most one "fetch" per currency pair per cache window.
///
/// CMA currencies are pegged 1:1 to ZAR, so those conversions are exact rather than mocked.
/// </summary>
public sealed class MockFxRateProvider(IMemoryCache cache, IOptions<FxOptions> options) : IFxRateProvider
{
    private readonly FxOptions _options = options.Value;

    // Indicative units of ZAR per 1 unit of the source currency.
    private static readonly IReadOnlyDictionary<string, decimal> ToZar =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["ZAR"] = 1.0000m,
            ["USD"] = 18.5000m,
            ["BWP"] = 1.3500m,
            ["ZWL"] = 0.0500m,
            ["AOA"] = 0.0200m,
            ["KMF"] = 0.0400m,
            ["CDF"] = 0.0065m,
            ["MGA"] = 0.0040m,
            ["MWK"] = 0.0110m,
            ["MUR"] = 0.4000m,
            ["MZN"] = 0.2900m,
            ["SCR"] = 1.3500m,
            ["TZS"] = 0.0072m,
            ["ZMW"] = 0.7000m,
        };

    public Task<decimal> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var from = Normalize(fromCurrency);
        var to = Normalize(toCurrency);

        if (from == to)
        {
            return Task.FromResult(1.0000m);
        }

        // Currencies at par within the Common Monetary Area convert 1:1 to ZAR.
        if (to == SadcRegion.AnchorCurrency && SadcRegion.CmaCurrencies.Contains(from))
        {
            return Task.FromResult(1.0000m);
        }

        var key = $"fx::{from}->{to}";
        if (cache.TryGetValue(key, out decimal cached))
        {
            return Task.FromResult(cached);
        }

        var rate = Fetch(from, to);
        cache.Set(key, rate, _options.CacheDuration);
        return Task.FromResult(rate);
    }

    private static decimal Fetch(string from, string to)
    {
        if (to == SadcRegion.AnchorCurrency && ToZar.TryGetValue(from, out var toZar))
        {
            return toZar;
        }

        // Cross rate via ZAR for any other target (kept for completeness; the report only uses ZAR).
        if (ToZar.TryGetValue(from, out var fromZar) && ToZar.TryGetValue(to, out var targetZar) && targetZar != 0)
        {
            return decimal.Round(fromZar / targetZar, 6, MidpointRounding.AwayFromZero);
        }

        throw new InvalidOperationException($"No FX rate available for {from} -> {to}.");
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
