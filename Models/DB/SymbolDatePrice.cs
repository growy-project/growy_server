using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growy_server.Models.DB
{
    [Table("symbol_date_price")]
    public class SymbolDatePrice
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("symbol")]
        public string Symbol { get; set; }

        [Column("close_price")]
        public double ClosePrice { get; set; }

        [Column("unix_date")]
        public long UnixDate { get; set; }

        [Column("exchange")]
        public string? Exchange { get; set; }
    }
}
