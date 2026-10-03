using growy_server.Calculators;

namespace growy_server.Tests;

public class RsiCalculatorTests
{
    [Fact]
    public void Returns_Null_When_Prices_Below_Period_Plus_One()
    {
        var prices = Enumerable.Range(1, 14).Select(i => (double)i).ToList();

        var result = RsiCalculator.ComputeRsi("FOO", prices);

        Assert.Null(result);
    }

    [Fact]
    public void Constant_Prices_Return_Rsi_100()
    {
        var prices = Enumerable.Repeat(100.0, 20).ToList();

        var result = RsiCalculator.ComputeRsi("FOO", prices);

        Assert.NotNull(result);
        Assert.Equal("FOO", result!.Symbol);
        Assert.Equal(100.0, result.Rsi);
    }

    [Fact]
    public void Steady_Uptrend_Returns_Rsi_100()
    {
        var prices = Enumerable.Range(1, 30).Select(i => (double)i).ToList();

        var result = RsiCalculator.ComputeRsi("FOO", prices);

        Assert.NotNull(result);
        Assert.Equal(100.0, result!.Rsi);
    }

    [Fact]
    public void Steady_Downtrend_Returns_Rsi_Zero()
    {
        var prices = Enumerable.Range(1, 30).Select(i => (double)(31 - i)).ToList();

        var result = RsiCalculator.ComputeRsi("FOO", prices);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Rsi);
    }

    [Fact]
    public void Result_Is_Rounded_To_Two_Decimal_Places()
    {
        var prices = new List<double> { 44.34, 44.09, 44.15, 43.61, 44.33, 44.83, 45.10, 45.42, 45.84, 46.08, 45.89, 46.03, 45.61, 46.28, 46.28, 46.00, 46.03, 46.41, 46.22, 45.64, 46.21 };

        var result = RsiCalculator.ComputeRsi("FOO", prices);

        Assert.NotNull(result);
        Assert.Equal(Math.Round(result!.Rsi, 2), result.Rsi);
        Assert.InRange(result.Rsi, 0.0, 100.0);
    }

    // CalculateAsync loads only the most recent 23*period bars per symbol rather than
    // full history. That is sound because Wilder seed influence decays geometrically —
    // these tests pin the property, so shrinking the warm-up constant breaks the build
    // rather than silently shifting every RSI in the API.
    private const int WarmupPeriods = 23;

    private static List<double> RandomWalk(Random rng, int bars, double dailyVol)
    {
        var prices = new List<double>(bars);
        double price = rng.NextDouble() * 400 + 10;
        for (int i = 0; i < bars; i++)
        {
            price *= Math.Exp((rng.NextDouble() - 0.5) * 2 * dailyVol);
            prices.Add(price);
        }
        return prices;
    }

    [Theory]
    [InlineData(14)]
    [InlineData(7)]
    [InlineData(30)]
    public void Truncating_To_Warmup_Window_Matches_Full_History(int period)
    {
        var rng = new Random(20260811 + period);
        int warmup = period * WarmupPeriods;

        for (int trial = 0; trial < 200; trial++)
        {
            var full = RandomWalk(rng, bars: warmup + 800, dailyVol: 0.005 + rng.NextDouble() * 0.055);
            var truncated = full.Skip(full.Count - warmup).ToList();

            var fromFull = RsiCalculator.ComputeRsi("FOO", full, period);
            var fromTruncated = RsiCalculator.ComputeRsi("FOO", truncated, period);

            Assert.NotNull(fromFull);
            Assert.NotNull(fromTruncated);
            Assert.Equal(fromFull!.Rsi, fromTruncated!.Rsi);
        }
    }

    [Fact]
    public void Series_Shorter_Than_Warmup_Window_Is_Unaffected()
    {
        // Symbols holding fewer bars than the LIMIT load all of them, so the LATERAL
        // bound must be a no-op for them.
        var rng = new Random(4242);
        var prices = RandomWalk(rng, bars: 90, dailyVol: 0.03);

        var whole = RsiCalculator.ComputeRsi("FOO", prices);
        var takeMore = RsiCalculator.ComputeRsi("FOO", prices.TakeLast(14 * WarmupPeriods).ToList());

        Assert.NotNull(whole);
        Assert.Equal(whole!.Rsi, takeMore!.Rsi);
    }
}
