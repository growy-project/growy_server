namespace growy_server.Models
{
    public class UserResult
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Picture { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
