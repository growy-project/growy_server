using growy_server.Strategies;

namespace growy_server.Tests;

public class MomentumStrategyQueryTests
{
    [Fact]
    public void Exchange_Scan_Applies_Threshold_Without_Symbol_Filter()
    {
        var sql = MomentumStrategy.BuildQuery("symbol_date_price", isCedear: false, filterSymbols: false, applyThreshold: true);

        Assert.Contains("WHERE percentageChange > @Threshold", sql);
        Assert.DoesNotContain("@Symbols", sql);
        Assert.Contains("AND exchange = @Exchange", sql);
    }

    [Fact]
    public void Watchlist_Filters_Symbols_Without_Threshold()
    {
        var sql = MomentumStrategy.BuildQuery("symbol_date_price", isCedear: false, filterSymbols: true, applyThreshold: false);

        Assert.Contains("AND symbol = ANY(@Symbols)", sql);
        Assert.DoesNotContain("@Threshold", sql);
    }

    [Fact]
    public void Cedear_Has_No_Exchange_Filter()
    {
        var sql = MomentumStrategy.BuildQuery("symbol_date_price_cedears", isCedear: true, filterSymbols: false, applyThreshold: true);

        Assert.Contains("FROM symbol_date_price_cedears", sql);
        Assert.DoesNotContain("@Exchange", sql);
    }

    [Fact]
    public void Keeps_Growth_Ordering()
    {
        var sql = MomentumStrategy.BuildQuery("symbol_date_price", isCedear: false, filterSymbols: true, applyThreshold: false);

        Assert.EndsWith("ORDER BY percentageChange DESC;", sql);
    }
}
