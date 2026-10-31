using growy_server.Calculators;

namespace growy_server.Tests;

public class WilderRsiTests
{
    // The array-based implementation RsiCalculator.ComputeRsi used before WilderRsi existed.
    // WilderRsi must reproduce it exactly, so the Momentum grid's RSI does not move.
    private static (double AvgGain, double AvgLoss) ReferenceWilder(IReadOnlyList<double> closes, int period)
    {
        int count = closes.Count;
        var gains = new double[count - 1];
        var losses = new double[count - 1];
        for (int i = 0; i < count - 1; i++)
        {
            double change = closes[i + 1] - closes[i];
            gains[i] = change > 0 ? change : 0;
            losses[i] = change < 0 ? -change : 0;
        }

        double avgGain = 0, avgLoss = 0;
        for (int i = 0; i < period; i++)
        {
            avgGain += gains[i];
            avgLoss += losses[i];
        }
        avgGain /= period;
        avgLoss /= period;

        for (int i = period; i < gains.Length; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        return (avgGain, avgLoss);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 16)]
    [InlineData(3, 400)]
    [InlineData(4, 1200)]
    public void Matches_Reference_Implementation_Exactly(int seed, int length)
    {
        var random = new Random(seed);
        var closes = new List<double> { 100 };
        for (int i = 1; i < length; i++)
            closes.Add(Math.Max(0.5, closes[^1] * (1 + (random.NextDouble() - 0.5) * 0.08)));

        var rsi = new WilderRsi(14);
        foreach (var close in closes)
            rsi.Add(close);

        var (avgGain, avgLoss) = ReferenceWilder(closes, 14);
        Assert.True(rsi.IsReady);
        Assert.Equal(avgGain, rsi.AvgGain);
        Assert.Equal(avgLoss, rsi.AvgLoss);
    }

    [Fact]
    public void Not_Ready_Until_Period_Plus_One_Closes()
    {
        var rsi = new WilderRsi(14);
        for (int i = 0; i < 14; i++)
            rsi.Add(100 + i);

        Assert.False(rsi.IsReady);

        rsi.Add(120);
        Assert.True(rsi.IsReady);
    }
}
