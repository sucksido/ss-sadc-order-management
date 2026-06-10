namespace SadcOms.Domain.Common;

/// <summary>
/// Helpers for monetary rounding. We store and compute amounts as <see cref="decimal"/>
/// (never floating point) and round to the currency's minor-unit scale. All SADC
/// currencies we support use 2 decimal places, so 2 is the default scale.
///
/// We round away from zero ("commercial rounding") which is the convention most finance
/// teams expect on invoice totals; banker's rounding is available where statistical
/// neutrality matters.
/// </summary>
public static class Money
{
    public const int DefaultScale = 2;

    public static decimal Round(decimal amount, int scale = DefaultScale)
        => Math.Round(amount, scale, MidpointRounding.AwayFromZero);
}
