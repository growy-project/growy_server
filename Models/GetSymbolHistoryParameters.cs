namespace growy_server.Models
{
    public class GetSymbolHistoryParameters
    {
        public required string Exchange { get; set; }
        public long? StartUnixDate { get; set; }
        public long? EndUnixDate { get; set; }
    }
}
