using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growy_server.Models.DB
{
    [Table("users")]
    public class UserEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("picture")]
        public string? Picture { get; set; }

        [Column("role")]
        public string Role { get; set; } = "default";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
