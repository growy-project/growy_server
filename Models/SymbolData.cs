namespace growy_server.Models
{
    public class SymbolData
    {
        public string symbol { get; set; }
        public double close_price { get; set; }
        public long unix_date { get; set; } // Store as long for Unix timestamps
    }
}
