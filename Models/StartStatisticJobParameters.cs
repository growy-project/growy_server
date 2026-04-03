namespace growy_server.Models
{
    public class StartStatisticJobParameters
    {
        public long StartUnixDate { get; set; }
        public long EndUnixDate { get; set; }
        public double MinimumExpectedGrowth { get; set; }

        public required string Exchange { get; set; }
    }
}