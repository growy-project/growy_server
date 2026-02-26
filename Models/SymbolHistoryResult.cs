namespace growy_server.Models
{
    public class SymbolHistoryResult
    {
        public string Symbol { get; set; }
        public List<PriceEntry> Prices { get; set; }
        public List<EmaEntry> Ema20 { get; set; }
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
