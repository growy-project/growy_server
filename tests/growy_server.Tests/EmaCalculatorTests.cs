using growy_server.Calculators;
using growy_server.Models;

namespace growy_server.Tests;

public class EmaCalculatorTests
{
    [Fact]
    public void Returns_Empty_When_Fewer_Than_20_Prices()
    {
        var prices = BuildPrices(Enumerable.Repeat(100.0, 19).ToArray());

        var ema = EmaCalculator.Calculate20Ema(prices);

        Assert.Empty(ema);
    }

    [Fact]
    public void Constant_Prices_Produce_Same_Constant_Ema()
    {
        var prices = BuildPrices(Enumerable.Repeat(100.0, 25).ToArray());

        var ema = EmaCalculator.Calculate20Ema(prices);

        Assert.Equal(6, ema.Count);
        Assert.All(ema, entry => Assert.Equal(100.0, entry.Value, precision: 10));
    }

    [Fact]
    public void First_Entry_Uses_Sma_Seed_Over_First_20()
    {
        var closes = Enumerable.Range(1, 25).Select(i => (double)i).ToArray();
        var prices = BuildPrices(closes);

        var ema = EmaCalculator.Calculate20Ema(prices);

        double expectedSeed = closes.Take(20).Average();
        Assert.Equal(expectedSeed, ema[0].Value, precision: 10);
    }

    [Fact]
    public void Preserves_UnixDate_From_PriceEntry()
    {
        var prices = Enumerable.Range(0, 25)
            .Select(i => new PriceEntry { ClosePrice = 100.0 + i, UnixDate = 1_700_000_000 + i * 86_400 })
            .ToList();

        var ema = EmaCalculator.Calculate20Ema(prices);

        Assert.Equal(prices[19].UnixDate, ema[0].UnixDate);
        Assert.Equal(prices[20].UnixDate, ema[1].UnixDate);
        Assert.Equal(prices[^1].UnixDate, ema[^1].UnixDate);
    }

    [Fact]
    public void Known_Sequence_Matches_Expected_Smoothing()
    {
        var closes = Enumerable.Repeat(100.0, 20).Append(110.0).ToArray();
        var prices = BuildPrices(closes);

        var ema = EmaCalculator.Calculate20Ema(prices);

        double k = 2.0 / 21.0;
        double expectedAfterStep = 110.0 * k + 100.0 * (1 - k);
        Assert.Equal(100.0, ema[0].Value, precision: 10);
        Assert.Equal(expectedAfterStep, ema[1].Value, precision: 10);
    }

    private static List<PriceEntry> BuildPrices(double[] closes)
    {
        return closes
            .Select((c, i) => new PriceEntry { ClosePrice = c, UnixDate = 1_700_000_000 + i * 86_400 })
            .ToList();
    }
}
