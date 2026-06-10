namespace SadcOms.Domain.Regional;

/// <summary>
/// Reference data and validation for SADC (Southern African Development Community)
/// country/currency pairings.
///
/// Country codes are ISO 3166-1 alpha-2, currency codes are ISO 4217 alpha-3.
///
/// Two rules drive validation:
///   1. Each country has a set of currencies it transacts in (e.g. ZW accepts both ZWL
///      and USD as a multi-currency economy).
///   2. The Common Monetary Area (CMA) — South Africa, Lesotho, Namibia and Eswatini —
///      treats the South African Rand (ZAR) as legal tender everywhere in the area, with
///      the local units (LSL, NAD, SZL) pegged 1:1 to ZAR. So ZAR is accepted in any CMA
///      country in addition to that country's own currency.
///
/// This is intentionally a static, in-memory table. The list changes rarely and shipping
/// it in code keeps validation deterministic and free of an external dependency. If it
/// needed to change without a deploy it would move to a cached reference-data table.
/// </summary>
public static class SadcRegion
{
    // Country -> the currencies that country natively transacts in.
    private static readonly IReadOnlyDictionary<string, string[]> CountryCurrencies =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AO"] = ["AOA"],          // Angola
            ["BW"] = ["BWP"],          // Botswana
            ["KM"] = ["KMF"],          // Comoros
            ["CD"] = ["CDF"],          // DR Congo
            ["SZ"] = ["SZL"],          // Eswatini (CMA -> ZAR added below)
            ["LS"] = ["LSL"],          // Lesotho  (CMA -> ZAR added below)
            ["MG"] = ["MGA"],          // Madagascar
            ["MW"] = ["MWK"],          // Malawi
            ["MU"] = ["MUR"],          // Mauritius
            ["MZ"] = ["MZN"],          // Mozambique
            ["NA"] = ["NAD"],          // Namibia  (CMA -> ZAR added below)
            ["SC"] = ["SCR"],          // Seychelles
            ["ZA"] = ["ZAR"],          // South Africa
            ["TZ"] = ["TZS"],          // Tanzania
            ["ZM"] = ["ZMW"],          // Zambia
            ["ZW"] = ["ZWL", "USD"],   // Zimbabwe (multi-currency regime)
        };

    /// <summary>Members of the Common Monetary Area. ZAR is accepted in all of these.</summary>
    public static readonly IReadOnlySet<string> CommonMonetaryArea =
        new HashSet<string>(StringComparer.Ordinal) { "ZA", "LS", "NA", "SZ" };

    /// <summary>Currencies that circulate within the CMA at par with ZAR.</summary>
    public static readonly IReadOnlySet<string> CmaCurrencies =
        new HashSet<string>(StringComparer.Ordinal) { "ZAR", "NAD", "LSL", "SZL" };

    public const string AnchorCurrency = "ZAR";

    public static IReadOnlyCollection<string> SupportedCountries => (IReadOnlyCollection<string>)CountryCurrencies.Keys;

    public static bool IsSadcCountry(string? countryCode) =>
        !string.IsNullOrWhiteSpace(countryCode) && CountryCurrencies.ContainsKey(Normalize(countryCode));

    /// <summary>
    /// Returns the set of currencies accepted for a country, including ZAR for CMA members.
    /// Empty if the country is not a SADC member.
    /// </summary>
    public static IReadOnlySet<string> AcceptedCurrencies(string countryCode)
    {
        var country = Normalize(countryCode);
        if (!CountryCurrencies.TryGetValue(country, out var currencies))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var accepted = new HashSet<string>(currencies, StringComparer.Ordinal);
        if (CommonMonetaryArea.Contains(country))
        {
            accepted.Add(AnchorCurrency);
        }

        return accepted;
    }

    /// <summary>
    /// Validates that <paramref name="currencyCode"/> is usable for an order placed by a
    /// customer in <paramref name="countryCode"/>. Returns a structured result so callers
    /// can surface a precise validation message rather than a generic failure.
    /// </summary>
    public static CurrencyValidationResult ValidatePairing(string? countryCode, string? currencyCode)
    {
        // Trim first so surrounding whitespace is never treated as part of the code.
        var trimmedCountry = countryCode?.Trim();
        var trimmedCurrency = currencyCode?.Trim();

        if (!LooksLikeCountryCode(trimmedCountry))
        {
            return CurrencyValidationResult.Fail($"'{countryCode}' is not a valid ISO 3166-1 alpha-2 country code.");
        }

        if (!LooksLikeCurrencyCode(trimmedCurrency))
        {
            return CurrencyValidationResult.Fail($"'{currencyCode}' is not a valid ISO 4217 currency code.");
        }

        var country = trimmedCountry!.ToUpperInvariant();
        var currency = trimmedCurrency!.ToUpperInvariant();

        if (!CountryCurrencies.ContainsKey(country))
        {
            return CurrencyValidationResult.Fail($"'{country}' is not a supported SADC country.");
        }

        var accepted = AcceptedCurrencies(country);
        if (!accepted.Contains(currency))
        {
            return CurrencyValidationResult.Fail(
                $"Currency '{currency}' is not accepted for country '{country}'. Accepted: {string.Join(", ", accepted.OrderBy(c => c, StringComparer.Ordinal))}.");
        }

        return CurrencyValidationResult.Success();
    }

    private static bool LooksLikeCountryCode(string? code) =>
        code is { Length: 2 } && code.All(char.IsLetter);

    private static bool LooksLikeCurrencyCode(string? code) =>
        code is { Length: 3 } && code.All(char.IsLetter);

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}

public readonly record struct CurrencyValidationResult(bool IsValid, string? Error)
{
    public static CurrencyValidationResult Success() => new(true, null);
    public static CurrencyValidationResult Fail(string error) => new(false, error);
}
