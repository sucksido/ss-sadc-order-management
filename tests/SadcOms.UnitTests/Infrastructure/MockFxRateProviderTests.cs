using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SadcOms.Infrastructure.Fx;
using Xunit;

namespace SadcOms.UnitTests.Infrastructure;

public class MockFxRateProviderTests
{
    private static MockFxRateProvider CreateSut() =>
        new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new FxOptions()));

    [Fact]
    public async Task Same_currency_is_one_to_one()
    {
        var rate = await CreateSut().GetRateAsync("ZAR", "ZAR");
        rate.Should().Be(1m);
    }

    [Theory]
    [InlineData("NAD")]
    [InlineData("LSL")]
    [InlineData("SZL")]
    public async Task Cma_currencies_convert_to_zar_at_par(string currency)
    {
        var rate = await CreateSut().GetRateAsync(currency, "ZAR");
        rate.Should().Be(1m);
    }

    [Fact]
    public async Task Usd_to_zar_uses_the_configured_rate()
    {
        var rate = await CreateSut().GetRateAsync("USD", "ZAR");
        rate.Should().Be(18.5m);
    }
}
