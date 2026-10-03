namespace growy_server.Models
{
    public class WatchlistSectorResult
    {
        public required string Sector { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public required List<WatchlistSectorSymbol> Symbols { get; set; }
    }

    public class WatchlistSectorSymbol
    {
        public required string Symbol { get; set; }
        public required string Exchange { get; set; }
    }
}
