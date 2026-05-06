namespace growy_server.Models
{
    public class AddWatchlistSymbolRequest
    {
        public required string Symbol { get; set; }
        public required string Exchange { get; set; }
    }
}
