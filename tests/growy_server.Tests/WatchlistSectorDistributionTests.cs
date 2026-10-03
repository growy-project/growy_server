using growy_server.Services;

namespace growy_server.Tests;

public class WatchlistSectorDistributionTests
{
    [Fact]
    public void Empty_Watchlist_Returns_Empty_List()
    {
        var rows = new List<WatchlistSectorRow>();

        var result = WatchlistService.BuildSectorDistribution(rows);

        Assert.Empty(result);
    }

    [Fact]
    public void Single_Sector_Returns_One_Hundred_Percent()
    {
        var rows = BuildRows(("AAPL", "NASDAQ", "Technology"), ("MSFT", "NASDAQ", "Technology"));

        var result = WatchlistService.BuildSectorDistribution(rows);

        var group = Assert.Single(result);
        Assert.Equal("Technology", group.Sector);
        Assert.Equal(2, group.Count);
        Assert.Equal(100d, group.Percentage, precision: 10);
    }

    [Fact]
    public void Groups_Are_Ordered_By_Count_Descending()
    {
        var rows = BuildRows(
            ("KO", "NYSE", "Consumer Defensive"),
            ("AAPL", "NASDAQ", "Technology"),
            ("MSFT", "NASDAQ", "Technology"),
            ("PFE", "NYSE", "Healthcare"),
            ("MRK", "NYSE", "Healthcare"),
            ("JNJ", "NYSE", "Healthcare"));

        var result = WatchlistService.BuildSectorDistribution(rows);

        Assert.Equal(["Healthcare", "Technology", "Consumer Defensive"], result.Select(g => g.Sector));
        Assert.Equal([3, 2, 1], result.Select(g => g.Count));
        Assert.Equal(50d, result[0].Percentage, precision: 10);
        Assert.Equal(100d / 3, result[1].Percentage, precision: 1);
    }

    [Fact]
    public void Null_And_Empty_Sectors_Collapse_Into_Unknown()
    {
        var rows = BuildRows(
            ("AAPL", "NASDAQ", "Technology"),
            ("TQQQ", "CEDEAR", null),
            ("PBRG", "NASDAQ", ""),
            ("FOO", "NYSE", "   "));

        var result = WatchlistService.BuildSectorDistribution(rows);

        Assert.Equal(2, result.Count);
        var unknown = Assert.Single(result, g => g.Sector == "Unknown");
        Assert.Equal(3, unknown.Count);
        Assert.Equal(75d, unknown.Percentage, precision: 10);
        Assert.Equal(["FOO", "PBRG", "TQQQ"], unknown.Symbols.Select(s => s.Symbol));
    }

    [Fact]
    public void Unknown_Sorts_Last_Even_When_Largest()
    {
        var rows = BuildRows(
            ("AAPL", "NASDAQ", "Technology"),
            ("FOO", "NYSE", null),
            ("BAR", "NYSE", null),
            ("BAZ", "NYSE", null));

        var result = WatchlistService.BuildSectorDistribution(rows);

        Assert.Equal(["Technology", "Unknown"], result.Select(g => g.Sector));
        Assert.Equal(3, result[1].Count);
    }

    [Fact]
    public void Same_Symbol_On_Two_Exchanges_Counts_Twice()
    {
        var rows = BuildRows(
            ("AAPL", "NASDAQ", "Technology"),
            ("AAPL", "CEDEAR", "Technology"),
            ("PFE", "NYSE", "Healthcare"));

        var result = WatchlistService.BuildSectorDistribution(rows);

        var technology = Assert.Single(result, g => g.Sector == "Technology");
        Assert.Equal(2, technology.Count);
        Assert.Equal(["CEDEAR", "NASDAQ"], technology.Symbols.Select(s => s.Exchange));
        Assert.Equal(100d * 2 / 3, technology.Percentage, precision: 1);
    }

    [Fact]
    public void Percentages_Sum_To_One_Hundred()
    {
        var rows = BuildRows(
            ("AAPL", "NASDAQ", "Technology"),
            ("PFE", "NYSE", "Healthcare"),
            ("KO", "NYSE", "Consumer Defensive"),
            ("XOM", "NYSE", "Energy"));

        var result = WatchlistService.BuildSectorDistribution(rows);

        Assert.Equal(100d, result.Sum(g => g.Percentage), precision: 10);
        Assert.Equal(4, result.Sum(g => g.Count));
    }

    private static List<WatchlistSectorRow> BuildRows(params (string Symbol, string Exchange, string? Sector)[] rows)
        => rows.Select(r => new WatchlistSectorRow(r.Symbol, r.Exchange, r.Sector)).ToList();
}
