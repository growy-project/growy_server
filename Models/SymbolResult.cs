namespace growy_server.Models
{
    public class SymbolResult
    {
        public string Symbol { get; set; }
        public double PercentageChange { get; set; }
        public double OldestPrice { get; set; }
        public double NewestPrice { get; set; }
        public double MarketCap { get; set; }
        public double EarningsPerShare { get; set; }
        public double TargetPrice { get; set; }
        public double Rsi { get; set; }
        public double Volatility { get; set; }

    }   
}
