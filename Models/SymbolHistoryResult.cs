namespace growy_server.Models
{
    public class SymbolHistoryResult
    {
        public required string Symbol { get; set; }
        public required List<PriceEntry> Prices { get; set; }
        public required List<EmaEntry> Ema20 { get; set; }
    }

    public class PriceEntry
    {
        public double ClosePrice { get; set; }
        public long UnixDate { get; set; }
    }

    public class EmaEntry
    {
        public double Value { get; set; }
        public long UnixDate { get; set; }
    }
}
