using System.ComponentModel.DataAnnotations;

namespace growy_server.Models
{
    public class SendMessageRequest
    {
        [Required]
        [StringLength(200)]
        public required string Title { get; set; }

        [Required]
        [StringLength(5000)]
        public required string Message { get; set; }
    }
}
