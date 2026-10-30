namespace growy_server.Strategies
{
    // Unix dates in seconds (API units); the strategy converts to ms for SQL.
    public record MomentumRequest(
        string Exchange,
        long StartUnixDate,
        long EndUnixDate,
        double? MinimumExpectedGrowth = null,         // null = no threshold (watchlist)
        IReadOnlyCollection<string>? Symbols = null);  // null = whole exchange
}
