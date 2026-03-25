using growy_server.Models;

namespace growy_server.Calculators
{
    public static class EmaCalculator
    {
        public static List<EmaEntry> Calculate20Ema(List<PriceEntry> prices)
        {
            const int period = 20;
            var emaEntries = new List<EmaEntry>();

            if (prices.Count < period)
                return emaEntries;

            double k = 2.0 / (period + 1);

            double ema = prices.Take(period).Average(p => p.ClosePrice);
            emaEntries.Add(new EmaEntry { Value = ema, UnixDate = prices[period - 1].UnixDate });

            for (int i = period; i < prices.Count; i++)
            {
                ema = prices[i].ClosePrice * k + ema * (1 - k);
                emaEntries.Add(new EmaEntry { Value = ema, UnixDate = prices[i].UnixDate });
            }

            return emaEntries;
        }
    }
}
