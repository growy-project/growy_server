using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growy_server.Models.DB
{
    [Table("companies")]
    public class CompanyEntity
    {
        [Key]
        [Column("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [Column("is_toxic")]
        public bool IsToxic { get; set; }

        [Column("is_top_growth")]
        public bool IsTopGrowth { get; set; }
    }
}
