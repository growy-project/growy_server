namespace growy_server.Calculators
{
    // Streaming Wilder RSI: feed closes oldest-first. The first `period` changes seed the
    // averages with a simple mean, then each change is smoothed as (avg*(period-1) + x) / period.
    // Shared by RsiCalculator.ComputeRsi and the Bouncy calculator so both give identical values.
    public struct WilderRsi(int period = 14)
    {
        private readonly int _period = period;
        private double _previousClose;
        private int _closes;
        private double _gainSum;
        private double _lossSum;

        public double AvgGain { get; private set; }
        public double AvgLoss { get; private set; }

        // Ready once `period` changes (period + 1 closes) have been seen.
        public readonly bool IsReady => _closes > _period;

        public readonly double Value => AvgLoss == 0 ? 100 : 100 - (100 / (1 + AvgGain / AvgLoss));

        public void Add(double close)
        {
            if (_closes++ == 0)
            {
                _previousClose = close;
                return;
            }

            double change = close - _previousClose;
            _previousClose = close;
            double gain = change > 0 ? change : 0;
            double loss = change < 0 ? -change : 0;
            int changes = _closes - 1;

            if (changes < _period)
            {
                _gainSum += gain;
                _lossSum += loss;
            }
            else if (changes == _period)
            {
                AvgGain = (_gainSum + gain) / _period;
                AvgLoss = (_lossSum + loss) / _period;
            }
            else
            {
                AvgGain = (AvgGain * (_period - 1) + gain) / _period;
                AvgLoss = (AvgLoss * (_period - 1) + loss) / _period;
            }
        }
    }
}
