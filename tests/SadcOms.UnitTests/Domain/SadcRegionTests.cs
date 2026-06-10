using FluentAssertions;
using SadcOms.Domain.Regional;
using Xunit;

namespace SadcOms.UnitTests.Domain;

public class SadcRegionTests
{
    [Theory]
    [InlineData("ZA", "ZAR")]
    [InlineData("BW", "BWP")]
    [InlineData("ZW", "ZWL")]
    [InlineData("ZW", "USD")] // multi-currency economy
    [InlineData("LS", "LSL")]
    public void ValidatePairing_accepts_native_currencies(string country, string currency)
    {
        SadcRegion.ValidatePairing(country, currency).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ZA", "ZAR")]
    [InlineData("LS", "ZAR")]
    [InlineData("NA", "ZAR")]
    [InlineData("SZ", "ZAR")]
    public void ValidatePairing_accepts_ZAR_across_the_CMA(string country, string currency)
    {
        SadcRegion.ValidatePairing(country, currency).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePairing_rejects_ZAR_outside_the_CMA()
    {
        SadcRegion.ValidatePairing("BW", "ZAR").IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidatePairing_rejects_non_sadc_country()
    {
        var result = SadcRegion.ValidatePairing("US", "USD");
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("US");
    }

    [Theory]
    [InlineData("ZAR", "ZAR")] // 3-letter country code is invalid
    [InlineData("ZA", "ZA")]   // 2-letter currency code is invalid
    [InlineData("Z1", "ZAR")]  // non-letter characters
    public void ValidatePairing_rejects_malformed_codes(string country, string currency)
    {
        SadcRegion.ValidatePairing(country, currency).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidatePairing_normalises_casing_and_whitespace()
    {
        SadcRegion.ValidatePairing(" za ", "zar").IsValid.Should().BeTrue();
    }

    [Fact]
    public void CommonMonetaryArea_has_the_four_members()
    {
        SadcRegion.CommonMonetaryArea.Should().BeEquivalentTo(new[] { "ZA", "LS", "NA", "SZ" });
    }
}
