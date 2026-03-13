namespace growy_server.Models
{
    public class TagRequestModel
    {
        public required string Symbol { get; set; }
        public required string TagType { get; set; }
        public required string Reason { get; set; }
        public required string RequesterEmail { get; set; }
    }
}
