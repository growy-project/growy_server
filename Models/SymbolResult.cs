namespace growy_server.Models
{
    public class SymbolResult
    {
        public string Symbol { get; set; }
        public double PercentageChange { get; set; }
        public double OldestPrice { get; set; }
        public double NewestPrice { get; set; }
        public decimal? MarketCapitalization { get; set; }
        public decimal? Eps { get; set; }
        public double TargetPrice { get; set; }
        public double Rsi { get; set; } = 0;
        public double Volatility { get; set; } = 0; 
        public string? CompanyName { get; set; }
        public string? Description { get; set; }
        public string? Sector { get; set; }
        public string? Industry { get; set; }
    }
}
