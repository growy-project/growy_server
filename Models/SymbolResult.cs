namespace growy_server.Models
{
    public class SymbolResult
    {
        public required string Symbol { get; set; }
        public string? Exchange { get; set; }
        public double PercentageChange { get; set; }
        public double OldestPrice { get; set; }
        public double NewestPrice { get; set; }
        public decimal? MarketCapitalization { get; set; }
        public decimal? Eps { get; set; }
        public double TargetPrice { get; set; }
        public double Rsi { get; set; } = 0;
        public double Volatility { get; set; } = 0;
        public double PercentPositiveDays { get; set; } = 0;
        public double ReturnStdDev { get; set; } = 0;
        public double MaxDrawdown { get; set; } = 0;
        // IsInMomentum: Option B from docs/metrics_improvement_plan.md — top quintile of
        // result-set growth + smoothness/drawdown gates per docs/momentum_trading_summary.md
        // (§6 smooth-vs-choppy, §7 risk-adjusted scaling).
        public bool IsInMomentum { get; set; } = false;
        public string? CompanyName { get; set; }
        public string? Description { get; set; }
        public string? Sector { get; set; }
        public string? Industry { get; set; }
    }
}
