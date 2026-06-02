using growy_server.Calculators;

namespace growy_server.Tests;

public class BounceCalculatorTests
{
    [Fact]
    public void Returns_False_When_Fewer_Than_Two_Closes()
    {
        var prices = new List<double> { 42.0 };

        Assert.False(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 100));
    }

    [Fact]
    public void Returns_False_When_Target_Not_Above_Latest_Close()
    {
        // High (15) -> dip (9) -> recovering (11), but target sits below the latest close.
        var prices = new List<double> { 10, 12, 15, 11, 9, 10, 11 };

        Assert.False(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 10));
    }

    [Fact]
    public void Returns_False_When_Target_Is_Zero()
    {
        // A target of 0 means no analyst coverage -> never bouncing.
        var prices = new List<double> { 10, 12, 15, 11, 9, 10, 11 };

        Assert.False(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 0));
    }

    [Fact]
    public void Returns_False_When_At_New_High()
    {
        var prices = new List<double> { 10, 11, 12, 15 };

        Assert.False(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 20));
    }

    [Fact]
    public void Returns_False_When_Monotonic_Uptrend()
    {
        var prices = new List<double> { 1, 2, 3, 4, 5, 6 };

        Assert.False(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 10));
    }

    [Fact]
    public void Returns_False_When_Still_Falling_At_Post_Peak_Low()
    {
        // Latest close IS the post-peak low -> not yet recovering.
        var prices = new List<double> { 10, 15, 12, 9 };

        Assert.False(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 20));
    }

    [Fact]
    public void Returns_True_For_Classic_Bounce()
    {
        // High (15) -> dip (9) -> recovering (11), target above the latest close.
        var prices = new List<double> { 10, 12, 15, 11, 9, 10, 11 };

        Assert.True(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 14));
    }

    [Fact]
    public void Returns_True_For_V_Shaped_Recovery()
    {
        var prices = new List<double> { 100, 80, 60, 70, 90 };

        Assert.True(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 120));
    }

    [Fact]
    public void Returns_True_When_Latest_Just_Above_Post_Peak_Low()
    {
        var prices = new List<double> { 10, 15, 8, 8.01 };

        Assert.True(BounceCalculator.ComputeIsBouncing(prices, targetPrice: 20));
    }
}
