using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace growy_server.Models.DB
{
    [Table("symbol_date_price_cedears")]
    public class SymbolDatePriceCedear
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("symbol")]
        public string Symbol { get; set; } = null!;

        [Column("close_price")]
        public double ClosePrice { get; set; }

        [Column("unix_date")]
        public long UnixDate { get; set; }
    }
}
