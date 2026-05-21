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
}
