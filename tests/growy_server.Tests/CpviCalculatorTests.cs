using growy_server.Calculators;

namespace growy_server.Tests;

public class CpviCalculatorTests
{
    private const double NoMovementSentinel = 9999999;

    [Fact]
    public void Returns_Sentinel_When_Start_Equals_End()
    {
        var prices = new List<double> { 100, 110, 100 };

        var result = CpviCalculator.ComputeCpvi("FOO", prices);

        Assert.Equal("FOO", result.Symbol);
        Assert.Equal(NoMovementSentinel, result.CPVI);
    }

    [Fact]
    public void Straight_Line_Move_Returns_One()
    {
        var prices = new List<double> { 100, 101, 102, 103, 104 };

        var result = CpviCalculator.ComputeCpvi("FOO", prices);

        Assert.Equal(1.0, result.CPVI, precision: 10);
    }

    [Fact]
    public void Choppy_Move_With_Same_Endpoints_Returns_Sentinel()
    {
        var prices = new List<double> { 100, 110, 90, 120, 100 };

        var result = CpviCalculator.ComputeCpvi("FOO", prices);

        Assert.Equal(NoMovementSentinel, result.CPVI);
    }

    [Fact]
    public void Choppy_Move_With_Small_Net_Change_Returns_Large_Cpvi()
    {
        var prices = new List<double> { 100, 110, 90, 120, 80, 101 };

        var result = CpviCalculator.ComputeCpvi("FOO", prices);

        double expectedNumerator = 10 + 20 + 30 + 40 + 21;
        double expectedDenominator = 1;
        Assert.Equal(expectedNumerator / expectedDenominator, result.CPVI, precision: 10);
        Assert.True(result.CPVI > 1);
    }

    [Fact]
    public void Single_Price_Returns_Sentinel()
    {
        var prices = new List<double> { 100 };

        var result = CpviCalculator.ComputeCpvi("FOO", prices);

        Assert.Equal(NoMovementSentinel, result.CPVI);
    }

    [Fact]
    public void Empty_Prices_Returns_Sentinel()
    {
        var prices = new List<double>();

        var result = CpviCalculator.ComputeCpvi("FOO", prices);

        Assert.Equal(NoMovementSentinel, result.CPVI);
    }
}
