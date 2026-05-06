namespace growy_server.Models
{
    public class StartWatchlistJobParameters
    {
        public int UserId { get; set; }
        public long StartUnixDate { get; set; }
        public long EndUnixDate { get; set; }
    }
}
