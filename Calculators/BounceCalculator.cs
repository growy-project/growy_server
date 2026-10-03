using growy_server.Models;

namespace growy_server.Calculators
{
    public static class BounceCalculator
    {
        // Consumes a series loaded by PriceSeriesLoader.
        public static List<BounceResult> CalculateFromSeries(
            IReadOnlyDictionary<string, List<double>> priceSeries,
            IReadOnlyDictionary<string, double> targetPrices)
        {
            var results = new List<BounceResult>();
            foreach (var entry in priceSeries)
            {
                double targetPrice = targetPrices.TryGetValue(entry.Key, out var t) ? t : 0;
                results.Add(new BounceResult { Symbol = entry.Key, IsBouncing = ComputeIsBouncing(entry.Value, targetPrice) });
            }

            return results;
        }

        public static bool ComputeIsBouncing(IReadOnlyList<double> closesOrderedByDate, double targetPrice)
        {
            int n = closesOrderedByDate.Count;
            if (n < 2)
                return false;

            double latest = closesOrderedByDate[n - 1];

            // Analyst upside: target must sit above today's close (also excludes target == 0, i.e. no analyst data).
            if (targetPrice <= latest)
                return false;

            // Peak = window high; take its first occurrence for the longest post-peak window.
            int peakIndex = 0;
            double peak = closesOrderedByDate[0];
            for (int i = 1; i < n; i++)
                if (closesOrderedByDate[i] > peak)
                {
                    peak = closesOrderedByDate[i];
                    peakIndex = i;
                }

            // Must be below a prior high, and that high must be in the past (bars remain after it).
            if (peak <= latest || peakIndex >= n - 1)
                return false;

            double postPeakLow = double.MaxValue;
            for (int i = peakIndex + 1; i < n; i++)
                if (closesOrderedByDate[i] < postPeakLow)
                    postPeakLow = closesOrderedByDate[i];

            // Recovering: today's close is above the post-peak low.
            return latest > postPeakLow;
        }
    }
}
