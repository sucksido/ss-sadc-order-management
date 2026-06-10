namespace SadcOms.Application.Abstractions;

/// <summary>
/// Supplies FX rates used to express order totals in the regional anchor currency (ZAR).
/// Implemented by a mocked provider in this assessment; the contract is what a real
/// provider (e.g. a bank rates feed) would slot into.
/// </summary>
public interface IFxRateProvider
{
    /// <summary>
    /// Returns the rate to multiply an amount in <paramref name="fromCurrency"/> by to get
    /// the equivalent in <paramref name="toCurrency"/>. A 1:1 rate is returned when the
    /// currencies match or are at par within the CMA.
    /// </summary>
    Task<decimal> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
}
